# Phase 1 Data Model: City Search Relevance Ranking

## Changed entity

### `CityReference` (reference table, non-`BaseEntity`)

The seed-once GeoNames cities500 reference table gains one scalar column. All existing columns are
unchanged.

| Field | Type | Notes |
|-------|------|-------|
| ExternalId | string (PK) | unchanged — `"geonames:<id>"` |
| Name | string | unchanged |
| AsciiName | string | unchanged — accent-free prefix search |
| AlternateNames | string | unchanged — Latin exonyms |
| CountryCode | string | unchanged |
| CountryName | string | unchanged |
| Region | string | unchanged |
| Latitude | double | unchanged — proximity origin math |
| Longitude | double | unchanged |
| **Population** | **int** | **NEW.** Inhabitant count from GeoNames cities500 col 14. Non-negative; **default `0`** when the source value is blank/unknown. Used only for ranking (`ORDER BY … DESC`); `0` sorts last within a tier. |

**Validation / invariants**:
- `Population >= 0`. Blank source → `0`.
- No uniqueness or FK implications; it is a pure ranking attribute.
- Not exposed on any DTO (see contract) — internal ranking input only.

**Migration**: add `Population` (`integer`, `NOT NULL`, default `0`) to `CityReferences`. Because the
table is repopulated from the bundled seed, values are backfilled by a **one-time reseed** (truncate
`CityReferences` so `CityReferenceSeeder` reloads the regenerated bundle), not by a data migration.

**Seed format change**: `Data/Seed/cities500.seed.tsv.gz` goes from 9 to **10** tab-separated columns,
appending `Population` after `Longitude`. `CityReferenceSeeder`'s binary `COPY` column list and row
writer add the population column; `regenerate-cities500.mjs` emits GeoNames col 14 (blank → `0`).

## Referenced entity (unchanged, read-only here)

### `PlayerProfile.HomeCity` → `City`

The proximity origin. Read via projection; not modified by this feature.

- `PlayerProfile.UserId : Guid` — matches the current user's JWT `sub`.
- `PlayerProfile.HomeCityId : Guid?` — null when no home city set.
- `PlayerProfile.HomeCity : City?` — provides `Latitude` / `Longitude` for the origin.

**Usage**: `CityService` projects `HomeCity.Latitude/Longitude` for the current user (or `null`). Null
coordinates ⇒ distance ordering term omitted (FR-004).

## Ranking model (derived, not stored)

Ordering key applied to the prefix-filtered candidate set, before the `MaxResults` cap:

```
1. exactAsciiPrefixMatch   DESC   (bool; existing match tier — preserved)
2. distanceRank            ASC    (only if home coords exist)
                                   = (Lat − lat0)^2 + (kLon*(Lon − lon0))^2 ; kLon = cos(lat0 rad)
3. Population              DESC
4. Name.Length            ASC    (existing tiebreaker — preserved)
5. Name                   ASC    (existing tiebreaker — preserved)
```

No new persisted structure; `distanceRank` is a query-time expression with the home-latitude cosine as
a C# constant.
