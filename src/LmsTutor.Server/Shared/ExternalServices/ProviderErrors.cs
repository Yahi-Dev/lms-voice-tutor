using System.Net;

namespace LmsTutor.Server.Shared.ExternalServices;

/// <summary>Maps HTTP-level outcomes of an OpenAI-compatible API to provider-agnostic failures.</summary>
internal static class ProviderErrors
{
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(10);

    public static async Task<ExternalServiceException> FromResponseAsync(string service, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = response.StatusCode;
        var body = await ReadBodyAsync(response, cancellationToken);
        var detail = $"HTTP {(int)status}: {body}";

        return status switch
        {
            HttpStatusCode.TooManyRequests =>
                new ExternalServiceException(service, ExternalServiceFailure.RateLimited, detail, GetRetryAfter(response)),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound =>
                new ExternalServiceException(service, ExternalServiceFailure.Misconfigured, detail),
            HttpStatusCode.BadRequest when body.Contains("model_decommissioned", StringComparison.Ordinal)
                || body.Contains("model_not_found", StringComparison.Ordinal) =>
                new ExternalServiceException(service, ExternalServiceFailure.Misconfigured, detail),
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity or HttpStatusCode.RequestEntityTooLarge =>
                new ExternalServiceException(service, ExternalServiceFailure.InvalidInput, detail),
            _ => new ExternalServiceException(service, ExternalServiceFailure.Unavailable, detail),
        };
    }

    public static ExternalServiceException FromTransportException(string service, Exception exception) => exception switch
    {
        Polly.Timeout.TimeoutRejectedException or TaskCanceledException =>
            new ExternalServiceException(service, ExternalServiceFailure.Timeout, "The provider did not answer in time.", innerException: exception),
        _ => new ExternalServiceException(service, ExternalServiceFailure.Unavailable, exception.Message, innerException: exception),
    };

    private static TimeSpan GetRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
        {
            return delta;
        }

        if (header?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : DefaultRetryAfter;
        }

        return DefaultRetryAfter;
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return body.Length <= 500 ? body : body[..500];
    }
}
