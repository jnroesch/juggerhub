# Tasks: Trainings

**Feature**: 018-trainings | **Branch**: `018-trainings`
**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/trainings-api.md](./contracts/trainings-api.md),
[quickstart.md](./quickstart.md)

Tests are included (the spec's Independent Tests + Success Criteria demand server-side authorization,
recurrence-math correctness, edit/detach/regenerate reconciliation, and public-vs-team-only scoping
coverage). `[P]` = parallelizable (different files, no incomplete-task dependency). Paths are
repo-relative. Mirrors the teams (005) and parties (016) slices throughout.

---

## Phase 1: Setup

- [x] T001 Confirm branch `018-trainings` is checked out and the latest migration is applied locally (`dotnet ef database update` from `backend/`); bring the stack up (`docker compose up -d`).
- [x] T002 [P] Instantiate the UI review checklist: copy `.specify/templates/ui-review-checklist-template.md` to `specs/018-trainings/checklists/ui-review.md` (filled during the frontend phases).

---

## Phase 2: Foundational (blocking prerequisites — MUST complete before any user story)

### Data layer

- [x] T003 [P] Add `backend/Entities/TrainingEnums.cs` — `TrainingInterval { Weekly, BiWeekly, Monthly }`, `TrainingVisibility { TeamOnly, Public }`, `TrainingSessionStatus { Scheduled, Cancelled, Skipped }`, `TrainingRsvp { Going, Maybe, Cant }` (XML docs; serialized by name; `LocationKind` reused from `EventEnums.cs`).
- [x] T004 [P] Add `backend/Entities/Training.cs` (`BaseEntity`: `TeamId`, `Name`, `Description?`, `LocationKind`, `Location?`, `VirtualLink?`, `IsRecurring`, `Weekday?`, `Interval?`, `StartTime`, `EndTime`, `StartDate`, `EndDate?`, `Visibility`, `CreatedByUserId`; nav `Team`, `CreatedBy`, `ICollection<TrainingSession> Sessions`) per data-model.md.
- [x] T005 [P] Add `backend/Entities/TrainingSession.cs` (`BaseEntity`: `TrainingId`, `TeamId`, `SessionDate`, `StartTimeOverride?`, `EndTimeOverride?`, `LocationKindOverride?`, `LocationOverride?`, `VirtualLinkOverride?`, `VisibilityOverride?`, `Detached`, `Status`, `CancelledDate?`; nav `Training`, `ICollection<TrainingResponse> Responses`) per data-model.md.
- [x] T006 [P] Add `backend/Entities/TrainingResponse.cs` (`BaseEntity`: `TrainingSessionId`, `UserId`, `Answer`, `IsGuest`; nav `Session`, `User`) per data-model.md.
- [x] T007 Edit `backend/Entities/NotificationEnums.cs` — append `NotificationType.TrainingScheduled = 6` and `NotificationType.TrainingUpdated = 7`; append `NotificationCategory.Trainings = 2`; map both new types to `NotificationCategory.Trainings` in `NotificationCategories.For`.
- [x] T008 Edit `backend/Data/AppDbContext.cs` — add `DbSet<Training>`/`DbSet<TrainingSession>`/`DbSet<TrainingResponse>`; configure: `Training(TeamId)` index + FKs (`Team` restrict, `CreatedBy` restrict) + string max-lengths (`Name` 120, `Description` 2000, `Location` 300, `VirtualLink` 500); `TrainingSession` FK `Training` cascade + indexes `(TeamId, SessionDate)` and `(TrainingId)`; `TrainingResponse` FK `Session` cascade, `User` restrict, index `(TrainingSessionId)` + **unique `(TrainingSessionId, UserId)`**; enum-as-int for the new enums (confirm existing convention).
- [x] T009 Create the EF migration `AddTrainings`: `dotnet ef migrations add AddTrainings` (from `backend/`); verify Up/Down match data-model.md (three tables, the unique response index, DateOnly/TimeOnly column types, FKs). No data backfill.

### Backend shared building blocks

