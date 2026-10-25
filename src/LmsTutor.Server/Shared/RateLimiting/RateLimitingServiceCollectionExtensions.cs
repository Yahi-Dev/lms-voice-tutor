using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace LmsTutor.Server.Shared.RateLimiting;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, 1000)]
    public int TutorRequestsPerMinute { get; init; } = 10;

    [Range(1, 1000)]
    public int SpeechRequestsPerMinute { get; init; } = 10;

    [Range(1, 1000)]
    public int LoginAttemptsPerMinute { get; init; } = 10;
}

public static class RateLimitPolicies
{
    public const string Tutor = "tutor";
    public const string Speech = "speech";
    public const string Login = "login";
}

/// <summary>
/// Per-user limits on the endpoints that consume the provider's shared quota, and a per-client limit on login.
/// Limits are read per request, so they can be tuned through configuration without code changes.
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            // The middleware defaults to 503, which the SPA would confuse with "provider down".
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = WriteRejectionAsync;

            limiter.AddPolicy(RateLimitPolicies.Tutor, context =>
                PerMinuteForUser(context, Options(context).TutorRequestsPerMinute));

            limiter.AddPolicy(RateLimitPolicies.Speech, context =>
                PerMinuteForUser(context, Options(context).SpeechRequestsPerMinute));

            limiter.AddPolicy(RateLimitPolicies.Login, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"login:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                    _ => FixedWindow(Options(context).LoginAttemptsPerMinute)));
        });

        return services;
    }

    private static RateLimitingOptions Options(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

    private static RateLimitPartition<string> PerMinuteForUser(HttpContext context, int permits)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter($"{context.GetEndpoint()?.DisplayName}:{userId}", _ => FixedWindow(permits));
    }

    private static FixedWindowRateLimiterOptions FixedWindow(int permits) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
    };

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value) ? value : TimeSpan.FromMinutes(1);
        var httpContext = context.HttpContext;

        httpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Hiciste demasiadas solicitudes seguidas. Espera un momento antes de intentarlo de nuevo.",
        };
        problem.Extensions["code"] = "rate_limited";
        problem.Extensions["retryable"] = true;

        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });
    }
}
