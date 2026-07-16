# Implementation Plan: Trainings

**Branch**: `018-trainings` | **Date**: 2026-07-15 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/018-trainings/spec.md`

## Summary

Add **team-scoped recurring trainings** as the fourth tab on the team page. A team admin creates a
**Training** — a recurring **series** (name, location, weekday, start/end time, interval, end date) or a
**one-off** (a single dated session) — which materialises into concrete **TrainingSession** rows up to
the end date. Members respond per session with a three-way **Going / Maybe / Can't** RSVP (no cap,
everyone welcome) and see who's coming grouped by answer. The admin edits **this-session-vs-the-series**
(single-session edits **detach**; whole-series edits apply in place for time/place fields and
**regenerate** the future set when the recurrence pattern or end date changes), and can **skip** a date
(quiet soft-remove) or **cancel** a session (stays visible marked-off, responders notified). A training —
series or single session — can be flipped **public**, letting any signed-in non-member view and RSVP as a
counted, removable **guest** who is never added to the team. A **"Your trainings"** dashboard agenda
surfaces the member's next sessions across all teams plus public sessions they joined.

**Technical approach**: Three new aggregates — `Training` (the parent; a one-off is a `Training` with
`IsRecurring=false` and one session), `TrainingSession` (a dated occurrence with nullable per-session
overrides + a `Detached` flag + `Scheduled/Cancelled/Skipped` status), and `TrainingResponse`
(session + user + Going/Maybe/Can't + `IsGuest`) — in one EF Core migration. A guest is simply a
`TrainingResponse` with `IsGuest=true`; removal is a row delete. Two new `NotificationType`s
(`TrainingScheduled`, `TrainingUpdated`) map to a new `NotificationCategory.Trainings`. Services behind
interfaces mirror the teams/parties slices: a `TrainingGuard` (resolves the caller's team role + a
session's effective visibility/ownership in one query, reusing the `TeamMembershipGuard` shape), a
`TrainingResult`/`TrainingOutcome` uniform result, a `RecurrenceExpander` pure helper (weekday +
interval + date-range → session dates, incl. monthly-by-weekday-of-month), and services
`TrainingSeriesService` (create/edit-series/regenerate/visibility), `TrainingSessionService`
(session detail, edit-single/detach, skip, cancel, session visibility), `TrainingResponseService`
(upsert RSVP, who's-coming, attendance, guest removal, dashboard agenda). A thin `TrainingsController`
(team-scoped list/create + public list) and `TrainingSessionsController` (session-scoped read/edit/
response/attendance/guest) delegate to them; `INotificationService` fan-out handles the team heads-up
and responder notices (in-app only). The Angular client gains a `trainings` feature area (Trainings tab
embedded in the team page, a stepped create wizard, a session page with inline RSVP + who's-coming, an
admin manage sheet, edit-series / edit-session / skip-cancel / visibility sheets, an attendance list
with removable guests) plus a **"Your trainings"** dashboard module and a public-session view reachable
by shareable link. The team page's existing placeholder "upcoming trainings" section (event sign-ups) is
replaced by the real Trainings tab.

## Technical Context

**Language/Version**: C# / .NET 10 (backend, `backend/`), TypeScript / Angular 21 (frontend Nx
workspace, `frontend/`). Zoneless change detection (no `fakeAsync` in specs — see
`catalogue-014-decisions`).

**Primary Dependencies**: EF Core 10 + Npgsql (PostgreSQL 18), Microsoft Identity, Mapster
(entity→DTO), Asp.Versioning (`api/v{version}` routes); Angular + Nx + Tailwind, Angular signals /
`resource`.

**Storage**: PostgreSQL. New tables `Trainings`, `TrainingSessions`, `TrainingResponses`; new
`NotificationType` and `NotificationCategory` enum members (no notification-table schema change —
values append). `TimeOnly`/`DateOnly` for time-of-day and session dates; `DayOfWeek` for the weekday;
times compared in UTC-anchored `DateTime` for cross-cutting ordering.

**Testing**: Backend xUnit integration tests (`backend/tests/JuggerHub.Api.IntegrationTests`,
`WebApplicationFactory` as used by the events/parties/teams suites) plus focused unit tests for the
`RecurrenceExpander`; Angular specs (zoneless). The recurrence math and the edit/detach/regenerate
reconciliation get dedicated tests.

**Target Platform**: Linux containers (backend + Postgres via docker-compose locally); responsive web
(phone + desktop) per DESIGN.md — the wireframes are phone-first.

**Project Type**: Web application (separate `backend/` API + `frontend/` SPA).

**Performance Goals**: Standard interactive web latency; every session list, attendance list, and the
dashboard agenda paginated/bounded; reads use projections + `AsNoTracking`; series generation is a
single `AddRange` + one `SaveChanges`; regeneration diffs by date to avoid needless writes.

**Constraints**: Never-trust-the-client — every authorization decision (team membership, admin role,
public-vs-team-only visibility, guest removal) enforced server-side via `TrainingGuard`; no raw
exceptions/secrets to the client; DTOs out via Mapster/projection; `.html`/`.css`/`.ts` kept separate;
PowerShell-only scripts; environment parity (local/Dev/Prod identical behavior).

**Scale/Scope**: Adds 3 entities, 1 enum file, 2 new `NotificationType`s + 1 `NotificationCategory`,
1 migration, 3 backend services + 1 guard + 1 recurrence helper + 2 thin controllers, and one Angular
feature area (~9 components) plus a dashboard module, a public-session route, and edits to the team page
and the notification alerts renderer. No new middleware, no new infrastructure, no new email templates
(training notifications are in-app only for this feature).

## Constitution Check

*GATE: must pass before Phase 0 and re-checked after Phase 1. Constitution v1.1.0.*

| Principle | Applies | Compliance |
|---|---|---|
| I. Security-first, never trust client | Yes | Every training action (create/edit-series/edit-session/skip/cancel/visibility/guest-remove) is admin-gated server-side via `TrainingGuard` resolving the caller's `TeamRole`. RSVP is gated on team membership **or** the session's effective public visibility (outsider ⇒ guest); who's-coming/attendance reads are member/admin-scoped. Team-only sessions are 404 to outsiders (mirrors teams/parties). Generic errors only; no secrets/stack traces to the client. |
| II. Thin controllers, service-centric | Yes | Two new thin controllers delegate to DI'd services behind interfaces; no repository layer; entities→DTOs via projection/Mapster. Recurrence + reconciliation logic lives in services/helpers, not controllers. |
| III. Disciplined data access (EF + PG) | Yes | New entities derive from `BaseEntity` (UUIDv7); audit fields via interceptor; reads use `.Select`/`AsNoTracking`; every list paginates via `PaginationRequest`/`PagedResult<T>`; session generation uses `AddRange`; skip/cancel and regenerate drops use `ExecuteUpdateAsync`/`ExecuteDeleteAsync` with explicit `ModifiedDate`; a unique index enforces one response per (session, user). |
| IV. Secure auth & sessions | Yes | Reuses existing Identity/JWT-in-httpOnly-cookie unchanged; no auth surface added; the "shareable link" is an ordinary session URL gated by public visibility + a signed-in user (no token minting). |
| V. Env parity & containers | Yes | No new services; in-app notifications reuse the 010 spine; one migration runs identically per env; no email templates added. |
| VI. Conventions & tooling | Yes | Frontend keeps `.html`/`.css`/`.ts` separate; any scripts are `.ps1`; DESIGN.md drives UI with a per-feature UI review checklist instantiated during UI work. |

**Gate result**: PASS. No deviations — Complexity Tracking left empty.

## Project Structure

### Documentation (this feature)

```text
specs/018-trainings/
├── plan.md              # This file
├── research.md          # Phase 0 — design decisions
├── data-model.md        # Phase 1 — entities, indexes, constraints, migration, effective-field rules
├── quickstart.md        # Phase 1 — end-to-end validation guide
├── contracts/
│   └── trainings-api.md  # Phase 1 — REST endpoint contracts
├── checklists/
│   ├── requirements.md  # (from /speckit-specify)
│   └── ui-review.md     # (instantiate from template during UI work)
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

