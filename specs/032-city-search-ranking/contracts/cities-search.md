# Contract: `GET /api/v1/cities/search`

**Status**: existing endpoint — **request and response shapes are unchanged**. Only the *ordering* of
the returned array changes, plus a server-side personalization based on the caller's stored home city.

## Request

```
GET /api/v1/cities/search?q={term}
Authorization: Bearer <jwt>        # required (feature 026) — unchanged
```

- `q` — search term. Below `GeocodingOptions.MinQueryLength` (2) ⇒ `200 OK` with `[]` (unchanged).
- **No new query parameters.** The proximity signal is derived server-side from the authenticated
  user's `PlayerProfile.HomeCity`; the client sends no coordinates (FR-003).

## Response

`200 OK` — `CityOptionDto[]`, capped at `GeocodingOptions.MaxResults` (8). **Item shape unchanged:**

```jsonc
[
  {
    "externalId": "geonames:2950159",
    "name": "Berlin",
    "region": "Berlin",            // present only when it disambiguates (feature 030 behavior, unchanged)
    "countryName": "Germany",
    "countryCode": "DE",
    "label": "Berlin, Germany",    // unchanged label rules
    "latitude": 52.52,
    "longitude": 13.405
  }
]
```

- `population` is **not** added to the DTO — it is an internal ranking input only (FR-008).

## Ordering guarantees (the behavioral change)

Within the result set, order is:

1. Exact name/ASCII-prefix matches before alternate-name/exonym-only matches (**unchanged tier**).
2. Then, **if the caller has a stored home city with coordinates**, nearer cities before farther ones.
3. Then, larger population before smaller (unknown population = last).
4. Then, the existing name-length then name tiebreakers (stable, deterministic).

### Behavioral guarantees

| Given | When | Then |
|-------|------|------|
| Caller has **no** home city | `q=berlin` | Most-populous "Berlin" first; ordering deterministic; no error, no prompt. |
| Caller's home city is near a small same-named town | `q={that name}` | The nearby town outranks a larger distant same-named city. |
| Two cities share name + country | any | The more populous is listed first; region label still disambiguates them (feature 030). |
| Any caller, identical repeated request | same `q` | Identical ordering (deterministic). |
| A candidate has unknown population | any | It still appears, ranked last within its tier — never hidden. |

## Non-changes

- `GET /api/v1/cities/countries` — unchanged.
- Selecting/persisting a city (owning profile/team/event update) — unchanged.
- Auth, error handling, min-query/`[]` behavior, `MaxResults` cap — unchanged.
