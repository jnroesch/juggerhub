# Implementation Plan: Admin catalogue management (badges, achievements & team awards)

**Branch**: `014-admin-catalogue` | **Date**: 2026-07-10 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/014-admin-catalogue/spec.md`

## Summary

Build out the placeholder `/admin/catalogue` into the full management surface for
the two fixed catalogues (badges = recognition, achievements = milestones) and
restore admin award assignment for teams. On the existing feature-012 admin API
(create/edit/retire/icon/grant/revoke, all behind the `PlatformAdmin` policy) this
feature adds three small, **additive, migration-free** backend capabilities —
**reinstate** (un-retire), a **grant count + created date** on the catalogue list,
and **remove-icon** — plus a new **admin teams area** (`/admin/teams` search list +
`/admin/teams/{slug}` detail) that mirrors the existing admin users area and reuses
012's already-present team-awards read and `teamSlug` grant/revoke. The frontend
rewrites the catalogue component (list → create/edit modal → icon modal →
retire/reinstate confirm) and extracts the existing player Assign picker into a
shared component reused by the new team detail. Every authorization and validation
decision stays server-side; the UI follows DESIGN.md and passes the gate-7 review.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Angular + Nx (frontend)

**Primary Dependencies**: ASP.NET Core (controllers + `PlatformAdmin` policy), EF
Core + Npgsql, Mapster, JWT-in-httpOnly-cookie auth; Angular standalone components
with signals, Tailwind CSS mapped onto DESIGN.md tokens

**Storage**: PostgreSQL 18 — **no schema change**. `BadgeDefinition` /
`AchievementDefinition` already derive from `BaseEntity` (so `CreatedDate` exists);
grant count is a computed COUNT over existing `BadgeAward` / `AchievementAward`
(`Status == Active`); reinstate flips the existing `IsRetired`; remove-icon deletes
an existing `BadgeIcon` / `AchievementIcon` row.

**Testing**: xUnit integration via `JuggerHubApiFactory`
(`backend/tests/JuggerHub.Api.IntegrationTests`), `nx test web`, Playwright e2e
(`frontend/apps/web-e2e`).

**Target Platform**: Docker → Azure App Services (linux), local docker-compose.

**Project Type**: Web application (backend REST API + Angular SPA).

**Performance Goals**: Admin endpoints are low-traffic; catalogues are small (tens
of types) and paginate. Grant-count COUNTs are cheap and indexed by definition id.

**Constraints**: Fail-closed `PlatformAdmin` authorization server-side (client
nav/guard are UX only); retire never deletes and never alters already-granted
awards; icon uploads validated by magic-byte sniff + size cap server-side; no
behavior change to 012's public award display or existing grant/revoke.

**Scale/Scope**: +3 admin controller actions on each recognition controller
(reinstate, remove-icon) + list-DTO fields; 1 new admin controller + service +
DTOs (teams); ~1 frontend service extension + 1 model change; catalogue component
rewrite; 2 new admin components (teams list, team detail); 1 extracted shared
Assign-picker component; +2 admin routes and 1 shell nav entry.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Compliance |
|---|-----------|------------|
| I | Security-first, never trust the client | `PlatformAdmin` policy remains the sole boundary on every new/changed endpoint (reinstate, remove-icon, admin teams). Icon bytes validated server-side by magic-byte sniff + size cap (`ImageValidation`, `RecognitionOptions.MaxIconBytes`); client type ignored. Generic errors via existing middleware; no secrets client-side. Nav entry + route guard stay UX-only. |
| II | Thin controllers, service-centric | New `AdminTeamsController` is thin (outcome→HTTP mapping only); logic in `IAdminTeamService`. Reinstate/remove-icon/counts extend existing `IBadgeService`/`IAchievementService`. DTOs via Mapster/projection; controllers never see entities beyond mapping. |
| III | Disciplined data access | Reads use `.Select(...)` projections + `AsNoTracking`; grant count is a projected correlated COUNT (no N+1). Teams list paginates via `PaginationRequest`/`PagedResult`. Reinstate mutates one tracked entity; audit interceptor sets `ModifiedDate`. No `ExecuteUpdate` paths added. New entities: none. |
| IV | Secure auth & sessions | No auth surface change; reuses the established JWT-cookie + `PlatformAdmin` policy. No credentials handled. |
| V | Environment parity | No new config or infra; `RecognitionOptions` already flows identically local/Dev/Prod. No migration to coordinate. |
| VI | Conventions & tooling | Angular components keep separate `.html`/`.css`/`.ts`; no shell scripts (PowerShell only, none needed). |
| — | Quality gate 7 (UI review) | UI-bearing → instantiate `checklists/ui-review.md` from the template and verify against the diff before verification; DESIGN.md wins on conflict. |

**Result**: PASS — no violations; Complexity Tracking not needed.

## Project Structure

### Documentation (this feature)

```text
specs/014-admin-catalogue/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── admin-catalogue-api.md   # Phase 1 output — REST contract (deltas + new)
├── checklists/
│   ├── requirements.md  # spec quality (done)
│   └── ui-review.md     # instantiated during implementation (gate 7)
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── Dtos/
│   ├── Badges/BadgeDtos.cs                # + GrantedCount, CreatedAt on BadgeDefinitionDto
│   ├── Achievements/AchievementDtos.cs    # + GrantedCount, CreatedAt on AchievementDefinitionDto
│   └── Admin/AdminTeamDtos.cs             # NEW — AdminTeamListItemDto, AdminTeamDetailDto
├── Services/
│   ├── Badges/{IBadgeService,BadgeService}.cs          # + Reinstate…, RemoveIcon…, count/date projection
│   ├── Achievements/{IAchievementService,AchievementService}.cs  # same additions
│   └── Admin/{IAdminTeamService,AdminTeamService}.cs   # NEW — team search + detail
├── Controllers/Admin/
│   ├── BadgesAdminController.cs           # + POST {id}/reinstate, DELETE {id}/icon
│   ├── AchievementsAdminController.cs     # + POST {id}/reinstate, DELETE {id}/icon
│   └── AdminTeamsController.cs            # NEW — GET /admin/teams, GET /admin/teams/{slug}
└── tests/JuggerHub.Api.IntegrationTests/
    ├── Recognition/                       # + reinstate, remove-icon, list count/date coverage
    └── Admin/AdminTeamsTests.cs           # NEW — teams search/detail authz + shape

