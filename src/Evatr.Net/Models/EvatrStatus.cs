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

    /// <summary>Problem with the requester's own VAT ID (setup error on the caller's side).</summary>
    RequesterError,

    /// <summary>The request was rejected: missing field, malformed or German requested VAT ID, unknown country code.</summary>
    InvalidInput,
}

/// <summary>An entry of the status catalog (<c>GET /app/v1/info/statusmeldungen</c>).</summary>
/// <param name="Code">Status code (<c>status</c>), e.g. <c>evatr-0000</c>.</param>
/// <param name="Category">Category (<c>kategorie</c>): <c>Ergebnis</c>, <c>Hinweis</c> or <c>Fehler</c>.</param>
/// <param name="HttpStatus">HTTP status BZSt serves the code with (<c>httpcode</c>); absent for some codes.</param>
/// <param name="Message">German message text (<c>meldung</c>).</param>
/// <param name="Field">Request field the code refers to (<c>feld</c>), e.g. <c>angefragteUstid</c>; may list several, comma-separated.</param>
public sealed record EvatrStatus(string Code, string? Category, int? HttpStatus, string? Message, string? Field = null);
