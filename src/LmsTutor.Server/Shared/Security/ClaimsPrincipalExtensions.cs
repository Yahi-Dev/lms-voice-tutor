using System.Security.Claims;

namespace LmsTutor.Server.Shared.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The authenticated user's id. Endpoints never accept a user id from the request, only from the session.
    /// </summary>
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("The current principal has no valid user id.");
    }
}
