using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LmsTutor.Server.Shared.Http;

/// <summary>
/// Last-resort handler: logs the exception and returns a generic 500 problem without internal details.
/// </summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Since .NET 10 the middleware no longer logs exceptions that a handler reports as handled.
        LogUnhandledException(logger, exception, httpContext.Request.Path);

        // Malformed requests (for example invalid JSON) keep their 4xx status: the client must fix them.
        var (status, title, code) = exception is BadHttpRequestException badRequest
            ? (badRequest.StatusCode, "La solicitud no es válida.", "invalid_request")
            : (StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado.", "unexpected_error");

        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions["code"] = code;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while processing {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string path);
}
