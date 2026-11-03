namespace Evatr.Net.Tests.Live;

/// <summary>
/// Saves raw responses from live calls as fixtures when <c>EVATR_RECORD_FIXTURES=1</c>. Files go
/// into the source tree (<c>tests/fixtures/&lt;area&gt;/</c>) so unit tests replay what the API sent.
/// Fixtures are published with the MIT library: the requester VAT ID is masked as
/// <see cref="MaskedRequesterVatId"/>, and counterparties must be well-known large companies
/// (never sole traders, whose data is personal data).
/// </summary>
internal static class LiveFixtures
{
    public const string MaskedRequesterVatId = "DE000000000";

    /// <summary>The requester's own DE VAT ID for real (non-sandbox) eVatR checks; tests needing it skip when unset.</summary>
    public static string? RequesterVatId =>
        Environment.GetEnvironmentVariable("EVATR_REQUESTER_VAT_ID") is { Length: > 0 } v ? v.Replace(" ", "").ToUpperInvariant() : null;

    public static bool Recording => Environment.GetEnvironmentVariable("EVATR_RECORD_FIXTURES") == "1";

    public static string RequireRequesterVatId()
    {
        Assert.SkipUnless(RequesterVatId is not null, "Set EVATR_REQUESTER_VAT_ID to run real eVatR checks.");
        return RequesterVatId!;
    }

    public static void Record(string area, string name, string rawResponse)
    {
        if (!Recording) return;
        var path = Path.Combine(FindFixturesRoot(), area, name);
        File.WriteAllText(path, Mask(rawResponse));
    }

    /// <summary>Replaces the requester VAT ID (with and without country prefix) by the masked value.</summary>
    public static string Mask(string raw)
    {
        if (RequesterVatId is not { Length: > 2 } id) return raw;
        return raw.Replace(id, MaskedRequesterVatId, StringComparison.OrdinalIgnoreCase)
                  .Replace(id[2..], MaskedRequesterVatId[2..], StringComparison.OrdinalIgnoreCase);
    }

    public static HttpClient CreateHttpClient() => new() { Timeout = TimeSpan.FromSeconds(60) };

    private static string FindFixturesRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "fixtures");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException("Could not find tests/fixtures above " + AppContext.BaseDirectory);
    }
}
