using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LmsTutor.Server.Shared.Http;

/// <summary>
/// Builds RFC 9457 problem responses with a stable machine-readable <c>code</c> the SPA maps to UI states.
/// </summary>
public static class ApiProblem
{
    public static ProblemHttpResult Create(int status, string code, string title, string? detail = null)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        return TypedResults.Problem(problem);
    }

    public static ProblemHttpResult NotFound(string code, string title) =>
        Create(StatusCodes.Status404NotFound, code, title);

    public static ProblemHttpResult Forbidden(string code, string title) =>
        Create(StatusCodes.Status403Forbidden, code, title);

    public static ProblemHttpResult Unauthorized(string code, string title) =>
        Create(StatusCodes.Status401Unauthorized, code, title);
}
