# Quickstart — Search / Browse (007) validation

Runnable end-to-end checks that prove the feature works. Assumes the standard Docker workflow and the Dev seeder (which this feature extends per data-model §7). Backend at `/api/v1`; frontend browse pages at `/browse/*`.

## Prerequisites

```powershell
# From repo root — bring up the stack (backend applies AddDiscoveryFields on startup)
docker compose up -d --build
# Backend integration tests (extend the existing project)
./scripts/run-backend-tests.ps1   # or: dotnet test backend/tests/JuggerHub.Api.IntegrationTests
# Frontend unit + e2e
npx nx test web
npx nx e2e web-e2e --grep browse
```

Confirm the migration ran: the DB has `Teams.BeginnersWelcome`, `PlayerProfiles.AppearInSearch`, and the `unaccent` extension.

## Scenario A — Teams browse defaults (no query)

1. Open `/browse/teams`. **Expect**: a paginated list appears immediately (no typing), sorted A–Z, with a count line ("N teams"), the Filters button (badge shows 1 — "Active" is on by default), and the Sort control.
2. API check: `GET /api/v1/teams` → `200`, `items` sorted by name, `totalCount` = active teams only. `GET /api/v1/teams?activeOnly=false` returns a **larger** `totalCount` (dormant teams included).

## Scenario B — Live search + accent/case-insensitivity

1. Type `koln` in the Teams search. **Expect**: results narrow live to Köln teams; count updates.
2. API check: `GET /api/v1/teams?q=koln`, `?q=KÖLN`, and `?q=Köln` all return the same Köln teams (unaccent + ILIKE). Whitespace-only `q` returns the same as no `q`.

## Scenario C — Filter panel, chips, live count (sheet/drawer)

1. Mobile viewport: tap Filters → a **bottom sheet** opens. Desktop viewport: a **slide-over drawer** opens. Both show the team filter set, a **Reset**, and a primary **"Show N teams"** whose N updates live as you toggle "Beginners welcome".
2. Toggle "Beginners welcome" on, Apply. **Expect**: results = beginners-welcome teams; a removable **"Beginners ✕"** chip appears; count line reflects it; Filters badge increments. Remove the chip → results/count/badge revert.
3. Confirm the **"Near me — coming soon"** item is present and cannot be toggled.

## Scenario D — Events hide-past + cancelled excluded + date range/type

1. Open `/browse/events`. **Expect**: only upcoming (not-ended) events, soonest first; no cancelled event appears.
2. API: `GET /api/v1/events` excludes past + cancelled. `GET /api/v1/events?hidePast=false` includes past but **still excludes cancelled**. `?from=&to=&type=Tournament` narrows correctly; each applied filter shows as a chip.

## Scenario E — Players opt-in privacy invariant (the security check)

1. Open `/browse/players`. **Expect**: only opted-in players; the "only players who chose to appear are listed" note is visible.
2. API invariant (must all hold): pick a seeded player with `AppearInSearch = false`. They must NOT appear in:
   - `GET /api/v1/profiles` (no query)
   - `GET /api/v1/profiles?q=<their exact display name>`
   - any `?positions=…&city=…&sort=…` combination
   - anonymous **and** authenticated requests
3. Toggle: as that player, `PUT /api/v1/profiles/me { "appearInSearch": true }` → they now appear. Set back to `false` → they disappear on the next request.

## Scenario F — Position filter (derived from pompfen)

1. In Players filters, select one or more positions (e.g. Läufer/Runner, Q-Tip). **Expect**: only opted-in players whose declared pompfen include a selected position; chips reflect the choices.
2. API: `GET /api/v1/profiles?positions=Laeufer&positions=QTip` returns players having ANY of those pompfen.

## Scenario G — Pagination / infinite scroll stability

1. On a page with > 20 results, scroll to the bottom. **Expect**: the next page appends seamlessly; no row is duplicated or skipped; scrolling stops cleanly at the true end.
2. API: `skip=0..&take=20` pages tile the full set exactly once (verify with a tie on the sort key — the `Id` tiebreaker keeps order stable).

## Scenario H — Empty / no-results / settings writes

1. No-results: search a nonsense string. **Expect**: a no-results state with a one-action "clear" that restores results (not a blank page, not the empty state).
2. Empty: against an empty dataset, the distinct empty state shows.
3. Team settings write: as a team **admin**, toggle "Beginners welcome" in Team settings → `PATCH /api/v1/teams/{slug}` `204`; as a **non-admin**, the same call → `403`.
4. Profile settings write: the "Appear in search" toggle in profile settings persists via `PUT /api/v1/profiles/me` and is reflected on reload.

## Expected outcomes (traceability)

| Scenario | Proves |
|---|---|
| A | FR-003 (browse-all default), FR-022 (active default) |
| B | FR-004/005 (live, server-side), accent edge case |
| C | FR-006/007/008/011 (panel, chips, count, near-me) |
| D | FR-030/031/032 (events filters, cancelled/past) |
| E | FR-041/042/044 + **SC-003** (opt-in invariant) |
| F | FR-040/043 (position from pompfen) |
| G | FR-010 + SC-005 (bounded, stable paging) |
| H | FR-012 (states) + FR-023/045 (settings writes) |
