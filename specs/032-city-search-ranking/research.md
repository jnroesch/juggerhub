# Phase 0 Research: City Search Relevance Ranking

All Technical Context unknowns are resolved below. No open `NEEDS CLARIFICATION` remain.

## R1 — How to rank by distance without PostGIS and without the display cap dropping a nearby option

**Decision**: Compute an **equirectangular squared-distance** term in the database `ORDER BY`, so the
full prefix-filtered candidate set is ranked in SQL *before* `.Take(MaxResults)`:

```
distanceRank = (Latitude − lat₀)² + (kLon · (Longitude − lon₀))²      where kLon = cos(lat₀·π/180)
```

`lat₀/lon₀` are the user's home-city coordinates and `kLon` is a **C# constant** (computed once from
the home latitude) folded into the query. Because the whole expression is arithmetic on `double`
columns, EF Core translates it to plain SQL — no PostGIS, no `earthdistance`/`cube` extension, no trig
functions (which EF cannot translate), and no C#-side haversine loop over a large candidate set.

**Rationale**:
- **Correctness (FR-007)**: ranking happens in the DB across every candidate matched by the prefix
  filter, so `Take(MaxResults)` returns the true top-N. A "fetch a population-capped pool then re-rank
  distance in C#" approach would risk dropping a small-population *nearby* city before the distance
  sort — exactly the case the user prioritized ("match → distance → population").
- **Adequacy for ranking**: we need *relative order*, not kilometers. The equirectangular approximation
  preserves nearest-first order at city granularity for the distances that matter here; the `cos(lat₀)`
  factor corrects longitude compression away from the equator. We compare squared distances, so no
  `sqrt` is needed (monotonic).
- **Consistency with feature 030**: 030 deliberately avoided PostGIS ("haversine in C#, no PostGIS")
  for the persisted city-to-city cache. This keeps the same no-extension posture for search ranking
  while staying set-based in SQL.

**Alternatives considered**:
- *Raw great-circle (haversine) in SQL via `FromSql`*: exact, but forces raw SQL fragments mixed with
  the LINQ prefix filter and adds trig; unnecessary precision for ranking. Rejected.
- *C#-side haversine after a capped SQL pool*: simple math, but reintroduces the cap-before-rank
  hazard (FR-007) and pulls extra rows into memory each keystroke. Rejected.
- *`earthdistance`/`cube` or PostGIS*: a DB extension for a pure ordering nicety; contradicts 030's
  no-PostGIS decision and complicates every environment. Rejected.

**Antimeridian note**: the squared-longitude term does not wrap across ±180°. This only mis-ranks
candidates straddling the antimeridian relative to a home city on the far side — irrelevant at the
city-name-collision granularity this feature serves. Documented as an accepted approximation.

## R2 — Where the proximity origin comes from

**Decision**: Resolve the origin server-side from `PlayerProfile.HomeCityId → HomeCity`
(`Latitude/Longitude`) for the current user. The controller extracts the user id with the existing
`TryGetUserId` pattern (JWT `sub` claim) and passes it to `CityService.SearchAsync`; the service does a
single `AsNoTracking` projection to fetch the home coordinates (or `null`).

**Rationale**: FR-003 forbids browser geolocation and any new client input. The endpoint is already
auth-gated (feature 026), so the user is always known. Keeping the lookup in the service preserves thin
controllers (Principle II). When `HomeCity` is null or lacks coordinates, the service simply omits the
distance ordering term (FR-004) — no error, no prompt.

**Signature change**: `SearchAsync(string query, int limit, CancellationToken ct)` →
`SearchAsync(string query, int limit, Guid? userId, CancellationToken ct)`. `userId` is nullable so
the method stays callable in contexts without a user (none today, but future-proof and test-friendly).

**Alternatives considered**:
- *Controller resolves coordinates and passes `(lat, lon)?`*: leaks a data-access concern into the
  controller. Rejected for Principle II.
- *Ambient `IHttpContextAccessor` inside the service*: hidden dependency, harder to test. Rejected —
  explicit `userId` parameter is clearer.

## R3 — Adding population to the reference data

**Decision**: Add `Population` (`int`, non-negative, default `0`) to `CityReference`. Source it from
GeoNames cities500 **column 14** in `Data/Seed/regenerate-cities500.mjs`, appended as a 10th seed
column; extend the binary `COPY` in `CityReferenceSeeder` to read it. Cities with unknown/blank
population seed as `0` and therefore sort last within their tier under `ORDER BY Population DESC`
(FR-006).

**Type**: `int` is sufficient — the largest cities500 populations are ~10⁷–10⁸, well under `int.MaxValue`
(~2.1×10⁹). No need for `long`.

**Rationale**: population already exists in the bundled dataset, so no new external dependency (spec
Assumption). `DESC` puts populous cities first and naturally sinks `0`/unknown to the bottom.

**Reseed (operational)**: `CityReferenceSeeder` only runs when `CityReferences` is empty. Existing
environments therefore need a **one-time reseed** to backfill the new column: truncate `CityReferences`
so the seeder repopulates from the regenerated bundle on next startup. This is safe — the table is
seed-once reference data, never user-authored (consistent with how 030 introduced it). Captured as a
task; called out for Dev/Prod deploy notes (Principle V parity — same bundle, same step everywhere).

**Seed regeneration prerequisite**: `regenerate-cities500.mjs` reads `cities500.txt`, `countryInfo.txt`,
and `admin1CodesASCII.txt`, which are **not** committed (downloaded from GeoNames). Regenerating the
`.gz` bundle requires fetching those first. This is an implementation task with a network step, flagged
so it is not assumed to be a pure code edit.

**Alternatives considered**:
- *Nullable `int?` population*: adds null-handling with no benefit; `0` already means "unknown, rank
  last". Rejected.
- *Store a precomputed popularity score*: premature; raw population plus the existing match tier is
  enough and stays explainable. Rejected.

## R4 — Composing the ORDER BY (preserving the existing tier)

**Decision**: Build the ordering as, in sequence:

1. `OrderByDescending(exact ASCII-prefix ILike)` — **existing match-quality tier, unchanged** (FR-002):
   exact name/ascii prefix hits above alternate-name/exonym hits.
2. *(only when a home city with coordinates exists)* `ThenBy(equirectangular squared-distance)` (FR-001).
3. `ThenByDescending(Population)` (FR-001, FR-005).
4. `ThenBy(Name.Length)` then `ThenBy(Name)` — **existing deterministic tiebreakers, unchanged**
   (FR-009 stability).

The distance step is conditionally appended to the `IOrderedQueryable`, so a user with no home city
gets exactly steps 1 → 3 → 4 (population-first within the tier), matching FR-004.

**Rationale**: keeps the proven match tier and deterministic final tiebreakers intact while inserting
the two new relevance signals in the user-chosen precedence (match → distance → population). Everything
stays in one translat­able query.

## Summary of decisions

| # | Decision |
|---|----------|
| R1 | Rank in SQL via equirectangular squared-distance (`(Δlat)² + (cos lat₀·Δlon)²`); no PostGIS, no trig, no C# haversine; full set ranked before the cap. |
| R2 | Origin = server-side `PlayerProfile.HomeCity` coords; `SearchAsync` gains a nullable `userId`; omit distance term when absent. |
| R3 | Add `CityReference.Population` (`int`, default 0) from GeoNames col 14; regenerate bundle + one-time reseed; `DESC` sinks unknowns. |
| R4 | ORDER BY = match tier (kept) → [distance if home] → population DESC → name-length/name (kept). |
