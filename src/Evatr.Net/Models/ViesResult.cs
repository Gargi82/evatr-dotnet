namespace Evatr.Net.Models;

/// <summary>
/// Outcome of one VIES call: typed view plus the exact request/response strings exchanged
/// (kept verbatim for the evidence archive).
/// </summary>
public sealed record ViesResult
{
    public required ViesRequest Request { get; init; }
    public required string RawRequest { get; init; }
    public required string RawResponse { get; init; }
    public required int HttpStatusCode { get; init; }

    /// <summary>Parsed body; null if the body was not parseable JSON.</summary>
    public ViesResponse? Response { get; init; }

    /// <summary>First VIES error code (e.g. <c>MS_UNAVAILABLE</c>), if any.</summary>
    public string? ErrorCode => Response?.ErrorWrappers?.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Error))?.Error;

    public ViesStatusClass StatusClass => ViesStatusClassifier.Classify(Response?.Valid, ErrorCode, HttpStatusCode);

    /// <summary>Consultation number; only issued when the requester VAT ID was supplied.</summary>
    public string? RequestIdentifier => string.IsNullOrWhiteSpace(Response?.RequestIdentifier) ? null : Response.RequestIdentifier;

    public ViesMatch TraderName => ViesMatchParser.Parse(Response?.TraderNameMatch);
    public ViesMatch TraderStreet => ViesMatchParser.Parse(Response?.TraderStreetMatch);
    public ViesMatch TraderPostalCode => ViesMatchParser.Parse(Response?.TraderPostalCodeMatch);
    public ViesMatch TraderCity => ViesMatchParser.Parse(Response?.TraderCityMatch);
    public ViesMatch TraderCompanyType => ViesMatchParser.Parse(Response?.TraderCompanyTypeMatch);
}