frontend/apps/web/src/app/
├── core/
│   ├── models/recognition.models.ts       # + grantedCount, createdAt on RecognitionDefinition
│   ├── models/admin.models.ts             # + AdminTeamListItem, AdminTeamDetail
│   ├── services/recognition-admin.service.ts  # + create/update/retire/reinstate/setIcon/removeIcon
│   └── services/admin.service.ts          # + searchTeams, getTeamDetail
├── features/admin/
│   ├── catalogue/admin-catalogue.component.*  # REWRITE — list + create/edit + icon + retire/reinstate
│   ├── shared/assign-picker.component.*    # NEW — extracted reusable Assign picker (player|team)
│   ├── user-detail/admin-user-detail.component.*  # use the extracted picker (behavior-preserving)
│   ├── teams/admin-teams.component.*       # NEW — teams search list (table→cards)
│   ├── team-detail/admin-team-detail.component.*  # NEW — team identity + awards + Assign picker
│   └── shell/admin-shell.component.*       # + Teams nav entry (sidebar + bottom bar)
└── app.routes.ts                           # + /admin/teams and /admin/teams/:slug children

frontend/apps/web-e2e/src/admin-area.spec.ts  # + catalogue CRUD/icon/retire/reinstate + team grant/revoke
```

**Structure Decision**: Web application layout (existing `backend/` + `frontend/`
Nx workspace). Catalogue work stays in `features/admin/catalogue`; the new admin
teams area mirrors `features/admin/users` + `user-detail`; the Assign picker is
extracted to `features/admin/shared` so player and team details share one grant
surface. Backend additions sit beside 012's recognition controllers/services and
013's `Services/Admin`.

## Complexity Tracking

Not applicable — no constitution violations.
