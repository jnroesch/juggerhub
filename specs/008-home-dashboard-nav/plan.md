# Implementation Plan: Home dashboard & top-level navigation

**Branch**: `008-home-dashboard-nav` | **Date**: 2026-07-07 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-home-dashboard-nav/spec.md`

## Summary

Replace the entity-type sidebar drawer and the walking-skeleton dashboard with a **task/person navigation shell** and a **real, agenda-led Home dashboard**. The shell exposes Home · Browse · My team, a notifications (Alerts) destination, and Profile/Account under the avatar — a single sticky **top bar** on desktop and a fixed **bottom tab bar** on mobile — and wraps every in-shell screen. Home leads with **Up next** (the player's and their teams' upcoming events, with a one-tap RSVP/withdraw toggle that reuses the existing sign-up endpoints), then **Your teams** activity, **News** (team + event sources, tagged), and **Tournaments**, with a slim desktop right rail (per-team snapshot + tournament) and a distinct **new-player empty state** for players on no team.

This is a **read-and-compose** feature: it adds **no new entities and no writes** of its own. RSVP reuses `POST /events/{id}/signup` and `DELETE /events/{id}/signup/{signupId}`. The backend adds a cohesive `Services/Home/` slice with a **composite `GET /api/v1/home`** (viewer summary + capped top-N per module for a fast first paint) plus two paginated **"see all"** reads (`GET /api/v1/home/up-next`, `GET /api/v1/home/news`) and a small **`GET /api/v1/profiles/me/teams`** the shell uses to drive "My team" routing. Tournaments "see all" links to the existing Browse events page (type = Tournament); team activity "open team" links to the existing team space. Every read is `.Select`/`ProjectToType` + `AsNoTracking` + `PagedResult<T>`, entitlement-filtered server-side (a player only ever sees their own sign-ups and their teams' news). The frontend rebuilds `layout/` (top bar + new bottom tab bar + avatar menu, replacing the sidebar) and `features/dashboard/` (module components with loading/empty/error states), adds a placeholder `features/alerts/`, a `HomeService` + `MembershipService`, and styles everything from DESIGN.md (warm sand/coral, Lucide, sentence case, mono for scores/times). **Notifications (real unread counts), an official "League" news source, and a unified activity feed (roster joins, results) are deferred** — their destinations/modules render placeholder/empty states seeded for later features; no new `EventType` values are added.

## Technical Context

**Language/Version**: Backend — C# 13 on .NET 10 (ASP.NET Core, EF Core 10, ASP.NET Core Identity). Frontend — TypeScript on Angular (standalone components, signals) in an Nx workspace.

**Primary Dependencies**:
- Backend (existing, reused): `Npgsql.EntityFrameworkCore.PostgreSQL`, `Mapster`, `Asp.Versioning.Mvc`, the shared `PaginationRequest`/`PagedResult<T>` (`backend/Common/Pagination.cs`), the existing `IEventSignupService` (RSVP), `IEventActivityService`/`ITeamActivityService`, `IEventNewsService`/`ITeamNewsService`, `IEventSearchService` (tournaments query pattern), the `EventAdminGuard`/`TeamMembershipGuard` authorization helpers. **No new NuGet packages.**
- Frontend: `@angular/*` (router, signals), RxJS, Tailwind mapped onto the DESIGN.md CSS custom properties in `frontend/apps/web/src/styles.css`, Lucide icons (per DESIGN.md); `jest` (unit), `@playwright/test` (e2e). **No new runtime dependency.**

**Storage**: PostgreSQL 18. **No new entities, no new columns.** A single optional migration `AddHomeIndexes` adds read indexes to keep the dashboard cheap where they don't already exist: `EventSignups(UserId)`, `EventSignups(TeamId)`, `TeamMemberships(UserId)`, `TeamNewsPosts(TeamId, CreatedDate)`, `EventNewsPosts(EventId, CreatedDate)`. `DevDataSeeder` is extended so demo data exercises Home (a player on ≥1 team with upcoming individual + team events, team + event news, and an upcoming tournament; plus a no-team demo player for the empty state).

**Testing**: Backend — xUnit + `WebApplicationFactory<Program>` + `Testcontainers.PostgreSql` (extend the existing integration project): composite Home shape (team-member vs no-team variant, `hasTeam`), Up next (own individuals-mode sign-ups + teams' team-mode entries, soonest-first, **past/cancelled excluded**, team-mode item exposes "team is going" with no personal signup id, individuals item exposes the viewer's signup id + status), News aggregation (team + event sources merged newest-first, each tagged by source), the **entitlement invariants** (a caller never sees another user's private sign-ups nor a non-member team's news, across composite + both "see all" reads), pagination on `up-next`/`news` (skip/take clamp, stable order on ties), and `me/teams`. Frontend — Jest unit for `HomeService`, the dashboard **variant selector** (team-member vs new-player) and per-module **state machine** (loading/empty/error), the shell's **active-destination** logic, and **"My team" routing** (0 → find-a-team, 1 → team space, many → chooser); Playwright `dashboard.spec.ts` (sign in → Home shows Up next → RSVP toggles to "going" and back with confirm → new-player variant shows find-a-team + open-to-everyone → nav reaches every destination on desktop top bar and mobile bottom tab bar → Alerts placeholder), desktop + mobile.

**Target Platform**: Linux containers via Docker (`docker-compose`). Product UI targets desktop + mobile (responsive web).

**Project Type**: Web application — existing sibling `backend/` (.NET) and `frontend/` (Nx/Angular) trees.

**Performance Goals**: No throughput targets. The composite `GET /api/v1/home` is the first-paint path: it issues a small, bounded set of projected `AsNoTracking` queries (capped top-N per module) so Home's greeting + Up next appear within ~1 s under normal conditions (SC-003). News aggregation reads a **bounded window** from the two source tables and merge-sorts in memory (community scale); a denormalized feed table is the documented scale path, out of scope here. "See all" reads are ordinary `Skip/Take` paginated queries.

**Constraints**: Security-first / never-trust-the-client — Home is **authenticated** (in-shell `authGuard`); every module is **entitlement-filtered server-side** (own sign-ups; member-team news/activity; public tournaments) and returns **public/permitted fields only** stripped at the `.Select` boundary. RSVP reuses the existing sign-up service unchanged (capacity/waitlist/approval enforced server-side); Home only surfaces the outcome. Generic `ProblemDetails`; no stack traces/secrets. Angular keeps separate `.html`/`.css`/`.ts`; scripts are `.ps1` only; Tailwind styled from DESIGN.md tokens; `EventType` serializes as names via the existing global `JsonStringEnumConverter`; Lucide line icons, sentence case, mono for scores/times, no emoji.

**Scale/Scope**: 1 composite read + 2 paginated "see all" reads + 1 `me/teams` read (4 new GET endpoints, all authed, 0 new writes). 0 new entities, 0 new columns, 1 optional index-only migration. Backend `Services/Home/` (1 service + projections + a small news-merge helper) + Home DTOs. Frontend: `layout/` rework (top bar + new bottom tab bar + avatar menu, sidebar removed), `features/dashboard/` rebuild (Home + ~7 module components), `features/alerts/` placeholder, `HomeService` + `MembershipService` + `home.models.ts`, routes + Alerts route. Sized so the later **Notifications**, **League news**, and **activity-feed** features slot into the reserved destinations/modules without reshaping the shell or Home.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | How this plan complies | Verdict |
|---|-----------|------------------------|---------|
| I | Security-First, Never Trust the Client | Home is authenticated; **every** module is entitlement-filtered server-side — Up next is scoped to the caller's own `EventSignup`s and the sign-ups of teams they are a `TeamMembership` of; Your teams / News are scoped to the caller's member teams (and events they're connected to); tournaments are public. The client cannot request another user's sign-ups or a non-member team's news (a dedicated test asserts this across the composite and both "see all" reads). DTOs carry public/permitted fields only, stripped at the `.Select` projection. RSVP delegates to the existing `IEventSignupService`, which authorizes and enforces capacity/waitlist server-side; Home adds no new write path. Generic `ProblemDetails`; no secrets/stack traces. OWASP: **A01 broken access control** — the entitlement scoping is the core authz; **A03 injection** — EF parameterization, no string SQL; **A04 insecure design** — bounded/capped lists, no cross-tenant leak. | ✅ |
| II | Thin Controllers, Service-Centric | New endpoints are thin `[HttpGet]` actions on a new `HomeController` (and one added action on `ProfilesController` for `me/teams`) that bind the caller id + `PaginationRequest` and delegate to `IHomeService` in `Services/Home/`. DI behind an interface; **no repository layer** (EF Core directly). The service returns `HomeDto` / `PagedResult<XDto>` via projection; Mapster config extended only for any non-trivial shaping (most is direct `.Select`). No business logic in controllers. | ✅ |
| III | Disciplined Data Access (EF Core + PostgreSQL) | **Every** read is `.Select`/`ProjectToType` + `AsNoTracking`; all lists are bounded — composite modules are capped top-N, "see all" reads use `Skip/Take` via `PaginationRequest`/`PagedResult<T>` with a **stable secondary sort key** (Id) so paging never drops/dupes on ties. No new entity/column; the optional `AddHomeIndexes` migration adds only read indexes (auto-applied on startup). News aggregation reads a bounded window and merges in memory (documented). No unbounded collection is ever returned. No writes → the `AuditFieldsInterceptor` is untouched. | ✅ |
| IV | Secure Authentication & Session Management | Reuses Identity + JWT-in-httpOnly-cookie unchanged. All four new reads require the existing bearer scheme (`[Authorize]`, caller id from the `sub` claim via the established `TryGetUserId` helper). No password/identity surface changes; the shell's session hydration reuses `AuthService.loadSession()`. | ✅ |
| V | Environment Parity & Containerized Deployments | Same `docker-compose` stack; **no new services or secrets**. The index-only migration applies in every environment (local/Dev/Prod parity). Per-service Dockerfiles unchanged. CI/CD + Terraform remain deferred (allowed by scope). | ✅ |
| VI | Consistent Conventions & Tooling | Angular components keep separate `.html`/`.css`/`.ts`; any scripts are `.ps1` only; Tailwind styled from DESIGN.md tokens; the Dashboard wireframe informs layout only (recreated, not copied); `EventType` serializes as names via the existing global `JsonStringEnumConverter`; the dashboard and its modules are lazy-loaded where it helps keep the initial bundle lean. | ✅ |
| — | Secret & Configuration Management | No new secrets. Optional `HomeOptions` (per-module caps, news-window size) are plain config with safe defaults; no Key Vault. | ✅ |

**Result**: PASS — no violations; Complexity Tracking left empty. Two items worth flagging, both **accepted trade-offs, not deviations**: (1) News is aggregated by an **in-memory merge over a bounded window** of the two existing news tables rather than a denormalized feed — the simplest correct choice at community scale, with a feed table noted as the future scale path (research §3). (2) The desktop **team snapshot omits a win/loss record** because match/result modeling is deferred (clarified 2026-07-07); the snapshot shows only real data (team name + next fixture).

## Project Structure

### Documentation (this feature)

```text
specs/008-home-dashboard-nav/
├── plan.md              # This file
├── spec.md              # Feature specification (clarified 2026-07-07)
├── research.md          # Phase 0 — composite-vs-per-module reads, up-next union, news aggregation, my-teams, shell pattern, indexes
├── data-model.md        # Phase 1 — no new entities; Home DTOs, projections, entitlement predicates, caps, indexes, seeding
├── quickstart.md        # Phase 1 — runnable end-to-end validation (Scenarios A–H)
├── contracts/
│   ├── openapi.yaml     #   GET /home, GET /home/up-next, GET /home/news, GET /profiles/me/teams
│   └── README.md
└── checklists/
    └── requirements.md  # Spec quality checklist (from specify + clarify)
```

### Source Code (repository root)

```text
backend/                                            # .NET 10 solution (namespace JuggerHub)
├── Controllers/
│   ├── HomeController.cs                            # NEW — [Authorize] GET "" (composite), GET "up-next", GET "news"
│   └── ProfilesController.cs                        # EXTEND — [HttpGet] "me/teams" (viewer memberships for the shell)
├── Services/
│   ├── Home/
│   │   ├── IHomeService.cs / HomeService.cs         # NEW — GetHomeAsync (composite), ListUpNextAsync, ListNewsAsync, ListMyTeamsAsync
│   │   ├── HomeProjections.cs                       # NEW — reusable Expression<> projections (up-next item, news item, tournament card, team snapshot)
│   │   └── HomeNewsMerge.cs                         # NEW — bounded-window merge of TeamNewsPost + EventNewsPost, tagged by source, newest-first
│   ├── Events/… / Teams/…                           # REUSE — IEventSignupService (RSVP), activity/news services, EventSearchService, guards
│   └── …                                            # EXISTING
├── Dtos/
│   └── Home/                                        # NEW — HomeDto, ViewerSummaryDto, UpNextItemDto, HomeNewsDto, TournamentCardDto, TeamSnapshotDto, MyTeamDto
├── Data/
│   ├── AppDbContext.cs                              # EXTEND — read indexes (only those not already present)
│   ├── Migrations/                                  # NEW (optional) migration: AddHomeIndexes (index-only)
│   └── DevDataSeeder.cs                             # EXTEND — Home demo data (team-member player + no-team player, upcoming events, news, tournament)
├── Common/
│   └── HomeOptions.cs                               # NEW (optional) — per-module caps, news-window size; safe defaults
├── Program.cs                                       # EXTEND — register IHomeService; bind HomeOptions
└── tests/JuggerHub.Api.IntegrationTests/
    └── Home/                                        # NEW — composite shape, up-next union, news aggregation, entitlement invariants, pagination, me/teams

frontend/apps/web/src/app/
├── layout/
│   ├── shell/               { *.ts/.html/.css }     # EXTEND — compose top bar + bottom tab bar; remove sidebar; hydrate session + memberships
│   ├── top-nav/             { *.ts/.html/.css }     # REWORK — desktop: brand + Home/Browse/My team + bell(+count) + avatar menu; mobile: slim strip (wordmark + avatar)
│   ├── bottom-nav/          { *.ts/.html/.css }     # NEW — mobile fixed bottom tab bar: Home · Browse · My team · Alerts (active state)
│   ├── avatar-menu/         { *.ts/.html/.css }     # NEW — Profile · Account · Sign out
│   ├── nav-model.ts                                 # NEW — shared destination list + active-match + "My team" target resolver (0/1/many)
│   └── sidebar/                                     # REMOVE — replaced by top bar + bottom tab bar
├── core/
│   ├── services/home.service.ts                     # NEW — getHome(), getUpNext(skip,take), getNews(skip,take); RSVP via EventService
│   ├── services/membership.service.ts               # NEW — viewer teams signal (GET /profiles/me/teams); drives "My team" routing + snapshot
│   └── models/home.models.ts                        # NEW — HomeDto, viewer summary, up-next item, news item, tournament card, team snapshot, my-team
├── features/dashboard/                              # REBUILD (replaces the health-check stub)
│   ├── dashboard.component.  { *.ts/.html/.css }     #   Home: loads GET /home; picks team-member vs new-player variant; hosts modules + right rail
│   └── modules/             { *.ts/.html/.css each } #   up-next-card, team-activity, news-list, tournament-card, team-snapshot, new-player-prompts, open-to-everyone
├── features/alerts/         { *.ts/.html/.css }     # NEW — placeholder "you're all caught up" screen (no backend)
└── app.routes.ts                                    # EXTEND — '' → DashboardComponent (in-shell, authGuard); add '/alerts' (authGuard); '/my-team' resolver → team space or find-a-team

frontend/apps/web-e2e/src/
└── dashboard.spec.ts                                # NEW — Home modules + RSVP toggle + new-player variant + nav (top bar/bottom tab) + Alerts, desktop+mobile

backend/appsettings*.json / docker-compose.yml        # EXTEND (optional) — Home section (module caps, news-window) with safe defaults
```

**Structure Decision**: Web application extending the existing `backend/` and `frontend/` trees. The backend groups the new read logic in a cohesive `Services/Home/` slice (one service + projection/merge helpers), mirroring how `Services/Search/` and `Services/Events/` are organized; the composite dashboard read gets its **own `HomeController`** (rather than hanging off an existing resource controller) because it is a cross-resource *view*, not a resource — while the small `me/teams` read attaches to the **existing `ProfilesController`** since it is the caller's own membership list. RSVP intentionally **reuses** the existing `EventsController` sign-up endpoints so Home introduces no second write path for the same operation. The frontend replaces the single `sidebar` with a **top bar + bottom tab bar + avatar menu** sharing one `nav-model.ts` (so desktop and mobile provably expose the same destinations, SC-002), and rebuilds `features/dashboard/` around small per-module components each owning their loading/empty/error states (SC-005). Home and its modules are lazy-loadable; the reserved `features/alerts/` and the News/activity modules are shaped so the deferred Notifications / League-news / activity-feed features drop in without reshaping the shell.

## Complexity Tracking

> No constitution violations. No entries required. This feature adds **no new entities, no new columns, and no new write paths** — it composes existing data behind read endpoints and rebuilds the shell/dashboard UI. The only accepted trade-offs are the in-memory news merge over a bounded window (vs. a denormalized feed table, deferred) and the record-less team snapshot (match/results modeling deferred), both documented above and in research.
