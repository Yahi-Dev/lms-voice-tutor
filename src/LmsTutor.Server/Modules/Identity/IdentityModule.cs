using System.ComponentModel.DataAnnotations;
using LmsTutor.Server.Shared.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LmsTutor.Server.Modules.Identity;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// <c>Always</c> behind HTTPS. <c>SameAsRequest</c> lets the local http://localhost demo work in every browser.
    /// </summary>
    public CookieSecurePolicy CookieSecurePolicy { get; init; } = CookieSecurePolicy.Always;

    [Range(1, 60)]
    public int LockoutMinutes { get; init; } = 5;

    [Range(3, 20)]
    public int MaxFailedAccessAttempts { get; init; } = 5;
}

public static class IdentityModule
{
    public const string SessionCookieName = "lmstutor.session";

    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddOptions<AuthOptions>()
            .BindConfiguration(AuthOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();

        services.AddIdentityCore<AppUser>(options => options.User.RequireUniqueEmail = true)
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        services.AddOptions<IdentityOptions>().Configure<IOptions<AuthOptions>>((identity, auth) =>
        {
            identity.Lockout.AllowedForNewUsers = true;
            identity.Lockout.MaxFailedAccessAttempts = auth.Value.MaxFailedAccessAttempts;
            identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(auth.Value.LockoutMinutes);
        });

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = SessionCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;

            // The API answers with status codes; the SPA decides what to show.
            options.Events.OnRedirectToLogin = context => WriteStatus(context, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => WriteStatus(context, StatusCodes.Status403Forbidden);
        });

        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<AuthOptions>>((cookie, auth) => cookie.Cookie.SecurePolicy = auth.Value.CookieSecurePolicy);

        // Deny by default: every endpoint requires a session unless it opts out with AllowAnonymous.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    private static Task WriteStatus(Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
