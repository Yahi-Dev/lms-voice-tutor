namespace LmsTutor.Server.Modules.Catalog;

public sealed class Enrollment
{
    public Guid UserId { get; init; }
    public Guid CourseId { get; init; }
    public DateTime EnrolledAt { get; init; }
}