- [x] T010 [P] Add `backend/Services/Trainings/TrainingResults.cs` — `TrainingOutcome { Ok, NotFound, Forbidden, NotTeamAdmin, Invalid, Conflict }` and `TrainingResult`/`TrainingResult<T>` (mirror `PartyResults.cs`).
- [x] T011 [P] Add `backend/Services/Trainings/RecurrenceExpander.cs` — pure `IReadOnlyList<DateOnly> Expand(DateOnly startDate, DayOfWeek weekday, TrainingInterval interval, DateOnly endDate)`: first on/after occurrence of `weekday`, then Weekly=+7d, BiWeekly=+14d, Monthly=same weekday-of-month position (nth weekday; skip months lacking it), inclusive of `endDate`; returns empty on no occurrence.
- [x] T012 Add `backend/Services/Trainings/TrainingGuard.cs` — `TrainingAccess` record (`TeamId`, caller `TeamRole?`, `IsTeamMember`) via slug, and a session-scoped resolve returning `(SessionId, TrainingId, TeamId, EffectiveVisibility, Status, StartsAtUtc/IsPast, TeamRole?, IsTeamMember)` in one query; team-only + non-member ⇒ null (404). Mirror `TeamMembershipGuard`/`PartyGuard`.
- [x] T013 [P] Add `backend/Dtos/Trainings/TrainingDtos.cs` with all records from contracts (`TrainingSessionRowDto`, `TrainingSeriesSummaryDto`, `CreateTrainingDto`, `CreatedTrainingDto`, `TrainingSessionDetailDto`, `WhosComingDto`/`WhosComingGroupDto`/`WhosComingPersonDto`, `AttendanceEntryDto`, `AgendaSessionDto`, `EditSeriesRequest`, `EditSessionRequest`, `SetResponseRequest`, `SetVisibilityRequest`, `SeriesEditResultDto`).
- [x] T014 [P] Add service interfaces in `backend/Services/Trainings/`: `ITrainingSeriesService`, `ITrainingSessionService`, `ITrainingResponseService` (signatures return `TrainingResult`/`TrainingResult<T>` and `PagedResult<T>` per contracts).
- [x] T015 Register `TrainingGuard` + the three services in DI (`backend/Program.cs`, alongside the teams/parties registrations).

### Frontend foundation

- [x] T016 [P] Add `frontend/apps/web/src/app/core/models/trainings.models.ts` — TS interfaces/enums mirroring the DTOs + enums (Rsvp, Interval, Visibility, SessionStatus, LocationKind).
- [x] T017 [P] Add `frontend/apps/web/src/app/core/services/trainings.service.ts` — typed client for every contract endpoint (team-scoped list/create/public, session detail/response/edit/skip/cancel/visibility/attendance/guest, `me/trainings`), `withCredentials`.
- [x] T018 Edit `frontend/apps/web/src/app/app.routes.ts` — add the create route under the team path and the session route `/trainings/sessions/:id` (the public-shareable entry; guard allows any signed-in user).

### RecurrenceExpander unit tests (foundational correctness — SC-002)

- [x] T019 [P] Add `backend/tests/JuggerHub.Api.IntegrationTests/Trainings/RecurrenceExpanderTests.cs` — weekly/bi-weekly counts over known ranges, monthly-by-weekday (incl. 5th-weekday months that skip), first-occurrence alignment when `startDate` is not the weekday, single-day range, and zero-occurrence guard.

**Checkpoint**: entities, migration, guard, result type, expander (green), DTOs, interfaces, DI, and the
frontend client/routes exist. User stories can now proceed.

---

## Phase 3: User Story 1 — Member responds to sessions (Priority: P1) 🎯 MVP

**Goal**: A member sees the Trainings-tab dated list and can open a session, set/change Going/Maybe/Can't,
and see who's coming grouped by answer.

**Independent test**: Seed a team with one upcoming session + a member; the member lists it, opens it,
sets Going→Can't (single current response), and the who's-coming breakdown reflects it.

### Backend