```text
backend/
├── Entities/
│   ├── Training.cs                     # NEW (team + name/desc + location + recurrence template + visibility)
│   ├── TrainingSession.cs              # NEW (training + date + nullable overrides + Detached + status)
│   ├── TrainingResponse.cs             # NEW (session + user + Going/Maybe/Cant + IsGuest)
│   ├── TrainingEnums.cs                # NEW (TrainingInterval, TrainingVisibility, TrainingSessionStatus, TrainingRsvp)
│   └── NotificationEnums.cs            # EDIT (+ TrainingScheduled, TrainingUpdated → new Trainings category)
├── Services/
│   └── Trainings/
│       ├── TrainingGuard.cs / TrainingAccess (record)   # caller role + session effective visibility resolver
│       ├── TrainingResults.cs           # TrainingOutcome + TrainingResult<T>/TrainingResult
│       ├── RecurrenceExpander.cs         # pure: weekday+interval+range → session dates (monthly-by-weekday)
│       ├── ITrainingSeriesService.cs / TrainingSeriesService.cs   # create, edit-series (in-place + regenerate), series visibility
│       ├── ITrainingSessionService.cs / TrainingSessionService.cs # session detail, edit-single/detach, skip, cancel, session visibility
│       └── ITrainingResponseService.cs / TrainingResponseService.cs # upsert RSVP, who's-coming, attendance, guest removal, dashboard agenda
├── Controllers/
│   ├── TrainingsController.cs          # NEW (team-scoped: list sessions + active series, create, public list)
│   └── TrainingSessionsController.cs   # NEW (session-scoped: detail, response, edit, skip, cancel, visibility, attendance, guest remove) + me/trainings agenda
├── Dtos/Trainings/*.cs                 # NEW DTOs (create, session-row, session-detail, whos-coming, attendance, agenda, edit)
├── Data/AppDbContext.cs                # EDIT (3 DbSets + model config: indexes, unique response, enum conversions)
├── Data/Migrations/*                   # NEW migration: AddTrainings
├── Data/DevDataSeeder.cs               # EDIT (seed a weekly series + a one-off + responses + one public session with a guest)
└── tests/JuggerHub.Api.IntegrationTests/Trainings/*   # NEW (create/rsvp/edit-fork/skip-cancel/public-guest/agenda) + RecurrenceExpander unit tests

frontend/apps/web/src/app/
├── features/trainings/
│   ├── trainings-tab/                  # dated session list + active series (embedded in the team page)
│   ├── training-create/                # stepped one-decision-per-screen wizard (series/one-off)
│   ├── training-session/               # session page: inline RSVP + who's-coming + About
│   ├── session-manage/                 # admin manage sheet (edit this / edit series / attendance / make public / cancel)
│   ├── edit-series/                    # whole-series edit form
│   ├── edit-session/                   # single-session edit (detaches)
│   ├── skip-cancel/                    # skip a date / cancel a session
│   ├── visibility/                     # team-only ↔ public toggle (+ shareable link)
│   └── attendance/                     # admin attendance with removable guests
├── features/teams/team-detail/         # EDIT (replace placeholder "upcoming trainings" with the Trainings tab)
├── features/dashboard/modules/your-trainings-card.component.*  # NEW (dashboard agenda module)
├── features/alerts/*                   # EDIT (render TrainingScheduled / TrainingUpdated rows)
├── core/services/trainings.service.ts + core/models/trainings.models.ts  # NEW
└── app.routes.ts                       # EDIT (session route incl. public link, create route under the team path)
```

**Structure Decision**: Web-application layout. The feature is a faithful mirror of the teams (005) and
parties (016) slices — services behind interfaces, a single-query guard, a uniform result type, thin
controllers, projection/Mapster DTOs, in-app notification fan-out — with one genuinely new piece, the
pure `RecurrenceExpander` (weekday + interval + range → dates, monthly-by-weekday-of-month), isolated
and unit-tested so the create/edit/regenerate paths stay simple.

## Complexity Tracking

No constitution deviations — this section is intentionally empty. The only new abstraction without a
direct precedent is the `RecurrenceExpander`, and it is justified: isolating the calendar math into a
pure, unit-tested helper keeps the series create/edit/regenerate services thin and testable, which is
simpler than inlining date arithmetic across three service methods.
