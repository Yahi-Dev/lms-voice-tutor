using System.ComponentModel.DataAnnotations;

namespace LmsTutor.Server.Modules.Tutoring;

public enum TutorProvider
{
    /// <summary>Groq when an API key is configured, otherwise the built-in demo model.</summary>
    Auto,

    /// <summary>Any OpenAI-compatible chat completions endpoint; Groq by default.</summary>
    Groq,

    /// <summary>Deterministic keyword-based model for tests, CI and running without a key.</summary>
    Fake,
}

public enum SimulatedFailure
{
    None,
    RateLimited,
    Unavailable,
}

public sealed class TutorOptions
{
    public const string SectionName = "Tutoring";

    public TutorProvider Provider { get; init; } = TutorProvider.Auto;

    [Required, Url]
    public string BaseUrl { get; init; } = "https://api.groq.com/openai/v1/";

    public string? ApiKey { get; init; }

    [Required]
    public string Model { get; init; } = "openai/gpt-oss-120b";

    /// <summary>Used when the primary model is rate limited; on Groq each model has its own quota.</summary>
    public string? FallbackModel { get; init; } = "openai/gpt-oss-20b";

    [Range(0.0, 2.0)]
    public double Temperature { get; init; } = 0.6;

    /// <summary>Reasoning tokens share this budget, so it must leave room for the visible answer.</summary>
    [Range(256, 8192)]
    public int MaxCompletionTokens { get; init; } = 1500;

    public string? ReasoningEffort { get; init; } = "low";

    /// <summary>How many previous answered turns of the same lesson are sent for follow-up questions.</summary>
    [Range(0, 10)]
    public int HistoryTurns { get; init; } = 4;

    [Range(10, 2000)]
    public int MaxQuestionLength { get; init; } = 500;

    [Range(0, 10_000)]
    public int FakeLatencyMilliseconds { get; init; } = 600;

    /// <summary>Lets the demo model reproduce provider incidents to show how the UI recovers.</summary>
    public SimulatedFailure FakeFailure { get; init; } = SimulatedFailure.None;

    public TutorProvider EffectiveProvider => Provider switch
    {
        TutorProvider.Auto => string.IsNullOrWhiteSpace(ApiKey) ? TutorProvider.Fake : TutorProvider.Groq,
        var explicitProvider => explicitProvider,
    };
}
