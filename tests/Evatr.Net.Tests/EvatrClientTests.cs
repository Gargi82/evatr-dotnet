using System.Net;
using Evatr.Net.Models;
using AwesomeAssertions;

namespace Evatr.Net.Tests;

public class EvatrClientTests
{
    private static readonly EvatrRequest Qualified = new()
    {
        RequesterVatId = "DE123456789",
        RequestedVatId = "ATU12345678",
        CompanyName = "Musterhaus GmbH & Co KG",
        City = "musterort",
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Full_match_maps_status_and_all_fields()
    {
        var (client, _) = Create(HttpStatusCode.OK, Fixture("recorded-qualified-match.json"));

        var result = await client.CheckAsync(Qualified, Ct);

        result.StatusCode.Should().Be("evatr-0000");
        result.StatusClass.Should().Be(EvatrStatusClass.Valid);
        result.CompanyName.Should().Be(FieldResult.Match);
        result.Street.Should().Be(FieldResult.Match);
        result.PostalCode.Should().Be(FieldResult.Match);
        result.City.Should().Be(FieldResult.Match);
        result.Response!.Id.Should().Be("0710b54ecf2f19f6");
        result.Response.RequestTimestamp.Should().Be("2026-11-03T01:52:44.101259501+01:00");
    }

    [Fact]
    public async Task Mixed_result_is_reported_per_field()
    {
        var (client, _) = Create(HttpStatusCode.OK, Fixture("recorded-qualified-mixed.json"));

        var result = await client.CheckAsync(Qualified, Ct);

        result.StatusClass.Should().Be(EvatrStatusClass.Valid);
        result.CompanyName.Should().Be(FieldResult.Match);
        result.Street.Should().Be(FieldResult.NotProvided);
        result.PostalCode.Should().Be(FieldResult.NotProvided);
        result.City.Should().Be(FieldResult.NoMatch);
    }

    [Fact]
    public async Task Simple_check_has_no_field_results()
    {
        var (client, _) = Create(HttpStatusCode.OK, Fixture("recorded-valid-simple.json"));

        var result = await client.CheckAsync(new EvatrRequest { RequesterVatId = "DE123456789", RequestedVatId = "ATU12345678" }, Ct);

        result.StatusClass.Should().Be(EvatrStatusClass.Valid);
        result.CompanyName.Should().Be(FieldResult.Unknown);
        result.City.Should().Be(FieldResult.Unknown);
    }

    [Fact]
    public async Task Not_found_is_invalid()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, Fixture("recorded-not-found.json"));

        var result = await client.CheckAsync(Qualified with { RequestedVatId = "ATU12345675" }, Ct);

