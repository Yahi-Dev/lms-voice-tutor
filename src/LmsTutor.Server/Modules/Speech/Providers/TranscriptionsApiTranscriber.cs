using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using LmsTutor.Server.Shared.ExternalServices;
using Microsoft.Extensions.Options;

namespace LmsTutor.Server.Modules.Speech.Providers;

/// <summary>
/// Adapter for OpenAI-compatible audio transcription APIs (Groq Whisper by default).
/// </summary>
internal sealed class TranscriptionsApiTranscriber(HttpClient httpClient, IOptions<SpeechOptions> options) : ISpeechTranscriber
{
    private const string ServiceName = "speech";

    // Whisper's prompt is capped at 224 tokens; this keeps the hint well below that.
    private const int MaxHintLength = 600;

    // Whisper tends to "hear" these stock phrases in silent or noisy audio.
    private static readonly string[] KnownHallucinations =
    [
        "amara.org",
        "subtítulos realizados por",
        "subtitulado por",
        "gracias por ver",
        "suscríbete",
    ];

    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<Transcript> TranscribeAsync(AudioClip clip, string? vocabularyHint, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        // The byte array content can be re-sent if the resilience pipeline retries.
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(clip.Content.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue(clip.MediaType);
        form.Add(file, "file", clip.FileName);
        form.Add(new StringContent(settings.Model), "model");
        form.Add(new StringContent(settings.Language), "language");
        form.Add(new StringContent("verbose_json"), "response_format");
        form.Add(new StringContent("0"), "temperature");

        if (!string.IsNullOrWhiteSpace(vocabularyHint))
        {
            form.Add(new StringContent(vocabularyHint.Length <= MaxHintLength ? vocabularyHint : vocabularyHint[..MaxHintLength]), "prompt");
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync("audio/transcriptions", form, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or Polly.Timeout.TimeoutRejectedException
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw ProviderErrors.FromTransportException(ServiceName, exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw await ProviderErrors.FromResponseAsync(ServiceName, response, cancellationToken);
            }

            TranscriptionResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<TranscriptionResponse>(WireJson, cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new ExternalServiceException(ServiceName, ExternalServiceFailure.InvalidResponse, exception.Message);
            }

            var text = result?.Text?.Trim() ?? string.Empty;
            return new Transcript(text, HasSpeech(text, result?.Segments));
        }
    }

    internal static bool HasSpeech(string text, IReadOnlyList<Segment>? segments)
    {
        if (text.Length == 0)
        {
            return false;
        }

        var lowered = text.ToLower(CultureInfo.InvariantCulture);
        if (KnownHallucinations.Any(phrase => lowered.Contains(phrase, StringComparison.Ordinal)))
        {
            return false;
        }

        // Every segment both likely silent and low-confidence: treat the whole clip as silence.
        return segments is not { Count: > 0 }
            || !segments.All(s => s.NoSpeechProb > 0.6 && s.AvgLogprob < -1.0);
    }

    internal sealed record Segment(double NoSpeechProb, double AvgLogprob);

    private sealed record TranscriptionResponse(string? Text, IReadOnlyList<Segment>? Segments);
}
