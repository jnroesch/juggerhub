# Phase 1 Data Model: Structured Locations

**Feature**: 030-structured-locations | **Date**: 2026-07-25

All new entities derive from `BaseEntity` (UUIDv7 PK, `CreatedDate`/`ModifiedDate` via the audit interceptor). No migration of existing values — freeform columns are dropped and replaced (test data only; R6).

---

## New entity: `City`

The persisted canonical city, resolved from Photon once and reused (R3, R4).

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` (UUIDv7) | PK from `BaseEntity`. |
| `ExternalId` | `string` | `"{OsmType}:{OsmId}"` (e.g. `"R:62578"`). **Unique index.** De-dupe key. |
| `Name` | `string` | Canonical city name (e.g. "Köln"). Required. |
| `CountryName` | `string` | e.g. "Germany". Required (an entry with no country is not a usable location). |
| `CountryCode` | `string?` | ISO-3166-1 alpha-2 (e.g. "DE") where the provider supplies it; used by the country filter. |
| `Region` | `string?` | State/county/admin area (e.g. "North Rhine-Westphalia") — disambiguates same-named cities. |
| `Latitude` | `double` | Required — a City with no usable coordinates is not created (unlocated selections are rejected server-side). |
| `Longitude` | `double` | Required. |

**Relationships**: referenced by `PlayerProfile.HomeCityId`, `Team.CityId`, `Event.CityId` (all nullable, `OnDelete: Restrict` — a City in use is not deleted out from under a reference). One-to-many City → each referencing entity.

**Validation / rules**:
- Created only via `CityService` server-side upsert keyed on `ExternalId`; never created from client-sent name/coords directly (Principle I).
- `Latitude`/`Longitude` must be present and plausible (lat ∈ [-90,90], lon ∈ [-180,180]); a provider result lacking them is not persisted and the selection fails with a clear error.
- Immutable in practice — refreshing from the provider is out of scope; a city's identity is its `ExternalId`.

**Display**: `locationLabel = "{Name}, {CountryName}"` (FR-010), rendered by a shared helper on both ends.

---

## New entity: `CityDistance`

Precomputed great-circle distance cache powering proximity sort without PostGIS (R2). Stored **bidirectionally** so a proximity query is a single-sided join.

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` (UUIDv7) | PK from `BaseEntity`. |
| `FromCityId` | `Guid` | FK → City. |
| `ToCityId` | `Guid` | FK → City. |
| `DistanceKm` | `double` | Haversine distance, computed in C# at insert time. |

**Indexes**: unique composite `(FromCityId, ToCityId)`; index on `(FromCityId, DistanceKm)` to serve `WHERE FromCityId = @home ORDER BY DistanceKm`. `OnDelete: Cascade` from City (a removed City's pairs go with it).

**Lifecycle (backfill)**: when `CityService` creates a **new** City `X`, it computes `distance(X, Y)` for every existing City `Y` and inserts rows `(X→Y)` and `(Y→X)`, **plus the self-row `(X→X)=0`**. The self-row is required, not optional: the proximity join anchors on `FromCityId = @homeCityId`, so a player's own-city entities only appear (at distance 0, ranked first) when a `(home→home)=0` row exists. Runs inside the EF execution strategy as one retriable unit with all inserts inside the delegate (Principle VII). Cost is O(number of existing cities) — negligible at Jugger scale.

---

## Modified entity: `PlayerProfile`

| Change | Before | After |
|--------|--------|-------|
| Home location | `string? Hometown` | `Guid? HomeCityId` (FK → City) + `City? HomeCity` nav |

- Nullable — a player need not set a city; null ⇒ excluded from proximity anchoring and shown with no location.
- Set during onboarding and profile edit via server-side city resolution.
- Every `Hometown` reader (Profile, Search `PlayerCardDto`, Admin, quick-actions, onboarding prefill) switches to the structured `{ city, country }` shape.

## Modified entity: `Team`

| Change | Before | After |
|--------|--------|-------|
| Home city | `string? City` | `Guid? CityId` (FK → City) + `City? City` nav |

- Still **required for `TeamType.CityTeam`**, **absent for Mixteam** (enforced in `TeamService` as today, now against the FK).
- `TeamCardDto.City` (string) becomes a structured `{ name, country }` (see contracts); browse city-substring filter is replaced by the country filter + proximity sort.

## Modified entity: `Event`

| Change | Before | After |
|--------|--------|-------|
| City | `string? City` | `Guid? CityId` (FK → City) + `City? City` nav |
| Country | `string? Country` | removed (now derived from `City.CountryName`) |
| Street/PostalCode/VenueName | unchanged | unchanged (structured street address is orthogonal to the canonical city) |
| `Location` (legacy free-text) | present | **Verify during implementation**: retained only if still read by `ActivityItemDto`; otherwise removed (R6 open item). |

- `CityId` present only for `LocationKind.InPerson`; **null for `Virtual`** — virtual events are excluded from the proximity-sorted event view (FR-016).

---

## Query shapes

**Proximity-sorted browse (teams)** — opt-in `sort=Proximity`, requires the caller's `HomeCityId`:

```text
Teams
  JOIN CityDistances d ON d.FromCityId = @homeCityId AND d.ToCityId = Teams.CityId
  WHERE <existing filters> [AND City.CountryCode = @country]
  ORDER BY d.DistanceKm, Teams.Id      -- stable tiebreaker (Principle III)
  OFFSET @skip LIMIT @take
```

Teams with `CityId = null` (Mixteam) or with no distance row are **not** joined ⇒ excluded from this view; they remain visible under the default `NameAsc` sort. Events are analogous (virtual events have null `CityId`).

**Country filter (any sort)**: `WHERE City.CountryCode = @country` (or `CountryName` when code absent) — independent of the sort (FR-015).

**Default sort unchanged**: when `sort != Proximity`, existing `NameAsc` / `StartsAtAsc` ordering is used verbatim (FR-014), and no `HomeCityId` is required.

---

## Data-integrity invariants

1. A stored location is a City FK or null — never a free string (FR-005).
2. Every City has a country and coordinates (else it is not persisted).
3. `CityDistance` contains a row for every ordered pair of cities that both exist ⇒ any two located entities are comparable by proximity.
4. Client-supplied coordinates/names are never trusted; the City is authoritative via server-side resolution (Principle I).
