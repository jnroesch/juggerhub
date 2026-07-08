# Implementation Plan: Notification Preferences

**Branch**: `011-notification-preferences` | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/011-notification-preferences/spec.md`

## Summary

Add a per-user notification-preferences layer over feature 010. A sparse `NotificationPreference`
entity stores one row per changed (category, channel) cell; absence means the opt-out default (on).
An `INotificationPreferenceService` resolves effective settings and exposes a fail-safe
`IsEnabledAsync(userId, category, channel)`. The in-app channel is enforced centrally in
`NotificationService` (it maps a `NotificationType` → category and skips creation when In-app is
off); the email channel is enforced in the producers before each category email. Because two
in-scope categories had no email path, this feature adds **role-change** and **team-news** emails
(base-template transactional mail via the existing `EmailTemplateService` + `IEmailSender`), each
gated by the recipient's Email preference; the existing invite email becomes gated too. A
`NotificationPreferencesController` serves the effective matrix and per-cell upserts. The Angular
**Notification settings** screen renders a category × channel matrix on desktop and stacked cards on
mobile, auto-saving each toggle, with Security & sign-in shown as always-on.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Angular (Nx) frontend.

**Primary Dependencies**: EF Core + Npgsql, Mapster, existing `EmailTemplateService` + `IEmailSender`
(Mailpit/Resend), Microsoft Identity/JWT. No new dependencies.

**Storage**: PostgreSQL — one new table `NotificationPreferences` (sparse, unique per user×category×channel).

**Testing**: xUnit integration tests via `WebApplicationFactory` (with the local mail sink
`TestEmailSender`); Angular unit specs.

**Target Platform**: Linux server + Angular SPA (mobile-first, desktop parity).

**Project Type**: Web application (`backend/` + `frontend/apps/web`).

**Performance Goals**: Preference resolution is a single indexed read per recipient at delivery time
(or one batched read for news fan-out). Settings GET is one small per-user query.

**Constraints**: Never trust the client — all reads/writes scoped to the JWT subject. Fail-safe
enforcement: a preference-lookup error defaults to deliver and never throws into the producer or
rolls back its action. Security/sign-in email is exempt and always sent. No unbounded lists.

**Scale/Scope**: One entity + service (+interface), one controller, enforcement hooks in
`NotificationService` and three producers, two new email templates, one Angular settings screen +
service/model.

## Constitution Check

| Principle | Compliance |
|-----------|------------|
| I. Security-first, never trust client | Preferences resolved from the JWT subject; per-cell upsert authorizes the owner only. Enforcement fails safe (deliver on error), never leaks internals. |
| II. Thin controllers, service-centric | `NotificationPreferencesController` shapes HTTP + maps DTOs; logic in `INotificationPreferenceService`. Enforcement lives in services, not controllers. |
| III. Disciplined data access | `NotificationPreference : BaseEntity` (UUIDv7); audit via interceptor. Reads use `AsNoTracking` + projection; upsert is a tracked find-or-add or `ExecuteUpdate` with explicit `ModifiedDate`. Unique index per (user, category, channel). No unbounded lists. |
| IV. Auth & session | Reuses JWT-in-cookie; no token in `localStorage`. |
| V. Env parity & containerization | No new infra; same behavior local/Dev/Prod. Emails via existing Mailpit/Resend split. |
| VI. Conventions & tooling | Frontend separate `.html`/`.css`/`.ts`; enums serialized by name; new email templates are HTML with inline CSS extending the base header/footer; `.ps1` only. UI follows DESIGN.md. |

**No violations** → Complexity Tracking omitted.

## Project Structure

### Documentation (this feature)

```text
specs/011-notification-preferences/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── preferences-api.md
└── tasks.md
```

### Source Code

```text
backend/
├── Entities/
│   ├── NotificationPreference.cs        # NEW — sparse per-cell setting : BaseEntity
│   └── NotificationEnums.cs             # EDIT — add NotificationCategory, NotificationChannel (+ Type→Category map helper)
├── Dtos/Notifications/
│   └── NotificationPreferenceDtos.cs    # NEW — matrix DTO, cell upsert request
├── Services/Notifications/
│   ├── INotificationPreferenceService.cs# NEW — GetMatrix / SetCell / IsEnabledAsync / GetEnabledRecipientsAsync
│   ├── NotificationPreferenceService.cs # NEW — EF-direct impl, fail-safe resolution
│   ├── INotificationService.cs / *.cs   # EDIT — gate in-app create/fan-out by In-app preference
├── Services/Email/
│   └── TeamEmailService.cs              # EDIT — SendRoleChangedEmailAsync, SendTeamNewsEmailAsync
├── Services/EmailTemplateService/
│   ├── IEmailTemplateService.cs / *.cs  # EDIT — GenerateRoleChangedEmailAsync, GenerateTeamNewsEmailAsync
├── EmailTemplates/
│   ├── team-role-changed.html           # NEW — extends base header/footer
│   └── team-news.html                   # NEW
├── Services/Teams/
│   ├── TeamInvitationService.cs         # EDIT — gate invite email by Email pref
│   ├── TeamService.cs                   # EDIT — send role-change email when Email pref on
│   └── TeamNewsService.cs               # EDIT — send news email to members with Email pref on
├── Controllers/
│   └── NotificationPreferencesController.cs # NEW — GET matrix, PUT {category}/{channel}
├── Data/AppDbContext.cs                 # EDIT — DbSet + config (unique index)
├── Data/Migrations/                     # NEW — AddNotificationPreferences
└── tests/.../Notifications/PreferenceTests.cs # NEW — scoping, enforcement per channel, always-on, defaults

frontend/apps/web/src/app/
├── core/models/notification-preferences.models.ts   # NEW
├── core/services/notification-preferences.service.ts# NEW — GET matrix + PUT cell + signals
├── features/settings/notifications/                 # NEW — settings screen (matrix + stacked)
├── app.routes.ts                                    # EDIT — /settings/notifications (authGuard)
└── layout/avatar-menu + features/account            # EDIT — link to notification settings
```

**Structure Decision**: Existing web-app layout, extending the feature-010 notification module and
the existing email stack. The preference layer is additive: a new entity + service + controller, an
enforcement hook centralized in `NotificationService` (in-app) and per-producer (email), two new
templates, and one Angular settings screen.

## Phased Delivery (maps to user stories)

- **Phase A — Model + resolution + API (US1 core):** entity, migration, category/channel enums +
  Type→Category map, preference service (matrix, upsert, `IsEnabledAsync`, batched
  `GetEnabledRecipientsAsync`), controller, DI.
- **Phase B — In-app enforcement (US1):** gate `NotificationService.CreateAsync`/`CreateManyAsync`
  by the recipient's In-app preference.
- **Phase C — Email paths + enforcement (US2):** role-change + team-news email templates/methods;
  gate invite/role/news emails by the Email preference.
- **Phase D — Settings UI (US1/US3):** preferences service + model; matrix (desktop) / stacked
  (mobile) screen with auto-save and load/save-error states; route + entry point.

## Complexity Tracking

No constitution violations; section intentionally empty.
