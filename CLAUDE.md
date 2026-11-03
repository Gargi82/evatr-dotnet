# CLAUDE.md — Evatr.Net

Open-source (MIT) .NET client for the German BZSt eVatR REST API and the EU VIES REST API,
published on NuGet as `Evatr.Net`.

## Stack
- .NET 10, C# 14, nullable enabled, warnings as errors. System.Text.Json.
- Only dependency: `System.Threading.RateLimiting`. Keep it that way; no app- or cloud-specific dependencies.
- Tests: xUnit v3 + AwesomeAssertions on Microsoft.Testing.Platform; fixtures under `tests/fixtures/`.

## Rules
- Unit tests use fixtures only. Never call the real BZSt/VIES APIs in unit tests.
- Contract tests that hit live APIs are opt-in: `[Trait("Category","Live")]`. They record fixtures through the same code path (`EVATR_RECORD_FIXTURES=1`).
- When recording fixtures, mask the requester VAT ID as `DE000000000` and only use well-known large companies or made-up IDs as counterparties (sole traders are personal data).
- Keep the exact request/response strings exchanged with BZSt/VIES (`RawRequest`, `RawResponse`); normalize only for display. Recorded fixtures stay byte-exact (`.gitattributes`).
- Treat `UNAVAILABLE`/`MS_UNAVAILABLE`, temporary `evatr-*` errors and non-JSON responses as "retry", never as "invalid". Unknown codes map to `Unknown`, never `Invalid`.
- Classify eVatR results by the `status` code, not the HTTP status.
- No secrets or real requester VAT IDs in code, fixtures or commits.
- Rate limits and service hours are undocumented: keep them configurable, apply none by default.
- Code, comments and docs in English.
- Check current docs (Context7) before using unfamiliar or recently changed library APIs.

## Commands
- Build: `dotnet build`
- Test: `dotnet test --filter "Category!=Live"`
- Live tests: `dotnet test --filter "Category=Live"` (`EVATR_REQUESTER_VAT_ID=DE...` for real eVatR checks)

## Release
- Push a tag `v*` (e.g. `v0.1.0-preview.1`); `.github/workflows/publish.yml` builds in CI and pushes to nuget.org via trusted publishing. Never publish locally built packages (they embed local paths).
