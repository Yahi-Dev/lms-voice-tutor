using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LmsTutor.Server.Shared.Http;
using LmsTutor.Server.Shared.Persistence;
using LmsTutor.Server.Shared.RateLimiting;
using LmsTutor.Server.Shared.Security;
using LmsTutor.Server.Shared.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LmsTutor.Server.Modules.Tutoring;

public sealed record AskQuestionRequest(
    [Required, StringLength(2000)] string Question,
    Guid ClientMessageId,
    string? InputMode);

public sealed record TutorCitationResponse(string Key, string Title);

public sealed record TutorExchangeResponse(
    Guid Id,
    Guid ClientMessageId,
    string Question,
    string Answer,
    string Status,
    string InputMode,
    IReadOnlyList<TutorCitationResponse> Citations,
    DateTime CreatedAt);

public static class TutoringEndpoints
{
    private const int HistoryLimit = 100;

    /// <summary>Maps the tutor under the lesson group, which already enforces enrollment.</summary>
    public static RouteGroupBuilder MapTutoringEndpoints(this RouteGroupBuilder lessons)
    {
        lessons.MapGet("/messages", GetMessagesAsync).WithTags("Tutoring");
        lessons.MapPost("/messages", AskAsync).WithTags("Tutoring").RequireRateLimiting(RateLimitPolicies.Tutor);
        return lessons;
    }

    private static async Task<Ok<List<TutorExchangeResponse>>> GetMessagesAsync(
        Guid lessonId,
        ClaimsPrincipal principal,
        TutorService tutor,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        // Always filtered by the session's user: there is no way to ask for someone else's conversation.
        var exchanges = await tutor.GetHistoryAsync(principal.GetRequiredUserId(), lessonId, HistoryLimit, cancellationToken);
        var sectionTitles = await LoadSectionTitlesAsync(db, lessonId, cancellationToken);

        return TypedResults.Ok(exchanges.Select(e => ToResponse(e, sectionTitles)).ToList());
    }

    private static async Task<Results<Ok<TutorExchangeResponse>, ValidationProblem, ProblemHttpResult>> AskAsync(
        Guid lessonId,
        AskQuestionRequest request,
        ClaimsPrincipal principal,
        TutorService tutor,
        AppDbContext db,
        IOptions<TutorOptions> options,
        CancellationToken cancellationToken)
    {
        var question = TextNormalizer.CleanUserText(request.Question);
        var errors = Validate(request, question, options.Value.MaxQuestionLength);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var inputMode = string.Equals(request.InputMode, "voice", StringComparison.OrdinalIgnoreCase)
            ? QuestionInputMode.Voice
            : QuestionInputMode.Text;

        var result = await tutor.AskAsync(
            new AskQuestionCommand(principal.GetRequiredUserId(), lessonId, request.ClientMessageId, question, inputMode),
            cancellationToken);

        if (result is not AskQuestionResult.Answered answered)
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, "idempotency_key_conflict",
                "El identificador del mensaje ya se usó en otra lección.");
        }

        var sectionTitles = await LoadSectionTitlesAsync(db, lessonId, cancellationToken);
        return TypedResults.Ok(ToResponse(answered.Exchange, sectionTitles));
    }

    private static Dictionary<string, string[]> Validate(AskQuestionRequest request, string question, int maxLength)
    {
        var errors = new Dictionary<string, string[]>();

        if (question.Length < 2)
        {
            errors["question"] = ["Escribe una pregunta."];
        }
        else if (question.Length > maxLength)
        {
            errors["question"] = [$"La pregunta no puede superar {maxLength} caracteres."];
        }

        if (request.ClientMessageId == Guid.Empty)
        {
            errors["clientMessageId"] = ["Falta el identificador del mensaje."];
        }

        return errors;
    }

    // Citation titles come from the database, never from model output.
    private static Task<Dictionary<string, string>> LoadSectionTitlesAsync(AppDbContext db, Guid lessonId, CancellationToken cancellationToken) =>
        db.LessonSections.AsNoTracking()
            .Where(s => s.LessonId == lessonId)
            .ToDictionaryAsync(s => s.Key, s => s.Title, StringComparer.Ordinal, cancellationToken);

    private static TutorExchangeResponse ToResponse(TutorExchange exchange, Dictionary<string, string> sectionTitles) => new(
        exchange.Id,
        exchange.ClientMessageId,
        exchange.Question,
        exchange.Answer,
        exchange.Status == TutorReplyStatus.Answered ? "answered" : "insufficient_information",
        exchange.InputMode == QuestionInputMode.Voice ? "voice" : "text",
        exchange.CitedSectionKeys
            .Where(sectionTitles.ContainsKey)
            .Select(key => new TutorCitationResponse(key, sectionTitles[key]))
            .ToList(),
        exchange.CreatedAt);
}
