# Implementation Plan: Structured Locations & "Near You" Discovery

**Branch**: `feat/030-structured-locations` | **Date**: 2026-07-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/030-structured-locations/spec.md`

## Summary

Replace freeform location entry (`PlayerProfile.Hometown`, `Team.City`, `Event.City/Country`) with selection from a **self-hosted OpenStreetMap geocoder (Photon)**, so every location resolves to a persisted **Canonical City** carrying country, region, and coordinates. That structured data drives country-qualified display ("Köln, Germany") and **"near you"** discovery — onboarding auto-leads with teams near the player's city; browse offers an opt-in nearest-first sort and a country filter. Proximity is computed at **city-to-city** granularity via a precomputed `CityDistance` cache (haversine in C#, no PostGIS), keeping the stock Postgres image and DB-side pagination intact. The geocoder is a backend-proxied, resilient outbound integration (Principle VII); no data migration (test data only).

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Angular (Nx) frontend

**Primary Dependencies**: EF Core 10 + Npgsql; `Microsoft.Extensions.Http.Resilience` (Polly v8, already present); Mapster; Angular + Tailwind. **New**: Photon geocoder container (`komoot/photon`).

**Storage**: PostgreSQL 18 (stock `postgres:18.3-alpine`, **no PostGIS**). New tables: `Cities`, `CityDistances`. New FKs on `PlayerProfiles`, `Teams`, `Events`.

**Testing**: xUnit (backend unit + integration, incl. `OutboundResilienceHarness`); Jasmine/Karma (Angular); Playwright (e2e).

**Target Platform**: Linux containers (docker-compose local; AKS Dev/Prod).

**Project Type**: Web application (ASP.NET Core API + Angular SPA).

**Performance Goals**: City autocomplete suggestions perceptibly instant (attempt timeout ~3s, retry within ~8–10s total); proximity-sorted browse first page within today's browse budget (no perceptible slowdown). `CityDistance` backfill O(existing-cities) per new city, negligible at Jugger scale.

**Constraints**: No user location leaves our infra (self-hosted geocoder); geocoder credentials/config from env only; local dev must work with a regional extract (offline-capable, no paid service); all list endpoints paginated; frontend keeps separate `.html`/`.css`/`.ts`.

**Scale/Scope**: Small community app; tens–low hundreds of canonical cities; `CityDistance` pairs bounded accordingly. Cross-cutting: `Hometown`/`City` referenced in Profile, Search, Events, Marketplace, Parties, Admin DTOs + Angular models (all updated together).

## Constitution Check

*GATE: evaluated against constitution v1.3.0. Re-checked after Phase 1 design — still passing.*

| # | Gate | Assessment |
|---|------|-----------|
| 1 | **Architecture** (thin controllers, DI'd services w/ interfaces, no repository, Mapster DTOs) | PASS — new `IGeocodingClient`, `ICityService`; browse logic stays in the existing `*SearchService`s; controllers thin; `CityOptionDto`/`CityDto` mapped via Mapster. |
| 2 | **Data access** (paginate, projections, `AsNoTracking`, `BaseEntity`) | PASS — `City`/`CityDistance` derive from `BaseEntity`; proximity browse stays `Skip/Take` paginated with a stable `ThenBy(Id)`; reads projected + `AsNoTracking`. |
| 3 | **Security-first / never-trust-client** (OWASP, server-side authority, no leaks) | PASS — city persisted by **server-side re-resolution** from provider id; client coords are display hints only; geocoder is internal; generic errors, no stack traces. |
| 4 | **Auth / sessions** | PASS — all surfaces behind existing auth (026); no auth changes. |
| 5 | **Conventions & tooling** (Angular split files, `.ps1` only) | PASS — `jh-city-picker` split files; any scripts `.ps1`. |
| 6 | **Environment parity** (identical shape; `.env` local, GitHub Environments deployed) | PASS *with noted call* — same Photon image + API shape everywhere; only the index **extract size** differs, treated as a sizing knob (R1). Geocoder URL/config via `.env` / GitHub Environments. |
| 7 | **UI/Design compliance** (DESIGN.md + UI review checklist) | PASS — city picker, "City, Country" display, and browse proximity/country controls follow DESIGN.md; `checklists/ui-review.md` instantiated. |
| 8 | **Resilience** (Principle VII) | PASS — geocoder via `AddJuggerHubResilience(…, "Geocoding")`; GET → retry-safe (documented, opposite of the email POST); breaker tuned to interactive volume; graceful degradation; distance backfill through the EF execution strategy; no PII/secrets in resilience logs. |

**No violations → Complexity Tracking is empty.**

## Project Structure

### Documentation (this feature)

```text
specs/030-structured-locations/
├── plan.md              # This file
├── research.md          # Phase 0 decisions (R1–R7)
├── data-model.md        # Phase 1 — City, CityDistance, FK changes
├── quickstart.md        # Phase 1 — end-to-end validation guide
├── contracts/           # Phase 1 — API contracts
│   ├── README.md
│   ├── cities.md        # GET /api/cities/search, city selection payload
│   └── browse-and-profile.md  # updated browse queries + profile/team/event DTOs
├── checklists/
│   ├── requirements.md  # (from /speckit-specify)
│   └── ui-review.md      # instantiated during UI work
└── tasks.md             # /speckit-tasks output (NOT created here)
```

### Source Code (repository root)

```text
backend/
├── Entities/
│   ├── City.cs                     # NEW — canonical city (BaseEntity)
│   ├── CityDistance.cs             # NEW — precomputed city-to-city km cache
│   ├── PlayerProfile.cs            # Hometown → HomeCityId (FK)
│   ├── Team.cs                     # City (string) → CityId (FK)
│   └── Event.cs                    # City/Country (string) → CityId (FK)
├── Services/
│   ├── Geocoding/                  # NEW
│   │   ├── IGeocodingClient.cs     # search + resolve-by-id against Photon
│   │   ├── PhotonGeocodingClient.cs
│   │   ├── ICityService.cs         # search proxy + upsert/link + distance backfill
│   │   └── CityService.cs
│   └── Search/                     # proximity sort added to Team/Event browse
│       ├── TeamSearchService.cs
│       └── EventSearchService.cs
├── Controllers/
│   └── CitiesController.cs         # NEW — GET /api/cities/search
├── Common/GeocodingOptions.cs      # NEW — provider base URL, extract hint
├── Dtos/                           # City DTOs + updated Profile/Search/Event/… DTOs
├── Data/
│   ├── AppDbContext.cs             # DbSet<City>, DbSet<CityDistance> + config
│   ├── DevDataSeeder.cs            # seed cities + link entities
│   └── Migrations/                 # one migration: add City/CityDistance, swap FKs
└── tests/                          # unit + integration (resilience harness reuse)

