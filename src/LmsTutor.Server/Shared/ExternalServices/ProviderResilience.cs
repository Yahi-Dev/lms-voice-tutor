using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace LmsTutor.Server.Shared.ExternalServices;

internal static class ProviderResilience
{
    /// <summary>
    /// Tunes the standard resilience pipeline for paid-per-call providers: at most one retry, and only for
    /// transient failures. A 429 is never retried automatically: the free quota is shared, so retrying burns
    /// it faster; the client is told how long to wait instead.
    /// </summary>
    public static void Configure(HttpStandardResilienceOptions options, TimeSpan attemptTimeout, TimeSpan totalTimeout)
    {
        options.AttemptTimeout.Timeout = attemptTimeout;
        options.TotalRequestTimeout.Timeout = totalTimeout;

        options.Retry.MaxRetryAttempts = 1;
        options.Retry.ShouldHandle = args => ValueTask.FromResult(IsTransient(args.Outcome));

        // The validator requires a sampling window of at least twice the attempt timeout.
        options.CircuitBreaker.SamplingDuration = attemptTimeout * 3;
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        options.CircuitBreaker.ShouldHandle = args => ValueTask.FromResult(IsTransient(args.Outcome));
    }

    private static bool IsTransient(Outcome<HttpResponseMessage> outcome) => outcome switch
    {
        { Exception: HttpRequestException or TimeoutRejectedException } => true,
        { Result: { } response } => (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout,
        _ => false,
    };
}
