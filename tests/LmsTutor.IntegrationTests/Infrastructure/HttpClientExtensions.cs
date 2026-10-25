using System.Net.Http.Json;
using System.Text.Json;
using LmsTutor.Server.Shared.Persistence.Seed;

namespace LmsTutor.IntegrationTests.Infrastructure;

internal static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password }, TestContext.Current.CancellationToken);

    public static async Task LoginAsync(this HttpClient client, DemoData.DemoStudent student)
    {
        var response = await client.LoginAsync(student.Email, student.Password);
        response.EnsureSuccessStatusCode();
    }

    public static async Task<string?> ReadProblemCodeAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
