using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Evatr.Net.Models;

namespace Evatr.Net;

public sealed class ViesClientOptions
{
    public static readonly Uri DefaultBaseAddress = new("https://ec.europa.eu/taxation_customs/vies/rest-api/");

    public Uri BaseAddress { get; set; } = DefaultBaseAddress;

    /// <summary>
    /// Sends checks to <c>check-vat-test-service</c> instead of <c>check-vat-number</c>. The test
    /// service returns canned answers keyed by the VAT number (e.g. <c>100</c> valid, <c>200</c>
    /// invalid, <c>301</c> <c>MS_UNAVAILABLE</c>); use it for integration tests, never in production.
    /// </summary>
    public bool UseTestService { get; set; }
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
    private const string TestCheckPath = "check-vat-test-service";
    private const string StatusPath = "check-status";

    private static readonly JsonSerializerOptions RequestJson = new() { PropertyNamingPolicy = null };

    private readonly HttpClient _http;
    private readonly string _checkPath;

    public ViesClient(HttpClient http, ViesClientOptions? options = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        options ??= new ViesClientOptions();
        _http.BaseAddress ??= options.BaseAddress;
        _checkPath = options.UseTestService ? TestCheckPath : CheckPath;
    }

    /// <summary>
    /// Checks a VAT number. The body is parsed whatever the HTTP status: VIES reports errors such
    /// as <c>MS_UNAVAILABLE</c> in <c>errorWrappers</c>, observed with HTTP 200, and possibly 4xx/5xx.
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
        using var response = await _http.PostAsync(_checkPath, content, ct).ConfigureAwait(false);
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
