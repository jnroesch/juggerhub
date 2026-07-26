# Contract: City search & selection

## `GET /api/cities/search`

Backend-proxied type-ahead search against the self-hosted geocoder (R4). Returns **transient** options — nothing is persisted by searching.

**Auth**: required. **Query params**:

| Param | Type | Rules |
|-------|------|-------|
| `q` | string | Required; trimmed. Below the minimum length (reuse `SearchOptions.MinQueryLength`) ⇒ empty result, not an error. |
| `limit` | int | Optional; server-capped (e.g. ≤ 10). |

**200 response** — `CityOptionDto[]`:

```jsonc
[
  {
    "externalId": "R:62578",           // provider identity; echoed back on selection
    "name": "Köln",
    "region": "North Rhine-Westphalia", // nullable
    "countryName": "Germany",
    "countryCode": "DE",                // nullable
    "label": "Köln, North Rhine-Westphalia, Germany", // display/disambiguation (FR-003)
    "latitude": 50.9384,                // display hint only; NOT trusted on write
    "longitude": 6.9601
  }
]
```

**Behavior**:
- Ordered by provider relevance; results without a country or without coordinates are filtered out server-side (they can't become a valid City).
- **Degradation (FR-018/019)**: on geocoder timeout / breaker-open / transient exhaustion, return **`503`** with a generic retryable body (`"City search is unavailable right now."`). The picker surfaces a transient "can't search right now — retry" state. Never leaks provider errors or the user's query into logs tied to identity (FR-021).
- `q` too short or no matches ⇒ **`200`** with `[]` (the UI distinguishes "no matches" from "unavailable").

---

## City selection payload (used by profile/team/event writes)

A user selects a city by sending its **`externalId`** on the owning resource's existing update endpoint. The backend **re-resolves** the city server-side (from the `Cities` cache, else `IGeocodingClient.ResolveByIdAsync(externalId)`), upserts it, backfills `CityDistance`, and links the FK. Client-sent `name`/`latitude`/`longitude` are **ignored for storage** (Principle I).

**Shared request fragment** (embedded in profile/team/event update DTOs):

```jsonc
// Set a city:
"location": { "cityExternalId": "R:62578" }
// Clear a city:
"location": { "cityExternalId": null }
// Omit "location" entirely ⇒ no change to the current city.
```

**Resolution outcomes**:
- Valid, resolvable id ⇒ City upserted + linked; response echoes the structured location (see browse-and-profile.md).
- Unknown/unresolvable id (provider can't find it, or it lacks country/coords) ⇒ **`422 Unprocessable Entity`**, generic message; the caller's other fields are still validated normally.
- Geocoder unavailable during a *new* city resolution ⇒ **`503`**, and the rest of the update is **not** partially applied for the city field (the write either links a city or leaves it unchanged — never a half-state). Non-city fields of the same request follow the endpoint's existing transactional behavior.

**Idempotency / race**: concurrent first-selections of the same `externalId` must converge on one City row (unique index on `ExternalId`; upsert handles the conflict). Distance backfill runs once per new City inside the EF execution strategy (Principle VII).
