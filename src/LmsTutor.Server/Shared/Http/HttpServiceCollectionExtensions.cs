using LmsTutor.Server.Shared.ExternalServices;

namespace LmsTutor.Server.Shared.Http;

public static class HttpServiceCollectionExtensions
{
    public static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();

        // Handlers run in registration order; the generic one must stay last.
        services.AddExceptionHandler<ExternalServiceExceptionHandler>();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
