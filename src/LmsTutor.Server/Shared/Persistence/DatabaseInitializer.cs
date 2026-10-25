using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LmsTutor.Server.Shared.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies pending migrations (and therefore the configured seeding) when enabled.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue("Database:MigrateOnStartup", defaultValue: true))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        EnsureDataDirectoryExists(db.Database.GetConnectionString());
        await db.Database.MigrateAsync();
    }

    // SQLite creates the database file but not its folder.
    private static void EnsureDataDirectoryExists(string? connectionString)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
