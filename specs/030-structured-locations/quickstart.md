# Quickstart & Validation: Structured Locations

**Feature**: 030-structured-locations | **Date**: 2026-07-25

This guide proves the feature end-to-end locally. It is a **validation guide**, not implementation — code lives in `tasks.md` / the implementation phase.

## Prerequisites

- Docker + docker-compose (the stack, including the new **Photon** geocoder, comes up via compose).
- `.env` populated from `.env.sample` (adds the geocoder base URL, e.g. `GEOCODING__BASEURL=http://photon:2322`).
- A regional Photon index extract available to the geocoder container (see R1; local default = DACH/Europe extract). The first container start imports/loads the extract — allow for cold-start time.

## Bring up the stack

```powershell
docker compose up -d           # backend, frontend, database, redis, mailpit, photon
docker compose logs -f photon  # wait until the geocoder reports ready
```

Then apply the migration and seed:

```powershell
# EF migration adds Cities + CityDistances and swaps the freeform columns for FKs
# DevDataSeeder seeds a handful of real cities and links seeded profiles/teams/events
```

Confirm the geocoder answers (server-side sanity check — the browser never calls it):

```powershell
curl "http://localhost:2322/api/?q=koln&limit=5"   # expect GeoJSON features with country + coords
```

## Validate the user stories

### US1 — Choose a real home city (P1)

1. Sign in as a fresh user; onboarding shows the city step.
2. Type `köl` in the city picker → debounced suggestions appear, each labelled `"City, Region, Country"` (disambiguation, FR-003).
3. Select **Köln, Germany**; finish onboarding.
4. Open the profile → location shows **"Köln, Germany"** (FR-010).
5. Edit profile → clear the city → profile shows no location (FR-006).
6. **Negative**: search a nonsense string → "no matching city" empty state, no forced save (US1 scenario 5).

**Pass**: profile stores a `HomeCityId` (verify a `Cities` row with `ExternalId`, country, lat/lon exists), display is country-qualified.

### US2 — Teams & events carry a real city (P1)

1. Create a city-team → pick a city in the picker → team card + page show `"City, Country"`.
2. Create a **Mixteam** → no city required, none shown; not placed for proximity.
3. Create an **in-person event** → pick a city → shows `"City, Country"`.
4. Create a **virtual event** → no city; excluded from proximity later.

**Pass**: `Teams.CityId` / `Events.CityId` populated; `CityDistances` gained rows for any new city (both directions).

### US3 — Onboarding "near you" teams (P2)

1. As a player whose home city is **Köln**, reach the onboarding team step.
2. Teams in/near Köln rank ahead of a demonstrably farther team (e.g. Berlin) (SC-004, FR-013).
3. **Degradation**: stop the geocoder container mid-flow → the step still loads with the default beginner-friendly list; no error, no trap (US3 scenario 3).

### US4 — Browse near you (P2)

1. Browse teams with a home city set → default sort is unchanged (name); choose **"Near me"** → nearest-first (FR-014).
2. Browse events → **"Near me"** → only located events, nearest-first; virtual events absent until you switch back to date sort (FR-016).
3. Apply a **country filter** → only that country's teams/events (FR-015), independent of sort.
4. As a player with **no** home city → "Near me" is unavailable / prompts to set a city; default ordering still works (US4 scenario 4).

## Validate resilience & privacy (Principles I, VII)

1. **Geocoder down**: stop the `photon` container.
   - `GET /api/cities/search?q=koln` → **503** generic body; picker shows retryable transient error (not a stack trace).
   - Onboarding completes; profile/team/event save with city left unset; browse works with default sort (SC-006).
2. **Slow provider**: confirm the picker fails fast within the configured total timeout, not a hang (`Resilience:Outbound:Geocoding`).
3. **Retry-safe**: city search is GET — a transient fault retries (contrast the email POST); verify via the resilience harness style used in `backend/tests/.../Resilience`.
4. **Logs**: confirm no user query tied to identity, no secrets, appear in resilience/telemetry logs (SC-007, FR-021).

## Automated checks to run

```powershell
# Backend
dotnet test backend            # unit + integration (city service, upsert/dedupe, distance backfill, proximity sort, geocoder resilience)
# Frontend
npx nx test web                # jh-city-picker, city.service, updated models/components
npx nx lint web
# E2E
docker compose -f docker-compose.e2e.yml up --build   # onboarding city pick + near-you browse
```

**Definition of done for the slice**: US1–US4 acceptance scenarios pass; SC-001…SC-007 verified; UI review checklist (`checklists/ui-review.md`) green against DESIGN.md; geocoder-down path degrades with zero hard failures.
