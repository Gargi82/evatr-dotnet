using System.Net;
using AwesomeAssertions;
using Evatr.Net.Models;

namespace Evatr.Net.Tests;

public class ViesClientTests
{
    private static readonly ViesRequest WithRequester = ViesRequest.FromVatId("DE123456789", "DE987654321") with
    {
        TraderName = "Beispiel GmbH",
        TraderCity = "Berlin",
    };

    [Fact]
    public async Task Request_uses_documented_endpoint_and_field_names_and_keeps_raw_strings()
    {
        var (client, handler) = Create(HttpStatusCode.OK, Fixture("synthetic-valid.json"));

        var result = await client.CheckAsync(WithRequester, TestContext.Current.CancellationToken);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString().Should().Be("https://ec.europa.eu/taxation_customs/vies/rest-api/check-vat-number");
        handler.LastBody.Should().Be(result.RawRequest);
        result.RawRequest.Should().Be(
            "{\"countryCode\":\"DE\",\"vatNumber\":\"123456789\",\"requesterMemberStateCode\":\"DE\",\"requesterNumber\":\"987654321\",\"traderName\":\"Beispiel GmbH\",\"traderCity\":\"Berlin\"}");
        result.RawResponse.Should().Be(Fixture("synthetic-valid.json"));
    }

    [Fact]
    public async Task Valid_response_maps_status_consultation_number_and_matches()
    {
        var (client, _) = Create(HttpStatusCode.OK, Fixture("synthetic-valid.json"));

        var result = await client.CheckAsync(WithRequester, TestContext.Current.CancellationToken);

        result.StatusClass.Should().Be(ViesStatusClass.Valid);
        result.RequestIdentifier.Should().Be("WAPIAAAAZsynthetic1");
        result.TraderName.Should().Be(ViesMatch.Valid);
        result.TraderCity.Should().Be(ViesMatch.Invalid);
        result.TraderStreet.Should().Be(ViesMatch.NotProcessed);
        result.Response!.Name.Should().Be("Beispiel GmbH");
    }

