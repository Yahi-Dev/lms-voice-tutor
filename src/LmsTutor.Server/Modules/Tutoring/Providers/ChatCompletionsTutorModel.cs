using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LmsTutor.Server.Shared.ExternalServices;
using Microsoft.Extensions.Options;

namespace LmsTutor.Server.Modules.Tutoring.Providers;

/// <summary>
/// Adapter for OpenAI-compatible chat completions APIs (Groq by default). Requests strict JSON-schema output
/// so the reply status is machine-readable instead of being guessed from prose.
/// </summary>
internal sealed partial class ChatCompletionsTutorModel(
    HttpClient httpClient,
    IOptions<TutorOptions> options,
    ILogger<ChatCompletionsTutorModel> logger) : ITutorModel
{
    private const string ServiceName = "tutor";

    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions ReplyJson = new(JsonSerializerDefaults.Web);

    public async Task<TutorModelReply> CompleteAsync(TutorRequest request, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        try
        {
            return await SendAsync(settings.Model, request, settings, cancellationToken);
        }
        catch (ExternalServiceException exception) when (
            exception.Failure == ExternalServiceFailure.RateLimited
            && !string.IsNullOrWhiteSpace(settings.FallbackModel)
            && settings.FallbackModel != settings.Model)
        {
            // Each model has its own quota on Groq, so the fallback usually still has room.
            LogFallback(logger, settings.Model, settings.FallbackModel);
            return await SendAsync(settings.FallbackModel, request, settings, cancellationToken);
        }
    }

    private async Task<TutorModelReply> SendAsync(string model, TutorRequest request, TutorOptions settings, CancellationToken cancellationToken)
    {
        var body = new ChatCompletionRequest(
            Model: model,
            Messages: PromptBuilder.Build(request).Select(m => new ChatMessage(m.Role, m.Content)).ToList(),
            Temperature: settings.Temperature,
            MaxCompletionTokens: settings.MaxCompletionTokens,
            ReasoningEffort: settings.ReasoningEffort,
            IncludeReasoning: string.IsNullOrWhiteSpace(settings.ReasoningEffort) ? null : false,
            ResponseFormat: new ResponseFormat("json_schema", new JsonSchemaFormat("tutor_reply", true, PromptBuilder.BuildReplySchema(request.Lesson))));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync("chat/completions", body, WireJson, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or Polly.Timeout.TimeoutRejectedException
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw ProviderErrors.FromTransportException(ServiceName, exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = await ProviderErrors.FromResponseAsync(ServiceName, response, cancellationToken);

                // A rejected request here means the model could not satisfy the schema: worth one more try.
                throw error.Failure == ExternalServiceFailure.InvalidInput
                    ? new ExternalServiceException(ServiceName, ExternalServiceFailure.InvalidResponse, error.Message)
                    : error;
            }

            var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(WireJson, cancellationToken);
            return ParseReply(completion, model);
        }
    }

    private static TutorModelReply ParseReply(ChatCompletionResponse? completion, string requestedModel)
    {
        var choice = completion?.Choices?.FirstOrDefault();
        var content = choice?.Message?.Content;

        if (choice is null || string.IsNullOrWhiteSpace(content))
        {
            throw InvalidResponse("empty completion");
        }

        if (choice.FinishReason == "length")
        {
            throw InvalidResponse("completion was truncated");
        }

        TutorReplyPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<TutorReplyPayload>(content, ReplyJson);
        }
        catch (JsonException exception)
        {
            throw InvalidResponse($"reply is not valid JSON: {exception.Message}");
        }

        var status = payload?.Status switch
        {
            "answered" => TutorReplyStatus.Answered,
            "insufficient_information" => TutorReplyStatus.InsufficientInformation,
            _ => throw InvalidResponse($"unknown status '{payload?.Status}'"),
        };

        return new TutorModelReply(
            status,
            payload.Answer ?? string.Empty,
            payload.CitedSectionIds ?? [],
            completion!.Model ?? requestedModel,
            completion.Usage?.PromptTokens,
            completion.Usage?.CompletionTokens);
    }

    private static ExternalServiceException InvalidResponse(string reason) =>
        new(ServiceName, ExternalServiceFailure.InvalidResponse, reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Model {Model} is rate limited, falling back to {FallbackModel}")]
    private static partial void LogFallback(ILogger logger, string model, string fallbackModel);

    private sealed record ChatCompletionRequest(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        double Temperature,
        int MaxCompletionTokens,
        string? ReasoningEffort,
        bool? IncludeReasoning,
        ResponseFormat ResponseFormat);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ResponseFormat(string Type, JsonSchemaFormat JsonSchema);

    private sealed record JsonSchemaFormat(string Name, bool Strict, JsonObject Schema);

    private sealed record ChatCompletionResponse(string? Model, IReadOnlyList<Choice>? Choices, Usage? Usage);

    private sealed record Choice(ChatMessage? Message, string? FinishReason);

    private sealed record Usage(int? PromptTokens, int? CompletionTokens);

    private sealed record TutorReplyPayload(IReadOnlyList<string>? CitedSectionIds, string? Status, string? Answer);
}
