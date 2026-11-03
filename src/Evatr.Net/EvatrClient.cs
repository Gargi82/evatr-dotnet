using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Evatr.Net.Models;

namespace Evatr.Net;

public sealed class EvatrClientOptions
{
    public static readonly Uri DefaultBaseAddress = new("https://api.evatr.vies.bzst.de/");

    public Uri BaseAddress { get; set; } = DefaultBaseAddress;
}

/// <summary>
/// Client for the BZSt eVatR REST API. Takes an <see cref="HttpClient"/> so callers own
/// retries, rate limiting and circuit breaking (e.g. Polly handlers). Transport failures
/// propagate as <see cref="HttpRequestException"/> / <see cref="TaskCanceledException"/>;
/// callers must treat them as "retry", never as "invalid".
/// </summary>
public sealed class EvatrClient
{
    private const string CheckPath = "app/v1/abfrage";
    private const string StatusCatalogPath = "v1/info/statusmeldungen";
    private const string MemberStatesPath = "app/v1/info/eu_mitgliedstaaten";

    private static readonly JsonSerializerOptions RequestJson = new() { PropertyNamingPolicy = null };

    private readonly HttpClient _http;

    public EvatrClient(HttpClient http, EvatrClientOptions? options = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _http.BaseAddress ??= (options ?? new EvatrClientOptions()).BaseAddress;
    }

    /// <summary>
    /// Runs a (qualified, if name and city are set) confirmation. The body is parsed whatever the
    /// HTTP status, because eVatR reports errors as <c>evatr-xxxx</c> codes on 4xx/5xx too.
    /// </summary>
    public async Task<EvatrResult> CheckAsync(EvatrRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.RequesterVatId)) throw new ArgumentException("RequesterVatId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.RequestedVatId)) throw new ArgumentException("RequestedVatId is required.", nameof(request));

        var rawRequest = JsonSerializer.Serialize(request, RequestJson);
        using var content = new StringContent(rawRequest, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        using var response = await _http.PostAsync(CheckPath, content, ct).ConfigureAwait(false);
        var rawResponse = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        return new EvatrResult
        {
            Request = request,
            RawRequest = rawRequest,
            RawResponse = rawResponse,
            HttpStatusCode = (int)response.StatusCode,
            Response = TryParse(rawResponse),
        };
    }

    /// <summary>Fetches the raw status catalog JSON (store it verbatim if needed).</summary>
    public Task<string> GetStatusCatalogRawAsync(CancellationToken ct = default) => GetStringAsync(StatusCatalogPath, ct);

    /// <summary>Fetches and parses the status catalog.</summary>
    public async Task<IReadOnlyList<EvatrStatus>> GetStatusCatalogAsync(CancellationToken ct = default) =>
        EvatrStatusCatalog.Parse(await GetStatusCatalogRawAsync(ct).ConfigureAwait(false));

    /// <summary>Fetches the raw member-state availability JSON.</summary>
    public Task<string> GetMemberStatesRawAsync(CancellationToken ct = default) => GetStringAsync(MemberStatesPath, ct);

    private async Task<string> GetStringAsync(string path, CancellationToken ct)
    {
        using var response = await _http.GetAsync(path, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    private static EvatrResponse? TryParse(string raw)
    {
        try
        {
            return JsonSerializer.Deserialize<EvatrResponse>(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
