using System.Net;
using System.Net.Http.Json;
using LmsTutor.IntegrationTests.Infrastructure;
using LmsTutor.Server.Modules.Tutoring;
using LmsTutor.Server.Shared.ExternalServices;
using LmsTutor.Server.Shared.Http;
using LmsTutor.Server.Shared.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LmsTutor.IntegrationTests;

public sealed class TutorTests(TutorApiFactory factory) : IClassFixture<TutorApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ask_AboutPostInTheApisLesson_IsAnsweredAndCitesTheLesson()
    {
        var lessonId = await factory.GetLessonIdAsync("metodos-http");
        var client = factory.CreateApiClient();
        await client.LoginAsync(DemoData.Ana);

        var exchange = await client.AskSuccessfullyAsync(lessonId, "¿Para qué sirve POST?");

        Assert.Equal("answered", exchange.Status);
        var citation = Assert.Single(exchange.Citations);
        Assert.Equal(new TutorCitationResponse("post", "POST: crear recursos"), citation);
        Assert.Contains("POST", exchange.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ask_QuestionNotCoveredByTheLesson_ReportsInsufficientInformation()
    {
        var lessonId = await factory.GetLessonIdAsync("metodos-http");
        var client = factory.CreateApiClient();
        await client.LoginAsync(DemoData.Ana);

        var exchange = await client.AskSuccessfullyAsync(lessonId, "¿Cómo despliego mi aplicación en Kubernetes?");

        Assert.Equal("insufficient_information", exchange.Status);
        Assert.Empty(exchange.Citations);
    }

    [Fact]
    public async Task History_IsRestoredInALaterSession()
    {
        var lessonId = await factory.GetLessonIdAsync("metodos-http");
        var student = await factory.CreateStudentAsync(enrolled: true);
        var firstVisit = factory.CreateApiClient();
        await firstVisit.LoginAsync(student);
        var asked = await firstVisit.AskSuccessfullyAsync(lessonId, "¿Qué hace DELETE?");

        var laterVisit = factory.CreateApiClient();
        await laterVisit.LoginAsync(student);
        var history = await laterVisit.GetHistoryAsync(lessonId);

        var restored = Assert.Single(history);
        Assert.Equal(asked.Id, restored.Id);
        Assert.Equal(asked.Answer, restored.Answer);
    }

    [Fact]
    public async Task History_OfAnotherStudentInTheSameLesson_IsNeverReturned()
    {
        var lessonId = await factory.GetLessonIdAsync("metodos-http");
        var owner = factory.CreateApiClient();
        await owner.LoginAsync(await factory.CreateStudentAsync(enrolled: true));
        await owner.AskSuccessfullyAsync(lessonId, "¿Para qué sirve GET?");

        var classmate = factory.CreateApiClient();
        await classmate.LoginAsync(await factory.CreateStudentAsync(enrolled: true));
        var history = await classmate.GetHistoryAsync(lessonId);

        Assert.Empty(history);
    }

    [Fact]
    public async Task Tutor_ForStudentWithoutAccess_Returns403AndStoresNothing()
    {
        var lessonId = await factory.GetLessonIdAsync("metodos-http");
        var client = factory.CreateApiClient();
        await client.LoginAsync(DemoData.Bruno);

        var ask = await client.AskAsync(lessonId, "¿Para qué sirve POST?");
        var history = await client.GetAsync($"/api/lessons/{lessonId}/messages", _ct);

        Assert.Equal(HttpStatusCode.Forbidden, ask.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, history.StatusCode);
        Assert.Equal(0, await CountExchangesAsync(DemoData.Bruno.Email));
    }

    [Fact]
    public async Task Ask_RetriedWithTheSameClientMessageId_DoesNotCreateADuplicate()
    {
        var lessonId = await factory.GetLessonIdAsync("metodos-http");
        var student = await factory.CreateStudentAsync(enrolled: true);
        var client = factory.CreateApiClient();
        await client.LoginAsync(student);
        var clientMessageId = Guid.NewGuid();

        var first = await (await client.AskAsync(lessonId, "¿Qué hace PUT?", clientMessageId)).Content.ReadFromJsonAsync<TutorExchangeResponse>(_ct);
        var retry = await (await client.AskAsync(lessonId, "¿Qué hace PUT?", clientMessageId)).Content.ReadFromJsonAsync<TutorExchangeResponse>(_ct);

        Assert.Equal(first!.Id, retry!.Id);
        Assert.Single(await client.GetHistoryAsync(lessonId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("   ")]
    public async Task Ask_WithoutARealQuestion_ReturnsValidationProblem(string question)
    {
        var lessonId = await factory.GetLessonIdAsync("metodos-http");
        var client = factory.CreateApiClient();
        await client.LoginAsync(DemoData.Ana);

        var response = await client.AskAsync(lessonId, question);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ask_WithMalformedBody_Returns400InsteadOfAServerError()
    {
        // In Development minimal APIs throw on bad requests; the error handler must still answer 400.
        // Providers stay on the demo model because the test configuration overrides them.
        var app = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        var lessonId = await factory.GetLessonIdAsync("metodos-http");
        var client = ApiClient.Create(app);
        await client.LoginAsync(DemoData.Ana);
        // "question" holds a byte that is not valid UTF-8, like text sent in a legacy code page.
        using var body = new ByteArrayContent([.. "{\"question\":\""u8, 0xBF, .. "\"}"u8]);
        body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var response = await client.PostAsync($"/api/lessons/{lessonId}/messages", body, _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_request", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Ask_TooLongQuestion_ReturnsValidationProblem()
    {
        var lessonId = await factory.GetLessonIdAsync("metodos-http");
        var client = factory.CreateApiClient();
        await client.LoginAsync(DemoData.Ana);

        var response = await client.AskAsync(lessonId, new string('a', 501));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ask_WithMarkup_KeepsItAsPlainTextForTheClientToRenderSafely()
    {
        const string question = "<img src=x onerror=alert(1)> ¿Para qué sirve POST?";
        var lessonId = await factory.GetLessonIdAsync("metodos-http");
        var client = factory.CreateApiClient();
        await client.LoginAsync(DemoData.Ana);

        var exchange = await client.AskSuccessfullyAsync(lessonId, question);

        Assert.Equal(question, exchange.Question);
    }

    [Fact]
    public async Task Ask_WhenTheProviderIsDown_Returns503WithRetryAfterAndStoresNothing()
    {
        var app = factory.WithServices(services => services.AddScoped<ITutorModel>(_ => new StubTutorModel(_ =>
            throw new ExternalServiceException("tutor", ExternalServiceFailure.Unavailable, "down"))));
        var lessonId = await factory.GetLessonIdAsync("metodos-http");
        var student = await factory.CreateStudentAsync(enrolled: true);
        var client = ApiClient.Create(app);
        await client.LoginAsync(student);

        var response = await client.AskAsync(lessonId, "¿Para qué sirve POST?");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("tutor_unavailable", await response.ReadProblemCodeAsync());
        Assert.NotNull(response.Headers.RetryAfter);
        Assert.Equal(0, await CountExchangesAsync(student.Email));
    }

    [Fact]
    public async Task Ask_WhenTheProviderIsRateLimited_Returns429WithItsRetryAfter()
    {
        var app = factory.WithServices(services => services.AddScoped<ITutorModel>(_ => new StubTutorModel(_ =>
            throw new ExternalServiceException("tutor", ExternalServiceFailure.RateLimited, "quota", TimeSpan.FromSeconds(7)))));
        var lessonId = await factory.GetLessonIdAsync("metodos-http");
        var client = ApiClient.Create(app);
        await client.LoginAsync(DemoData.Ana);

        var response = await client.AskAsync(lessonId, "¿Para qué sirve POST?");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("tutor_rate_limited", await response.ReadProblemCodeAsync());
        Assert.Equal(TimeSpan.FromSeconds(7), response.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task Ask_WhenTheModelKeepsAnsweringWithoutValidCitations_DegradesToInsufficientInformation()
    {
        var stub = new StubTutorModel(_ => new TutorModelReply(TutorReplyStatus.Answered, "Invented answer", ["not-a-section"], "stub"));
        var app = factory.WithServices(services => services.AddScoped<ITutorModel>(_ => stub));
        var lessonId = await factory.GetLessonIdAsync("metodos-http");
        var client = ApiClient.Create(app);
        await client.LoginAsync(DemoData.Ana);

        var exchange = await client.AskSuccessfullyAsync(lessonId, "¿Para qué sirve POST?");

        Assert.Equal("insufficient_information", exchange.Status);
        Assert.Equal(TutorService.UnreliableAnswerFallback, exchange.Answer);
        Assert.Equal(2, stub.Calls);
    }

    [Fact]
    public async Task SystemStatus_WithoutProviderKey_ReportsDemoMode()
    {
        var client = factory.CreateApiClient();
        await client.LoginAsync(DemoData.Ana);

        var status = await client.GetFromJsonAsync<SystemStatusResponse>("/api/system/status", _ct);

        Assert.True(status!.DemoMode);
    }

    private Task<int> CountExchangesAsync(string email) =>
        factory.WithDbContextAsync(db => db.TutorExchanges.CountAsync(e => db.Users.Any(u => u.Id == e.UserId && u.Email == email)));
}
