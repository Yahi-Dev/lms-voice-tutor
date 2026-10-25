using LmsTutor.Server.Modules.Catalog;
using LmsTutor.Server.Modules.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LmsTutor.Server.Shared.Persistence.Seed;

/// <summary>
/// Idempotent seeding that runs after every migration (EF Core UseSeeding/UseAsyncSeeding).
/// </summary>
public static class DemoDataSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Slug == DemoData.CourseSlug, cancellationToken);
        if (course is null)
        {
            course = DemoData.CreateCourse();
            db.Courses.Add(course);
        }

        var passwordHasher = new PasswordHasher<AppUser>();

        foreach (var student in DemoData.Students)
        {
            var normalizedEmail = Normalize(student.Email);
            var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

            if (user is null)
            {
                user = CreateUser(student, normalizedEmail);
                user.PasswordHash = passwordHasher.HashPassword(user, student.Password);
                db.Users.Add(user);
            }
            else
            {
                // Demo accounts must never stay locked out between runs of the evaluation.
                user.AccessFailedCount = 0;
                user.LockoutEnd = null;
            }

            var enrolled = await db.Enrollments.AnyAsync(e => e.UserId == user.Id && e.CourseId == course.Id, cancellationToken);
            if (student.EnrolledInCourse && !enrolled)
            {
                db.Enrollments.Add(new Enrollment { UserId = user.Id, CourseId = course.Id, EnrolledAt = DateTime.UtcNow });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static AppUser CreateUser(DemoData.DemoStudent student, string normalizedEmail) => new()
    {
        Id = Guid.CreateVersion7(),
        UserName = student.Email,
        NormalizedUserName = normalizedEmail,
        Email = student.Email,
        NormalizedEmail = normalizedEmail,
        EmailConfirmed = true,
        DisplayName = student.DisplayName,
        SecurityStamp = Guid.NewGuid().ToString("N"),
        ConcurrencyStamp = Guid.NewGuid().ToString(),
        LockoutEnabled = true,
    };

    // Matches Identity's UpperInvariantLookupNormalizer.
    private static string Normalize(string value) => value.Normalize().ToUpperInvariant();
}
