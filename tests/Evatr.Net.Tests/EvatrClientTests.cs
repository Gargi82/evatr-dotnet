using System.Net;
using System.Text;
using Evatr.Net.Models;
using AwesomeAssertions;

namespace Evatr.Net.Tests;

public class EvatrClientTests
{
    private static readonly EvatrRequest Qualified = new()
    {
        RequesterVatId = "DE123456789",
        RequestedVatId = "ATU12345678",
        CompanyName = "Beispiel GmbH",
        City = "Wien",
    };

    [Fact]
    public async Task Valid_match_maps_status_and_fields()
    {
        var (client, _) = Create(HttpStatusCode.OK, Fixture("synthetic-valid-match.json"));

        var result = await client.CheckAsync(Qualified, TestContext.Current.CancellationToken);

        result.StatusCode.Should().Be("evatr-0000");
        result.StatusClass.Should().Be(EvatrStatusClass.Valid);
        result.CompanyName.Should().Be(FieldResult.Match);
        result.City.Should().Be(FieldResult.Match);
        result.Street.Should().Be(FieldResult.NotRequested);
        result.PostalCode.Should().Be(FieldResult.NotRequested);
        result.Response!.Id.Should().Be("synthetic-0001");
    }

    [Fact]
    public async Task Mismatch_is_reported_per_field()
    {
        var (client, _) = Create(HttpStatusCode.OK, Fixture("synthetic-mismatch-name.json"));

        var result = await client.CheckAsync(Qualified, TestContext.Current.CancellationToken);

        result.StatusClass.Should().Be(EvatrStatusClass.Valid);
        result.CompanyName.Should().Be(FieldResult.NoMatch);
        result.City.Should().Be(FieldResult.Match);
    }

    [Fact]
    public async Task Not_found_is_invalid()
    {
        var (client, _) = Create(HttpStatusCode.OK, Fixture("synthetic-not-found.json"));

        (await client.CheckAsync(Qualified, TestContext.Current.CancellationToken)).StatusClass.Should().Be(EvatrStatusClass.Invalid);
    }

    [Fact]
    public async Task Temporary_error_body_is_parsed_on_5xx_and_is_retryable()
    {
        var (client, _) = Create(HttpStatusCode.ServiceUnavailable, Fixture("synthetic-unavailable.json"));

        var result = await client.CheckAsync(Qualified, TestContext.Current.CancellationToken);

        result.HttpStatusCode.Should().Be(503);
        result.StatusClass.Should().Be(EvatrStatusClass.Retryable);
    }

    [Fact]
    public async Task Requester_problem_is_flagged_as_setup_error()
    {
        var (client, _) = Create(HttpStatusCode.BadRequest, Fixture("synthetic-requester-error.json"));

        (await client.CheckAsync(Qualified, TestContext.Current.CancellationToken)).StatusClass.Should().Be(EvatrStatusClass.RequesterError);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway, EvatrStatusClass.Retryable)]
    [InlineData(HttpStatusCode.TooManyRequests, EvatrStatusClass.Retryable)]
    [InlineData(HttpStatusCode.BadRequest, EvatrStatusClass.Unknown)]
    public async Task Unparseable_body_is_never_invalid(HttpStatusCode http, EvatrStatusClass expected)
    {
        var (client, _) = Create(http, "<html>gateway</html>");

        var result = await client.CheckAsync(Qualified, TestContext.Current.CancellationToken);

        result.Response.Should().BeNull();
        result.RawResponse.Should().Be("<html>gateway</html>");
        result.StatusClass.Should().Be(expected);
    }

    [Fact]
    public async Task Unknown_status_code_is_unknown_not_invalid()
    {
        var (client, _) = Create(HttpStatusCode.OK, "{\"status\":\"evatr-9999\"}");

        (await client.CheckAsync(Qualified, TestContext.Current.CancellationToken)).StatusClass.Should().Be(EvatrStatusClass.Unknown);
    }

    [Fact]
    public async Task Request_uses_documented_endpoint_and_field_names_and_keeps_raw_strings()
    {
        var (client, handler) = Create(HttpStatusCode.OK, Fixture("synthetic-valid-match.json"));

        var result = await client.CheckAsync(Qualified, TestContext.Current.CancellationToken);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString().Should().Be("https://api.evatr.vies.bzst.de/app/v1/abfrage");
        handler.LastBody.Should().Be(result.RawRequest);
        result.RawRequest.Should().Be(
            "{\"anfragendeUstid\":\"DE123456789\",\"angefragteUstid\":\"ATU12345678\",\"firmenname\":\"Beispiel GmbH\",\"ort\":\"Wien\"}");
        result.RawResponse.Should().Be(Fixture("synthetic-valid-match.json"));
    }

    [Fact]
    public async Task Unqualified_request_omits_optional_fields()
    {
        var (client, _) = Create(HttpStatusCode.OK, Fixture("synthetic-valid-match.json"));
        var request = new EvatrRequest { RequesterVatId = "DE123456789", RequestedVatId = "ATU12345678" };

        var result = await client.CheckAsync(request, TestContext.Current.CancellationToken);

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
    public void Status_catalog_parses_entries()
    {
        var catalog = EvatrStatusCatalog.Parse(Fixture("synthetic-status-catalog.json"));

        catalog.Should().HaveCount(3);
        catalog[2].Should().Be(new EvatrStatus("evatr-2011", "Error", 503, "temporary error"));
    }

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
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "evatr", name)).TrimEnd('\r', '\n');

    private static (EvatrClient, StubHandler) Create(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        return (new EvatrClient(new HttpClient(handler)), handler);
    }
}