- [x] T020 [US1] Implement `TrainingResponseService` (`backend/Services/Trainings/TrainingResponseService.cs`): `SetResponseAsync` (upsert one row per (session,user); block if session Cancelled/Skipped/past or not accessible; compute `IsGuest` = not a team member) and `GetWhosComingAsync` (grouped Going/Maybe/Cant with counts + top-N people incl. position/handle/avatar fields, guest flag, isYou), authorized via `TrainingGuard`.
- [x] T021 [US1] Implement `TrainingSessionService.GetDetailAsync` (`backend/Services/Trainings/TrainingSessionService.cs`): project `TrainingSessionDetailDto` with effective fields, series label, `viewerIsAdmin/viewerIsGuest/myAnswer`, and who's-coming top-N; team-only ⇒ 404 for outsiders.
- [x] T022 [US1] Implement the Trainings-tab session list on `TrainingSeriesService` (or a list method): `ListSessionsAsync(slug, window, pagination)` → `PagedResult<TrainingSessionRowDto>` with badge/counts/`myAnswer`, member-gated, ordered by `StartsAtUtc`, excludes Skipped.
- [x] T023 [US1] Add `backend/Controllers/TrainingSessionsController.cs` with `GET /api/v1/trainings/sessions/{id}` and `PUT /api/v1/trainings/sessions/{id}/response`; add `backend/Controllers/TrainingsController.cs` with `GET /api/v1/teams/{slug}/trainings/sessions`. Thin; map `TrainingOutcome`→HTTP; caller id from JWT subject.

### Backend tests

- [x] T024 [P] [US1] `backend/tests/JuggerHub.Api.IntegrationTests/Trainings/ResponseTests.cs` — set Going then Can't yields one current response (no duplicate) + updated counts; Maybe grouping; non-member on a team-only session gets 404 on read + RSVP; RSVP on a past/cancelled session → 409.
- [x] T025 [P] [US1] `backend/tests/JuggerHub.Api.IntegrationTests/Trainings/SessionListTests.cs` — tab list shows badge (series vs one-off), going count, and viewer's own answer; excludes Skipped; paginates; non-member → 404.

### Frontend

