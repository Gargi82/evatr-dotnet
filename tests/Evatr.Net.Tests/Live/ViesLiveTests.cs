using System.Text.Json;
using AwesomeAssertions;
using Evatr.Net.Models;

namespace Evatr.Net.Tests.Live;

/// <summary>
/// Contract tests against the live VIES REST API. Opt-in: excluded by
/// <c>--filter "Category!=Live"</c>. Checks go to the official <c>check-vat-test-service</c>,
/// which returns canned answers keyed by the VAT number.
/// </summary>
[Trait("Category", "Live")]
public sealed class ViesLiveTests : IDisposable
{
    private readonly HttpClient _http = LiveFixtures.CreateHttpClient();
    private readonly ViesClient _client;

    public ViesLiveTests() => _client = new ViesClient(_http, new ViesClientOptions { UseTestService = true });

    public void Dispose() => _http.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("100", null, ViesStatusClass.Valid, "recorded-test-valid.json")]
    [InlineData("200", null, ViesStatusClass.Invalid, "recorded-test-invalid.json")]
    [InlineData("201", "INVALID_INPUT", ViesStatusClass.InvalidInput, "recorded-test-invalid-input.json")]
    [InlineData("202", "INVALID_REQUESTER_INFO", ViesStatusClass.RequesterError, "recorded-test-invalid-requester-info.json")]
    [InlineData("300", "SERVICE_UNAVAILABLE", ViesStatusClass.Retryable, "recorded-test-service-unavailable.json")]
    [InlineData("301", "MS_UNAVAILABLE", ViesStatusClass.Retryable, "recorded-test-ms-unavailable.json")]
    [InlineData("302", "TIMEOUT", ViesStatusClass.Retryable, "recorded-test-timeout.json")]
    [InlineData("400", "VAT_BLOCKED", ViesStatusClass.Blocked, "recorded-test-vat-blocked.json")]
    [InlineData("500", "GLOBAL_MAX_CONCURRENT_REQ", ViesStatusClass.Retryable, "recorded-test-global-max-concurrent-req.json")]
    [InlineData("600", "MS_MAX_CONCURRENT_REQ", ViesStatusClass.Retryable, "recorded-test-ms-max-concurrent-req.json")]
    [InlineData("601", "MS_MAX_CONCURRENT_REQ_TIME", ViesStatusClass.Retryable, "recorded-test-ms-max-concurrent-req-time.json")]
    public async Task Test_service_outcomes_are_classified(string number, string? error, ViesStatusClass expected, string fixture)
    {
        var result = await _client.CheckAsync(new ViesRequest { CountryCode = "DE", VatNumber = number }, Ct);
        LiveFixtures.Record("vies", fixture, result.RawResponse);

        result.Response.Should().NotBeNull();
        result.ErrorCode.Should().Be(error);
        result.StatusClass.Should().Be(expected);
    }

    [Fact]
    public async Task Requester_and_trader_data_yield_consultation_number_and_matches()
    {
        var request = ViesRequest.FromVatId("DE100", "DE100") with { TraderName = "Test GmbH", TraderCity = "Berlin" };

        var result = await _client.CheckAsync(request, Ct);
        LiveFixtures.Record("vies", "recorded-test-valid-with-requester.json", result.RawResponse);

        result.StatusClass.Should().Be(ViesStatusClass.Valid);
        result.RequestIdentifier.Should().NotBeNullOrEmpty();
        result.TraderName.Should().NotBe(ViesMatch.Unknown);
        result.TraderCity.Should().NotBe(ViesMatch.Unknown);
    }

    [Fact]
    public async Task Real_check_of_a_large_company_is_valid()
    {
        using var http = LiveFixtures.CreateHttpClient();
        var result = await new ViesClient(http).CheckAsync(ViesRequest.FromVatId("IE6388047V"), Ct); // Google Ireland Limited
        LiveFixtures.Record("vies", "recorded-real-valid-google-ireland.json", result.RawResponse);

        Assert.SkipWhen(result.StatusClass == ViesStatusClass.Retryable, "IE is unavailable in VIES right now: " + result.ErrorCode);
        result.StatusClass.Should().Be(ViesStatusClass.Valid);
        result.Response!.Name.Should().Contain("GOOGLE");
    }

    [Fact]
    public async Task Real_check_of_an_unassigned_vat_id_is_invalid()
    {
        using var http = LiveFixtures.CreateHttpClient();
        var result = await new ViesClient(http).CheckAsync(ViesRequest.FromVatId("ATU12345675"), Ct); // dummy, correct check digit
        LiveFixtures.Record("vies", "recorded-real-invalid.json", result.RawResponse);

        Assert.SkipWhen(result.StatusClass == ViesStatusClass.Retryable, "AT is unavailable in VIES right now: " + result.ErrorCode);
        result.StatusClass.Should().Be(ViesStatusClass.Invalid);
    }

    [Fact]
    public async Task Status_lists_member_state_availability()
    {
        var raw = await _client.GetStatusRawAsync(Ct);
        LiveFixtures.Record("vies", "recorded-check-status.json", raw);

        using var doc = JsonDocument.Parse(raw);
        doc.RootElement.GetProperty("vow").GetProperty("available").ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
        doc.RootElement.GetProperty("countries").EnumerateArray().Should().Contain(c =>
            c.GetProperty("countryCode").GetString() == "AT" && c.GetProperty("availability").GetString() != null);
    }
}
