using LmsTutor.Server.Shared.Http;
using LmsTutor.Server.Shared.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LmsTutor.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the real application against its own SQLite file. Startup applies the real migrations and the
/// demo seeding, so the tests also cover the schema and the seed data.
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
        }));
    }

    /// <summary>
    /// A client that behaves like the SPA: HTTPS (the session cookie is Secure), cookies on, no redirects
    /// followed and the CSRF header on every request.
    /// </summary>
    public HttpClient CreateApiClient(bool includeCsrfHeader = true)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
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

    public async Task<T> WithDbContextAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task<Guid> GetLessonIdAsync(string slug) =>
        WithDbContextAsync(db => db.Lessons.Where(l => l.Slug == slug).Select(l => l.Id).SingleAsync());

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
