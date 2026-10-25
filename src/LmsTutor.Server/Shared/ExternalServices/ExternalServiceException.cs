namespace LmsTutor.Server.Shared.ExternalServices;

public enum ExternalServiceFailure
{
    /// <summary>The provider's quota was exceeded; the caller may retry after <see cref="ExternalServiceException.RetryAfter"/>.</summary>
    RateLimited,

    /// <summary>The provider did not answer in time.</summary>
    Timeout,

    /// <summary>The provider is down or returned a server error.</summary>
    Unavailable,

    /// <summary>Bad key, unknown model or similar: an operator has to fix the configuration.</summary>
    Misconfigured,

    /// <summary>The provider answered with something that could not be used.</summary>
    InvalidResponse,

    /// <summary>The provider rejected the input we sent (for example, audio it cannot decode).</summary>
    InvalidInput,
}

/// <summary>
/// Raised by provider adapters so callers never deal with HTTP details of a specific vendor.
/// </summary>
public sealed class ExternalServiceException(
    string service,
    ExternalServiceFailure failure,
    string message,
    TimeSpan? retryAfter = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>Logical service name used in error codes, for example <c>tutor</c> or <c>speech</c>.</summary>
    public string Service { get; } = service;

    public ExternalServiceFailure Failure { get; } = failure;

    public TimeSpan? RetryAfter { get; } = retryAfter;
}
