# Implementation Plan: Browse Public Trainings

**Branch**: `feat/043-browse-public-trainings` | **Date**: 2026-08-04 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/043-browse-public-trainings/spec.md` · GitHub Issue
[#145](https://github.com/jnroesch/juggerhub/issues/145)

## Summary

Give public trainings a discovery surface of their own: a fourth Browse tab at `/browse/trainings`
listing, one row per dated session, every training session teams have opened to everyone — searchable
by name, filterable by city, country and date range, ordered soonest-first with an opt-in
nearest-first for viewers who have a home city. Then repoint the home empty state's "Browse open
trainings" button, which today lands in the events browser.

**This adds no data.** Every ingredient exists: the `TrainingVisibility` public/team-only split with
its per-session override (018), the canonical city and structured address (042), the `CityDistance`
proximity cache (030), the shared browse shell (007), and a session page that already admits
outsiders as guests. The work is one query, one endpoint, one card DTO, one Angular page, a fourth
tab, and three catalogue entries. **No entity, no migration, no new dependency.**

The design has two load-bearing points, both inherited rather than invented:

1. **The address resolves as an indivisible block keyed on `CityIdOverride`** — in the `WHERE` clause
   as much as in the projection. Filtering by city must resolve the block, or a relocated session is
   returned under the series' city (research R3).
2. **The location label is composed in memory by the one shared helper**, never re-implemented, which
   is what makes SC-003 structural (research R4).

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Angular 22 with Nx (frontend)

**Primary Dependencies**: EF Core 10 + Npgsql; ASP.NET Core MVC with Asp.Versioning; Angular signals
(zoneless), Tailwind CSS, Transloco. **Nothing new is added.**

**Storage**: PostgreSQL 18. Read-only for this feature — `TrainingSessions`, `Trainings`, `Teams`,
`Cities`, `CityDistances`, `PlayerProfiles`. **No migration.**

**Testing**: xUnit + Testcontainers (`backend/tests/JuggerHub.Api.IntegrationTests/Search/`); Jest
(frontend unit); Playwright (`frontend/apps/web-e2e/src/`)

**Target Platform**: Linux containers on AKS (Dev/Prod), docker-compose locally

**Project Type**: Web application — .NET REST API + Angular SPA

**Performance Goals**: one indexed query per page, 20 rows, no N+1. The proximity path is a
single-sided join on `IX_CityDistances_FromCityId_DistanceKm`, matching teams and events.

**Constraints**: pagination mandatory (constitution III); explicit `.Select` projections, no object
mapper (II); server-side filtering only — the client never receives a non-matching row (I).

**Scale/Scope**: tens of teams, low hundreds of cities, low thousands of sessions. 1 new endpoint,
1 new backend service, 1 new Angular page, 1 modified shared component, ~14 new i18n keys × 3.

## Constitution Check

*GATE: evaluated before Phase 0 and re-evaluated after Phase 1. Constitution v1.4.0.*

| # | Gate | Verdict | How this feature satisfies it |
|---|------|---------|-------------------------------|
| I | Security-first, never trust the client | **PASS** | The visibility rule is a server-side `WHERE` on effective visibility; no client input can widen it. The query carries no viewer-supplied identity — the home city is resolved server-side from the caller's own profile, never accepted as a parameter. Team-only sessions are absent from the result set, not filtered client-side. |
| II | Thin controllers, service-centric | **PASS** | One `[HttpGet]` on `TrainingsController` doing auth-resolve → home-city-resolve → delegate. Logic in `TrainingSearchService` behind `ITrainingSearchService`, DI-registered beside the other three search services. Explicit `.Select` projection to a DTO; no mapper. |
| III | Disciplined data access | **PASS** | `AsNoTracking`, explicit projection of only the needed columns, mandatory `Skip`/`Take` via the shared `PaginationRequest`/`PagedResult<T>`. No new entity, so no `BaseEntity`/UUIDv7 obligation. Read-only — no `ExecuteUpdate`, no `ModifiedDate` concern. |
| IV | Auth & session | **PASS** | Inherits `TrainingsController`'s class-level JWT `[Authorize]`. No token handling, no new auth surface, no `[AllowAnonymous]`. |
| V | Environment parity | **PASS** | No infrastructure, config, secret, or container change. Behaves identically in all three environments. |
| VI | Conventions & tooling | **PASS** | Angular page ships separate `.html`/`.css`/`.ts`. No new scripts (and none would be `.sh`). |
| VII | Resilient by default | **NOT ENGAGED** | No outbound HTTP call is added. City resolution and distance lookup are local SQL (research R10). ⚠ Adding an `HttpClient`, retry policy, or breaker here would be wrong — see the warning in R10. |
| 7 | UI/design compliance | **REQUIRED** | Ships UI ⇒ `checklists/ui-review.md` instantiated from the template and verified against the diff before verification. The fourth tab at 375px (research R9) is the item that actually needs judgement. |
| 8 | Resilience review | **PASS** (vacuous) | No network call added; gate satisfied by not engaging it. |

**Result: no violations. Complexity Tracking is empty and omitted.**

## Project Structure

### Documentation (this feature)

```text
specs/043-browse-public-trainings/
├── plan.md              # This file
├── spec.md
├── research.md          # Phase 0 — 12 findings, incl. the FR-024 resolution
├── data-model.md        # Phase 1 — read model + query semantics (no schema change)
├── quickstart.md        # Phase 1 — runnable validation scenarios
├── contracts/
│   └── trainings-browse.md
├── checklists/
│   ├── requirements.md
│   └── ui-review.md     # instantiated at implementation time (gate 7)
└── tasks.md             # /speckit-tasks — NOT created by /speckit-plan
```

### Source Code (repository root)

```text
backend/
├── Controllers/
│   └── TrainingsController.cs            # + root [HttpGet] Browse (R6)
├── Dtos/
│   └── Search/SearchDtos.cs              # + TrainingCardDto, TrainingBrowseQuery
├── Services/
│   ├── Search/
│   │   ├── SearchQuery.cs                # + TrainingSort enum
│   │   └── TrainingSearchService.cs      # NEW — the whole query
│   └── Trainings/TrainingSeriesService.cs # unchanged; LocationLabelFor reused (R4)
└── tests/JuggerHub.Api.IntegrationTests/Search/
    └── TrainingBrowseTests.cs            # NEW

