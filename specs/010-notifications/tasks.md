---
description: "Task list for the in-app notification system (010-notifications)"
---

# Tasks: In-App Notification System

**Input**: Design documents from `specs/010-notifications/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/notifications-api.md

**Tests**: Included — the spec's success criteria call out authorization-scoping and producer
correctness that must be verified; backend integration tests are the cheapest honest verification.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: US1 (inbox+badge), US2 (invite+inline actions), US3 (role+news), US4 (realtime)

## Path Conventions

Web app: backend at `backend/`, frontend at `frontend/apps/web/src/app/`.

---

## Phase 1: Foundational — Notification engine (blocks everything)

- [ ] T001 Add `NotificationType` enum in `backend/Entities/NotificationEnums.cs` (`TeamInvite`, `TeamRoleChanged`, `TeamNews`).
- [ ] T002 Add `Notification : BaseEntity` in `backend/Entities/Notification.cs` (RecipientUserId, Type, Payload string, IsRead, ReadDate, ActorUserId, DedupeKey + nav props).
- [ ] T003 Register `DbSet<Notification>` and configure the entity in `backend/Data/AppDbContext.cs`: `Payload` as `jsonb`, FKs (Recipient cascade, Actor set-null), indexes `(RecipientUserId, CreatedDate desc)`, partial unread index, partial unique `(RecipientUserId, DedupeKey)`.
- [ ] T004 Create EF migration `AddNotifications` (`dotnet ef migrations add AddNotifications`) and confirm it builds.
- [ ] T005 [P] Add DTOs in `backend/Dtos/Notifications/NotificationDtos.cs` (`NotificationDto`, `UnreadCountDto`, typed payload records) + Mapster config if needed.
- [ ] T006 Add `INotificationRealtime` + no-op-safe contract in `backend/Services/Notifications/Realtime/INotificationRealtime.cs` (PushCreatedAsync, PushUnreadCountAsync).
- [ ] T007 Add `INotificationService` + `NotificationService` in `backend/Services/Notifications/` (CreateAsync, CreateManyAsync, ListAsync paginated, CountUnreadAsync, MarkReadAsync, MarkAllReadAsync). Reads use `AsNoTracking`+projection; mark-read via `ExecuteUpdateAsync` w/ explicit ModifiedDate; dedupe via unique-violation catch. Push after persist; never throw into producers.
- [ ] T008 Register services in `backend/Program.cs` DI (`INotificationService`, `INotificationRealtime`).

**Checkpoint**: engine compiles; a notification can be created + read in a unit/integration test.

---

## Phase 2: US1 — Inbox + unread badge (P1) 🎯 MVP

- [ ] T009 [US1] Add `NotificationsController` in `backend/Controllers/` — GET list (paginated), GET unread-count, POST `{id}/read`, POST `read-all`. Thin; recipient from JWT subject; 404 for non-owned ids.
- [ ] T010 [P] [US1] Backend integration tests `tests/JuggerHub.Api.IntegrationTests/Notifications/NotificationsApiTests.cs`: per-recipient scoping (other user's id → 404), unread count, mark-read + mark-all idempotency, anonymous → 401.
- [ ] T011 [P] [US1] Frontend model `frontend/apps/web/src/app/core/models/notification.models.ts` (NotificationDto, type union, payload interfaces, PagedResult reuse).
- [ ] T012 [US1] Frontend `notification.service.ts` (core/services): REST client (list/unread-count/mark/mark-all) + `unreadCount` and `items` signals; seed unread count on init.
- [ ] T013 [US1] Replace `/alerts` placeholder with a real inbox in `features/alerts/`: list, loading/empty/error states, relative time, per-type icon; "mark all read". Follow DESIGN.md tokens.
- [ ] T014 [P] [US1] Add a `notification-row` presentational component in `features/alerts/notification-row/` (icon + title + supporting line + timestamp + unread dot per type).
- [ ] T015 [US1] Bind the unread badge on the bell in `layout/top-nav/` and `layout/bottom-nav/` to `notificationService.unreadCount` (capped display, aria-label with count).
- [ ] T016 [P] [US1] Frontend spec for `notification.service` (unread signal updates on mark-read) and alerts component (empty vs list rendering).

**Checkpoint**: with a seeded notification, inbox + badge work over REST end-to-end.

---

## Phase 3: US2 — Invite producer + inline accept/decline (P1) 🎯 MVP

- [ ] T017 [US2] In `TeamInvitationService.CreateTargetedAsync`, after the invite persists + email, create a `TeamInvite` notification for the target (payload: invitationId, token, teamSlug, teamName, inviterName; actor = inviter; DedupeKey `invite:{id}`). Failure must not fail the invite.
- [ ] T018 [US2] Compute `resolved` for `TeamInvite` DTOs at read time by joining the invite's live status (usable → actionable; else resolved).
- [ ] T019 [US2] Frontend: inline Accept/Decline in `notification-row` for `TeamInvite`; call existing `POST /invitations/{token}/accept|decline` (reuse/add to a small invite client), then refresh the row + unread count. Hide actions when `resolved`.
- [ ] T020 [P] [US2] Integration test: issuing a targeted invite creates exactly one notification for the target (and not the actor); accepting via the invite endpoint flips `resolved`; no duplicate on repeat.

**Checkpoint**: invite → notification → inline accept/decline → resolved, end-to-end.

---

## Phase 4: US3 — Role-change + team-news producers (P2)

- [ ] T021 [US3] In `TeamService.MutateMembershipAsync` (role change path, not removal, not self step-down), create a `TeamRoleChanged` notification for the target (payload: teamSlug, teamName, newRole; actor = admin). Skip when target == actor.
- [ ] T022 [US3] Add `PostAsync(slug, actorUserId, body)` to `ITeamNewsService`/`TeamNewsService`: admin-gated via `TeamMembershipGuard`, validate body (1..1000), insert `TeamNewsPost`, then fan out `TeamNews` notifications to all other current members (`CreateManyAsync`).
- [ ] T023 [US3] Add `POST /api/v1/teams/{slug}/news` to `TeamsController` (thin; 201 TeamNewsDto, 400 invalid, 403 non-admin, 404 not-member/not-found).
- [ ] T024 [US3] Frontend: admin-only "Post news" control on the team space that calls the new endpoint; on success the author sees it in the feed (no self-notification).
- [ ] T025 [P] [US3] Integration tests: role change notifies target only; news POST as admin fans out to other members not the author; non-admin news POST → 403; self step-down does not self-notify.

**Checkpoint**: all three producers create correct, per-recipient notifications.

---

## Phase 5: US4 — Real-time delivery (P2)

- [ ] T026 [US4] Add `NotificationHub` (`[Authorize]`) in `backend/Services/Notifications/Realtime/NotificationHub.cs`; on connect join group `user:{sub}` derived from the token.
- [ ] T027 [US4] Implement `INotificationRealtime` over `IHubContext<NotificationHub>` (push `notificationCreated` + `unreadCountChanged` to the recipient's group); wire into `NotificationService`. `AddSignalR()` + `MapHub("/hubs/notifications")` in `Program.cs`.
- [ ] T028 [US4] Frontend: add `@microsoft/signalr` (pin major); in `notification.service.ts` open an authenticated connection to `/hubs/notifications`, prepend on `notificationCreated`, set badge on `unreadCountChanged`; auto-reconnect; re-fetch on (re)connect and on Alerts navigation.
- [ ] T029 [US4] Frontend dev proxy: ensure `/hubs` is proxied with WebSocket upgrade (`frontend` proxy config).
- [ ] T030 [P] [US4] Integration test: anonymous hub handshake rejected; (smoke) a created notification pushes to the recipient's connection only.

**Checkpoint**: online recipient sees new notifications + badge without refresh; REST path still correct if the hub is down.

---

## Phase 6: Polish & verification

- [ ] T031 [P] Accessibility + responsive pass on `/alerts` and the badge (focus rings, aria-live for badge, mobile/desktop layouts) per DESIGN.md.
- [ ] T032 Run `dotnet test backend/JuggerHub.slnx` and `npx nx test web`; run `npx nx lint web` + `dotnet build`. Fix failures.
- [ ] T033 Update backlog TASK-4 acceptance criteria to done; note spec drift (team-news posting added) and finalize.

## Dependencies

- Phase 1 blocks all others.
- US1 (Phase 2) is the MVP surface; US2 depends on US1 (row rendering) + engine.
- US3 depends on the engine (Phase 1) and, for display, US1's row.
- US4 (realtime) layers on the engine + US1; the system is correct without it.
- Phase 6 last.

## Parallel opportunities

- T005, T011 early (DTOs/model) are independent.
- Within a story, `[P]` test tasks run alongside implementation of different files.
