# VIES fixtures

Exact response bodies from the live VIES REST API, written by the opt-in live tests in
`tests/Evatr.Net.Tests/Live/ViesLiveTests.cs`. Git keeps them byte-exact (`.gitattributes`: `-text`).

- `recorded-test-*.json`: official `check-vat-test-service`, which answers by VAT number:
  `100` valid, `200` invalid, `201` `INVALID_INPUT`, `202` `INVALID_REQUESTER_INFO`,
  `300` `SERVICE_UNAVAILABLE`, `301` `MS_UNAVAILABLE`, `302` `TIMEOUT`, `400` `VAT_BLOCKED`,
  `500` `GLOBAL_MAX_CONCURRENT_REQ`, `600` `MS_MAX_CONCURRENT_REQ`, `601` `MS_MAX_CONCURRENT_REQ_TIME`.
  All arrived with **HTTP 200**.
- `recorded-real-valid-google-ireland.json`: real `check-vat-number` for `IE6388047V` (Google Ireland Limited).
- `recorded-real-invalid.json`: real `check-vat-number` for `ATU12345675` (dummy ID with a valid check digit).
- `recorded-check-status.json`: `GET /check-status`.

Not covered by the test service: `IP_BLOCKED`, `GLOBAL_MAX_CONCURRENT_REQ_TIME`.

Recording: `EVATR_RECORD_FIXTURES=1 dotnet test --filter "Category=Live"`. Only use well-known
large companies as counterparties (never sole traders: their data is personal data).
