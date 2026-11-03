using System.Text.Json.Serialization;

namespace Evatr.Net.Models;

/// <summary>Request body for <c>POST /app/v1/abfrage</c>. Supplying company name and city makes it a qualified request.</summary>
public sealed record EvatrRequest
{
    /// <summary>The requester's own German VAT ID (required by BZSt).</summary>
    [JsonPropertyName("anfragendeUstid")]
    public required string RequesterVatId { get; init; }

    /// <summary>The foreign VAT ID to confirm (required).</summary>
    [JsonPropertyName("angefragteUstid")]
    public required string RequestedVatId { get; init; }

    [JsonPropertyName("firmenname")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CompanyName { get; init; }

    [JsonPropertyName("ort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? City { get; init; }

    [JsonPropertyName("strasse")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Street { get; init; }

    [JsonPropertyName("plz")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PostalCode { get; init; }

    /// <summary>True when both company name and city are supplied.</summary>
    [JsonIgnore]
    public bool IsQualified => !string.IsNullOrWhiteSpace(CompanyName) && !string.IsNullOrWhiteSpace(City);
}
