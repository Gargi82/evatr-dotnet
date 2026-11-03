namespace Evatr.Net;

/// <summary>
/// The client did not call the service because of its own configuration: outside the
/// <see cref="ServiceWindow"/>, or the rate limiter refused a permit. Derives from
/// <see cref="HttpRequestException"/> so existing "transport failure → retry" handling applies;
/// it never means "invalid".
/// </summary>
public sealed class ServiceNotCalledException(string message, DateTimeOffset? retryAt) : HttpRequestException(message)
{
    /// <summary>Earliest sensible retry time, if known.</summary>
    public DateTimeOffset? RetryAt { get; } = retryAt;
}
