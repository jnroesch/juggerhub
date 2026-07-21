# Implementation Plan: "My team" home for teamless players

**Branch**: `023-my-team-home` | **Date**: 2026-07-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/023-my-team-home/spec.md`

## Summary

Teamless players currently have the "My team" navigation destination silently reroute them to Browse teams, so the destination never lights up and feels broken. This feature routes players on **zero teams** to a dedicated `/my-team` home (empty state) that offers three ways onto a team: **find a team** (into the existing Browse teams), **act on pending invitations** addressed to them (accept/decline inline), and **create a team** (into the existing `/teams/new`).

The only genuinely new backend behavior is a **read**: list the caller's usable (pending + unexpired) **targeted** invitations. Accept and decline **reuse the existing token-based invitee endpoints** — the new list returns each invite's token, which is safe because the list is scoped server-side to the caller (the same token they already received by email). No new entity and **no database migration** are required: the data already exists on `TeamInvitation.TargetUserId`.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Angular 21 (zoneless) with Nx + Tailwind (frontend)

**Primary Dependencies**: EF Core (PostgreSQL 18), ASP.NET Core (JWT-in-cookie auth, API versioning); Angular signals, RouterLink

**Storage**: PostgreSQL via EF Core. **No schema change** — reuses existing `TeamInvitation` (`TargetUserId`, `Kind`, `Status`, `ExpiresDate`, `Token`) and `TeamMembership`.

**Testing**: xUnit integration tests (`backend/tests/JuggerHub.Api.IntegrationTests`); Angular component/unit specs (zoneless — no `fakeAsync`, per project convention)

**Target Platform**: Linux containers on AKS (Dev/Prod); docker-compose locally

**Project Type**: Web application (backend REST API + Angular SPA)

**Performance Goals**: Standard interactive web latency; the new list is a single paginated, projected, `AsNoTracking` query bounded by `PaginationRequest` (default 20, max 100).

**Constraints**: Server-side authorization is the only boundary (constitution I); friendly errors only (no raw exceptions/stack traces); `.html`/`.css`/`.ts` kept separate; PowerShell-only scripts.

**Scale/Scope**: A teamless player typically has 0–a handful of pending invites. One new read endpoint, one new DTO, one new service method; frontend adds an invitations service + rebuilds the `/my-team` empty state and flips one nav routing rule.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Security-first, never trust the client | ✅ PASS | New list is authed (`/me/*`), scoped to the JWT subject; a player can only see invites where `TargetUserId == self`. Accept/decline stay server-enforced. Returning the caller's own token is not new exposure (already emailed to them). Suspended/banned handling unchanged (server is the gate). |
| II. Thin controllers, service-centric | ✅ PASS | Controller only extracts the subject + shapes the response; logic in `ITeamInvitationService.ListMineAsync`. Matches the existing `me/teams` → `IHomeService` pattern. |
| III. Disciplined data access | ✅ PASS | New query is `AsNoTracking`, projects to a DTO via `.Select(...)`, paginated with `PaginationRequest`/`PagedResult`. No migration. |
| IV. Secure auth/session | ✅ PASS | Reuses JWT-in-cookie; no token/localStorage changes. |
| V. Environment parity / containerized | ✅ PASS | No infra change; behaves identically local/Dev/Prod. |
| VI. Conventions & tooling | ✅ PASS | Angular files stay split; no `.sh` added. Follows the codebase's project-to-DTO-in-service convention (source code outranks the constitution's Mapster note per CLAUDE.md source-of-truth order). |
| Quality Gate 7 (UI/DESIGN.md) | ⚠️ REQUIRED | UI-bearing change — instantiate `checklists/ui-review.md` from the template and verify against DESIGN.md before verification. |

**Result**: PASS (no violations; Complexity Tracking not required).

## Project Structure

### Documentation (this feature)

```text
specs/023-my-team-home/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── me-invitations.md # Phase 1 — the one new endpoint contract
└── checklists/
    ├── requirements.md   # (from /speckit-specify)
    └── ui-review.md      # (instantiated during implementation, Gate 7)
```

### Source Code (repository root)

```text
backend/
├── Controllers/
│   └── ProfilesController.cs          # ADD GET me/invitations (inject ITeamInvitationService)
├── Dtos/Teams/
│   └── (invitation DTOs)              # ADD MyInvitationDto (token + team context + dates)
├── Services/Teams/
│   ├── ITeamInvitationService.cs      # ADD ListMineAsync(userId, pagination)
│   └── TeamInvitationService.cs       # IMPLEMENT ListMineAsync (targeted + usable, projected)
└── tests/JuggerHub.Api.IntegrationTests/
    └── Teams/ (or Home/)              # ADD tests: scoping, usable-only, targeted-only, auth, paging

frontend/apps/web/src/app/
├── layout/
│   ├── nav-model.ts                   # CHANGE myTeamTarget: 0 teams -> '/my-team' (not '/browse/teams')
│   └── nav-model.spec.ts              # UPDATE the 0-team expectation
├── core/
│   ├── models/team.models.ts          # ADD MyInvitation model
│   └── services/
│       ├── membership.service.ts      # (unchanged API; myTeamTarget now yields /my-team for 0 teams)
│       └── invitation.service.ts      # ADD listMine(); reuse TeamService.acceptInvite/declineInvite
└── features/my-team/
    ├── my-team.component.ts           # LOAD invites when teamless; accept/decline; refresh + navigate
    ├── my-team.component.html         # REBUILD empty state: invites section + find + create
    └── my-team.component.spec.ts      # ADD/So state rendering + accept/decline flows
```

**Structure Decision**: Existing web-app layout (constitution V/VI). The `/my-team` route and component already exist (feature 008); this feature enriches the zero-team branch and flips one routing rule. The new endpoint lives on `ProfilesController` beside `me/teams` for a consistent `/profiles/me/*` surface, backed by the existing `TeamInvitationService`.

## Key Design Decisions

1. **Reuse token-based accept/decline.** The new list returns each invite's `token`; the frontend calls the existing `POST /api/v1/invitations/{token}/accept|decline`. No new mutation endpoints, and every edge case (NotUsable, AlreadyMember, idempotent consume, unique-violation races) is already handled in `AcceptAsync`/`DeclineAsync`.
2. **Targeted + usable only.** `ListMineAsync` filters `Kind == Targeted && Status == Pending && ExpiresDate > now && TargetUserId == userId`. Link invites (no target) and expired/revoked/consumed invites are never returned (FR-008, FR-009, Edge Cases).
3. **Endpoint placement.** `GET /api/v1/profiles/me/invitations` on `ProfilesController` (mirrors `me/teams`), paginated, `AsNoTracking`, projected to `MyInvitationDto`.
4. **Routing flip is the whole nav fix.** Changing `myTeamTarget([])` to `/my-team` makes both the top bar and bottom bar land teamless players on the home, and `isActiveDestination` already lights up "My team" for `/my-team` — so FR-001/FR-002 fall out of one line + a test update.
5. **Post-accept transition.** On a successful accept the component calls `membership.load()` (refresh) then navigates to `/t/{slug}` — the joined team space (FR-017, FR-018; clarified auto-navigate).
6. **No migration.** Confirmed `TargetUserId` already exists; this feature only reads it.

## Complexity Tracking

No constitution violations — no entries required.
