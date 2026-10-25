using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LmsTutor.Server.Shared.Http;
using LmsTutor.Server.Shared.Security;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace LmsTutor.Server.Modules.Identity;

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128)] string Password);

public sealed record CurrentUserResponse(Guid Id, string Email, string DisplayName)
{
    public static CurrentUserResponse From(AppUser user) => new(user.Id, user.Email ?? string.Empty, user.DisplayName);
}

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/login", LoginAsync).AllowAnonymous();
        auth.MapPost("/logout", LogoutAsync);
        auth.MapGet("/me", GetCurrentUserAsync);

        return api;
    }

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return InvalidCredentials();
        }

        var result = await signInManager.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);

        // Unknown user, wrong password and locked account get the same answer so accounts cannot be enumerated.
        return result.Succeeded ? TypedResults.Ok(CurrentUserResponse.From(user)) : InvalidCredentials();
    }

    private static async Task<NoContent> LogoutAsync(SignInManager<AppUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.FindByIdAsync(principal.GetRequiredUserId().ToString());
        return user is null
            ? ApiProblem.Unauthorized("session_invalid", "La sesión ya no es válida.")
            : TypedResults.Ok(CurrentUserResponse.From(user));
    }

    private static ProblemHttpResult InvalidCredentials() =>
        ApiProblem.Unauthorized("invalid_credentials", "Correo o contraseña incorrectos.");
}
