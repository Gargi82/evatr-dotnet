namespace Evatr.Net.Models;

/// <summary>How a client should react to an eVatR status code.</summary>
public enum EvatrStatusClass
{
    /// <summary>Code not known; never treat as invalid.</summary>
    Unknown = 0,

    /// <summary>The VAT ID is valid (<c>evatr-0000</c>).</summary>
    Valid,

    /// <summary>The VAT ID is not valid / not found.</summary>
    Invalid,

    /// <summary>Temporary processing error; retry later.</summary>
    Retryable,

    /// <summary>Problem with the requester's own VAT ID (merchant setup error).</summary>
    RequesterError,
}

/// <summary>An entry of the status catalog (<c>GET /v1/info/statusmeldungen</c>).</summary>
public sealed record EvatrStatus(string Code, string? Category, int? HttpStatus, string? Message);
