using LmsTutor.IntegrationTests.Infrastructure;
using LmsTutor.Server.Shared.Persistence.Seed;

namespace LmsTutor.IntegrationTests;

public sealed class SecurityHeadersTests(TutorApiFactory factory) : IClassFixture<TutorApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Responses_CarryBrowserHardeningHeaders()
    {
        var client = factory.CreateApiClient();

        var response = await client.GetAsync("/health", _ct);

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Contains("microphone=(self)", Assert.Single(response.Headers.GetValues("Permissions-Policy")), StringComparison.Ordinal);
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task ApiResponses_AreNeverCached()
    {
        var client = factory.CreateApiClient();
        await client.LoginAsync(DemoData.Ana);

        var response = await client.GetAsync("/api/courses", _ct);

        Assert.True(response.Headers.CacheControl?.NoStore);
    }
}
