# Evatr.Net

Unofficial .NET client for the German **BZSt eVatR** REST API (simple and qualified confirmation of
foreign EU VAT IDs) and the **EU VIES** REST API.

> **Not affiliated with, endorsed by or connected to the Bundeszentralamt für Steuern (BZSt) or the
> European Commission.** This library only calls their public APIs. It is not tax advice.

- Typed results: status classes (valid, invalid, retry, requester error, invalid input) and the
  per-field A/B/C/D results of a qualified check.
- Keeps the **exact request and response strings** (`RawRequest`, `RawResponse`) so you can store
  them as evidence.
- Temporary failures (`evatr-1001`…, `MS_UNAVAILABLE`, HTML gateway errors, timeouts) are never
  reported as "invalid".
- Optional rate limiter and service window, because neither service documents its limits.
- No dependencies besides `System.Threading.RateLimiting`. Targets .NET 10.

## Install

```sh
dotnet add package Evatr.Net
```

## eVatR (BZSt)

```csharp
using Evatr.Net;
using Evatr.Net.Models;

var client = new EvatrClient(new HttpClient());

var result = await client.CheckAsync(new EvatrRequest
{
    RequesterVatId = "DE123456789",   // your own German VAT ID (required by BZSt)
    RequestedVatId = "ATU12345678",   // the foreign VAT ID to confirm
    CompanyName = "Musterhaus GmbH & Co KG", // name + city make it a qualified check
    City = "musterort",
    Street = "Musterstrasse 22",      // optional
    PostalCode = "12345",             // optional
});

switch (result.StatusClass)
{
    case EvatrStatusClass.Valid:
        Console.WriteLine($"{result.StatusCode}: name {result.CompanyName}, city {result.City}");
        break;
    case EvatrStatusClass.Retryable:
        // try again later; never treat as invalid
        break;
}

// Store result.RawRequest, result.RawResponse and result.HttpStatusCode verbatim.
```

eVatR only confirms **foreign** VAT IDs. Requesting a German VAT ID returns `evatr-0006`
(`InvalidInput`); use VIES for those.

| `EvatrStatusClass` | Status codes |
|---|---|
| `Valid` | `evatr-0000`, `evatr-2008` (valid; BZSt flags a special case for the qualified check) |
| `Invalid` | `evatr-2001` not assigned, `evatr-2002` not yet valid, `evatr-2006` no longer valid |
| `Retryable` | `evatr-0008`, `0011`, `0013`, `1001`–`1004`, `2004`, `2007`, `2011`, `3011`; any non-JSON response; 429/5xx without status |
| `RequesterError` | `evatr-0004`, `evatr-2005` (problem with your own DE VAT ID) |
| `InvalidInput` | `evatr-0002`, `0003`, `0005`, `0006`, `0012`, `2003` |
| `Unknown` | anything else — never treat as invalid |

Field results (`CompanyName`, `Street`, `PostalCode`, `City`): `Match` (A), `NoMatch` (B),
`NotRequested` (C), `NotProvided` (D, not provided by the member state).

Note that `evatr-2002`/`2006` come with HTTP 200 and `evatr-2001` with HTTP 404: always classify by
the status code, not the HTTP status.

Status catalog and member-state availability: `GetStatusCatalogAsync()`,
`GetStatusCatalogRawAsync()`, `GetMemberStatesRawAsync()`.

## VIES (EU Commission)

```csharp
var vies = new ViesClient(new HttpClient());

// With your own VAT ID, VIES returns a consultation number (RequestIdentifier).
var result = await vies.CheckAsync(ViesRequest.FromVatId("IE6388047V", requesterVatId: "DE123456789"));

if (result.StatusClass == ViesStatusClass.Valid)
    Console.WriteLine($"{result.Response!.Name} — consultation number {result.RequestIdentifier}");
```

`ViesStatusClass`: `Valid`, `Invalid`, `Retryable` (`MS_UNAVAILABLE`, `SERVICE_UNAVAILABLE`,
`TIMEOUT`, `*_MAX_CONCURRENT_REQ*`, non-JSON responses), `InvalidInput`, `RequesterError`, `Blocked`,
`Unknown`. VIES reports these errors with **HTTP 200**.

## Rate limits and service hours

**BZSt and the EU Commission do not publicly document rate limits or service hours for these
APIs.** On eVatR, `evatr-0008` signals a per-session limit for qualified checks, and VIES answers
`MS_MAX_CONCURRENT_REQ`/`GLOBAL_MAX_CONCURRENT_REQ` when overloaded; gateway HTML errors were
observed at night. Because the real limits are unknown, the library applies **none by default** and
makes both configurable:

```csharp
using System.Threading.RateLimiting;

var client = new EvatrClient(httpClient, new EvatrClientOptions
{
    // any System.Threading.RateLimiting limiter, e.g. 1 request per second
    RateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
    {
        TokenLimit = 1, TokensPerPeriod = 1, ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        QueueLimit = 100, AutoReplenishment = true,
    }),
    // only call between 05:00 and 23:00 Berlin time (Europe/Berlin is the default time zone)
    ServiceWindow = new ServiceWindow { Start = new(5, 0), End = new(23, 0) },
});
```

`ViesClientOptions` has the same `RateLimiter` and `ServiceWindow` settings. When the limiter
refuses a permit or the window is closed, no request is sent and the client throws
`ServiceNotCalledException` (an `HttpRequestException`) with `RetryAt`. Treat it as "retry".

Retries and circuit breaking are left to you: pass an `HttpClient` with your own handlers
(e.g. Polly / `Microsoft.Extensions.Http.Resilience`).

## Testing your integration

- eVatR answers the documented test pair `DE123456789` → `ATU12345678` with canned results
  (e.g. company `Musterhaus GmbH & Co KG`, street `Musterstrasse 22`, postcode `12345`, city `musterort`).
- VIES: set `ViesClientOptions.UseTestService = true` to call `check-vat-test-service`, which answers
  by VAT number: `100` valid, `200` invalid, `201` `INVALID_INPUT`, `301` `MS_UNAVAILABLE`, and more.

## Development

```sh
dotnet build
dotnet test --filter "Category!=Live"   # unit tests, replay recorded fixtures
dotnet test --filter "Category=Live"    # contract tests against the live APIs (opt-in)
```

Fixtures under `tests/fixtures/` are exact responses recorded by the live tests
(`EVATR_RECORD_FIXTURES=1`). Real eVatR checks need your own DE VAT ID in
`EVATR_REQUESTER_VAT_ID`; it is masked as `DE000000000` in recorded fixtures. When recording, only
use well-known large companies or made-up IDs as counterparties.

## License

[MIT](LICENSE).
