using LmsTutor.Server.Shared.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace LmsTutor.Server.Shared.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        // Configuration is read when the context is resolved, so test hosts can override it.
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

            options.UseSqlite(connectionString);

            if (configuration.GetValue<bool>("Seed:DemoData"))
            {
                options
                    .UseSeeding((context, _) => DemoDataSeeder.SeedAsync((AppDbContext)context, CancellationToken.None).GetAwaiter().GetResult())
                    .UseAsyncSeeding((context, _, cancellationToken) => DemoDataSeeder.SeedAsync((AppDbContext)context, cancellationToken));
            }
        });

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

        return services;
    }
}
