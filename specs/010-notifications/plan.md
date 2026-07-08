# Implementation Plan: In-App Notification System

**Branch**: `010-notifications` | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/010-notifications/spec.md`

## Summary

Add a reusable, per-recipient in-app notification engine backed by a single `Notification` entity
(UUIDv7, `BaseEntity`) with a `NotificationType` discriminator and a small JSON payload. A
`NotificationService` creates and reads notifications; three producers call it — targeted team
invite (with inline accept/decline), team role change, and team news post (news posting is added
here, admin-only, since only reading exists today). A REST controller serves the initial list,
unread count, and mark-read; a SignalR hub (`/hubs/notifications`) pushes new notifications and
unread-count deltas to the recipient's connected clients in real time, authenticated by the same
JWT-in-cookie scheme as the REST API. The Angular `/alerts` surface and the top-nav bell (both
placeholders from feature 008) are wired to the REST + SignalR client: the bell gains a live
unread badge, the inbox renders a paginated, typed list with an honest empty state, and invite
rows carry inline Accept/Decline that reuse the existing invitation endpoints.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Angular (Nx workspace) frontend.

**Primary Dependencies**: EF Core + Npgsql, ASP.NET Core SignalR (`Microsoft.AspNetCore.SignalR`,
in-framework — no NuGet add), Mapster, Microsoft Identity/JWT. Frontend adds
`@microsoft/signalr` (pinned major) for the realtime client.

**Storage**: PostgreSQL 18 — one new table `Notifications` (+ indexes). Payload stored as `jsonb`.

**Testing**: xUnit integration tests via `WebApplicationFactory` (existing
`JuggerHub.Api.IntegrationTests`); Angular unit specs (Jest/Karma per existing `*.spec.ts`).

**Target Platform**: Linux server (containerized) + Angular SPA (mobile-first, desktop parity).

**Project Type**: Web application (existing `backend/` + `frontend/apps/web`).

**Performance Goals**: New notification visible to an online recipient within a few seconds
(SC-001). List/unread reads use projections + `AsNoTracking`; unread count is a single indexed
`COUNT`.

**Constraints**: Never trust the client — every read/mutation is scoped server-side to the
authenticated recipient; SignalR connections are `[Authorize]` and users only ever receive their
own notifications (per-user group). No unbounded lists. No raw exceptions/secrets to client.
Environment parity: SignalR uses the same in-process host on local/Dev/Prod (single App Service
instance today → no backplane needed; documented in research).

**Scale/Scope**: Small rosters/personal inboxes (tens–hundreds of items). One entity, one service
(+interface), one hub, one controller, three producer call-sites, plus one new team-news POST.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | How this plan complies |
|-----------|------------------------|
| I. Security-first, never trust client | All notification reads/mutations resolve the recipient from the JWT subject, not the request body. SignalR hub is `[Authorize]`; each connection joins only its own `user:{id}` group, so a client can never subscribe to another user's stream. Errors flow through the existing exception middleware; no internals leak. |
| II. Thin controllers, service-centric | `NotificationsController` only shapes HTTP + maps DTOs (Mapster); all logic in `INotificationService`. Hub is a thin transport that delegates reads to the service. Producers depend on `INotificationService`, not on EF directly for notifications. |
| III. Disciplined data access | `Notification : BaseEntity` (UUIDv7); audit fields via interceptor. List + count use `.Select`/`AsNoTracking`; mark-read uses `ExecuteUpdateAsync` with explicit `ModifiedDate`. Mandatory pagination via shared `PaginationRequest`/`PagedResult`. Payload is `jsonb`. |
| IV. Auth & session | Reuses JWT-in-httpOnly-cookie. SignalR reads the token from the cookie (default) — no token in `localStorage`, no query-string token needed for same-origin cookie auth. |
| V. Env parity & containerization | No new infra service; SignalR is in-process. Same behavior local/Dev/Prod. Frontend dev proxy forwards `/hubs` (WebSocket upgrade) like `/api`. |
| VI. Conventions & tooling | Frontend keeps separate `.html`/`.css`/`.ts`. Any scripts are `.ps1`. Enum serialized by name (existing global `JsonStringEnumConverter`). UI follows DESIGN.md tokens. |

**No violations** → Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/010-notifications/
├── plan.md              # This file
├── research.md          # Decisions: SignalR hosting/auth, payload shape, fan-out, idempotency
├── data-model.md        # Notification entity, enum, indexes, payload schema
├── quickstart.md        # How to run & manually verify end-to-end
├── contracts/
│   └── notifications-api.md   # REST + SignalR contract
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── Entities/
│   ├── Notification.cs            # NEW — Notification : BaseEntity (+ NotificationType, payload)
│   └── NotificationEnums.cs       # NEW — NotificationType enum
├── Dtos/Notifications/
│   └── NotificationDtos.cs        # NEW — NotificationDto, UnreadCountDto, payload record(s)
├── Services/Notifications/
│   ├── INotificationService.cs    # NEW — Create*/List/Count/MarkRead(+All) + realtime push
│   └── NotificationService.cs     # NEW — EF-direct impl; pushes via IHubContext
├── Services/Notifications/Realtime/
│   ├── NotificationHub.cs         # NEW — [Authorize] hub; joins user:{id} group
│   └── INotificationRealtime.cs   # NEW — thin push abstraction over IHubContext (testable)
├── Controllers/
│   └── NotificationsController.cs # NEW — GET list, GET unread-count, POST read, POST read-all
├── Data/
│   ├── AppDbContext.cs            # EDIT — DbSet<Notification> + entity config (indexes, jsonb)
│   └── Migrations/               # NEW — AddNotifications migration
├── Services/Teams/
│   ├── TeamInvitationService.cs   # EDIT — create invite notification (inline actions)
│   ├── TeamService.cs             # EDIT — role-change notification in MutateMembershipAsync
│   ├── TeamNewsService.cs         # EDIT — NEW PostAsync (admin-only) + fan-out notifications
│   └── ITeamNewsService.cs        # EDIT — add PostAsync
├── Controllers/TeamsController.cs # EDIT — POST {slug}/news
├── Program.cs                     # EDIT — AddSignalR, MapHub, DI for notification service/realtime
└── tests/JuggerHub.Api.IntegrationTests/Notifications/  # NEW — auth-scoping, producers, mark-read

frontend/apps/web/src/app/
├── core/models/notification.models.ts     # NEW — NotificationDto, type unions, payloads
├── core/services/notification.service.ts  # NEW — REST client + SignalR connection + signals
├── features/alerts/                        # EDIT — real inbox (list, states, inline invite actions)
│   └── notification-row/                    # NEW — presentational row component per type
├── layout/top-nav/ (+ bottom-nav)          # EDIT — bind unread badge to notification service
└── features/teams/ (team space)            # EDIT — admin "post news" control
```

**Structure Decision**: Existing web-app layout. Backend gains a self-contained
`Services/Notifications` module (service + hub) and one entity; frontend gains one core service +
model and upgrades the existing `/alerts` feature and nav. Producers are edits at existing
call-sites, keeping the engine decoupled and reusable.

## Phased Delivery (maps to user stories)

- **Phase A — Engine + surface (US1, P1):** entity + migration, service (create/list/count/mark),
  controller, DI; frontend model + service + REST-backed inbox and unread badge. Independently
  shippable with a seeded notification.
- **Phase B — Realtime (US4, P2):** SignalR hub + push on create + client subscription. Layers on
  A; A stays correct without it.
- **Phase C — Invite producer + inline actions (US2, P1):** invite create → notification; inline
  Accept/Decline reusing invitation endpoints; notification resolves.
- **Phase D — Role-change + team-news producers (US3, P2):** role-change push; admin news POST +
  fan-out; team-space post-news control.

## Complexity Tracking

No constitution violations; section intentionally empty.
