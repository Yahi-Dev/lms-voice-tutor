using LmsTutor.Server.Modules.Tutoring;
using LmsTutor.Server.Shared.ExternalServices;
using Microsoft.Extensions.Options;

namespace LmsTutor.Server.Modules.Speech.Providers;

/// <summary>
/// Demo transcriber: accepts a real recording and returns a configured sentence, so the voice flow can be
/// shown end to end without an API key.
/// </summary>
internal sealed class FakeSpeechTranscriber(IOptions<SpeechOptions> options, TimeProvider timeProvider) : ISpeechTranscriber
{
    public async Task<Transcript> TranscribeAsync(AudioClip clip, string? vocabularyHint, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (settings.FakeLatencyMilliseconds > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(settings.FakeLatencyMilliseconds), timeProvider, cancellationToken);
        }

        return settings.FakeFailure switch
        {
            SimulatedFailure.RateLimited => throw new ExternalServiceException("speech", ExternalServiceFailure.RateLimited, "Simulated rate limit", TimeSpan.FromSeconds(15)),
            SimulatedFailure.Unavailable => throw new ExternalServiceException("speech", ExternalServiceFailure.Unavailable, "Simulated outage"),
            _ => new Transcript(settings.FakeTranscript, SpeechDetected: true),
        };
    }
}
