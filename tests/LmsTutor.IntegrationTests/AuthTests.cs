using System.Net;
using System.Net.Http.Json;
using LmsTutor.IntegrationTests.Infrastructure;
using LmsTutor.Server.Modules.Identity;
using LmsTutor.Server.Shared.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LmsTutor.IntegrationTests;

public sealed class AuthTests(TutorApiFactory factory) : IClassFixture<TutorApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_WithValidCredentials_SetsHttpOnlyStrictSessionCookie()
    {
        var client = factory.CreateApiClient();

        var response = await client.LoginAsync(DemoData.Ana.Email, DemoData.Ana.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(IdentityModule.SessionCookieName, StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);

        var user = await response.Content.ReadFromJsonAsync<CurrentUserResponse>(_ct);
        Assert.Equal(DemoData.Ana.Email, user?.Email);
    }

    [Theory]
    [InlineData("ana@demo.local", "wrong-password")]
    [InlineData("nobody@demo.local", "Ana-Demo-2026!")]
    public async Task Login_WithInvalidCredentials_ReturnsTheSameGenericError(string email, string password)
    {
        var client = factory.CreateApiClient();

        var response = await client.LoginAsync(email, password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_credentials", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Login_WithMalformedRequest_ReturnsValidationProblem()
    {
        var client = factory.CreateApiClient();

        var response = await client.LoginAsync("not-an-email", "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutSession_Returns401InsteadOfRedirecting()
    {
        var client = factory.CreateApiClient();

        var response = await client.GetAsync("/api/auth/me", _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Me_AfterLogin_ReturnsTheCurrentUser()
    {
        var client = factory.CreateApiClient();
        await client.LoginAsync(DemoData.Bruno);

        var user = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", _ct);

        Assert.Equal(DemoData.Bruno.DisplayName, user?.DisplayName);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var client = factory.CreateApiClient();
        await client.LoginAsync(DemoData.Ana);

        var logout = await client.PostAsync("/api/auth/logout", content: null, _ct);
        var me = await client.GetAsync("/api/auth/me", _ct);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Login_AfterTooManyFailedAttempts_IsLockedOutEvenWithTheRightPassword()
    {
        const string email = "lockout@test.local";
        const string password = "Lockout-Test-2026!";
        await CreateUserAsync(email, password);
        var client = factory.CreateApiClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await client.LoginAsync(email, "wrong-password");
        }

        var response = await client.LoginAsync(email, password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_credentials", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task StateChangingRequest_WithoutCsrfHeader_IsRejected()
    {
        var client = factory.CreateApiClient(includeCsrfHeader: false);

        var response = await client.LoginAsync(DemoData.Ana.Email, DemoData.Ana.Password);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("csrf_validation_failed", await response.ReadProblemCodeAsync());
    }

    [Theory]
    [InlineData("Sec-Fetch-Site", "cross-site")]
    [InlineData("Origin", "https://attacker.example")]
    public async Task StateChangingRequest_FromAnotherSite_IsRejected(string header, string value)
    {
        var client = factory.CreateApiClient();
        client.DefaultRequestHeaders.Add(header, value);

        var response = await client.LoginAsync(DemoData.Ana.Email, DemoData.Ana.Password);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task CreateUserAsync(string email, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var result = await userManager.CreateAsync(new AppUser { UserName = email, Email = email, DisplayName = "Lockout" }, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
    }
}
