namespace Evatr.Net.Models;

/// <summary>Per-field comparison result of a qualified confirmation.</summary>
public enum FieldResult
{
    /// <summary>Field was not part of the response (null/empty/unrecognised).</summary>
    Unknown = 0,

    /// <summary>A: the supplied value matches the registered value.</summary>
    Match,

    /// <summary>B: the supplied value does not match.</summary>
    NoMatch,

    /// <summary>C: the field was not requested.</summary>
    NotRequested,

    /// <summary>D: the member state did not provide the value.</summary>
    NotProvided,
}

public static class FieldResultParser
{
    /// <summary>Parses the single-letter code returned by eVatR.</summary>
    public static FieldResult Parse(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "A" => FieldResult.Match,
        "B" => FieldResult.NoMatch,
        "C" => FieldResult.NotRequested,
        "D" => FieldResult.NotProvided,
        _ => FieldResult.Unknown,
    };
}
