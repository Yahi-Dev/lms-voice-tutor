using LmsTutor.Server.Modules.Catalog;
using LmsTutor.Server.Modules.Identity;
using LmsTutor.Server.Shared.Http;
using LmsTutor.Server.Shared.Persistence;
using LmsTutor.Server.Shared.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LmsTutor.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the real application against its own SQLite file. Startup applies the real migrations and the
/// demo seeding, so the tests also cover the schema and the seed data. The tutor uses the deterministic
/// demo model unless a test replaces it.
/// </summary>
public sealed class TutorApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"lmstutor-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = $"Data Source={_databasePath}",
            ["Database:MigrateOnStartup"] = "true",
            ["Seed:DemoData"] = "true",
            ["Auth:CookieSecurePolicy"] = "Always",
            ["Tutoring:Provider"] = "Fake",
            ["Tutoring:ApiKey"] = "",
            ["Tutoring:FakeLatencyMilliseconds"] = "0",
            ["RateLimiting:TutorRequestsPerMinute"] = "1000",
            ["RateLimiting:SpeechRequestsPerMinute"] = "1000",
            ["RateLimiting:LoginAttemptsPerMinute"] = "1000",
        }));
    }

    /// <summary>A variant of the app with replaced services (for example a scripted tutor model).</summary>
    public WebApplicationFactory<Program> WithServices(Action<IServiceCollection> configureServices) =>
        WithWebHostBuilder(builder => builder.ConfigureTestServices(configureServices));

    /// <summary>A variant of the app with overridden configuration values.</summary>
    public WebApplicationFactory<Program> WithSettings(IDictionary<string, string?> settings) =>
        WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings)));

    public HttpClient CreateApiClient(bool includeCsrfHeader = true) => ApiClient.Create(this, includeCsrfHeader);

    public async Task<T> WithDbContextAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task<Guid> GetLessonIdAsync(string slug) =>
        WithDbContextAsync(db => db.Lessons.Where(l => l.Slug == slug).Select(l => l.Id).SingleAsync());

    /// <summary>Creates an extra student that only exists in the test, optionally enrolled in the demo course.</summary>
    public async Task<DemoData.DemoStudent> CreateStudentAsync(bool enrolled)
    {
        var student = new DemoData.DemoStudent($"student-{Guid.NewGuid():N}@test.local", "Student-Test-2026!", "Test Student", enrolled);

        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = student.Email, Email = student.Email, DisplayName = student.DisplayName };

        var result = await userManager.CreateAsync(user, student.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        if (enrolled)
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var courseId = await db.Courses.Where(c => c.Slug == DemoData.CourseSlug).Select(c => c.Id).SingleAsync();
            db.Enrollments.Add(new Enrollment { UserId = user.Id, CourseId = courseId, EnrolledAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        return student;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            File.Delete(_databasePath + suffix);
        }
    }
}

public static class ApiClient
{
    /// <summary>
    /// A client that behaves like the SPA: HTTPS (the session cookie is Secure), cookies on, no redirects
    /// followed and the CSRF header on every request.
    /// </summary>
    public static HttpClient Create(WebApplicationFactory<Program> factory, bool includeCsrfHeader = true)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        if (includeCsrfHeader)
        {
            client.DefaultRequestHeaders.Add(CsrfProtectionMiddleware.HeaderName, "1");
        }

        return client;
    }
}
