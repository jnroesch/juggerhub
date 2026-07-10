# Implementation Plan: Platform Admin Area

**Branch**: `013-admin-area` | **Date**: 2026-07-10 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/013-admin-area/spec.md`

## Summary

Replace feature 012's temporary per-request email-allowlist admin gate with a real
ASP.NET Identity role (`PlatformAdmin`) that is synchronized from the same
configuration at startup (mirror semantics: additions grant, removals revoke), while
keeping the existing `PlatformAdmin` authorization policy and all 012 controllers
untouched. On top of that boundary, build the full admin area per the wireframes:
account states (Active / Suspended / Banned soft-delete with implicit re-registration
block via the retained unique email), an admin overview (`/admin`), user
search/management (`/admin/users`), a per-player admin detail (`/admin/users/{handle}`)
with account actions (suspend/reinstate, ban/unban, send reset link — all recorded in
an append-only `AdminActionRecord`), and the badge/achievement Assign picker re-homed
onto the player detail using 012's existing grant/revoke endpoints.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Angular + Nx (frontend)

**Primary Dependencies**: ASP.NET Core Identity (`IdentityRole<Guid>` store already
wired), EF Core + Npgsql, Mapster, JWT-in-httpOnly-cookie auth (feature 002), SignalR
(untouched), Tailwind CSS

**Storage**: PostgreSQL 18 — new: `AccountStatus` on `AspNetUsers`,
`AdminActionRecords` table, first use of `AspNetRoles`/`AspNetUserRoles`; EF global
query filter on `PlayerProfile` for ban invisibility

**Testing**: xUnit integration tests via `JuggerHubApiFactory`
(`backend/tests/JuggerHub.Api.IntegrationTests`), Playwright e2e
(`frontend/apps/web-e2e`), `nx test web`

**Target Platform**: Docker → Azure App Services (linux), local docker-compose

**Project Type**: Web application (backend REST API + Angular SPA)

**Performance Goals**: Users search over hundreds of players < 2 s (SC-007); admin
endpoints are low-traffic, one extra DB roundtrip per admin request for the role check
is acceptable

**Constraints**: Fail-closed authorization (zero admins ⇒ nobody authorized); banned
accounts invisible on every player-facing surface by default (filter opt-out only in
admin services); suspended/banned sessions end within the access-token lifetime
(`Jwt.AccessTokenLifetimeMinutes`); no behavior change to 012 admin endpoints

**Scale/Scope**: ~6 new backend endpoints + 1 startup sync, 1 new entity + 2 user
columns + 1 migration, ~4 new Angular routes/components in a new admin shell, ~1
frontend service; existing 012 admin catalogue surface re-mounted under the shell

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Compliance |
|---|-----------|------------|
| I | Security-first, never trust the client | The `PlatformAdmin` policy stays the sole server-side boundary; the nav entry / route guard remain UX-only. Role check is per-request against the DB (no stale JWT role claims). Fail closed on zero admins. Ban/suspend enforced in login + refresh, not just UI. Generic errors via existing middleware; enumeration-neutral registration behavior preserved for banned emails. |
| II | Thin controllers, service-centric | New `AdminUsersController` / `AdminOverviewController` are thin; logic in `IAdminUserService` / `IAdminOverviewService` behind interfaces; DTOs mapped via Mapster. 012 admin controllers untouched (issue #21's promise). |
| III | Disciplined data access | `AdminActionRecord : BaseEntity` (UUIDv7, audit interceptor). Users list paginates via `PaginationRequest`/`PagedResult`. Reads use projections + `AsNoTracking`. `ExecuteUpdateAsync` paths (refresh-token revocation) already set `ModifiedDate`. Global query filter documented in data-model. |
| IV | Secure auth & sessions | Reuses Identity + argon2 + JWT-in-cookie unchanged. Suspend/ban revoke refresh tokens via existing `IRefreshTokenService.RevokeAllForUserAsync`; access dies at token expiry (documented window). Admin-triggered reset reuses `ForgotPasswordAsync` internals — admin never sees credentials. |
| V | Environment parity | Admin config continues to flow `.env` (`ADMIN_EMAILS` → `Admin__Emails`) locally and GitHub Environments deployed; sync runs identically everywhere. Migration auto-applies on startup as established. |
| VI | Conventions & tooling | Angular components keep separate `.html`/`.css`/`.ts`; no shell scripts; PowerShell only (none needed). |
| — | Quality gate 7 (UI review) | UI-bearing → `checklists/ui-review.md` instantiated from the template and run against the diff before verification. |

**Result**: PASS — no violations; Complexity Tracking not needed.

## Project Structure

### Documentation (this feature)

```text
specs/013-admin-area/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── admin-api.md     # Phase 1 output — admin REST contract
├── checklists/
│   ├── requirements.md  # spec quality (done)
│   └── ui-review.md     # instantiated during implementation (gate 7)
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── Common/
│   └── AdminOptions.cs                      # kept — now the sync source, not a live gate
├── Entities/
│   ├── User.cs                              # + AccountStatus, StatusChangedAt
│   ├── AccountEnums.cs                      # NEW — AccountStatus, AdminAccountAction
│   └── AdminActionRecord.cs                 # NEW — append-only action log
├── Data/
│   ├── AppDbContext.cs                      # + AdminActionRecords, query filter, indexes
│   ├── Migrations/                          # + AddAccountStateAndAdminActions
│   └── DevDataSeeder.cs                     # + dev admin/suspended/banned samples
├── Security/PlatformAdmin/
│   ├── PlatformAdminHandler.cs              # REWRITTEN — role membership, not allowlist
│   ├── PlatformAdminRequirement.cs          # unchanged
│   └── PlatformAdminRoleSync.cs             # NEW — startup mirror sync
├── Services/Admin/                          # NEW
│   ├── IAdminUserService.cs / AdminUserService.cs
│   ├── IAdminOverviewService.cs / AdminOverviewService.cs
│   └── AdminMappingRegister.cs
├── Services/Auth/AuthService.cs             # + status checks in Login/Refresh; banned-email register guard
├── Controllers/Admin/
│   ├── AdminUsersController.cs              # NEW — list/detail/actions
│   └── AdminOverviewController.cs           # NEW — stats + recent lists
├── Dtos/Admin/AdminUserDtos.cs              # NEW
└── tests/JuggerHub.Api.IntegrationTests/
    ├── Admin/                               # NEW — role sync, authz, account actions, overview, users
    └── Recognition/AdminAuthorizationTests.cs  # updated for role-based gate

