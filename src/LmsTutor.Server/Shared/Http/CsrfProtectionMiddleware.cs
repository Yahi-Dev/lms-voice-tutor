using Microsoft.AspNetCore.Mvc;

namespace LmsTutor.Server.Shared.Http;

/// <summary>
/// CSRF defense for the cookie-authenticated JSON API. State-changing requests must carry the
/// <c>X-CSRF</c> header, which a cross-site page cannot add without a CORS preflight that this API never
/// grants, and must not be flagged as cross-site by Fetch Metadata or by the Origin header.
/// The session cookie is also SameSite=Strict as defense in depth.
/// </summary>
public sealed class CsrfProtectionMiddleware(RequestDelegate next, IProblemDetailsService problemDetailsService)
{
    public const string HeaderName = "X-CSRF";

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresCheck(context.Request) && !IsSameOriginRequest(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "La solicitud fue rechazada por seguridad.",
            };
            problem.Extensions["code"] = "csrf_validation_failed";

            await problemDetailsService.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
            return;
        }

        await next(context);
    }

    private static bool RequiresCheck(HttpRequest request) =>
        request.Path.StartsWithSegments("/api")
        && !HttpMethods.IsGet(request.Method)
        && !HttpMethods.IsHead(request.Method)
        && !HttpMethods.IsOptions(request.Method);

    private static bool IsSameOriginRequest(HttpRequest request)
    {
        if (!request.Headers.ContainsKey(HeaderName))
        {
            return false;
        }

        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        if (fetchSite.Length > 0)
        {
            return fetchSite is "same-origin" or "none";
        }

        // Older clients do not send Fetch Metadata; fall back to comparing the Origin with the host.
        var origin = request.Headers.Origin.ToString();
        return origin.Length == 0
            || (Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
                && string.Equals(originUri.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase));
    }
}
