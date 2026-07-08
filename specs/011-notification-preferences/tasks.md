---
description: "Task list for notification preferences (011-notification-preferences)"
---

# Tasks: Notification Preferences

**Input**: Design documents from `specs/011-notification-preferences/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/preferences-api.md. Builds
on feature 010 (notification engine + invite/role/news producers).

**Tests**: Included — the spec's SC-002/003/004/005 require verifying per-channel suppression,
scoping, always-on, and fail-safe; backend integration tests are the cheapest honest verification.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different files, no unfinished dependency)
- **[Story]**: US1 (matrix + enforcement), US2 (email paths), US3 (layouts)

---

## Phase 1: Foundational — model + resolution + API

- [ ] T001 Add `NotificationCategory` + `NotificationChannel` enums and a static `NotificationCategories.For(NotificationType)` map in `backend/Entities/NotificationEnums.cs`.
- [ ] T002 Add `NotificationPreference : BaseEntity` (UserId, Category, Channel, Enabled) in `backend/Entities/NotificationPreference.cs`.
- [ ] T003 Register `DbSet<NotificationPreference>` + config in `backend/Data/AppDbContext.cs`: unique `(UserId, Category, Channel)`, cascade FK to user.
- [ ] T004 Migration `AddNotificationPreferences` (`dotnet ef migrations add`) — verify it builds.
- [ ] T005 [P] DTOs in `backend/Dtos/Notifications/NotificationPreferenceDtos.cs` (matrix + category + channels + alwaysOn + `SetPreferenceRequest`).
- [ ] T006 `INotificationPreferenceService` + `NotificationPreferenceService` in `backend/Services/Notifications/`: `GetMatrixAsync`, `SetCellAsync` (upsert), `IsEnabledAsync` (default-on, fail-safe), `GetEnabledRecipientsAsync` (batched). Register in `Program.cs`.
- [ ] T007 [US1] `NotificationPreferencesController` — GET matrix, PUT `{category}/{channel}`; recipient from JWT subject; 400 on unknown category/channel.
- [ ] T008 [P] [US1] Integration tests `tests/.../Notifications/PreferenceTests.cs`: per-user scoping (no cross-user read/write; anon → 401), defaults (no row = on), upsert round-trips via GET.

**Checkpoint**: preferences can be read as a matrix and set per cell, per user.

---

## Phase 2: US1 — In-app enforcement

- [ ] T009 [US1] Inject `INotificationPreferenceService` into `NotificationService`; in `CreateAsync`, skip when `IsEnabled(recipient, categoryOf(type), InApp)` is false (no row, no unread bump).
- [ ] T010 [US1] In `NotificationService.CreateManyAsync`, filter recipients by In-app preference (batched) before persisting the fan-out.
- [ ] T011 [P] [US1] Integration test: with In-app off for a category, its producer event creates 0 notifications and leaves the unread count unchanged; other categories unaffected.

**Checkpoint**: the In-app toggle actually suppresses in-app notifications.

---

## Phase 3: US2 — Email paths + email enforcement

- [ ] T012 [US2] Add `team-role-changed.html` and `team-news.html` under `backend/EmailTemplates/` (extend base header/footer/styles), and `GenerateRoleChangedEmailAsync` / `GenerateTeamNewsEmailAsync` on `IEmailTemplateService`/`EmailTemplateService`.
- [ ] T013 [US2] Add `SendRoleChangedEmailAsync` + `SendTeamNewsEmailAsync` to `TeamEmailService` (build the team link, hand HTML to `IEmailSender`).
- [ ] T014 [US2] In `TeamService` (role change), after the change, send the role-change email when `IsEnabled(target, InvitesAndRoster, Email)`; best-effort, non-fatal.
- [ ] T015 [US2] In `TeamNewsService.PostAsync`, send the team-news email to members with `Email` on (batched filter); best-effort, non-fatal.
- [ ] T016 [US2] In `TeamInvitationService.CreateTargetedAsync`, gate the existing invite email by `IsEnabled(target, InvitesAndRoster, Email)`.
- [ ] T017 [P] [US2] Integration tests: role-change/news email sent iff Email on (captured by `TestEmailSender`); invite email suppressed when Email off; security email always sent regardless of prefs.

**Checkpoint**: the Email toggle governs real emails for every in-scope category; security mail exempt.

---

## Phase 4: US1/US3 — Settings UI

- [ ] T018 [P] [US3] Frontend model `core/models/notification-preferences.models.ts` (matrix, category, channels, alwaysOn, channel keys).
- [ ] T019 [US3] `notification-preferences.service.ts` (core/services): GET matrix into a signal, PUT one cell with optimistic update + reconcile.
- [ ] T020 [US3] Settings screen `features/settings/notifications/`: desktop category × channel matrix + mobile stacked cards, per-toggle auto-save, always-on group read-only, loading/empty/error + "couldn't save" states. Follow DESIGN.md.
- [ ] T021 [US3] Route `/settings/notifications` (authGuard) in `app.routes.ts`; entry points from the avatar menu and the account page.
- [ ] T022 [P] [US3] Frontend spec: toggling a cell issues the PUT and reflects state; always-on group renders without toggles.

**Checkpoint**: users can manage preferences on desktop and mobile with auto-save.

---

## Phase 5: Polish & verification

- [ ] T023 [P] Accessibility + responsive pass (switch/checkbox semantics, labels, focus, matrix on desktop / cards on mobile).
- [ ] T024 Run `dotnet test backend/JuggerHub.slnx` + `npx nx test web`; `dotnet build` + `npx nx build web`. Fix failures; confirm no bundle-budget regression.
- [ ] T025 Update the GitHub issue + note this extends feature 010; record any spec drift.

## Dependencies

- Phase 1 blocks all. US1 in-app enforcement (Phase 2) and US2 email (Phase 3) both depend on the
  preference service. UI (Phase 4) depends on the API (Phase 1). Phase 5 last.

## Parallel opportunities

- T005, T018 (DTOs/models) and the `[P]` test tasks run alongside their siblings.
