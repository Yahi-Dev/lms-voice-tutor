using System.ComponentModel.DataAnnotations;
using LmsTutor.Server.Modules.Tutoring;

namespace LmsTutor.Server.Modules.Speech;

public enum SpeechProvider
{
    /// <summary>Groq when an API key is available (its own or the tutor's), otherwise the demo transcriber.</summary>
    Auto,

    /// <summary>Any OpenAI-compatible audio transcriptions endpoint; Groq by default.</summary>
    Groq,

    /// <summary>Returns a fixed transcript; for tests, CI and running without a key.</summary>
    Fake,
}

public sealed class SpeechOptions
{
    public const string SectionName = "Speech";

    public SpeechProvider Provider { get; set; } = SpeechProvider.Auto;

    [Required, Url]
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";

    /// <summary>Falls back to the tutor's key, since one Groq key covers both services.</summary>
    public string? ApiKey { get; set; }

    [Required]
    public string Model { get; set; } = "whisper-large-v3-turbo";

    [Required, StringLength(5, MinimumLength = 2)]
    public string Language { get; set; } = "es";

    /// <summary>About a minute of compressed speech fits well below this limit.</summary>
    [Range(64_000, 25_000_000)]
    public int MaxUploadBytes { get; set; } = 5_000_000;

    public string FakeTranscript { get; set; } = "¿Para qué sirve POST?";

    [Range(0, 10_000)]
    public int FakeLatencyMilliseconds { get; set; } = 500;

    public SimulatedFailure FakeFailure { get; set; } = SimulatedFailure.None;

    public SpeechProvider EffectiveProvider => Provider switch
    {
        SpeechProvider.Auto => string.IsNullOrWhiteSpace(ApiKey) ? SpeechProvider.Fake : SpeechProvider.Groq,
        var explicitProvider => explicitProvider,
    };
}
