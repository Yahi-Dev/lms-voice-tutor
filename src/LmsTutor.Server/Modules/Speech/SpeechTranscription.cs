namespace LmsTutor.Server.Modules.Speech;

public enum AudioContainer
{
    WebM,
    Ogg,
    Mp4,
    Wav,
}

/// <summary>Recorded audio kept in memory only; it is never written to disk or stored.</summary>
public sealed record AudioClip(ReadOnlyMemory<byte> Content, AudioContainer Container)
{
    /// <summary>Transcription APIs infer the format from the file name, so the extension must match the container.</summary>
    public string FileName => Container switch
    {
        AudioContainer.WebM => "audio.webm",
        AudioContainer.Ogg => "audio.ogg",
        AudioContainer.Mp4 => "audio.m4a",
        AudioContainer.Wav => "audio.wav",
        _ => throw new ArgumentOutOfRangeException(nameof(Container), Container, null),
    };

    public string MediaType => Container switch
    {
        AudioContainer.WebM => "audio/webm",
        AudioContainer.Ogg => "audio/ogg",
        AudioContainer.Mp4 => "audio/mp4",
        AudioContainer.Wav => "audio/wav",
        _ => throw new ArgumentOutOfRangeException(nameof(Container), Container, null),
    };
}

/// <param name="Text">What was understood, ready for the student to review before sending.</param>
/// <param name="SpeechDetected">False when the clip had no intelligible speech.</param>
public sealed record Transcript(string Text, bool SpeechDetected);

/// <summary>
/// Port to the speech-to-text service. The vocabulary hint carries lesson terms that help the recognizer
/// spell technical words correctly.
/// </summary>
public interface ISpeechTranscriber
{
    Task<Transcript> TranscribeAsync(AudioClip clip, string? vocabularyHint, CancellationToken cancellationToken);
}
