using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Evatr.Net.Models;

namespace Evatr.Net;

public sealed class EvatrClientOptions
{
    public static readonly Uri DefaultBaseAddress = new("https://api.evatr.vies.bzst.de/");

    public Uri BaseAddress { get; set; } = DefaultBaseAddress;

    /// <summary>
    /// Limits outgoing requests (e.g. a <see cref="System.Threading.RateLimiting.TokenBucketRateLimiter"/>).
    /// BZSt does not document rate limits, so none is applied by default. When no permit is granted
    /// the client throws <see cref="ServiceNotCalledException"/>.
    /// </summary>
    public System.Threading.RateLimiting.RateLimiter? RateLimiter { get; set; }

    /// <summary>
    /// Only call the service inside this daily window; outside it the client throws
    /// <see cref="ServiceNotCalledException"/> with the next opening time. BZSt does not document
    /// service hours, so no window is applied by default.
    /// </summary>
    public ServiceWindow? ServiceWindow { get; set; }

    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
}

/// <summary>
/// Client for the BZSt eVatR REST API. Takes an <see cref="HttpClient"/> so callers can add
/// retries and circuit breaking (e.g. Polly handlers); rate limit and service window are
/// configured via <see cref="EvatrClientOptions"/>. Transport failures propagate as
/// <see cref="HttpRequestException"/> (including <see cref="ServiceNotCalledException"/>) or
/// <see cref="TaskCanceledException"/>; callers must treat them as "retry", never as "invalid".
/// </summary>
public sealed class EvatrClient
{
    private const string CheckPath = "app/v1/abfrage";
    // The OpenAPI server URL is /app, so all paths live under it (the un-prefixed
    // /v1/info/statusmeldungen also answers today, but is not documented).
    private const string StatusCatalogPath = "app/v1/info/statusmeldungen";
    private const string MemberStatesPath = "app/v1/info/eu_mitgliedstaaten";

    private static readonly JsonSerializerOptions RequestJson = new() { PropertyNamingPolicy = null };

    private readonly HttpClient _http;
    private readonly RequestGate _gate;

    public EvatrClient(HttpClient http, EvatrClientOptions? options = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        options ??= new EvatrClientOptions();
        _http.BaseAddress ??= options.BaseAddress;
        _gate = new RequestGate(options.RateLimiter, options.ServiceWindow, options.TimeProvider);
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
        using var lease = await _gate.EnterAsync(ct).ConfigureAwait(false);
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
        using var lease = await _gate.EnterAsync(ct).ConfigureAwait(false);
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
