using System.Text.Json.Serialization;

namespace Evatr.Net.Models;

/// <summary>
/// Request body for VIES <c>POST /check-vat-number</c>. Supplying the requester's VAT ID makes
/// VIES return a consultation number (<c>requestIdentifier</c>); the trader fields request
/// approximate matching where the member state supports it.
/// </summary>
public sealed record ViesRequest
{
    /// <summary>Two-letter VIES country code of the VAT ID to check (Greece is <c>EL</c>).</summary>
    [JsonPropertyName("countryCode")]
    public required string CountryCode { get; init; }

    /// <summary>The VAT number without the country prefix.</summary>
    [JsonPropertyName("vatNumber")]
    public required string VatNumber { get; init; }

    [JsonPropertyName("requesterMemberStateCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequesterCountryCode { get; init; }

    [JsonPropertyName("requesterNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequesterVatNumber { get; init; }

    [JsonPropertyName("traderName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraderName { get; init; }

    [JsonPropertyName("traderStreet")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraderStreet { get; init; }

    [JsonPropertyName("traderPostalCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraderPostalCode { get; init; }

    [JsonPropertyName("traderCity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraderCity { get; init; }

    [JsonPropertyName("traderCompanyType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraderCompanyType { get; init; }

    /// <summary>
    /// Builds a request from full VAT IDs such as <c>DE123456789</c>. Removes whitespace and
    /// uppercases; the serialized body is what gets stored as evidence.
    /// </summary>
    public static ViesRequest FromVatId(string vatId, string? requesterVatId = null)
    {
        var (country, number) = Split(vatId, nameof(vatId));
        if (requesterVatId is null) return new ViesRequest { CountryCode = country, VatNumber = number };

        var (requesterCountry, requesterNumber) = Split(requesterVatId, nameof(requesterVatId));
        return new ViesRequest
        {
            CountryCode = country,
            VatNumber = number,
            RequesterCountryCode = requesterCountry,
            RequesterVatNumber = requesterNumber,
        };
    }

    private static (string Country, string Number) Split(string vatId, string paramName)
    {
        ArgumentNullException.ThrowIfNull(vatId, paramName);
        var compact = string.Concat(vatId.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        if (compact.Length < 3 || !char.IsAsciiLetter(compact[0]) || !char.IsAsciiLetter(compact[1]))
        {
            throw new ArgumentException("VAT ID must start with a two-letter country code followed by the number.", paramName);
        }

        return (compact[..2], compact[2..]);
    }
}
