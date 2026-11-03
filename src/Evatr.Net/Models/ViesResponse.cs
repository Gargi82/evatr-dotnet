using System.Text.Json.Serialization;

namespace Evatr.Net.Models;

/// <summary>
/// Wire model of a VIES <c>check-vat-number</c> response. Successful checks carry <c>valid</c>;
/// failures carry <c>actionSucceed: false</c> and <c>errorWrappers</c>. Values stay raw strings;
/// use <see cref="ViesResult"/> for typed access.
/// </summary>
public sealed record ViesResponse
{
    [JsonPropertyName("countryCode")] public string? CountryCode { get; init; }
    [JsonPropertyName("vatNumber")] public string? VatNumber { get; init; }
    [JsonPropertyName("requestDate")] public string? RequestDate { get; init; }
    [JsonPropertyName("valid")] public bool? Valid { get; init; }
    [JsonPropertyName("requestIdentifier")] public string? RequestIdentifier { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("address")] public string? Address { get; init; }
    [JsonPropertyName("traderName")] public string? TraderName { get; init; }
    [JsonPropertyName("traderStreet")] public string? TraderStreet { get; init; }
    [JsonPropertyName("traderPostalCode")] public string? TraderPostalCode { get; init; }
    [JsonPropertyName("traderCity")] public string? TraderCity { get; init; }
    [JsonPropertyName("traderCompanyType")] public string? TraderCompanyType { get; init; }
    [JsonPropertyName("traderNameMatch")] public string? TraderNameMatch { get; init; }
    [JsonPropertyName("traderStreetMatch")] public string? TraderStreetMatch { get; init; }
    [JsonPropertyName("traderPostalCodeMatch")] public string? TraderPostalCodeMatch { get; init; }
    [JsonPropertyName("traderCityMatch")] public string? TraderCityMatch { get; init; }
    [JsonPropertyName("traderCompanyTypeMatch")] public string? TraderCompanyTypeMatch { get; init; }
    [JsonPropertyName("actionSucceed")] public bool? ActionSucceed { get; init; }
    [JsonPropertyName("errorWrappers")] public IReadOnlyList<ViesError>? ErrorWrappers { get; init; }
}

public sealed record ViesError
{
    [JsonPropertyName("error")] public string? Error { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
}
