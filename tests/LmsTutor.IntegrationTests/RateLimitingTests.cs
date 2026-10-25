using System.Net;
using LmsTutor.IntegrationTests.Infrastructure;

namespace LmsTutor.IntegrationTests;

// Separate fixture: limits are global per host and would interfere with other test classes.
public sealed class RateLimitingTests(TutorApiFactory factory) : IClassFixture<TutorApiFactory>
{
    [Fact]
    public async Task Ask_BeyondThePerMinuteLimit_Returns429WithRetryAfter()
    {
        var app = factory.WithSettings(new Dictionary<string, string?> { ["RateLimiting:TutorRequestsPerMinute"] = "2" });
        var lessonId = await factory.GetLessonIdAsync("metodos-http");
        var student = await factory.CreateStudentAsync(enrolled: true);
        var client = ApiClient.Create(app);
        await client.LoginAsync(student);

        await client.AskAsync(lessonId, "¿Para qué sirve GET?");
        await client.AskAsync(lessonId, "¿Para qué sirve POST?");
        var response = await client.AskAsync(lessonId, "¿Para qué sirve PUT?");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("rate_limited", await response.ReadProblemCodeAsync());
        Assert.NotNull(response.Headers.RetryAfter);
    }
}
