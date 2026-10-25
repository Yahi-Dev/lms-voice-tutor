using LmsTutor.Server.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LmsTutor.Server.Modules.Catalog;

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.Property(c => c.Slug).HasMaxLength(100);
        builder.Property(c => c.Title).HasMaxLength(200);
        builder.Property(c => c.Description).HasMaxLength(1000);
        builder.HasIndex(c => c.Slug).IsUnique();
        builder.HasMany(c => c.Lessons).WithOne().HasForeignKey(l => l.CourseId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.Property(l => l.Slug).HasMaxLength(100);
        builder.Property(l => l.Title).HasMaxLength(200);
        builder.Property(l => l.Summary).HasMaxLength(500);
        builder.HasIndex(l => new { l.CourseId, l.Slug }).IsUnique();
        builder.HasIndex(l => new { l.CourseId, l.Position }).IsUnique();
        builder.HasMany(l => l.Sections).WithOne().HasForeignKey(s => s.LessonId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LessonSectionConfiguration : IEntityTypeConfiguration<LessonSection>
{
    public void Configure(EntityTypeBuilder<LessonSection> builder)
    {
        builder.Property(s => s.Key).HasMaxLength(60);
        builder.Property(s => s.Title).HasMaxLength(200);
        builder.Property(s => s.Body).HasMaxLength(4000);
        builder.HasIndex(s => new { s.LessonId, s.Key }).IsUnique();
        builder.HasIndex(s => new { s.LessonId, s.Position }).IsUnique();
    }
}

internal sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.HasKey(e => new { e.UserId, e.CourseId });
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Course>().WithMany().HasForeignKey(e => e.CourseId).OnDelete(DeleteBehavior.Cascade);
    }
}
