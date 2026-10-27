using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LmsTutor.Server.Shared.ExternalServices;

/// <summary>
/// Turns provider failures into problem responses the SPA can recover from: 429 with Retry-After when the
/// quota is exhausted, 422 for input the provider rejected and 503/504 otherwise. Internal details and
/// provider messages are logged, never returned.
/// </summary>
internal sealed partial class ExternalServiceExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ExternalServiceExceptionHandler> logger) : IExceptionHandler
{
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(10);

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ExternalServiceException failure)
        {
            return false;
        }

        LogProviderFailure(logger, failure.Service, failure.Failure, failure.Message);

        var (status, suffix, title) = failure.Failure switch
        {
            ExternalServiceFailure.RateLimited => (StatusCodes.Status429TooManyRequests, "rate_limited",
                "El servicio está recibiendo muchas solicitudes. Intenta de nuevo en unos segundos."),
            ExternalServiceFailure.Timeout => (StatusCodes.Status504GatewayTimeout, "timeout",
                "El servicio tardó demasiado en responder."),
            ExternalServiceFailure.InvalidInput => (StatusCodes.Status422UnprocessableEntity, "invalid_input",
                "El servicio no pudo procesar la solicitud."),
            _ => (StatusCodes.Status503ServiceUnavailable, "unavailable",
                "El servicio no está disponible en este momento."),
        };

        // Rejected input and a misconfigured provider (bad key, unknown model) do not get better by retrying.
        var retryable = failure.Failure is not (ExternalServiceFailure.InvalidInput or ExternalServiceFailure.Misconfigured);
        if (retryable)
        {
            var retryAfter = failure.RetryAfter ?? DefaultRetryAfter;
            httpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions["code"] = $"{failure.Service}_{suffix}";
        problem.Extensions["retryable"] = retryable;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "External service {Service} failed with {Failure}: {Detail}")]
    private static partial void LogProviderFailure(ILogger logger, string service, ExternalServiceFailure failure, string detail);
}
