using AwesomeAssertions;
using Evatr.Net.Models;

namespace Evatr.Net.Tests;

internal static class CatalogAssertions
{
    /// <summary>Codes that never come back from <c>/abfrage</c> (UI-only or generic), so Unknown is fine.</summary>
    private static readonly HashSet<string> AllowedUnknown = ["evatr-0001", "evatr-0007"];

    /// <summary>
    /// Fails when BZSt adds a status code the classifier does not know, or when a code BZSt
    /// serves with HTTP 5xx is not treated as retryable.
    /// </summary>
    public static void AssertEveryCodeIsClassified(IReadOnlyList<EvatrStatus> catalog)
    {
        catalog.Where(s => !AllowedUnknown.Contains(s.Code))
            .Where(s => EvatrStatusClassifier.Classify(s.Code, s.HttpStatus ?? 200) == EvatrStatusClass.Unknown)
            .Select(s => s.Code)
            .Should().BeEmpty("every catalog code needs an explicit classification");

        catalog.Where(s => s.HttpStatus >= 500)
            .Where(s => EvatrStatusClassifier.Classify(s.Code, s.HttpStatus!.Value) != EvatrStatusClass.Retryable)
            .Select(s => s.Code)
            .Should().BeEmpty("5xx codes are temporary and must be retried, never treated as invalid");
    }
}
