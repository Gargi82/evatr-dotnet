using System.Text.Json;
using AwesomeAssertions;
using Evatr.Net.Models;

namespace Evatr.Net.Tests.Live;

/// <summary>
/// Contract tests against the live BZSt eVatR API. Opt-in: excluded by
/// <c>--filter "Category!=Live"</c>. They use the test pair documented in the eVatR OpenAPI
/// description, which the live API answers without a real requester VAT ID, so no secret is needed.
/// </summary>
[Trait("Category", "Live")]
public sealed class EvatrLiveTests : IDisposable
{
    private const string SandboxRequester = "DE123456789";
    private const string SandboxRequested = "ATU12345678";

    private readonly HttpClient _http = LiveFixtures.CreateHttpClient();
    private readonly EvatrClient _client;

    public EvatrLiveTests() => _client = new EvatrClient(_http);

    public void Dispose() => _http.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Simple_check_is_valid_without_field_results()
    {
        var result = await _client.CheckAsync(new EvatrRequest { RequesterVatId = SandboxRequester, RequestedVatId = SandboxRequested }, Ct);
        LiveFixtures.Record("evatr", "recorded-valid-simple.json", result.RawResponse);

        result.HttpStatusCode.Should().Be(200);
        result.StatusCode.Should().Be("evatr-0000");
        result.StatusClass.Should().Be(EvatrStatusClass.Valid);
        result.Response!.Id.Should().NotBeNullOrEmpty();
        result.Response.RequestTimestamp.Should().NotBeNullOrEmpty();
        result.CompanyName.Should().Be(FieldResult.Unknown);
    }

    [Fact]
    public async Task Qualified_check_with_documented_data_matches_all_fields()
    {
        var request = new EvatrRequest
        {
            RequesterVatId = SandboxRequester,
            RequestedVatId = SandboxRequested,
            CompanyName = "Musterhaus GmbH & Co KG",
            Street = "Musterstrasse 22",
            PostalCode = "12345",
            City = "musterort",
        };

        var result = await _client.CheckAsync(request, Ct);
        LiveFixtures.Record("evatr", "recorded-qualified-match.json", result.RawResponse);

        result.HttpStatusCode.Should().Be(200);
        result.StatusClass.Should().Be(EvatrStatusClass.Valid);
        result.CompanyName.Should().Be(FieldResult.Match);
        result.Street.Should().Be(FieldResult.Match);
        result.PostalCode.Should().Be(FieldResult.Match);
        result.City.Should().Be(FieldResult.Match);
    }

    [Fact]
    public async Task Qualified_check_with_wrong_city_returns_mixed_field_results()
    {
        // The sandbox answers this with A (name), D (street, postal code: not sent) and B (city).
        var request = new EvatrRequest
        {
            RequesterVatId = SandboxRequester,
            RequestedVatId = SandboxRequested,
            CompanyName = "Musterhaus GmbH & Co KG",
            City = "falschort",
        };

        var result = await _client.CheckAsync(request, Ct);
        LiveFixtures.Record("evatr", "recorded-qualified-mixed.json", result.RawResponse);

        result.HttpStatusCode.Should().Be(200);
        result.StatusClass.Should().Be(EvatrStatusClass.Valid);
        result.CompanyName.Should().Be(FieldResult.Match);
        result.City.Should().Be(FieldResult.NoMatch);
        result.Street.Should().Be(FieldResult.NotProvided);
    }

    [Fact]
    public async Task Real_qualified_check_of_a_large_company_is_valid()
    {
        var requester = LiveFixtures.RequireRequesterVatId();
        var request = new EvatrRequest
        {
            RequesterVatId = requester,
            RequestedVatId = "IE6388047V", // Google Ireland Limited
            CompanyName = "Google Ireland Limited",
            Street = "Barrow Street",
            PostalCode = "D04",
            City = "Dublin",
        };

        var result = await _client.CheckAsync(request, Ct);
        LiveFixtures.Record("evatr", "recorded-real-qualified-google-ireland.json", result.RawResponse);

        result.RawResponse.Should().NotContain(requester[2..], "the response must not echo the requester VAT ID");
        result.HttpStatusCode.Should().Be(200);
        result.StatusClass.Should().Be(EvatrStatusClass.Valid);
        result.CompanyName.Should().NotBe(FieldResult.Unknown);
        result.City.Should().NotBe(FieldResult.Unknown);
    }