        result.StatusCode.Should().Be("evatr-2001");
        result.StatusClass.Should().Be(EvatrStatusClass.Invalid);
    }

    [Fact]
    public async Task Bad_check_digit_is_invalid_input_not_invalid()
    {
        var (client, _) = Create(HttpStatusCode.BadRequest, Fixture("recorded-bad-check-digit.json"));

        var result = await client.CheckAsync(Qualified with { RequestedVatId = "ATU12345670" }, Ct);

        result.StatusCode.Should().Be("evatr-0005");
        result.StatusClass.Should().Be(EvatrStatusClass.InvalidInput);
    }

    [Fact]
    public async Task Real_qualified_check_reports_member_state_gaps_as_not_provided()
    {
        var (client, _) = Create(HttpStatusCode.OK, Fixture("recorded-real-qualified-google-ireland.json"));

        var result = await client.CheckAsync(Qualified with { RequestedVatId = "IE6388047V" }, Ct);

        result.StatusClass.Should().Be(EvatrStatusClass.Valid);
        result.CompanyName.Should().Be(FieldResult.Match);
        result.Street.Should().Be(FieldResult.Match);
        result.PostalCode.Should().Be(FieldResult.NotProvided);
        result.City.Should().Be(FieldResult.Match);
    }

    [Fact]
    public async Task Temporary_error_body_is_parsed_on_5xx_and_is_retryable()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, Fixture("synthetic-unavailable.json"));

        var result = await client.CheckAsync(Qualified, Ct);

        result.HttpStatusCode.Should().Be(500);
        result.StatusClass.Should().Be(EvatrStatusClass.Retryable);
    }

    [Fact]
    public async Task Requester_problem_is_flagged_as_setup_error()
    {
        var (client, _) = Create(HttpStatusCode.BadRequest, Fixture("recorded-requester-syntax-error.json"));

        var result = await client.CheckAsync(Qualified, Ct);

        result.StatusCode.Should().Be("evatr-0004");
        result.StatusClass.Should().Be(EvatrStatusClass.RequesterError);
    }

    [Fact]
    public async Task German_requested_vat_id_is_invalid_input_not_a_setup_error()
    {
        var (client, _) = Create(HttpStatusCode.Forbidden, Fixture("recorded-requested-de.json"));

        var result = await client.CheckAsync(Qualified with { RequestedVatId = "DE123456789" }, Ct);

        result.StatusCode.Should().Be("evatr-0006");
        result.StatusClass.Should().Be(EvatrStatusClass.InvalidInput);
    }

    [Theory]
    [InlineData("evatr-0000", EvatrStatusClass.Valid)]
    [InlineData("evatr-2008", EvatrStatusClass.Valid)]
    [InlineData("evatr-2001", EvatrStatusClass.Invalid)]
    [InlineData("evatr-2002", EvatrStatusClass.Invalid)]
    [InlineData("evatr-2006", EvatrStatusClass.Invalid)]
    [InlineData("evatr-0008", EvatrStatusClass.Retryable)]
    [InlineData("evatr-2007", EvatrStatusClass.Retryable)]
    [InlineData("evatr-2011", EvatrStatusClass.Retryable)]
    [InlineData("evatr-0004", EvatrStatusClass.RequesterError)]
    [InlineData("evatr-2005", EvatrStatusClass.RequesterError)]
    [InlineData("evatr-0002", EvatrStatusClass.InvalidInput)]
    [InlineData("evatr-0005", EvatrStatusClass.InvalidInput)]
    [InlineData("evatr-0006", EvatrStatusClass.InvalidInput)]
    [InlineData("evatr-2003", EvatrStatusClass.InvalidInput)]
    [InlineData("EVATR-0000", EvatrStatusClass.Valid)]
    [InlineData("evatr-9999", EvatrStatusClass.Unknown)]
    public void Classifier_maps_codes(string code, EvatrStatusClass expected) =>
        EvatrStatusClassifier.Classify(code).Should().Be(expected);

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)] // observed live: HTML 404 from the gateway for a valid request
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.OK)]
    public async Task Non_json_body_is_always_retryable(HttpStatusCode http)
    {
        const string html = "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\"><html><head><title>404 Not Found</title></head></html>";
        var (client, _) = Create(http, html);

        var result = await client.CheckAsync(Qualified, Ct);

        result.Response.Should().BeNull();
        result.RawResponse.Should().Be(html);
        result.StatusClass.Should().Be(EvatrStatusClass.Retryable);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway, EvatrStatusClass.Retryable)]
    [InlineData(HttpStatusCode.TooManyRequests, EvatrStatusClass.Retryable)]
    [InlineData(HttpStatusCode.BadRequest, EvatrStatusClass.Unknown)]
    public async Task Json_without_status_classifies_by_http_status(HttpStatusCode http, EvatrStatusClass expected)
    {
        var (client, _) = Create(http, "{}");

        (await client.CheckAsync(Qualified, Ct)).StatusClass.Should().Be(expected);
    }

    [Fact]
    public async Task Request_uses_documented_endpoint_and_field_names_and_keeps_raw_strings()
    {
        var (client, handler) = Create(HttpStatusCode.OK, Fixture("recorded-qualified-mixed.json"));

        var result = await client.CheckAsync(Qualified, Ct);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString().Should().Be("https://api.evatr.vies.bzst.de/app/v1/abfrage");
        handler.LastBody.Should().Be(result.RawRequest);
        result.RawRequest.Should().Be(
            "{\"anfragendeUstid\":\"DE123456789\",\"angefragteUstid\":\"ATU12345678\",\"firmenname\":\"Musterhaus GmbH \\u0026 Co KG\",\"ort\":\"musterort\"}");
        result.RawResponse.Should().Be(Fixture("recorded-qualified-mixed.json"));
    }

    [Fact]
    public async Task Unqualified_request_omits_optional_fields()
    {
        var (client, _) = Create(HttpStatusCode.OK, Fixture("recorded-valid-simple.json"));
        var request = new EvatrRequest { RequesterVatId = "DE123456789", RequestedVatId = "ATU12345678" };

        var result = await client.CheckAsync(request, Ct);

        request.IsQualified.Should().BeFalse();
        Qualified.IsQualified.Should().BeTrue();
        result.RawRequest.Should().Be("{\"anfragendeUstid\":\"DE123456789\",\"angefragteUstid\":\"ATU12345678\"}");
    }

    [Fact]
    public async Task Missing_vat_ids_throw_before_any_call()
    {
        var (client, handler) = Create(HttpStatusCode.OK, "{}");

        var act = () => client.CheckAsync(new EvatrRequest { RequesterVatId = " ", RequestedVatId = "ATU12345678" });

        await act.Should().ThrowAsync<ArgumentException>();
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task Info_endpoints_use_documented_paths()
    {
        var (client, handler) = Create(HttpStatusCode.OK, Fixture("recorded-status-catalog.json"));

        await client.GetStatusCatalogAsync(Ct);
        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://api.evatr.vies.bzst.de/app/v1/info/statusmeldungen");

        await client.GetMemberStatesRawAsync(Ct);
        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://api.evatr.vies.bzst.de/app/v1/info/eu_mitgliedstaaten");
    }

    [Fact]
    public void Recorded_status_catalog_parses_all_fields()
    {
        var catalog = EvatrStatusCatalog.Parse(Fixture("recorded-status-catalog.json"));

        catalog.Should().HaveCount(26);
        catalog.Should().ContainSingle(s => s.Code == "evatr-0000").Which.Should().Be(new EvatrStatus(
            "evatr-0000", "Ergebnis", 200, "Die angefragte USt-IdNr. ist zum Anfragezeitpunkt gültig."));
        var privacy = catalog.Single(s => s.Code == "evatr-0001");
        privacy.HttpStatus.Should().BeNull();
        privacy.Field.Should().Be("datenschutz");
    }

    [Fact]
    public void Recorded_status_catalog_is_fully_classified() =>
        CatalogAssertions.AssertEveryCodeIsClassified(EvatrStatusCatalog.Parse(Fixture("recorded-status-catalog.json")));

    [Fact]
    public void Status_catalog_accepts_wrapped_array_and_rejects_garbage()
    {
        EvatrStatusCatalog.Parse("{\"statusmeldungen\":[{\"code\":\"evatr-0000\",\"httpcode\":\"200\"}]}")
            .Should().ContainSingle().Which.HttpStatus.Should().Be(200);

        var act = () => EvatrStatusCatalog.Parse("{\"x\":1}");
        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("A", FieldResult.Match)]
    [InlineData("b", FieldResult.NoMatch)]
    [InlineData("C", FieldResult.NotRequested)]
    [InlineData("D", FieldResult.NotProvided)]
    [InlineData(null, FieldResult.Unknown)]
    [InlineData("Z", FieldResult.Unknown)]
    public void Field_result_codes_parse(string? code, FieldResult expected) =>
        FieldResultParser.Parse(code).Should().Be(expected);

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "evatr", name));

    private static (EvatrClient, StubHandler) Create(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        return (new EvatrClient(new HttpClient(handler)), handler);
    }
}
