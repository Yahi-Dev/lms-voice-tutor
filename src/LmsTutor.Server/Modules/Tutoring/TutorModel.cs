namespace LmsTutor.Server.Modules.Tutoring;

public sealed record LessonSectionContent(string Key, string Title, string Body);

/// <summary>The only material the tutor may use: the lesson the student is authorized to read.</summary>
public sealed record LessonContext(Guid Id, string Slug, string Title, IReadOnlyList<LessonSectionContent> Sections);

public sealed record ChatTurn(string Question, string Answer);

public sealed record TutorRequest(LessonContext Lesson, IReadOnlyList<ChatTurn> History, string Question);

public enum TutorReplyStatus
{
    Answered,
    InsufficientInformation,
}

public sealed record TutorModelReply(
    TutorReplyStatus Status,
    string Answer,
    IReadOnlyList<string> CitedSectionKeys,
    string Model,
    int? PromptTokens = null,
    int? CompletionTokens = null);

/// <summary>
/// Port to the language model. Adapters translate provider failures into
/// <see cref="Shared.ExternalServices.ExternalServiceException"/> so callers stay provider-agnostic.
/// </summary>
public interface ITutorModel
{
    Task<TutorModelReply> CompleteAsync(TutorRequest request, CancellationToken cancellationToken);
}
