namespace Evatr.Net.Models;

/// <summary>
/// Outcome of one eVatR call: typed view plus the exact request/response strings exchanged
/// (kept verbatim for the evidence archive).
/// </summary>
public sealed record EvatrResult
{
    public required EvatrRequest Request { get; init; }
    public required string RawRequest { get; init; }
    public required string RawResponse { get; init; }
    public required int HttpStatusCode { get; init; }

    /// <summary>Parsed body; null if the body was not parseable JSON.</summary>
    public EvatrResponse? Response { get; init; }

    public string? StatusCode => Response?.Status;
    public EvatrStatusClass StatusClass => EvatrStatusClassifier.Classify(StatusCode, HttpStatusCode);

    public FieldResult CompanyName => FieldResultParser.Parse(Response?.CompanyNameResult);
    public FieldResult Street => FieldResultParser.Parse(Response?.StreetResult);
    public FieldResult PostalCode => FieldResultParser.Parse(Response?.PostalCodeResult);
    public FieldResult City => FieldResultParser.Parse(Response?.CityResult);
}
