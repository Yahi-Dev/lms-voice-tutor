using LmsTutor.Server.Modules.Catalog;
using LmsTutor.Server.Modules.Identity;
using LmsTutor.Server.Modules.Tutoring;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LmsTutor.Server.Shared.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonSection> LessonSections => Set<LessonSection>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<TutorExchange> TutorExchanges => Set<TutorExchange>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no time zone type: everything is stored in UTC and read back marked as UTC,
        // so the JSON payload carries the 'Z' suffix and browsers show local time correctly.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}
