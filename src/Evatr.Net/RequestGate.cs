using System.Threading.RateLimiting;

namespace Evatr.Net;

/// <summary>Applies the configured service window and rate limiter before every request.</summary>
internal sealed class RequestGate(RateLimiter? rateLimiter, ServiceWindow? serviceWindow, TimeProvider timeProvider)
{
    public async ValueTask<IDisposable?> EnterAsync(CancellationToken ct)
    {
        if (serviceWindow is not null)
        {
            var now = timeProvider.GetUtcNow();
            if (!serviceWindow.IsOpen(now))
            {
                throw new ServiceNotCalledException(
                    $"Outside the configured service window {serviceWindow.Start}-{serviceWindow.End} ({serviceWindow.TimeZone.Id}).",
                    serviceWindow.NextOpening(now));
            }
        }

        if (rateLimiter is null) return null;

        var lease = await rateLimiter.AcquireAsync(1, ct).ConfigureAwait(false);
        if (lease.IsAcquired) return lease;

        DateTimeOffset? retryAt = lease.TryGetMetadata(MetadataName.RetryAfter, out var after) ? timeProvider.GetUtcNow() + after : null;
        lease.Dispose();
        throw new ServiceNotCalledException("Rate limiter refused a permit.", retryAt);
    }
}
