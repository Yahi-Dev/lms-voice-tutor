namespace LmsTutor.Server.Modules.Catalog;

public sealed class Lesson
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CourseId { get; init; }
    public required string Slug { get; init; }
    public required string Title { get; set; }
    public required string Summary { get; set; }
    public int Position { get; set; }
    public List<LessonSection> Sections { get; init; } = [];
}
