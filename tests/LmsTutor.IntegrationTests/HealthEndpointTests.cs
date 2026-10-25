using System.Net;
using LmsTutor.IntegrationTests.Infrastructure;

namespace LmsTutor.IntegrationTests;

public sealed class HealthEndpointTests(TutorApiFactory factory) : IClassFixture<TutorApiFactory>
{
    [Fact]
    public async Task Health_WhenDatabaseIsReachable_ReturnsOk()
    {
        var client = factory.CreateApiClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
