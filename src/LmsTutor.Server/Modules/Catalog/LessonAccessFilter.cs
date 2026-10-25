using LmsTutor.Server.Shared.Http;
using LmsTutor.Server.Shared.Persistence;
using LmsTutor.Server.Shared.Security;
using Microsoft.EntityFrameworkCore;

namespace LmsTutor.Server.Modules.Catalog;

/// <summary>
/// Enrollment policy shared by every endpoint under <c>/api/lessons/{lessonId}</c>: the lesson must exist
/// (404) and the current user must be enrolled in its course (403). It runs before any handler reads lesson
/// content, so neither the content nor the tutor can be reached without access.
/// </summary>
internal sealed class LessonAccessFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (!Guid.TryParse(httpContext.GetRouteValue("lessonId")?.ToString(), out var lessonId))
        {
            return LessonNotFound();
        }

        var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
        var cancellationToken = httpContext.RequestAborted;

        var courseId = await db.Lessons
            .Where(l => l.Id == lessonId)
            .Select(l => (Guid?)l.CourseId)
            .FirstOrDefaultAsync(cancellationToken);

        if (courseId is null)
        {
            return LessonNotFound();
        }

        var userId = httpContext.User.GetRequiredUserId();
        var enrolled = await db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == courseId, cancellationToken);

        return enrolled
            ? await next(context)
            : ApiProblem.Forbidden("lesson_forbidden", "No tienes acceso a esta lección.");
    }

    private static object LessonNotFound() => ApiProblem.NotFound("lesson_not_found", "La lección no existe.");
}
