using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LmsTutor.IntegrationTests.Infrastructure;
using LmsTutor.Server.Modules.Speech;
using LmsTutor.Server.Shared.ExternalServices;
using LmsTutor.Server.Shared.Persistence.Seed;
using Microsoft.Extensions.DependencyInjection;

namespace LmsTutor.IntegrationTests;

public sealed class SpeechTests(TutorApiFactory factory) : IClassFixture<TutorApiFactory>
{
    private static readonly byte[] WebMHeader = [0x1A, 0x45, 0xDF, 0xA3];
    private static readonly byte[] Mp4Header = [0x00, 0x00, 0x00, 0x20, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'M', (byte)'4', (byte)'A', (byte)' '];

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("audio/webm;codecs=opus", "webm")]
    [InlineData("audio/mp4", "mp4")]
    public async Task Transcribe_RecordingFromTheBrowser_ReturnsTheTranscript(string contentType, string container)
    {
        var client = await LoggedInClientAsync(DemoData.Ana);

        var response = await PostAudioAsync(client, await LessonIdAsync(), Audio(container == "webm" ? WebMHeader : Mp4Header), contentType);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transcript = await response.Content.ReadFromJsonAsync<TranscriptionResponse>(_ct);
        Assert.Equal("¿Para qué sirve POST?", transcript!.Text);
    }

    [Fact]
    public async Task Transcribe_ForStudentWithoutAccess_Returns403()
    {
        var client = await LoggedInClientAsync(DemoData.Bruno);

        var response = await PostAudioAsync(client, await LessonIdAsync(), Audio(WebMHeader), "audio/webm");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/octet-stream")]
    public async Task Transcribe_WithUnsupportedContentType_Returns415(string contentType)
    {
        var client = await LoggedInClientAsync(DemoData.Ana);

        var response = await PostAudioAsync(client, await LessonIdAsync(), Audio(WebMHeader), contentType);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("unsupported_audio_format", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Transcribe_WhenContentDoesNotMatchTheDeclaredType_Returns415()
    {
        var client = await LoggedInClientAsync(DemoData.Ana);

        var response = await PostAudioAsync(client, await LessonIdAsync(), "this is not audio"u8.ToArray(), "audio/webm");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Transcribe_LargerThanTheLimit_Returns413()
    {
        var app = factory.WithSettings(new Dictionary<string, string?> { ["Speech:MaxUploadBytes"] = "64000" });
        var client = ApiClient.Create(app);
        await client.LoginAsync(DemoData.Ana);

        var response = await PostAudioAsync(client, await LessonIdAsync(), Audio(WebMHeader, size: 70_000), "audio/webm");

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("audio_too_large", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Transcribe_EmptyBody_Returns400()
    {
        var client = await LoggedInClientAsync(DemoData.Ana);

        var response = await PostAudioAsync(client, await LessonIdAsync(), [], "audio/webm");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transcribe_WhenNoSpeechIsDetected_Returns422()
    {
        var app = factory.WithServices(services => services.AddScoped<ISpeechTranscriber>(_ => new StubSpeechTranscriber(new Transcript("", SpeechDetected: false))));
        var client = ApiClient.Create(app);
        await client.LoginAsync(DemoData.Ana);

        var response = await PostAudioAsync(client, await LessonIdAsync(), Audio(WebMHeader), "audio/webm");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("no_speech_detected", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Transcribe_WhenTheProviderIsDown_Returns503()
    {
        var app = factory.WithServices(services => services.AddScoped<ISpeechTranscriber>(_ =>
            new StubSpeechTranscriber(new ExternalServiceException("speech", ExternalServiceFailure.Unavailable, "down"))));
        var client = ApiClient.Create(app);
        await client.LoginAsync(DemoData.Ana);

        var response = await PostAudioAsync(client, await LessonIdAsync(), Audio(WebMHeader), "audio/webm");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("speech_unavailable", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Transcribe_WithoutCsrfHeader_IsRejected()
    {
        var client = await LoggedInClientAsync(DemoData.Ana);
        client.DefaultRequestHeaders.Remove(Server.Shared.Http.CsrfProtectionMiddleware.HeaderName);

        var response = await PostAudioAsync(client, await LessonIdAsync(), Audio(WebMHeader), "audio/webm");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private Task<Guid> LessonIdAsync() => factory.GetLessonIdAsync("metodos-http");

    private async Task<HttpClient> LoggedInClientAsync(DemoData.DemoStudent student)
    {
        var client = factory.CreateApiClient();
        await client.LoginAsync(student);
        return client;
    }

    private async Task<HttpResponseMessage> PostAudioAsync(HttpClient client, Guid lessonId, byte[] audio, string contentType)
    {
        using var content = new ByteArrayContent(audio);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return await client.PostAsync($"/api/lessons/{lessonId}/transcriptions", content, _ct);
    }

    private static byte[] Audio(byte[] header, int size = 2_048)
    {
        var audio = new byte[size];
        header.CopyTo(audio, 0);
        return audio;
    }

    private sealed class StubSpeechTranscriber : ISpeechTranscriber
    {
        private readonly Transcript? _transcript;
        private readonly Exception? _failure;

        public StubSpeechTranscriber(Transcript transcript) => _transcript = transcript;

        public StubSpeechTranscriber(Exception failure) => _failure = failure;

        public Task<Transcript> TranscribeAsync(AudioClip clip, string? vocabularyHint, CancellationToken cancellationToken) =>
            _failure is null ? Task.FromResult(_transcript!) : Task.FromException<Transcript>(_failure);
    }
}