    [Fact]
    public async Task Invalid_number_is_invalid_and_empty_consultation_number_is_null()
    {
        var (client, _) = Create(HttpStatusCode.OK, Fixture("synthetic-invalid.json"));

        var result = await client.CheckAsync(ViesRequest.FromVatId("DE000000000"), TestContext.Current.CancellationToken);

        result.StatusClass.Should().Be(ViesStatusClass.Invalid);
        result.RequestIdentifier.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Ms_unavailable_is_retryable_never_invalid(HttpStatusCode status)
    {
        var (client, _) = Create(status, Fixture("synthetic-ms-unavailable.json"));

        var result = await client.CheckAsync(WithRequester, TestContext.Current.CancellationToken);

        result.ErrorCode.Should().Be("MS_UNAVAILABLE");
        result.StatusClass.Should().Be(ViesStatusClass.Retryable);
    }

    [Fact]
    public async Task Invalid_input_is_reported_separately_from_invalid()
    {
        var (client, _) = Create(HttpStatusCode.BadRequest, Fixture("synthetic-invalid-input.json"));

        (await client.CheckAsync(WithRequester, TestContext.Current.CancellationToken))
            .StatusClass.Should().Be(ViesStatusClass.InvalidInput);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway, ViesStatusClass.Retryable)]
    [InlineData(HttpStatusCode.TooManyRequests, ViesStatusClass.Retryable)]
    [InlineData(HttpStatusCode.NotFound, ViesStatusClass.Unknown)]
    public async Task Unparseable_body_classifies_by_http_status(HttpStatusCode status, ViesStatusClass expected)
    {
        var (client, _) = Create(status, "<html>gateway error</html>");

        var result = await client.CheckAsync(WithRequester, TestContext.Current.CancellationToken);

        result.Response.Should().BeNull();
        result.RawResponse.Should().Be("<html>gateway error</html>");
        result.StatusClass.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, null, 200, ViesStatusClass.Valid)]
    [InlineData(false, null, 200, ViesStatusClass.Invalid)]
    [InlineData(false, null, 500, ViesStatusClass.Retryable)]
    [InlineData(true, "TIMEOUT", 200, ViesStatusClass.Retryable)]
    [InlineData(null, "GLOBAL_MAX_CONCURRENT_REQ", 200, ViesStatusClass.Retryable)]
    [InlineData(null, "MS_MAX_CONCURRENT_REQ", 200, ViesStatusClass.Retryable)]
    [InlineData(null, "SERVICE_UNAVAILABLE", 200, ViesStatusClass.Retryable)]
    [InlineData(null, "INVALID_REQUESTER_INFO", 200, ViesStatusClass.RequesterError)]
    [InlineData(null, "IP_BLOCKED", 200, ViesStatusClass.Blocked)]
    [InlineData(null, "VAT_BLOCKED", 200, ViesStatusClass.Blocked)]
    [InlineData(false, "SOMETHING_NEW", 200, ViesStatusClass.Unknown)]
    [InlineData(null, null, 200, ViesStatusClass.Unknown)]
    public void Classifier_maps_outcomes(bool? valid, string? error, int http, ViesStatusClass expected) =>
        ViesStatusClassifier.Classify(valid, error, http).Should().Be(expected);

    [Fact]
    public void FromVatId_splits_and_normalizes()
    {
        var request = ViesRequest.FromVatId(" el 123 456 789 ", "de987654321");

        request.CountryCode.Should().Be("EL");
        request.VatNumber.Should().Be("123456789");
        request.RequesterCountryCode.Should().Be("DE");
        request.RequesterVatNumber.Should().Be("987654321");
    }

    [Theory]
    [InlineData("")]
    [InlineData("DE")]
    [InlineData("123456789")]
    public void FromVatId_rejects_malformed_ids(string vatId)
    {
        var act = () => ViesRequest.FromVatId(vatId);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Invalid_requests_throw_before_any_call()
    {
        var (client, handler) = Create(HttpStatusCode.OK, "{}");
        var ct = TestContext.Current.CancellationToken;

        var badCountry = () => client.CheckAsync(new ViesRequest { CountryCode = "DEU", VatNumber = "123456789" }, ct);
        var noNumber = () => client.CheckAsync(new ViesRequest { CountryCode = "DE", VatNumber = " " }, ct);
        var halfRequester = () => client.CheckAsync(new ViesRequest { CountryCode = "DE", VatNumber = "1", RequesterCountryCode = "DE" }, ct);

        await badCountry.Should().ThrowAsync<ArgumentException>();
        await noNumber.Should().ThrowAsync<ArgumentException>();
        await halfRequester.Should().ThrowAsync<ArgumentException>();
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task Status_endpoint_returns_raw_json()
    {
        const string body = "{\"vow\":{\"available\":true},\"countries\":[{\"countryCode\":\"AT\",\"availability\":\"Available\"}]}";
        var (client, handler) = Create(HttpStatusCode.OK, body);

        var raw = await client.GetStatusRawAsync(TestContext.Current.CancellationToken);

        raw.Should().Be(body);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.ToString().Should().Be("https://ec.europa.eu/taxation_customs/vies/rest-api/check-status");
    }

    [Theory]
    [InlineData("VALID", ViesMatch.Valid)]
    [InlineData("invalid", ViesMatch.Invalid)]
    [InlineData("NOT_PROCESSED", ViesMatch.NotProcessed)]
    [InlineData(null, ViesMatch.Unknown)]
    [InlineData("", ViesMatch.Unknown)]
    public void Match_codes_parse(string? code, ViesMatch expected) =>
        ViesMatchParser.Parse(code).Should().Be(expected);

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "vies", name)).TrimEnd('\r', '\n');

    private static (ViesClient, StubHandler) Create(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        return (new ViesClient(new HttpClient(handler)), handler);
    }
}