frontend/apps/web/src/app/
├── core/
│   ├── guards/admin.guard.ts                # kept (probe-based)
│   ├── services/admin.service.ts            # NEW — overview/users/actions API
│   └── models/admin.models.ts               # NEW
├── layout/
│   ├── nav-model.ts / top-nav / avatar-menu # + lock-marked Admin entry (admins only)
├── features/admin/
│   ├── shell/admin-shell.component.*        # NEW — shield header, sidebar, back-to-app
│   ├── overview/admin-overview.component.*  # NEW — stats + lists + search
│   ├── users/admin-users.component.*        # NEW — search/filter/table→cards
│   ├── user-detail/admin-user-detail.component.*  # NEW — identity, actions, awards, Assign picker
│   └── recognition/admin-recognition.component.*  # kept — re-mounted at /admin/catalogue
└── app.routes.ts                            # /admin → shell with children

frontend/apps/web-e2e/src/admin-area.spec.ts # NEW
```

**Structure Decision**: Web application layout (existing `backend/` + `frontend/`
Nx workspace). The admin area becomes a lazy-loaded routed shell under
`features/admin/` with the existing 012 recognition surface preserved as a child
route; all new backend admin surface lives beside 012's controllers under
`Controllers/Admin` + a new `Services/Admin`.

## Complexity Tracking

Not applicable — no constitution violations.
