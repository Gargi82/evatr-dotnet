using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Evatr.Net.Models;

namespace Evatr.Net;

public sealed class ViesClientOptions
{
    public static readonly Uri DefaultBaseAddress = new("https://ec.europa.eu/taxation_customs/vies/rest-api/");

    public Uri BaseAddress { get; set; } = DefaultBaseAddress;
}

/// <summary>
/// Client for the EU Commission VIES REST API. Takes an <see cref="HttpClient"/> so callers own
/// retries, rate limiting and circuit breaking. Transport failures propagate as
/// <see cref="HttpRequestException"/> / <see cref="TaskCanceledException"/>; callers must treat
/// them, and <see cref="ViesStatusClass.Retryable"/> results such as <c>MS_UNAVAILABLE</c>, as
/// "retry", never as "invalid".
/// </summary>
public sealed class ViesClient
{
    private const string CheckPath = "check-vat-number";
    private const string StatusPath = "check-status";

    private static readonly JsonSerializerOptions RequestJson = new() { PropertyNamingPolicy = null };

    private readonly HttpClient _http;

    public ViesClient(HttpClient http, ViesClientOptions? options = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _http.BaseAddress ??= (options ?? new ViesClientOptions()).BaseAddress;
    }

    /// <summary>
    /// Checks a VAT number. The body is parsed whatever the HTTP status, because VIES reports
    /// errors such as <c>MS_UNAVAILABLE</c> in <c>errorWrappers</c> on 4xx/5xx too.
    /// </summary>
    public async Task<ViesResult> CheckAsync(ViesRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.CountryCode) || request.CountryCode.Length != 2)
            throw new ArgumentException("CountryCode must be a two-letter code.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.VatNumber))
            throw new ArgumentException("VatNumber is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.RequesterCountryCode) != string.IsNullOrWhiteSpace(request.RequesterVatNumber))
            throw new ArgumentException("RequesterCountryCode and RequesterVatNumber must be set together.", nameof(request));

        var rawRequest = JsonSerializer.Serialize(request, RequestJson);
        using var content = new StringContent(rawRequest, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        using var response = await _http.PostAsync(CheckPath, content, ct).ConfigureAwait(false);
        var rawResponse = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        return new ViesResult
        {
            Request = request,
            RawRequest = rawRequest,
            RawResponse = rawResponse,
            HttpStatusCode = (int)response.StatusCode,
            Response = TryParse(rawResponse),
        };
    }

    /// <summary>Fetches the raw member-state availability JSON (<c>GET /check-status</c>).</summary>
    public async Task<string> GetStatusRawAsync(CancellationToken ct = default)
    {
        using var response = await _http.GetAsync(StatusPath, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    private static ViesResponse? TryParse(string raw)
    {
        try
        {
            return JsonSerializer.Deserialize<ViesResponse>(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
