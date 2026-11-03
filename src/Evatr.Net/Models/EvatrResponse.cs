using System.Text.Json.Serialization;

namespace Evatr.Net.Models;

/// <summary>Wire model of an eVatR response body. Field-result codes stay raw strings; use <see cref="EvatrResult"/> for typed access.</summary>
public sealed record EvatrResponse
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("anfrageZeitpunkt")] public string? RequestTimestamp { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("gueltigAb")] public string? ValidFrom { get; init; }
    [JsonPropertyName("gueltigBis")] public string? ValidTo { get; init; }
    [JsonPropertyName("ergFirmenname")] public string? CompanyNameResult { get; init; }
    [JsonPropertyName("ergStrasse")] public string? StreetResult { get; init; }
    [JsonPropertyName("ergPlz")] public string? PostalCodeResult { get; init; }
    [JsonPropertyName("ergOrt")] public string? CityResult { get; init; }
}
