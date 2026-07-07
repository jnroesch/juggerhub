# Implementation Plan: Search / Browse

**Branch**: `007-search` | **Date**: 2026-07-07 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-search/spec.md`

## Summary

Add a **Browse** area with three sibling discovery pages — **Teams** (`/browse/teams`), **Events** (`/browse/events`), **Players** (`/browse/players`) — that share **one shell** and behave identically, differing only in filter set and default sort. The shell: a title + "Browse" caption, a full-width **search** field with **live results as you type** (client-debounced, server-filtered), a **Filters** button with an active-count **badge** opening an on-demand panel (**bottom sheet on mobile, slide-over drawer on desktop**) that carries a **Reset** and a primary **"Show N"** action reflecting a **live pending count**, a row of removable **active-filter chips** (+ "Clear all"), a **result-count line** summarizing filters in words, **compact list rows** that link to each entity's detail page, **infinite-scroll pagination** over the existing `PagedResult<T>` contract, a **locked "Near me — coming soon"** placeholder in every panel, and the four list states (**empty / no-results / loading / error-with-retry**). Before the user types, each page is **browsing everything** with its defaults already applied — never a blank "start typing" screen.

The backend adds three **public browse endpoints** at the collection roots that do not exist today — `GET /api/v1/teams`, `GET /api/v1/events`, `GET /api/v1/profiles` — each doing **all filtering, sorting, searching, and paging server-side** (the client never receives a non-matching row), returning slim **card DTOs** of public fields only via `.Select`/`ProjectToType` + `AsNoTracking` + `PagedResult<T>`. Search is **case- and accent-insensitive** (Postgres `unaccent`, so "koln" matches "Köln"). This slice follows the established **thin-controller / DI-service / EF-Core-directly / Mapster-DTO / `PaginationRequest`+`PagedResult<T>`** conventions and adds a cohesive `Services/Search/` domain slice (three services), mirroring how 006 grouped under `Services/Events/`.

Per the clarifications, this feature adds **exactly two stored fields** — a team **`BeginnersWelcome`** flag and a player **`AppearInSearch`** opt-in (both `bool NOT NULL DEFAULT false`) — plus enables the `unaccent` extension, in a single migration `AddDiscoveryFields`. **Team "active"** is **derived** (a team has an `EventParticipation` tied to an `Event` starting within the last 12 months), not stored. **Player "position"** is **derived** from existing `ProfilePompfe` rows (the `Pompfe` enum — Läufer/Stab/Q-Tip/Kette/Schild/Langpompfe/Doppel-Kurz); no position field is added. The **player opt-in is a hard server-side privacy invariant**: `GET /profiles` returns only `AppearInSearch = true` rows across **every** query, filter, sort, and auth state — a non-opted-in player can never be surfaced. Two small self-service write paths carry the new fields: `AppearInSearch` rides the existing `PUT /api/v1/profiles/me` (`UpdateProfileRequest`), and `BeginnersWelcome` needs a **new admin-only** team-settings write `PATCH /api/v1/teams/{slug}` (Teams has no update endpoint today).

The frontend adds a `features/browse/` slice: three lazy, **anonymous** (un-guarded, in-shell) page components composed from a shared **`BrowseShellComponent`** (search + toolbar + chips + count + list + the four states + filter panel host) driven by a small per-entity **config** (labels, filter schema, sort options, row template, fetch fn), a shared **`FilterPanelComponent`** (sheet/drawer responsive), and a `SearchService` in `core/services` (`browseTeams` / `browseEvents` / `browsePlayers`, signals). A **"Browse"** entry point is added to the shell navigation. The two settings toggles are wired into the existing **profile owner** (Appear in search) and **team settings** (Beginners welcome) screens. Everything is styled from **DESIGN.md** (warm sand/coral system) and validated desktop + mobile. **Near me/distance, global cross-entity search, saved searches, and geo ranking are explicitly out of scope** — the Near-me control is a non-functional locked placeholder that seeds a later location feature.

## Technical Context

**Language/Version**: Backend — C# 13 on .NET 10 (ASP.NET Core, EF Core 10, ASP.NET Core Identity). Frontend — TypeScript on Angular (standalone components, signals) in an Nx workspace.

**Primary Dependencies**:
- Backend (existing, reused): `Npgsql.EntityFrameworkCore.PostgreSQL`, `Mapster`, `Asp.Versioning.Mvc`, the shared `PaginationRequest`/`PagedResult<T>` (`backend/Common/Pagination.cs`). **No new NuGet packages** — accent-insensitive search uses the PostgreSQL **`unaccent`** extension via `EF.Functions.Unaccent`/`ILike` (Npgsql-mapped); no full-text search engine is introduced.
- Frontend: `@angular/*` (router, reactive forms, signals), RxJS (debounce), Tailwind mapped onto the DESIGN.md CSS custom properties in `frontend/apps/web/src/styles.css`; `jest` (unit), `@playwright/test` (e2e). **No new runtime dependency.**

**Storage**: PostgreSQL 18. **Two new columns** — `Teams.BeginnersWelcome bool NOT NULL DEFAULT false`, `PlayerProfiles.AppearInSearch bool NOT NULL DEFAULT false`. One EF migration `AddDiscoveryFields` (adds both columns + `CREATE EXTENSION IF NOT EXISTS unaccent`; auto-applies on startup). New **indexes** to keep browse cheap: partial index on `PlayerProfiles(AppearInSearch) WHERE AppearInSearch`, index on `Events(StartsAt)` and `Events(Status)`, index on `EventParticipations(TeamId)` (for the active-team EXISTS). Name/city trigram indexing is noted in research but deferred (default: `unaccent` + `ILIKE`, add a GIN/trigram index only if community scale needs it). `DevDataSeeder` extended so demo data exercises all three pages (dormant vs active teams, beginners-welcome teams, past/future/cancelled events, opted-in vs not-opted-in players, varied cities/positions).

**Testing**: Backend — xUnit + `WebApplicationFactory<Program>` + `Testcontainers.PostgreSql` (extend the existing integration project): **player opt-in invariant** (non-opted-in absent across query/filter/sort and both anon+authed — the security test), team **active derivation** (12-month window boundary), hide-past + **cancelled-excluded** events, date-range + type + city event filters, beginners-welcome + city team filters, **position** filter over pompfen, **accent/case-insensitive** search ("koln"→"Köln", "KÖLN"→"Köln"), **pagination** (skip/take clamp, stable order at page boundaries, no dup/drop on ties), **anonymous access** to all three browse endpoints, and the two write paths (`AppearInSearch` via `PUT /profiles/me`; `BeginnersWelcome` via `PATCH /teams/{slug}` admin-only, non-admin `403`). Frontend — Jest unit for the browse shell state machine (debounce, chip add/remove, pending-vs-applied filter state, live count, the four states, first-page reset on query/filter change) + Playwright `browse.spec.ts` (browse defaults visible without typing → live search narrows → open filter sheet/drawer → apply → chips + count → clear → no-results state; players page shows only opted-in and the opt-in note), desktop + mobile.

**Target Platform**: Linux containers via Docker (`docker-compose`). Product UI targets desktop + mobile (responsive web).

**Project Type**: Web application — existing sibling `backend/` (.NET) and `frontend/` (Nx/Angular) trees.

**Performance Goals**: No throughput targets. All browse reads are single projected, `AsNoTracking`, paginated queries; the count and the page share one predicate. Live search fires after a short client debounce (~250 ms) so keystrokes don't each hit the server. Target: results within ~1 s of a typing pause at community scale (SC-002). Player opt-in is a partial-indexed predicate; team-active is an `EXISTS` over an indexed FK.

**Constraints**: Security-first / OWASP / never-trust-the-client — **all** filtering/sorting/searching and the **player opt-in gate** are server-side; the client cannot request a hidden row. Browse returns **public fields only** (no emails, no admin-only internals) stripped at the projection boundary. Browse is **anonymous** by design (a visitor decides before signing in); the opt-in gate applies **identically** to anonymous and authenticated callers. Generic `ProblemDetails`; no stack traces/secrets. `.ps1`-only scripts; Docker-only workflow; responsive UI at multiple viewports; Angular `.html`/`.css`/`.ts` kept separate; `Pompfe`/`EventType` enums serialize as names via the existing global `JsonStringEnumConverter`.

**Scale/Scope**: 3 new browse endpoints + 1 new team-settings write + 1 extended profile write. 2 new columns, 1 migration, ~4 indexes, 0 new entities, 0 new enums. Backend `Services/Search/` (3 services + shared query/normalization helper) + card DTOs. Frontend `features/browse/` (3 page components + shared `BrowseShellComponent` + `FilterPanelComponent` + per-entity row components) + `SearchService` + nav entry + 2 settings toggles. Sized so a later **near-me/geo**, **global search**, and **saved searches** consume this shell and these endpoints without reshaping them.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | How this plan complies | Verdict |
|---|-----------|------------------------|---------|
| I | Security-First, Never Trust the Client | Every filter/sort/search decision and the **player opt-in gate** are enforced server-side; the client cannot surface a hidden or non-matching row (FR-005, FR-042). Browse DTOs carry **public fields only** (no user emails/admin internals), stripped at the `.Select` projection. The two write paths authorize server-side: `AppearInSearch` only mutates the caller's own profile (existing `PUT /profiles/me`), `BeginnersWelcome` requires **team-admin** (`PATCH /teams/{slug}`, non-admin `403`). Anonymous browse exposes nothing private. Generic `ProblemDetails`; no secrets/stack traces. OWASP: **A01 broken access control** — opt-in gate + team-admin write are the core authz; **A03 injection** — EF parameterization, `unaccent`/`ILIKE` via `EF.Functions` (no string concatenation); **A04 insecure design** — bounded lists, opt-in default OFF, no cross-entity leak. | ✅ |
| II | Thin Controllers, Service-Centric | Browse endpoints are thin `[HttpGet]` actions binding a `*BrowseQuery` record (+`PaginationRequest`) and delegating to `ITeamSearchService`/`IEventSearchService`/`IPlayerSearchService` in `Services/Search/`; the team-settings write delegates to the existing `ITeamService`. DI behind interfaces; **no repository layer** (EF Core directly). Services return `PagedResult<XCardDto>` via projection (consistent with existing paged endpoints like `GetMembers`); Mapster config extended for any non-trivial shaping. | ✅ |
| III | Disciplined Data Access (EF Core + PostgreSQL) | New columns default at the DB; the two-field migration also enables `unaccent`. **Every** browse read is `.Select`/`ProjectToType` + `AsNoTracking` + `Skip/Take` via `PaginationRequest`/`PagedResult<T>` with a **stable secondary sort key** (Id) so paging never drops/dupes on ties. Indexes added for the opt-in predicate, `Events.StartsAt`/`Status`, and `EventParticipations.TeamId`. The `AppearInSearch` and `BeginnersWelcome` toggles are tracked saves (interceptor sets `ModifiedDate`). Team-active `EXISTS` uses the indexed FK. No unbounded collection is ever returned. | ✅ |
| IV | Secure Authentication & Session Management | Reuses Identity + JWT-in-httpOnly-cookie unchanged. All three browse endpoints are **anonymous** (`[AllowAnonymous]`), matching the public dashboard/event-page precedent; the opt-in gate is data-level, not auth-level, so it holds for signed-out callers. The two writes require the existing bearer scheme (`PUT /profiles/me`, `PATCH /teams/{slug}`). No password/identity surface changes. | ✅ |
| V | Environment Parity & Containerized Deployments | Same `docker-compose` stack; **no new services or secrets**. The `unaccent` extension is created by the auto-applied migration in every environment (local/Dev/Prod parity). Per-service Dockerfiles unchanged. CI/CD + Terraform remain deferred (allowed by scope). | ✅ |
| VI | Consistent Conventions & Tooling | Angular components keep separate `.html`/`.css`/`.ts`; any scripts are `.ps1` only; Tailwind styled from DESIGN.md tokens; the Search wireframe informs layout only (recreated, not copied). `Pompfe`/`EventType` serialize as names via the existing global `JsonStringEnumConverter`. Browse pages are lazy-loaded (like events) to keep the initial bundle lean. | ✅ |
| — | Secret & Configuration Management | No new secrets. Optional `SearchOptions` (active-team window months = 12; min query length) are plain config with safe defaults; no Key Vault. | ✅ |

**Result**: PASS — no violations; Complexity Tracking left empty. The one item worth flagging is a **UX limitation, not a constitution deviation**: team browse rows link to `/t/:slug`, which is auth-guarded today (no public team page exists, unlike public profiles `/u/:handle` and public events `/events/:id`). See research §7 — v1 keeps the link and lets anonymous users hit the sign-in bounce; a public team page is a separate feature, not taken on here.

## Project Structure

### Documentation (this feature)

```text
specs/007-search/
├── plan.md              # This file
├── spec.md              # Feature specification (clarified 2026-07-07)
├── research.md          # Phase 0 — endpoint placement, accent-insensitive search, team-active derivation, opt-in gate, sort/keyset, shared-shell strategy, team-link limitation
├── data-model.md        # Phase 1 — 2 new fields, derived values, card DTOs, browse-query records, indexes, migration, seeding
├── quickstart.md        # Phase 1 — runnable end-to-end validation (Scenarios A–H)
├── contracts/
│   ├── openapi.yaml     #   GET /teams, /events, /profiles (browse) + PATCH /teams/{slug} + PUT /profiles/me (AppearInSearch)
│   └── README.md
└── checklists/
    └── requirements.md  # Spec quality checklist (from specify + clarify)
```

### Source Code (repository root)

```text
backend/                                            # .NET 10 solution (namespace JuggerHub)
├── Controllers/
│   ├── TeamsController.cs                           # EXTEND — [HttpGet] "" browse (anon) + [HttpPatch] "{slug}" settings (BeginnersWelcome, admin)
│   ├── EventsController.cs                          # EXTEND — [HttpGet] "" browse (anon)
│   └── ProfilesController.cs                        # EXTEND — [HttpGet] "" browse players (anon, opt-in gated); PUT me carries AppearInSearch
├── Services/
│   ├── Search/
│   │   ├── ITeamSearchService.cs / TeamSearchService.cs     # NEW — browse teams (name/city search, active-EXISTS, beginners, city; A–Z)
│   │   ├── IEventSearchService.cs / EventSearchService.cs   # NEW — browse events (name search, hide-past, date range, type, city; soonest)
│   │   ├── IPlayerSearchService.cs / PlayerSearchService.cs # NEW — browse players (opt-in gate, name search, position via pompfen, city; A–Z)
│   │   └── SearchQuery.cs                                   # NEW — shared: query normalization (unaccent/trim/min-len), sort enums, EF.Functions helpers
│   ├── Teams/TeamService.cs                          # EXTEND — UpdateSettings(slug, beginnersWelcome) admin-guarded (mirrors existing membership guard)
│   ├── Profile/ProfileService.cs                     # EXTEND — UpdateMine maps AppearInSearch
│   └── …                                             # EXISTING
├── Entities/
│   ├── Team.cs                                       # EXTEND — bool BeginnersWelcome (default false)
│   └── PlayerProfile.cs                              # EXTEND — bool AppearInSearch (default false)
├── Data/
│   ├── AppDbContext.cs                               # EXTEND — column config + indexes (opt-in partial, Events.StartsAt/Status, EventParticipations.TeamId)
│   ├── Migrations/                                   # NEW migration: AddDiscoveryFields (2 columns + CREATE EXTENSION unaccent + indexes)
│   └── DevDataSeeder.cs                              # EXTEND — dormant/active teams, beginners-welcome, past/future/cancelled events, opted-in/not players, cities/positions
├── Dtos/
│   ├── Search/                                       # NEW — TeamBrowseQuery, EventBrowseQuery, PlayerBrowseQuery (bind filters+sort+PaginationRequest);
│   │                                                 #        TeamCardDto, EventCardDto, PlayerCardDto (public card fields)
│   ├── Teams/UpdateTeamSettingsRequest.cs            # NEW — { bool beginnersWelcome }
│   └── Profile/UpdateProfileRequest.cs               # EXTEND — + bool appearInSearch
├── Common/
│   └── SearchOptions.cs                              # NEW (optional) — active-team window months (12), min query length; safe defaults
├── Program.cs                                        # EXTEND — register ITeamSearchService/IEventSearchService/IPlayerSearchService; bind SearchOptions
└── tests/JuggerHub.Api.IntegrationTests/
    └── Search/                                       # NEW — opt-in invariant, active derivation, hide-past/cancelled, filters, accent search, pagination, anon, writes

frontend/apps/web/src/app/
├── core/
│   ├── services/search.service.ts                   # NEW — browseTeams/browseEvents/browsePlayers (signals over PagedResult<T>)
│   └── models/search.models.ts                      # NEW — card DTOs, browse-query params, sort + filter types, Pompfe position catalog reuse
├── features/browse/
│   ├── browse-shell/            { *.ts/.html/.css }  # NEW — shared shell: header, search, Filters button+badge, Sort, chip row, count, list, 4 states, panel host
│   ├── filter-panel/            { *.ts/.html/.css }  # NEW — responsive bottom-sheet (mobile) / slide-over drawer (desktop); Reset + "Show N"; locked Near-me
│   ├── browse-teams/            { *.ts/.html/.css }  # NEW — Teams config + team row (logo initial, name, city, players, Beginners chip)
│   ├── browse-events/           { *.ts/.html/.css }  # NEW — Events config + event row (name, date, city/location, type)
│   └── browse-players/          { *.ts/.html/.css }  # NEW — Players config + player row (avatar, name, city, position) + opt-in note
├── features/profile/profile-owner/ { *.ts/.html }   # EXTEND — "Appear in search" toggle → PUT /profiles/me
├── features/teams/team-settings/  { *.ts/.html }    # EXTEND — "Beginners welcome" toggle → PATCH /teams/{slug}
├── layout/ (shell/side-nav/top-nav)                 # EXTEND — "Browse" entry point (Teams/Events/Players)
└── app.routes.ts                                    # EXTEND — in-shell, NO guard, lazy: browse (→teams), browse/teams, browse/events, browse/players

frontend/apps/web-e2e/src/
└── browse.spec.ts                                   # NEW — browse defaults → live search → filter sheet/drawer → chips/count → clear/no-results → players opt-in, desktop+mobile

backend/appsettings*.json / docker-compose.yml        # EXTEND (optional) — Search section (active-team window, min query length) with safe defaults
```

**Structure Decision**: Web application extending the existing `backend/` and `frontend/` trees. Backend groups the new read logic in a cohesive `Services/Search/` slice (three services + a shared query helper), mirroring `Services/Events/` and `Services/Teams/`; the browse endpoints attach to the **existing** resource controllers at their collection roots (`GET /teams`, `GET /events`, `GET /profiles`) rather than a separate `SearchController`, keeping each resource's surface together and the endpoints RESTfully discoverable. The two settings writes extend existing services/DTOs. The frontend introduces a `features/browse/` slice built around **one shared `BrowseShellComponent`** parameterized per entity, so the three pages are provably identical apart from filter set, sort, and row content (SC-004); browse pages are **in-shell but un-guarded** (anonymous), lazy-loaded like events. No new project or library is introduced.

## Complexity Tracking

> No constitution violations. No entries required. (Team "active" and player "position" are derived from existing data rather than adding fields — the simpler choice. The only accepted trade-off is the auth-guarded team link, documented in research §7 as a bounded UX limitation, not new complexity.)