frontend/apps/web/src/app/
├── shared/city-picker/            # NEW jh-city-picker (.ts/.html/.css)
├── core/services/city.service.ts   # NEW — search proxy client
├── core/models/city.models.ts      # NEW — CityOption, City view models
├── features/onboarding/            # city step → picker; team step → proximity
├── features/profile/               # edit → picker; display → "City, Country"
├── features/teams/ + features/events/  # create/edit → picker; cards show country
└── features/browse/                # proximity sort option + country filter

docker-compose.yml                  # NEW photon service (+ .test/.e2e/.debug as needed)
infra/ (terraform)                  # Photon deployment + volume on AKS (Dev/Prod)
```

**Structure Decision**: Existing Option-2 web layout (`backend/` + `frontend/`). The geocoder is a new backend service package mirroring the Email integration's shape; browse proximity extends the feature-007 search services in place; the picker is a shared Angular component.

## Phase 0 — Research

Complete. See [research.md](./research.md): R1 Photon provider (+ extract-size risk), R2 cached city-to-city haversine (no PostGIS), R3 OSM-id de-dupe, R4 backend-proxied select-to-persist, R5 geocoder resilience config (GET retry-safe), R6 drop-and-reseed (no migration), R7 shared `jh-city-picker`.

## Phase 1 — Design & Contracts

Complete. Artifacts:
- **[data-model.md](./data-model.md)** — `City`, `CityDistance`, and the FK swaps on profile/team/event, with validation and the distance-backfill lifecycle.
- **[contracts/](./contracts/)** — `GET /api/cities/search`; the city-selection payload contract (provider id, server re-resolved); updated browse query params (`sort=Proximity`, `country`) and the profile/team/event location DTO shape.
- **[quickstart.md](./quickstart.md)** — bring up Photon locally, seed cities, and validate each user story end-to-end incl. the geocoder-down degradation path.

## Complexity Tracking

No constitution violations — none required.