frontend/apps/web/src/app/
├── core/
│   ├── models/search.models.ts           # + TrainingCard, TrainingBrowseParams
│   └── services/search.service.ts        # + browseTrainings + toTrainingParams
├── features/browse/
│   ├── browse-shell/browse-shell.component.html   # 3 tabs → 4 (R9)
│   └── browse-trainings/                 # NEW — .ts / .html / .css / .spec.ts
├── features/dashboard/dashboard.component.html    # FR-027 one-line fix
└── app.routes.ts                         # + /browse/trainings

frontend/apps/web/public/i18n/{en,de,es}.json      # + browse.tabTrainings, browse.trainings.*
frontend/apps/web-e2e/src/browse.spec.ts           # + trainings tab scenarios
```

**Structure Decision**: the existing web-application split. This feature adds exactly one new
directory (`features/browse/browse-trainings/`) and one new backend file
(`Services/Search/TrainingSearchService.cs`); everything else is an edit to a file that already
exists. No new project, layer, or abstraction — the browse pattern is four instances of one shape,
and this is the fourth.

## Implementation Approach

### Backend

**`TrainingBrowseQuery`** mirrors `EventBrowseQuery` — `Q`, `HidePast` (default `true`), `From`,
`To`, `City`, `Country`, `Sort` — with no `Type` (trainings have no type) and a new `TrainingSort`
enum (`SessionDateAsc = 0`, `Proximity = 1`).

**`TrainingSearchService.BrowseAsync`** builds one `IQueryable<TrainingSession>`:

1. **The visibility gate first** — `(s.VisibilityOverride ?? s.Training.Visibility) == Public`. This
   is the one clause that must never be conditional on anything (FR-003/FR-004).
2. `s.Status == Scheduled` — excludes cancelled and skipped in one comparison (FR-005).
3. `HidePast` ⇒ `s.SessionDate >= today`, day-granular (research R2).
4. `From`/`To` against `SessionDate`.
5. City / country resolved **through the address block**:
   `(s.CityIdOverride != null ? s.CityOverride.Name : s.Training.City.Name)` — never
   `s.Training.City.Name` alone (research R3).
6. `Q` via the shared `SearchQuery.Normalize` + `ContainsPattern` + `AppDbContext.Unaccent` over
   `s.Training.Name`.
7. Order: `SessionDate, Id` by default; under proximity, join `CityDistances` on the **effective**
   city id and order `DistanceKm, SessionDate, Id`, with the total recomputed using the same `Any()`
   predicate as the join (research R5 — follow Teams, not Events).
8. Page in SQL, then compose `LocationLabel` in memory via `TrainingSeriesService.LocationLabelFor`
   (research R4).

**The 14-day proximity window** (research R1) is applied at step 4 as a query normalisation:
`if (Sort == Proximity && To is null) To = today.AddDays(14)`. The response echoes the effective
`From`/`To` so the frontend can render the chip honestly.

### Frontend

`BrowseTrainingsComponent` is a fourth instance of the established page shape —
`BrowseList<TrainingCard>` + `jh-browse-shell` + `jh-filter-panel`, copying
`browse-events.component.ts` structure including the `langChanges$` signal that keeps computed labels
reactive under Transloco.

Two things differ from a copy-paste:

- **The filter panel gains a `jh-city-picker`** alongside the country picker — the product's first
  city filter (research R8).
- **The row shows the team**, which no other browse row does, because "whose training is this" is the
  question a stranger asks first.

⚠ **Zoneless reactivity**: 042's e2e lesson was that a `computed()` over plain non-signal fields never
recomputes in a zoneless app. All filter state stays in signals.

## Open Decisions

| # | Decision | Status |
|---|----------|--------|
| 1 | **FR-024 — recurrence under nearest-first.** | ✅ **CLOSED by the owner, 2026-08-04, as option (c): accept it.** The plan's 14-day window was built, then removed — "a sort control that silently applies a filter does not make sense". `onSortChange` now changes the ordering and nothing else; FR-024 was rewritten to require exactly that. |
| 2 | **Fourth-tab layout at 375px**, where Spanish "Entrenamientos" is the binding case. | ✅ **CLOSED in implementation**: a 2×2 grid below `sm`, one row of four from `sm` up, `min-h-[44px]` per cell. Scrolling (hides the new tab), smaller type (breaches CHK009) and truncation were all rejected. Verified by Playwright at both viewports and against the longest label in each catalogue; proven non-vacuous by forcing the rejected 4-across layout, which clips "Entrenamientos" at 393px. |
| 3 | **Row layout** — the browse row leads with the **team**, not the training name, and carries the home screen's dark date chip. | ✅ **Owner decision, 2026-08-04.** A guest is choosing who to train with; the training's own name is a label someone typed and carries little information across teams. Deliberately the reverse hierarchy from the events row. |

## Spec Drift

| Item | Spec says | Plan delivers | Why |
|------|-----------|---------------|-----|
| FR-006 | "sessions that have not yet **ended**" | Sessions not on an **earlier day** (`SessionDate >= today`, UTC) | Every trainings query in the product already works this way; a session must not vanish from browse while still showing on the team tab. A session that ended this morning stays listed until midnight. Research R2. |
| FR-024 | Open question | Resolved by decision, not by owner answer | See Open Decisions #1. |

## Deliberate Non-Goals

Recorded so a reviewer does not read them as omissions:

- **The `EventSearchService` proximity-count defect** (research R5) is left in place. Fixing it would
  edit the events browse, which FR-030 forbids and SC-010 verifies. → follow-up issue.
- **No city filter is retrofitted** onto teams or events, though both backends already support it
  (research R8). Same reason.
- **No RSVP counts on the card** (research R12) — three subqueries per row for decoration that reads
  as capacity, a concept trainings do not have.
- **No anonymous access**, no map, no radius input, no "use my current location", no grouping by
  series.

## Phase Status

- [x] **Phase 0** — `research.md`: 12 findings, all unknowns resolved (R1 by decision — see Open
  Decisions #1)
- [x] **Phase 1** — `data-model.md`, `contracts/trainings-browse.md`, `quickstart.md`, agent context
  updated
- [x] **Constitution re-check after Phase 1** — still no violations. Phase 1 added no entity, no
  outbound call, and no new abstraction, so every verdict in the table above stands unchanged.
- [ ] **Phase 2** — `tasks.md` via `/speckit-tasks`
