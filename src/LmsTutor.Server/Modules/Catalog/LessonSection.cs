namespace LmsTutor.Server.Modules.Catalog;

/// <summary>
/// A titled block of lesson content. <see cref="Key"/> is stable within a lesson so the tutor can
/// cite it, and it is the unit that would be indexed if retrieval is added later.
/// </summary>
public sealed class LessonSection
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LessonId { get; init; }
    public required string Key { get; init; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public int Position { get; set; }
}
