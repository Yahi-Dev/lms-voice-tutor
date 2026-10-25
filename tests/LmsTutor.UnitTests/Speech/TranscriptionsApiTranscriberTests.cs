using System.Net;
using LmsTutor.Server.Modules.Speech;
using LmsTutor.Server.Modules.Speech.Providers;
using LmsTutor.Server.Shared.ExternalServices;
using Microsoft.Extensions.Options;

namespace LmsTutor.UnitTests.Speech;

public sealed class TranscriptionsApiTranscriberTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly AudioClip _clip = new(new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0, 0, 0, 0 }, AudioContainer.WebM);

    [Fact]
    public async Task Transcribe_SendsTheAudioWithAMatchingFileNameAndSpanishSettings()
    {
        var handler = new StubHttpMessageHandler((_, _) => Transcription("¿Para qué sirve POST?"));

        await CreateTranscriber(handler).TranscribeAsync(_clip, "Métodos HTTP: GET, POST", _ct);

        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal("https://api.example.test/openai/v1/audio/transcriptions", request.RequestUri!.ToString());
        Assert.Contains("filename=audio.webm", body, StringComparison.Ordinal);
        Assert.Contains("whisper-large-v3-turbo", body, StringComparison.Ordinal);
        Assert.Contains("name=language", body, StringComparison.Ordinal);
        Assert.Contains("verbose_json", body, StringComparison.Ordinal);
        Assert.Contains("Métodos HTTP: GET, POST", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Transcribe_ReturnsTheRecognizedText()
    {
        var handler = new StubHttpMessageHandler((_, _) => Transcription("  ¿Para qué sirve POST?  "));

        var transcript = await CreateTranscriber(handler).TranscribeAsync(_clip, null, _ct);

        Assert.Equal("¿Para qué sirve POST?", transcript.Text);
        Assert.True(transcript.SpeechDetected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Subtítulos realizados por la comunidad de Amara.org")]
    public void HasSpeech_RejectsEmptyTextAndKnownHallucinations(string text)
    {
        Assert.False(TranscriptionsApiTranscriber.HasSpeech(text, segments: null));
    }

    [Fact]
    public void HasSpeech_RejectsClipsWhereEverySegmentLooksSilent()
    {
        var segments = new List<TranscriptionsApiTranscriber.Segment> { new(NoSpeechProb: 0.9, AvgLogprob: -1.4) };

        Assert.False(TranscriptionsApiTranscriber.HasSpeech("POST", segments));
    }

    [Fact]
    public void HasSpeech_AcceptsConfidentSpeech()
    {
        var segments = new List<TranscriptionsApiTranscriber.Segment> { new(NoSpeechProb: 0.02, AvgLogprob: -0.2) };

        Assert.True(TranscriptionsApiTranscriber.HasSpeech("¿Para qué sirve POST?", segments));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, ExternalServiceFailure.RateLimited)]
    [InlineData(HttpStatusCode.BadRequest, ExternalServiceFailure.InvalidInput)]
    [InlineData(HttpStatusCode.Unauthorized, ExternalServiceFailure.Misconfigured)]
    [InlineData(HttpStatusCode.BadGateway, ExternalServiceFailure.Unavailable)]
    public async Task Transcribe_MapsProviderErrorsToFailures(HttpStatusCode status, ExternalServiceFailure expected)
    {
        var handler = new StubHttpMessageHandler((_, _) => StubHttpMessageHandler.Json(status, """{"error":{"message":"nope"}}"""));

        var failure = await Assert.ThrowsAsync<ExternalServiceException>(() => CreateTranscriber(handler).TranscribeAsync(_clip, null, _ct));

        Assert.Equal(expected, failure.Failure);
        Assert.Equal("speech", failure.Service);
    }

    private static TranscriptionsApiTranscriber CreateTranscriber(StubHttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/openai/v1/") };
        return new TranscriptionsApiTranscriber(client, Options.Create(new SpeechOptions { Provider = SpeechProvider.Groq, ApiKey = "test-key" }));
    }

    private static HttpResponseMessage Transcription(string text) =>
        StubHttpMessageHandler.Json(HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(new
        {
            text,
            language = "es",
            segments = new[] { new { text, no_speech_prob = 0.01, avg_logprob = -0.15 } },
        }));
}
