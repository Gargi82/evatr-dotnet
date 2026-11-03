namespace Evatr.Net.Models;

/// <summary>
/// Default mapping of status codes to reactions, taken from DESIGN.md §4.1.
/// TODO: verify against the live status catalog. Unknown codes deliberately map to
/// <see cref="EvatrStatusClass.Unknown"/>, never to Invalid.
/// </summary>
public static class EvatrStatusClassifier
{
    private static readonly HashSet<string> Retryable = new(StringComparer.OrdinalIgnoreCase)
    {
        "evatr-0011", "evatr-0013", "evatr-1001", "evatr-1002", "evatr-1003", "evatr-1004",
        "evatr-2004", "evatr-2011", "evatr-3011",
    };

    private static readonly HashSet<string> RequesterErrors = new(StringComparer.OrdinalIgnoreCase)
    {
        "evatr-0004", "evatr-0006", "evatr-2005",
    };

    public static EvatrStatusClass Classify(string? statusCode, int httpStatusCode = 200)
    {
        if (statusCode is null)
        {
            // No parseable status: server-side trouble is retryable, anything else is unknown.
            return httpStatusCode is 429 or >= 500 ? EvatrStatusClass.Retryable : EvatrStatusClass.Unknown;
        }

        if (statusCode.Equals("evatr-0000", StringComparison.OrdinalIgnoreCase)) return EvatrStatusClass.Valid;
        if (statusCode.Equals("evatr-2001", StringComparison.OrdinalIgnoreCase)) return EvatrStatusClass.Invalid;
        if (Retryable.Contains(statusCode)) return EvatrStatusClass.Retryable;
        if (RequesterErrors.Contains(statusCode)) return EvatrStatusClass.RequesterError;
        return EvatrStatusClass.Unknown;
    }
}
