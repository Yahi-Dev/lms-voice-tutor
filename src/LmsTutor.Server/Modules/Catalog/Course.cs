namespace LmsTutor.Server.Modules.Catalog;

public sealed class Course
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Slug { get; init; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public List<Lesson> Lessons { get; init; } = [];
}
