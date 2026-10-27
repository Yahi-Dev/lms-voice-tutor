using System.Diagnostics;
using LmsTutor.Server.Shared.ExternalServices;
using LmsTutor.Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LmsTutor.Server.Modules.Tutoring;

public sealed record AskQuestionCommand(Guid UserId, Guid LessonId, Guid ClientMessageId, string Question, QuestionInputMode InputMode);

public abstract record AskQuestionResult
{
    public sealed record Answered(TutorExchange Exchange) : AskQuestionResult;

    /// <summary>The client message id was already used for a different lesson.</summary>
    public sealed record IdempotencyConflict : AskQuestionResult;
}

internal sealed partial class TutorService(
    AppDbContext db,
    ITutorModel model,
    IOptions<TutorOptions> options,
    TimeProvider timeProvider,
    ILogger<TutorService> logger)
{
    internal const string UnreliableAnswerFallback =
        "No encontré en esta lección información suficiente para responder con seguridad. Prueba a reformular la pregunta sobre alguno de sus temas.";

    /// <summary>
    /// Answers a question for a student who already passed the lesson access check. Nothing is stored
    /// unless the model call succeeds, so a failed attempt can simply be retried with the same client id.
    /// </summary>
    public async Task<AskQuestionResult> AskAsync(AskQuestionCommand command, CancellationToken cancellationToken)
    {
        var existing = await FindByClientMessageIdAsync(command.UserId, command.ClientMessageId, cancellationToken);
        if (existing is not null)
        {
            return existing.LessonId == command.LessonId
                ? new AskQuestionResult.Answered(existing)
                : new AskQuestionResult.IdempotencyConflict();
        }

        var lesson = await LoadLessonAsync(command.LessonId, cancellationToken);
        var history = await LoadHistoryAsync(command.UserId, command.LessonId, cancellationToken);
        var request = new TutorRequest(lesson, history, command.Question);

        var stopwatch = Stopwatch.StartNew();
        var reply = await GetReliableReplyAsync(request, cancellationToken);
        stopwatch.Stop();

        var exchange = new TutorExchange
        {
            UserId = command.UserId,
            LessonId = command.LessonId,
            ClientMessageId = command.ClientMessageId,
            Question = command.Question,
            InputMode = command.InputMode,
            Status = reply.Status,
            Answer = reply.Answer,
            CitedSectionKeys = [.. reply.CitedSectionKeys],
            Model = reply.Model,
            PromptTokens = reply.PromptTokens,
            CompletionTokens = reply.CompletionTokens,
            LatencyMilliseconds = (int)stopwatch.ElapsedMilliseconds,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        db.TutorExchanges.Add(exchange);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two identical retries raced: the first one stored wins.
            db.Entry(exchange).State = EntityState.Detached;
            var concurrent = await FindByClientMessageIdAsync(command.UserId, command.ClientMessageId, cancellationToken);
            if (concurrent is null)
            {
                throw;
            }

            return new AskQuestionResult.Answered(concurrent);
        }

        LogExchange(logger, exchange.LessonId, exchange.Status, exchange.Model, exchange.PromptTokens, exchange.CompletionTokens, exchange.LatencyMilliseconds);
        return new AskQuestionResult.Answered(exchange);
    }

    public Task<List<TutorExchange>> GetHistoryAsync(Guid userId, Guid lessonId, int limit, CancellationToken cancellationToken) =>
        db.TutorExchanges
            .Where(e => e.UserId == userId && e.LessonId == lessonId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// One semantic retry when the reply is unusable (bad JSON, truncated, "answered" without a valid
    /// citation). If it is still unusable the student gets an honest "not enough information" answer.
    /// Transport failures are not retried here; the HTTP resilience pipeline already did that.
    /// </summary>
    private async Task<TutorModelReply> GetReliableReplyAsync(TutorRequest request, CancellationToken cancellationToken)
    {
        var sectionKeys = request.Lesson.Sections.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
        string? lastModel = null;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            TutorModelReply reply;
            try
            {
                reply = await model.CompleteAsync(request, cancellationToken);
            }
            catch (ExternalServiceException exception) when (exception.Failure == ExternalServiceFailure.InvalidResponse)
            {
                LogUnusableReply(logger, attempt, exception.Message);
                continue;
            }

            lastModel = reply.Model;
            if (TutorReplyValidator.Validate(reply, sectionKeys) is { } valid)
            {
                return valid;
            }

            LogUnusableReply(logger, attempt, "reply failed validation");
        }

        return new TutorModelReply(TutorReplyStatus.InsufficientInformation, UnreliableAnswerFallback, [], lastModel ?? "unavailable");
    }

    private Task<TutorExchange?> FindByClientMessageIdAsync(Guid userId, Guid clientMessageId, CancellationToken cancellationToken) =>
        db.TutorExchanges.AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId && e.ClientMessageId == clientMessageId, cancellationToken);

    private async Task<LessonContext> LoadLessonAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        var lesson = await db.Lessons.AsNoTracking()
            .Where(l => l.Id == lessonId)
            .Select(l => new
            {
                l.Id,
                l.Slug,
                l.Title,
                Sections = l.Sections.OrderBy(s => s.Position).Select(s => new LessonSectionContent(s.Key, s.Title, s.Body)).ToList(),
            })
            .FirstAsync(cancellationToken);

        return new LessonContext(lesson.Id, lesson.Slug, lesson.Title, lesson.Sections);
    }

    // Only answered turns are replayed, so refusals and injection attempts do not leak into later prompts.
    private async Task<IReadOnlyList<ChatTurn>> LoadHistoryAsync(Guid userId, Guid lessonId, CancellationToken cancellationToken)
    {
        var turns = options.Value.HistoryTurns;
        if (turns == 0)
        {
            return [];
        }

        var recent = await db.TutorExchanges.AsNoTracking()
            .Where(e => e.UserId == userId && e.LessonId == lessonId && e.Status == TutorReplyStatus.Answered)
            .OrderByDescending(e => e.CreatedAt)
            .Take(turns)
            .Select(e => new ChatTurn(e.Question, e.Answer, e.CitedSectionKeys))
            .ToListAsync(cancellationToken);

        recent.Reverse();
        return recent;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Tutor exchange for lesson {LessonId}: {Status} by {Model}, tokens {PromptTokens}/{CompletionTokens}, {LatencyMs} ms")]
    private static partial void LogExchange(ILogger logger, Guid lessonId, TutorReplyStatus status, string model, int? promptTokens, int? completionTokens, int latencyMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unusable tutor reply on attempt {Attempt}: {Reason}")]
    private static partial void LogUnusableReply(ILogger logger, int attempt, string reason);
}
