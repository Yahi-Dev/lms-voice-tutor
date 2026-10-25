using System.Net;
using System.Text.Json;
using LmsTutor.Server.Modules.Tutoring;
using LmsTutor.Server.Modules.Tutoring.Providers;
using LmsTutor.Server.Shared.ExternalServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LmsTutor.UnitTests.Tutoring;

public sealed class ChatCompletionsTutorModelTests
{
    private const string PrimaryModel = "openai/gpt-oss-120b";
    private const string FallbackModel = "openai/gpt-oss-20b";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly TutorRequest _request = new(TestLessons.HttpMethods, [], "¿Para qué sirve POST?");

    [Fact]
    public async Task Complete_SendsAStrictSchemaRequestToTheChatCompletionsEndpoint()
    {
        var handler = new StubHttpMessageHandler((_, _) => Completion("""{"citedSectionIds":["post"],"status":"answered","answer":"POST crea recursos."}"""));

        await CreateModel(handler).CompleteAsync(_request, _ct);

        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal("https://api.example.test/openai/v1/chat/completions", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal(PrimaryModel, root.GetProperty("model").GetString());
        Assert.Equal(1500, root.GetProperty("max_completion_tokens").GetInt32());
        Assert.False(root.GetProperty("include_reasoning").GetBoolean());
        Assert.Equal("low", root.GetProperty("reasoning_effort").GetString());

        var format = root.GetProperty("response_format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.True(format.GetProperty("json_schema").GetProperty("strict").GetBoolean());
        Assert.Equal("system", root.GetProperty("messages")[0].GetProperty("role").GetString());
    }

    [Fact]
    public async Task Complete_ParsesTheStructuredReplyAndUsage()
    {
        var handler = new StubHttpMessageHandler((_, _) => Completion("""{"citedSectionIds":["post"],"status":"answered","answer":"POST crea recursos."}"""));

        var reply = await CreateModel(handler).CompleteAsync(_request, _ct);

        Assert.Equal(TutorReplyStatus.Answered, reply.Status);
        Assert.Equal(["post"], reply.CitedSectionKeys);
        Assert.Equal("POST crea recursos.", reply.Answer);
        Assert.Equal(812, reply.PromptTokens);
    }

    [Fact]
    public async Task Complete_WhenThePrimaryModelIsRateLimited_UsesTheFallbackModel()
    {
        var handler = new StubHttpMessageHandler((_, body) => body.Contains(PrimaryModel, StringComparison.Ordinal)
            ? RateLimited(seconds: 7)
            : Completion("""{"citedSectionIds":[],"status":"insufficient_information","answer":"No lo cubre."}""", FallbackModel));

        var reply = await CreateModel(handler).CompleteAsync(_request, _ct);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(FallbackModel, reply.Model);
    }

    [Fact]
    public async Task Complete_WhenEveryModelIsRateLimited_ReportsTheProviderRetryAfter()
    {
        var handler = new StubHttpMessageHandler((_, _) => RateLimited(seconds: 7));

        var failure = await Assert.ThrowsAsync<ExternalServiceException>(() => CreateModel(handler).CompleteAsync(_request, _ct));

        Assert.Equal(ExternalServiceFailure.RateLimited, failure.Failure);
        Assert.Equal(TimeSpan.FromSeconds(7), failure.RetryAfter);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ExternalServiceFailure.Misconfigured)]
    [InlineData(HttpStatusCode.NotFound, ExternalServiceFailure.Misconfigured)]
    [InlineData(HttpStatusCode.InternalServerError, ExternalServiceFailure.Unavailable)]
    [InlineData(HttpStatusCode.BadRequest, ExternalServiceFailure.InvalidResponse)]
    public async Task Complete_MapsProviderErrorsToFailures(HttpStatusCode status, ExternalServiceFailure expected)
    {
        var handler = new StubHttpMessageHandler((_, _) => StubHttpMessageHandler.Json(status, """{"error":{"message":"nope","type":"invalid_request_error"}}"""));

        var failure = await Assert.ThrowsAsync<ExternalServiceException>(() => CreateModel(handler).CompleteAsync(_request, _ct));

        Assert.Equal(expected, failure.Failure);
    }

    [Theory]
    [InlineData("""{"citedSectionIds":["post"],"status":"answered","answer":"POST""", "stop")]
    [InlineData("""{"citedSectionIds":["post"],"status":"answered","answer":"POST crea."}""", "length")]
    [InlineData("""{"citedSectionIds":[],"status":"maybe","answer":"?"}""", "stop")]
    public async Task Complete_WithAnUnusableReply_ReportsAnInvalidResponse(string content, string finishReason)
    {
        var handler = new StubHttpMessageHandler((_, _) => Completion(content, finishReason: finishReason));

        var failure = await Assert.ThrowsAsync<ExternalServiceException>(() => CreateModel(handler).CompleteAsync(_request, _ct));

        Assert.Equal(ExternalServiceFailure.InvalidResponse, failure.Failure);
    }

    private static ChatCompletionsTutorModel CreateModel(StubHttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/openai/v1/") };
        client.DefaultRequestHeaders.Authorization = new("Bearer", "test-key");

        var options = Options.Create(new TutorOptions
        {
            Provider = TutorProvider.Groq,
            ApiKey = "test-key",
            Model = PrimaryModel,
            FallbackModel = FallbackModel,
        });

        return new ChatCompletionsTutorModel(client, options, NullLogger<ChatCompletionsTutorModel>.Instance);
    }

    private static HttpResponseMessage Completion(string content, string model = PrimaryModel, string finishReason = "stop") =>
        StubHttpMessageHandler.Json(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            model,
            choices = new[] { new { message = new { role = "assistant", content }, finish_reason = finishReason } },
            usage = new { prompt_tokens = 812, completion_tokens = 64 },
        }));

    private static HttpResponseMessage RateLimited(int seconds)
    {
        var response = StubHttpMessageHandler.Json(HttpStatusCode.TooManyRequests, """{"error":{"message":"Rate limit reached","type":"tokens","code":"rate_limit_exceeded"}}""");
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
        return response;
    }
}