    /// <summary>Made-up Austrian ID with a correct check digit (ATU + 7 digits + check digit).</summary>
    private const string UnassignedVatId = "ATU12345675";

    /// <summary>Same as <see cref="UnassignedVatId"/> with a wrong check digit.</summary>
    private const string BadChecksumVatId = "ATU12345670";

    [Fact]
    public async Task Real_check_of_an_unassigned_vat_id_is_not_found()
    {
        var requester = LiveFixtures.RequireRequesterVatId();

        var result = await _client.CheckAsync(new EvatrRequest { RequesterVatId = requester, RequestedVatId = UnassignedVatId }, Ct);

        // Assert on the status code only: if someone is ever assigned this number, the test fails
        // here with an obvious reason. A 0000 answer would belong to a real company or person, so it
        // is never recorded.
        result.StatusCode.Should().Be("evatr-2001", $"{UnassignedVatId} is made up and should not be assigned to anyone");
        LiveFixtures.Record("evatr", "recorded-not-found.json", result.RawResponse);
    }

    [Fact]
    public async Task Real_check_of_a_vat_id_with_bad_check_digit_is_invalid_input()
    {
        var requester = LiveFixtures.RequireRequesterVatId();

        var result = await _client.CheckAsync(new EvatrRequest { RequesterVatId = requester, RequestedVatId = BadChecksumVatId }, Ct);

        result.StatusCode.Should().NotBe("evatr-0000", "a wrong check digit can never be a valid VAT ID");
        result.StatusClass.Should().Be(EvatrStatusClass.InvalidInput, $"status was {result.StatusCode}");
        LiveFixtures.Record("evatr", "recorded-bad-check-digit.json", result.RawResponse);
    }

    [Fact]
    public async Task German_requested_vat_id_is_rejected_as_invalid_input()
    {
        var result = await _client.CheckAsync(new EvatrRequest { RequesterVatId = SandboxRequester, RequestedVatId = SandboxRequester }, Ct);
        LiveFixtures.Record("evatr", "recorded-requested-de.json", result.RawResponse);

        result.HttpStatusCode.Should().Be(403);
        result.StatusCode.Should().Be("evatr-0006");
        result.StatusClass.Should().Be(EvatrStatusClass.InvalidInput);
    }

    [Fact]
    public async Task Malformed_requester_vat_id_is_a_requester_error()
    {
        var result = await _client.CheckAsync(new EvatrRequest { RequesterVatId = "DE12345", RequestedVatId = SandboxRequested }, Ct);
        LiveFixtures.Record("evatr", "recorded-requester-syntax-error.json", result.RawResponse);

        result.HttpStatusCode.Should().Be(400);
        result.StatusCode.Should().Be("evatr-0004");
        result.StatusClass.Should().Be(EvatrStatusClass.RequesterError);
    }

    [Fact]
    public async Task Status_catalog_is_fully_classified()
    {
        var raw = await _client.GetStatusCatalogRawAsync(Ct);
        LiveFixtures.Record("evatr", "recorded-status-catalog.json", raw);

        var catalog = EvatrStatusCatalog.Parse(raw);

        catalog.Should().Contain(s => s.Code == "evatr-0000");
        CatalogAssertions.AssertEveryCodeIsClassified(catalog);
    }

    [Fact]
    public async Task Member_states_list_has_country_codes_and_availability()
    {
        var raw = await _client.GetMemberStatesRawAsync(Ct);
        LiveFixtures.Record("evatr", "recorded-member-states.json", raw);

        using var doc = JsonDocument.Parse(raw);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        var austria = doc.RootElement.EnumerateArray().Single(e => e.GetProperty("alpha2").GetString() == "AT");
        austria.GetProperty("verfuegbar").ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
    }
}
