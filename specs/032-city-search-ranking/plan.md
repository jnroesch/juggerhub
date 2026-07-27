# Implementation Plan: City Search Relevance Ranking

**Branch**: `032-city-search-ranking` | **Date**: 2026-07-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/032-city-search-ranking/spec.md`

## Summary

Re-rank the city-picker search options (`GET /api/v1/cities/search`) so the city people usually mean
comes first. Within the existing match-quality tier (exact name/ASCII-prefix hits above alternate-name/
exonym hits), order by **distance from the signed-in user's stored home city (ascending) → population
(descending) → the existing name-length/name tiebreakers**. The proximity origin is resolved
server-side from the user's `PlayerProfile.HomeCity` — no browser geolocation, no permission prompt, no
new request parameter. Users without a home city skip the distance tier and fall back to
population-then-name.

Technical approach: add a `Population` column to the `CityReference` reference table (already carried
by the bundled GeoNames cities500 dataset, column 14) and regenerate/reseed the bundled snapshot; rank
the **full candidate set in the database** — before the display cap — using an **equirectangular
squared-distance** ordering term (`(lat−lat₀)² + (cos lat₀·(lon−lon₀))²`). That term is pure arithmetic
EF Core translates to SQL, so it needs no PostGIS, no trig functions, and no C#-side haversine over a
large candidate set — sidestepping the "cap drops a nearby option before ranking" hazard (FR-007). The
home-latitude cosine is a C# constant folded into the query.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); no frontend change

**Primary Dependencies**: Entity Framework Core (Npgsql), existing `CityService`/`CitiesController`,
`PlayerProfile.HomeCity` (feature 030)

**Storage**: PostgreSQL 18 — `CityReferences` reference table (seed-once, ~235k rows) gains a
`Population` column; bundled seed `Data/Seed/cities500.seed.tsv.gz` regenerated to include it

**Testing**: xUnit + Testcontainers integration tests (`JuggerHub.Api.IntegrationTests`), extending
`Search/CitySearchTests.cs` and the `TestReferenceCities` fixture

**Target Platform**: Linux containers on AKS (Dev/Prod), docker-compose (local)

**Project Type**: Web service (backend-only change; frontend untouched)

**Performance Goals**: No user-noticeable slowdown vs. today's picker (SC-004). Ranking is one
DB query over the prefix-filtered candidate set; no extra round trips beyond a single home-city lookup.

**Constraints**: No PostGIS / no DB extension (Principle-consistent with feature 030's "haversine in
C#, no PostGIS"); ranking must run across the full candidate set before the `MaxResults` cap (FR-007);
proximity origin server-side only (FR-003, Principle I).

**Scale/Scope**: cities500 reference table (~235k rows); prefix-filtered candidate sets per query;
`MaxResults` = 8, `MinQueryLength` = 2 (`GeocodingOptions`).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I — Security-First, Never Trust the Client**: PASS. The proximity origin is the user's *stored*
  home city, read server-side from `PlayerProfile.HomeCity`; no client-supplied coordinates enter
  ranking, and no new client input is added. The endpoint stays auth-gated (feature 026). Reference
  `Latitude/Longitude` are already treated as display hints, never trusted for storage.
- **II — Thin Controllers, Service-Centric, DTOs via Mapster**: PASS. Logic stays in `CityService`;
  the controller only resolves the current user id (existing `TryGetUserId` pattern) and forwards it.
  Results remain `CityOptionDto`. No new controller logic beyond id extraction.
- **III — Disciplined Data Access**: PASS. Search stays `AsNoTracking` + `.Select` projection +
  `.Take(limit)`; the home-city lookup is a single `AsNoTracking` projection. `CityReference` is the
  established non-`BaseEntity` reference table (feature 030) — adding a scalar column is consistent.
  The result list is already bounded by `MaxResults` (no unbounded return).
- **V — Environment Parity & Reproducible Deployments**: PASS with an operational note. The seed is
  regenerated from the bundled dataset and reseeded identically across local/Dev/Prod; the reseed step
  (the seeder only runs on an empty table) is called out in research.md and tasks.
- **VI — Consistent Conventions & Tooling**: PASS. Backend-only; the seed regenerator is an existing
  Node `.mjs` build helper being modified (not a new `.sh` script). No frontend files change, so the
  `.html`/`.css`/`.ts` separation rule is not engaged.
- **VII — Resilient by Default**: N/A (Gate 8 not triggered). This change adds **no** network call or
  outbound integration — it is a local database query. No timeouts/retries/breakers to configure.
- **Gate 7 — UI/Design compliance**: N/A. No UI ships; option labels and layout are unchanged (FR-008),
  only the order in which options appear.

**Result**: No violations. Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/032-city-search-ranking/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── cities-search.md # Phase 1 output — endpoint contract (unchanged shape, new ordering)
├── checklists/
│   └── requirements.md  # From /speckit-specify
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
backend/
├── Entities/
│   └── CityReference.cs                  # + Population column
├── Data/
│   ├── AppDbContext.cs                    # Population mapping (if explicit config needed)
│   ├── CityReferenceSeeder.cs            # COPY command + parse the population column
│   ├── Seed/
│   │   ├── regenerate-cities500.mjs      # emit GeoNames population (col 14)
│   │   └── cities500.seed.tsv.gz         # regenerated bundle (now 10 columns)
│   └── Migrations/                        # new migration: add Population to CityReferences
├── Services/Geocoding/
│   ├── ICityService.cs                    # SearchAsync gains the current-user id
│   └── CityService.cs                     # home-city lookup + equirectangular ordering + population
└── Controllers/
    └── CitiesController.cs               # resolve current user id, forward to SearchAsync

backend/tests/JuggerHub.Api.IntegrationTests/
├── TestReferenceCities.cs                # fixture rows gain Population; add same-name/near cases
└── Search/CitySearchTests.cs            # + population-order and proximity-order tests
```

**Structure Decision**: Backend web-service change only. All edits live under `backend/` (entity,
data/seed, geocoding service, cities controller) with tests under `JuggerHub.Api.IntegrationTests`.
No `frontend/` changes — ranking is entirely server-side and the option DTO/label shape is unchanged.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
