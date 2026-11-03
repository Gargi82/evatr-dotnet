namespace Evatr.Net.Models;

/// <summary>
/// Default mapping of status codes to reactions, verified against the live status catalog
/// (<c>GET /app/v1/info/statusmeldungen</c>) and the eVatR OpenAPI spec on 2026-11-03.
/// Unknown codes deliberately map to <see cref="EvatrStatusClass.Unknown"/>, never to Invalid.
/// </summary>
public static class EvatrStatusClassifier
{
    private static readonly HashSet<string> Valid = new(StringComparer.OrdinalIgnoreCase)
    {
        "evatr-0000", // valid at request time
        "evatr-2008", // valid, but BZSt flags a special case for the qualified check (contact BZSt)
    };

    private static readonly HashSet<string> Invalid = new(StringComparer.OrdinalIgnoreCase)
    {
        "evatr-2001", // not assigned
        "evatr-2002", // not valid yet (valid from gueltigAb)
        "evatr-2006", // no longer valid (was valid gueltigAb..gueltigBis)
    };

    private static readonly HashSet<string> Retryable = new(StringComparer.OrdinalIgnoreCase)
    {
        "evatr-0008", // max qualified requests per session reached
        "evatr-0011", "evatr-0013", "evatr-1001", "evatr-1002", "evatr-1003", "evatr-1004",
        "evatr-2004", "evatr-2011", "evatr-3011", // temporary processing errors
        "evatr-2007", // error processing the member state's data
    };

    private static readonly HashSet<string> RequesterErrors = new(StringComparer.OrdinalIgnoreCase)
    {
        "evatr-0004", // requesting DE VAT ID is syntactically wrong
        "evatr-2005", // requesting DE VAT ID is not valid
    };

    private static readonly HashSet<string> InvalidInput = new(StringComparer.OrdinalIgnoreCase)
    {
        "evatr-0002", // a required field is missing
        "evatr-0003", // valid, but a required field for the qualified check is missing
        "evatr-0005", // requested VAT ID is syntactically wrong
        "evatr-0006", // a DE VAT ID was requested; eVatR only confirms foreign VAT IDs
        "evatr-0012", // requested VAT ID does not fit the issuing scheme
        "evatr-2003", // country code of the requested VAT ID is not valid
    };

    public static EvatrStatusClass Classify(string? statusCode, int httpStatusCode = 200)
    {
        if (statusCode is null)
        {
            // No parseable status: server-side trouble is retryable, anything else is unknown.
            return httpStatusCode is 429 or >= 500 ? EvatrStatusClass.Retryable : EvatrStatusClass.Unknown;
        }

        if (Valid.Contains(statusCode)) return EvatrStatusClass.Valid;
        if (Invalid.Contains(statusCode)) return EvatrStatusClass.Invalid;
        if (Retryable.Contains(statusCode)) return EvatrStatusClass.Retryable;
        if (RequesterErrors.Contains(statusCode)) return EvatrStatusClass.RequesterError;
        if (InvalidInput.Contains(statusCode)) return EvatrStatusClass.InvalidInput;
        return EvatrStatusClass.Unknown;
    }
}
