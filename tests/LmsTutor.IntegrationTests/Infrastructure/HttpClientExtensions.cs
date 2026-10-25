using System.Net.Http.Json;
using System.Text.Json;
using LmsTutor.Server.Modules.Tutoring;
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

    public static Task<HttpResponseMessage> AskAsync(this HttpClient client, Guid lessonId, string question, Guid? clientMessageId = null) =>
        client.PostAsJsonAsync(
            $"/api/lessons/{lessonId}/messages",
            new { question, clientMessageId = clientMessageId ?? Guid.NewGuid(), inputMode = "text" },
            TestContext.Current.CancellationToken);

    public static async Task<TutorExchangeResponse> AskSuccessfullyAsync(this HttpClient client, Guid lessonId, string question)
    {
        var response = await client.AskAsync(lessonId, question);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TutorExchangeResponse>(TestContext.Current.CancellationToken))!;
    }

    public static async Task<List<TutorExchangeResponse>> GetHistoryAsync(this HttpClient client, Guid lessonId) =>
        (await client.GetFromJsonAsync<List<TutorExchangeResponse>>($"/api/lessons/{lessonId}/messages", TestContext.Current.CancellationToken))!;

    public static async Task<string?> ReadProblemCodeAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
