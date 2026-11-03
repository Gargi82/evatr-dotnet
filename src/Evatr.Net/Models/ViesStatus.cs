namespace Evatr.Net.Models;

/// <summary>How a client should react to a VIES response.</summary>
public enum ViesStatusClass
{
    /// <summary>Outcome not recognised; never treat as invalid.</summary>
    Unknown = 0,

    /// <summary>The VAT number is valid.</summary>
    Valid,

    /// <summary>The VAT number is not valid.</summary>
    Invalid,

    /// <summary>Temporary error (e.g. <c>MS_UNAVAILABLE</c>, <c>TIMEOUT</c>); retry later.</summary>
    Retryable,

    /// <summary>The input was rejected (<c>INVALID_INPUT</c>), e.g. malformed number or unknown country code.</summary>
    InvalidInput,

    /// <summary>Problem with the requester's own VAT ID (<c>INVALID_REQUESTER_INFO</c>).</summary>
    RequesterError,

    /// <summary>VIES blocked the request (<c>IP_BLOCKED</c>, <c>VAT_BLOCKED</c>).</summary>
    Blocked,
}

/// <summary>Approximate-match result for a trader field.</summary>
public enum ViesMatch
{
    /// <summary>Field absent or unrecognised.</summary>
    Unknown = 0,

    /// <summary><c>VALID</c>: the supplied value matches.</summary>
    Valid,

    /// <summary><c>INVALID</c>: the supplied value does not match.</summary>
    Invalid,

    /// <summary><c>NOT_PROCESSED</c>: not requested or not supported by the member state.</summary>
    NotProcessed,
}

public static class ViesMatchParser
{
    public static ViesMatch Parse(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "VALID" => ViesMatch.Valid,
        "INVALID" => ViesMatch.Invalid,
        "NOT_PROCESSED" => ViesMatch.NotProcessed,
        _ => ViesMatch.Unknown,
    };
}

/// <summary>
/// Default mapping of VIES outcomes. An error code always wins over <c>valid</c>.
/// TODO: verify error codes against live responses. Unknown codes map to
/// <see cref="ViesStatusClass.Unknown"/>, never to Invalid.
/// </summary>
public static class ViesStatusClassifier
{
    private static readonly HashSet<string> Retryable = new(StringComparer.OrdinalIgnoreCase)
    {
        "MS_UNAVAILABLE", "SERVICE_UNAVAILABLE", "TIMEOUT",
        "MS_MAX_CONCURRENT_REQ", "MS_MAX_CONCURRENT_REQ_TIME",
        "GLOBAL_MAX_CONCURRENT_REQ", "GLOBAL_MAX_CONCURRENT_REQ_TIME",
    };

    private static readonly HashSet<string> Blocked = new(StringComparer.OrdinalIgnoreCase)
    {
        "IP_BLOCKED", "VAT_BLOCKED",
    };

    public static ViesStatusClass Classify(bool? valid, string? errorCode, int httpStatusCode = 200)
    {
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            if (Retryable.Contains(errorCode)) return ViesStatusClass.Retryable;
            if (Blocked.Contains(errorCode)) return ViesStatusClass.Blocked;
            if (errorCode.Equals("INVALID_INPUT", StringComparison.OrdinalIgnoreCase)) return ViesStatusClass.InvalidInput;
            if (errorCode.Equals("INVALID_REQUESTER_INFO", StringComparison.OrdinalIgnoreCase)) return ViesStatusClass.RequesterError;
            return ViesStatusClass.Unknown;
        }

        // Only trust "valid" on a successful HTTP status.
        if (valid is { } v && httpStatusCode is >= 200 and < 300) return v ? ViesStatusClass.Valid : ViesStatusClass.Invalid;

        return httpStatusCode is 429 or >= 500 ? ViesStatusClass.Retryable : ViesStatusClass.Unknown;
    }
}