- [x] T026 [P] [US1] Build `frontend/apps/web/src/app/features/trainings/training-session/` (`.ts/.html/.css`) — session page: three-way Going/Maybe/Can't control (inline save), who's-coming grouped with avatars/positions, About block, past/cancelled read-only states, per DESIGN.md.
- [x] T027 [US1] Build `frontend/apps/web/src/app/features/trainings/trainings-tab/` and embed it in `frontend/apps/web/src/app/features/teams/team-detail/` replacing the placeholder "upcoming trainings" section — dated list (Next up / Later), row badge + going count + inline RSVP, member empty state ("nothing scheduled").
- [ ] T028 [P] [US1] Component specs (zoneless) for training-session (RSVP switch + who's-coming render) and trainings-tab (row states + empty state).

**Checkpoint**: US1 independently testable — members can respond and see who's coming.

---

## Phase 4: User Story 2 — Admin creates a training (Priority: P1) 🎯 MVP

**Goal**: An admin creates a series (day/time/interval/end-date → generated sessions) or a one-off via
the stepped wizard; the team gets a heads-up.

**Independent test**: Complete the wizard for a weekly series with an end date and confirm the exact
session count appears; repeat for a one-off (single session).

### Backend

- [x] T029 [US2] Implement `TrainingSeriesService.CreateAsync` (`backend/Services/Trainings/TrainingSeriesService.cs`): admin-gated via `TrainingGuard`; validate (name, `EndTime>StartTime`, location-by-kind, series-fields-present, expansion ≥ 1); create `Training` + `AddRange` generated `TrainingSession`s from `RecurrenceExpander` (one-off ⇒ single session, no weekday/interval/end-date); return `CreatedTrainingDto { trainingId, sessionCount, firstSessionId }`.
- [x] T030 [US2] Fan out `NotificationType.TrainingScheduled` to team members via `INotificationService.CreateManyAsync` (dedupe `training-scheduled:{trainingId}`, link payload) on create — resilient (never fails the create).
- [x] T031 [US2] Implement `TrainingSeriesService.ListSeriesAsync(slug, pagination)` → `PagedResult<TrainingSeriesSummaryDto>` (admin-only active-series overview: weekday/interval/times/end-date, upcoming count, next date).
- [x] T032 [US2] Add to `TrainingsController`: `POST /api/v1/teams/{slug}/trainings` (create, admin) and `GET /api/v1/teams/{slug}/trainings/series` (admin overview). Admin surface on the tab list (`viewerIsAdmin`).

### Backend tests

- [x] T033 [P] [US2] `backend/tests/JuggerHub.Api.IntegrationTests/Trainings/CreateTests.cs` — weekly series generates the exact session count over a known range (SC-002); one-off ⇒ single session; non-admin member → 403; invalid schedules (end<start, end-time≤start-time, zero-session) → 400; `TrainingScheduled` fan-out reaches members.

### Frontend

- [x] T034 [US2] Build `frontend/apps/web/src/app/features/trainings/training-create/` (`.ts/.html/.css`) — stepped one-decision-per-screen wizard: (1) series-or-one-off + name, (2) day/time/interval/end-date (collapses to single date for one-off; live "~N sessions"), (3) location + optional description (In person/Virtual), (4) team-only or public, (5) review → create. Round-knob progress; back/cancel.
- [x] T035 [US2] Add the admin affordances to `trainings-tab`: "+ New training" button, the active-series panel, and the admin empty state ("Set up a training").
- [ ] T036 [P] [US2] Component spec (zoneless) for the wizard — branch collapse for one-off, validation gating on the schedule step, review summary count.

**Checkpoint**: US1+US2 = MVP — create trainings and respond to them.

---

## Phase 5: User Story 3 — Edit this-vs-series, skip, cancel (Priority: P2)

**Goal**: Admin edits a single session (detaches) or the whole series (in-place for time/place;
regenerate on pattern/end-date), skips a date (quiet), or cancels a session (visible, notifies).

**Independent test**: From a series, whole-series time change shifts upcoming non-detached only; a single
edit detaches; extend end date generates; change weekday regenerates; skip hides; cancel marks-off.

### Backend

- [x] T037 [US3] Implement `TrainingSessionService.EditSingleAsync` — set overrides (date/time/location) + `Detached=true`; block past/cancelled; 404 for non-admin.
- [x] T038 [US3] Implement `TrainingSeriesService.EditSeriesAsync` — in-place update of `Training` template for time/location/description/visibility (upcoming non-detached inherit, no per-row writes); on weekday/interval/end-date change, regenerate per data-model §Reconciliation (keep unchanged dates + responses, `Add` new, `ExecuteDelete` disappeared future non-detached; past/detached untouched); reject zero-future-session results; return `SeriesEditResultDto { added, removed, kept }`.
- [x] T039 [US3] Implement `TrainingSessionService.SkipAsync` (status→`Skipped`, no notification) and `CancelAsync` (status→`Cancelled` + `CancelledDate`, block further responses); both admin-gated; block on past.
- [x] T040 [US3] Notify responders: `EditSeriesAsync` and `CancelAsync` fan out `NotificationType.TrainingUpdated` (kind `seriesEdit`/`cancelled`) to affected sessions' responders via `INotificationService`; skip does not notify.
- [x] T041 [US3] Add endpoints: `PATCH /api/v1/trainings/{trainingId}` (edit series — on `TrainingsController`), `PATCH /api/v1/trainings/sessions/{id}`, `POST …/{id}/skip`, `POST …/{id}/cancel` (on `TrainingSessionsController`). Map outcomes; `Invalid`→400 on zero-future-session.

### Backend tests

- [x] T042 [P] [US3] `backend/tests/JuggerHub.Api.IntegrationTests/Trainings/EditForkTests.cs` — whole-series time edit updates upcoming non-detached, leaves past unchanged, notifies responders; single edit detaches + subsequent series edit skips it; extend end date generates new sessions preserving existing responses; change weekday regenerates (responses on surviving dates preserved); non-admin → 403.
- [x] T043 [P] [US3] `backend/tests/JuggerHub.Api.IntegrationTests/Trainings/SkipCancelTests.cs` — skip hides the session (no notification, absent from list); cancel keeps it visible marked-off, blocks new responses (409), notifies responders.

### Frontend

- [x] T044 [US3] Built the this-vs-series edit fork: `training-edit/` (fork chooser → single-session form that detaches, or whole-series form) wired from the session manage menu; skip/cancel/visibility/attendance already inline on the session page.
- [x] T045 [US3] Wire the scope fork ("This session only" vs "The whole series") ahead of the edit form; surface "responders are notified" copy; detached badge on the session page.
- [ ] T046 [P] [US3] Component specs (zoneless) for the edit fork routing and skip-vs-cancel confirmations.

**Checkpoint**: full admin lifecycle over a season.

---

## Phase 6: User Story 4 — Public trainings & guests (Priority: P2)

**Goal**: Admin flips a series/session public; signed-in non-members view + RSVP as counted, removable
guests via a shareable link; team-only stays hidden from outsiders.

**Independent test**: Make a session public, RSVP as an outsider (guest tag, counted, removable, not on
team); confirm a team-only session is 404 to that outsider.

### Backend

- [x] T047 [US4] Implement visibility setters: `TrainingSeriesService.SetSeriesVisibilityAsync` (`Training.Visibility`) and `TrainingSessionService.SetSessionVisibilityAsync` (`VisibilityOverride`); admin-gated.
- [x] T048 [US4] Extend read/RSVP paths for outsiders: `TrainingGuard` session resolve + `GetDetailAsync`/`SetResponseAsync`/who's-coming allow a signed-in non-member when **effective visibility is Public** (records `IsGuest=true`); team-only remains 404. Guests count in headcount.
- [x] T049 [US4] Implement `TrainingResponseService.GetAttendanceAsync` (admin full list incl. guests, paginated, guest/admin/isYou flags) and `RemoveGuestAsync` (delete a guest response; 400 if target is a team member; never touches `TeamMembership`).
- [x] T050 [US4] Implement `GET /api/v1/teams/{slug}/trainings/public` (outsider-facing public upcoming list). Add endpoints: `PUT …/{trainingId}/visibility`, `PUT …/sessions/{id}/visibility`, `GET …/sessions/{id}/attendance`, `DELETE …/sessions/{id}/guests/{userId}`.

### Backend tests

- [x] T051 [P] [US4] `backend/tests/JuggerHub.Api.IntegrationTests/Trainings/PublicGuestTests.cs` — outsider can view + RSVP a public session as guest (counted); guest appears in attendance with tag and is removable without joining the team; team-only session is 404 to the outsider; reverting to team-only removes outsider access; per-session public override works independently of series visibility.

### Frontend

- [x] T052 [US4] Build `visibility/` (team-only ↔ public toggle with whole-series/just-this-session scope + shareable link copy) and `attendance/` (admin list with guest tags + remove ✕) under `features/trainings/`; render the public/guest states on the session page (guest banner, "coming along?" for outsiders).
- [ ] T053 [P] [US4] Component specs (zoneless) for the visibility scope toggle and the attendance guest-remove flow.

**Checkpoint**: open-mat sharing works end-to-end.

---

## Phase 7: User Story 5 — "Your trainings" dashboard agenda (Priority: P3)

**Goal**: The dashboard surfaces the member's next sessions across all teams + public sessions joined as
guest, with inline RSVP.

**Independent test**: A member on two teams (plus one public guest session) sees all merged
chronologically on the dashboard and can RSVP inline.

### Backend

- [x] T054 [US5] Implement `TrainingResponseService.GetMyAgendaAsync(userId, pagination)` → `PagedResult<AgendaSessionDto>`: upcoming non-skipped sessions across all the user's teams + public sessions where they have a guest response, ordered by `StartsAtUtc`, bounded.
- [x] T055 [US5] Add `GET /api/v1/me/trainings` (on `TrainingSessionsController`).

### Backend tests

- [x] T056 [P] [US5] `backend/tests/JuggerHub.Api.IntegrationTests/Trainings/AgendaTests.cs` — cross-team merge in chronological order; includes public guest sessions; excludes team-only sessions of teams the user isn't on; paginated/bounded.

### Frontend

- [x] T057 [US5] Build `frontend/apps/web/src/app/features/dashboard/modules/your-trainings-card.component.*` and wire it into the dashboard — agenda rows with team name, date/time, and inline Going/Maybe/Can't; empty state.
- [ ] T058 [P] [US5] Component spec (zoneless) for the dashboard module (inline RSVP + empty state).

**Checkpoint**: full feature surface complete.

---

## Phase 8: Polish & Cross-Cutting

- [x] T059 Render the new notification types in the alerts UI: `frontend/apps/web/src/app/features/alerts/*` — `TrainingScheduled` (team heads-up, link) and `TrainingUpdated` (series edit / cancelled, link).
- [x] T060 [P] Seed data: edit `backend/Data/DevDataSeeder.cs` — a weekly series (mixed responses across a few generated sessions), a one-off, and one public session carrying a guest response, for the demo team (idempotent, guarded like existing seeds).
- [x] T061 [P] Removed the obsolete placeholder: dropped `TrainingDto`/`UpcomingTrainings` from `TeamDtos.cs` + `TeamService.cs` and the `Training`/`upcomingTrainings` frontend model, replaced by the real Trainings tab link.
- [x] T062 [P] Run the UI review checklist `specs/018-trainings/checklists/ui-review.md` against the diff (DESIGN.md tokens/components: tab, wizard steps, session card, sheets, attendance rows, guest tag, empty/loading/error states, responsive phone+desktop, sentence case, one coral CTA/view).
- [x] T063 Walk `quickstart.md` Scenarios A–E end-to-end locally and fix any gaps.
- [x] T064 Verify: `dotnet build` + `dotnet test` (backend), `npx nx build web` + `npx nx lint web` + `npx nx test web` (frontend); record results for the report.
- [x] T065 [P] Update memory + open a PR referencing the spec; note any spec/design drift.

---

## Dependencies & Execution Order

- **Setup (P1)** → **Foundational (P2, T003–T019)** block everything. Within Foundational: T003–T007 before T008; T008 before T009; T010–T014 [P] after enums/entities; T015 after interfaces; frontend T016–T018 [P]; T019 after T011.
- **User stories** in priority order: **US1 (T020–T028, P1)** and **US2 (T029–T036, P1)** form the MVP loop (US1 independently testable against a seeded session; US2 adds creation). **US3 (T037–T046, P2)** and **US4 (T047–T053, P2)** extend admin lifecycle + public sharing. **US5 (T054–T058, P3)** adds the dashboard agenda.
- Cross-story service reuse: US3/US4 extend `TrainingSessionService`/`TrainingSeriesService` created in US1/US2; US4's guest path extends US1's `SetResponseAsync`/who's-coming; US5 reuses `TrainingResponseService`.
- **Polish (T059–T065)** last; T061 (remove placeholder) only after T027 replaces the team-page section.

## Parallel Opportunities

- Foundational: T003/T004/T005/T006 (entities), T010/T011/T013/T014 (result/expander/DTOs/interfaces), T016/T017 (client models/service), T019 (expander tests) run in parallel.
- Within each story, `[P]` test tasks and `[P]` frontend component builds run alongside the story's backend once its service/endpoint exists.

## MVP Scope

**US1 + US2 (P1)**: members respond to sessions and admins create series/one-offs — the complete core
loop. US3 adds the edit/skip/cancel lifecycle, US4 adds public/guests, US5 adds the dashboard agenda.

## Format validation

All tasks use `- [ ] Txxx [P?] [USn?] description + path`. Setup/Foundational/Polish carry no story
label; every user-story task carries its `[USn]` label and a concrete file path.
