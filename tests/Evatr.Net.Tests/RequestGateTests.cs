using System.Net;
using System.Threading.RateLimiting;
using AwesomeAssertions;
using Evatr.Net.Models;

namespace Evatr.Net.Tests;

public class RequestGateTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly EvatrRequest Request = new() { RequesterVatId = "DE123456789", RequestedVatId = "ATU12345678" };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("2026-11-03T05:59:00+01:00", false)]
    [InlineData("2026-11-03T06:00:00+01:00", true)]
    [InlineData("2026-11-03T21:59:00+01:00", true)]
    [InlineData("2026-11-03T22:00:00+01:00", false)]
    [InlineData("2026-11-03T05:30:00+00:00", true)] // 06:30 in Berlin
    public void Day_window_is_evaluated_in_its_time_zone(string now, bool open) =>
        new ServiceWindow { Start = new(6, 0), End = new(22, 0), TimeZone = Berlin }
            .IsOpen(DateTimeOffset.Parse(now)).Should().Be(open);

    [Theory]
    [InlineData("2026-11-03T23:00:00+01:00", true)]
    [InlineData("2026-11-03T01:00:00+01:00", true)]
    [InlineData("2026-11-03T03:00:00+01:00", false)]
    public void Window_may_span_midnight(string now, bool open) =>
        new ServiceWindow { Start = new(22, 0), End = new(2, 0), TimeZone = Berlin }
            .IsOpen(DateTimeOffset.Parse(now)).Should().Be(open);

    [Fact]
    public void Default_time_zone_is_berlin_and_explicit_zone_is_kept()
    {
        new ServiceWindow { Start = new(6, 0), End = new(22, 0) }.TimeZone.BaseUtcOffset.Should().Be(TimeSpan.FromHours(1));
        new ServiceWindow { Start = new(6, 0), End = new(22, 0), TimeZone = TimeZoneInfo.Utc }.TimeZone.Should().Be(TimeZoneInfo.Utc);
    }

    [Fact]
    public void Next_opening_is_today_or_tomorrow()
    {
        var window = new ServiceWindow { Start = new(6, 0), End = new(22, 0), TimeZone = Berlin };

        window.NextOpening(DateTimeOffset.Parse("2026-11-03T02:00:00+01:00")).Should().Be(DateTimeOffset.Parse("2026-11-03T06:00:00+01:00"));
        window.NextOpening(DateTimeOffset.Parse("2026-11-03T23:00:00+01:00")).Should().Be(DateTimeOffset.Parse("2026-11-04T06:00:00+01:00"));
    }

    [Fact]
    public async Task Outside_service_window_no_request_is_sent_and_retry_time_is_given()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}");
        var client = new EvatrClient(new HttpClient(handler), new EvatrClientOptions
        {
            ServiceWindow = new ServiceWindow { Start = new(6, 0), End = new(22, 0), TimeZone = Berlin },
            TimeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2026-11-03T02:00:00+01:00")),
        });

        var act = () => client.CheckAsync(Request, Ct);

        (await act.Should().ThrowAsync<ServiceNotCalledException>())
            .Which.RetryAt.Should().Be(DateTimeOffset.Parse("2026-11-03T06:00:00+01:00"));
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task Rate_limiter_rejection_throws_retryable_exception_without_calling()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}");
        using var limiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = 1, Window = TimeSpan.FromHours(1), QueueLimit = 0, AutoReplenishment = false,
        });
        var client = new ViesClient(new HttpClient(handler), new ViesClientOptions { RateLimiter = limiter });

        await client.CheckAsync(new ViesRequest { CountryCode = "DE", VatNumber = "100" }, Ct);
        handler.LastRequest.Should().NotBeNull();

        var act = () => client.CheckAsync(new ViesRequest { CountryCode = "DE", VatNumber = "200" }, Ct);

        (await act.Should().ThrowAsync<ServiceNotCalledException>()).Which.Should().BeAssignableTo<HttpRequestException>();
        handler.LastBody.Should().Contain("\"100\"");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }
}
