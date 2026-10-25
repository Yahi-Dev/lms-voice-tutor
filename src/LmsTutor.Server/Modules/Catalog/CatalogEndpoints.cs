using System.Security.Claims;
using LmsTutor.Server.Shared.Persistence;
using LmsTutor.Server.Shared.Security;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace LmsTutor.Server.Modules.Catalog;

public sealed record LessonSummaryResponse(Guid Id, string Slug, string Title, string Summary, int Position);

public sealed record CourseSummaryResponse(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    IReadOnlyList<LessonSummaryResponse> Lessons);

public sealed record LessonSectionResponse(string Key, string Title, string Body);

public sealed record LessonResponse(
    Guid Id,
    Guid CourseId,
    string CourseTitle,
    string Slug,
    string Title,
    string Summary,
    IReadOnlyList<LessonSectionResponse> Sections);

public static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/courses", GetMyCoursesAsync).WithTags("Catalog");
        return api;
    }

    /// <summary>
    /// Group for every lesson-scoped endpoint. Other modules (tutoring, speech) map onto it so they inherit
    /// the enrollment check instead of re-implementing it.
    /// </summary>
    public static RouteGroupBuilder MapLessonGroup(this RouteGroupBuilder api)
    {
        var lessons = api.MapGroup("/lessons/{lessonId:guid}").AddEndpointFilter<LessonAccessFilter>();

        lessons.MapGet("", GetLessonAsync).WithTags("Catalog");

        return lessons;
    }

    private static async Task<Ok<List<CourseSummaryResponse>>> GetMyCoursesAsync(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetRequiredUserId();

        var courses = await db.Courses
            .Where(c => db.Enrollments.Any(e => e.UserId == userId && e.CourseId == c.Id))
            .OrderBy(c => c.Title)
            .Select(c => new CourseSummaryResponse(
                c.Id,
                c.Slug,
                c.Title,
                c.Description,
                c.Lessons
                    .OrderBy(l => l.Position)
                    .Select(l => new LessonSummaryResponse(l.Id, l.Slug, l.Title, l.Summary, l.Position))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(courses);
    }

    // Access was already checked by LessonAccessFilter.
    private static async Task<Ok<LessonResponse>> GetLessonAsync(
        Guid lessonId,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var lesson = await db.Lessons
            .Where(l => l.Id == lessonId)
            .Select(l => new LessonResponse(
                l.Id,
                l.CourseId,
                db.Courses.Where(c => c.Id == l.CourseId).Select(c => c.Title).First(),
                l.Slug,
                l.Title,
                l.Summary,
                l.Sections
                    .OrderBy(s => s.Position)
                    .Select(s => new LessonSectionResponse(s.Key, s.Title, s.Body))
                    .ToList()))
            .FirstAsync(cancellationToken);

        return TypedResults.Ok(lesson);
    }
}
