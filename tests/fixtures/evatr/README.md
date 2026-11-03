# eVatR fixtures

`recorded-*.json` are exact response bodies from the live BZSt eVatR API, written by the opt-in
live tests in `tests/Evatr.Net.Tests/Live/EvatrLiveTests.cs` (same code path for test and recording).
Git keeps them byte-exact (`.gitattributes`: `-text`).

| File | HTTP | Status | Source |
|---|---|---|---|
| `recorded-valid-simple.json` | 200 | `evatr-0000` | sandbox pair |
| `recorded-qualified-match.json` | 200 | `evatr-0000`, A/A/A/A | sandbox pair, documented test data |
| `recorded-qualified-mixed.json` | 200 | `evatr-0000`, name A, street/plz D, city B | sandbox pair, wrong city |
| `recorded-requested-de.json` | 403 | `evatr-0006` | DE VAT ID as requested ID |
| `recorded-requester-syntax-error.json` | 400 | `evatr-0004` | malformed requester ID |
| `recorded-status-catalog.json` | 200 | — | `GET /app/v1/info/statusmeldungen` |
| `recorded-member-states.json` | 200 | — | `GET /app/v1/info/eu_mitgliedstaaten` |
| `recorded-not-found.json` *(needs requester)* | 404 | `evatr-2001` | `ATU12345675` (made up, valid check digit; only recorded if 2001) |
| `recorded-bad-check-digit.json` *(needs requester)* | 400 | `evatr-0005`/`0012` expected | `ATU12345670` (wrong check digit) |
| `recorded-real-qualified-google-ireland.json` *(needs requester)* | 200 | `evatr-0000` | `IE6388047V` |
| `synthetic-not-found.json` | 404 | `evatr-2001` | hand-written until the recorded one exists |
| `synthetic-unavailable.json` | 500 | `evatr-2011` | hand-written; outages cannot be triggered |

The "sandbox pair" is `DE123456789` → `ATU12345678`, documented as test values in the eVatR OpenAPI
description; the live API answers it with canned results. Observed quirk: the sandbox returns **D**
(not C) for fields that were not sent.

## Recording

```sh
# sandbox + catalog only (no secret needed)
EVATR_RECORD_FIXTURES=1 dotnet test --filter "Category=Live"
# also real checks (not-found, real company); requester ID is masked as DE000000000 in fixtures
EVATR_REQUESTER_VAT_ID=DE... EVATR_RECORD_FIXTURES=1 dotnet test --filter "Category=Live"
```

Fixtures are published with the MIT library. Mask the requester VAT ID and only use well-known
large companies as counterparties (never sole traders: personal data, DESIGN.md §8).
