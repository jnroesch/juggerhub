# Phase 0 Research: Trainings

Design decisions resolving the Technical Context. Three product decisions were settled up front with
the owner (recorded in the spec's Assumptions / Clarifications); this document captures the engineering
decisions that follow.

## 1. New entities vs. extending Event

- **Decision**: Model trainings with new dedicated entities (`Training`, `TrainingSession`,
  `TrainingResponse`) rather than extending `Event`/`EventSignup`.
- **Rationale**: `Event`/`EventSignup` carry conflicting semantics — participation caps, waitlists,
  paid fees + out-of-band approval, `Joined/AwaitingApproval/Waitlisted` states, `ParticipantMode`
  teams-vs-individuals. Trainings have none of these: no fee, no cap, a three-way Going/Maybe/Can't
  answer, recurrence, per-session detach, and public guests. Overloading `Event` would fork almost
  every field with an `IsTraining` special case. Confirmed with the product owner.
- **Alternatives considered**: (a) Extend `Event` with recurrence + a `TrainingRsvp` on `EventSignup` —
  rejected: pollutes the events domain and its tests. (b) A shared "schedulable" base — rejected:
  premature abstraction over two aggregates with different lifecycles.

## 2. Everything is a `Training`; a one-off is a non-recurring `Training`

- **Decision**: A one-off is a `Training` with `IsRecurring=false`, `Weekday/Interval/EndDate=null`, and
  exactly one `TrainingSession`. Name/description/location always live on the `Training`; sessions carry
  only their date plus optional overrides.
- **Rationale**: Uniform reads (a session always has a parent for name/description), a single ownership
  path for authorization, and the Series/One-off badge is just `Training.IsRecurring`. Detach and
  per-session public toggle become the same override mechanism for one-offs and series alike.
- **Alternatives considered**: A series-less one-off session that carries its own name/description —
  rejected: duplicates display fields and doubles every read/authorization branch.

## 3. Recurrence expansion (weekday + interval + range → dates)

- **Decision**: A pure `RecurrenceExpander.Expand(startDate, weekday, interval, endDate)` returns the
  ordered list of session dates. `Weekly` = every 7 days on the weekday from the first on-or-after
  occurrence; `BiWeekly` = every 14 days; `Monthly` = the **same weekday-of-month position** as the
  first occurrence (e.g. "3rd Tuesday"), skipping months that lack that position (a 5th-weekday start
  only recurs in months that have a 5th such weekday). All dates are inclusive of `endDate`.
- **Rationale**: Isolating calendar math into a pure, exhaustively unit-tested function keeps the
  create/edit/regenerate services thin and makes the many edge cases (month-length, 5th-weekday, DST —
  which does not affect `DateOnly` math) independently testable. Monthly-by-weekday matches the
  owner-confirmed mental model of a fixed weekly slot going monthly.
- **Alternatives considered**: RFC-5545 RRULE parsing — rejected: far more than three intervals need;
  fixed 28-day "monthly" — rejected by the owner (drifts across calendar months).
- **Guards**: Expansion that yields zero dates (wrong weekday within a same-day range, end before
  start) is rejected at the service boundary with a validation outcome — never persisted as an empty
  training.

## 4. Effective session fields & the detach/override model

- **Decision**: `TrainingSession` stores nullable overrides (`StartTimeOverride`, `EndTimeOverride`,
  `LocationKindOverride`, `LocationOverride`, `VirtualLinkOverride`, `VisibilityOverride`) and a
  `Detached` bool. The **effective** value of a field is `Override ?? Training.<field>`. A single-session
  edit sets the relevant overrides and `Detached=true`; a per-session public toggle sets
  `VisibilityOverride` (which alone does not require full detach — see below).
- **Rationale**: One mechanism serves detach, single-session edits, and per-session visibility. Whole-
  series in-place edits update the `Training` template and every non-detached session inherits the new
  value automatically (no row writes needed for inherited fields). Reconciliation only has to skip
  `Detached` rows.
- **Visibility nuance**: Per-session visibility uses `VisibilityOverride` and is treated independently of
  `Detached` so an admin can open a single session without freezing its schedule against future series
  edits. Whole-series visibility sets `Training.Visibility`; sessions without a `VisibilityOverride`
  follow it.
- **Alternatives considered**: Copy-all-fields-onto-every-session at creation — rejected: whole-series
  in-place edits would have to write every row and couldn't cleanly express "inherited vs. overridden".

## 5. Whole-series edit reconciliation (in-place vs. regenerate)

- **Decision** (per spec Clarification 2026-07-15): Edits to time-of-day, location, description, and
  visibility apply **in place** (update `Training`; non-detached sessions inherit; no per-row writes for
  inherited fields). Edits to the recurrence **pattern** (weekday/interval) or the **end date**
  **regenerate** the future non-detached set: compute the new date list from today forward, keep
  sessions whose date is unchanged (preserving responses), `Add` sessions for new dates, and
  soft-`ExecuteDelete` future non-detached sessions whose date disappeared. Past and detached sessions
  are never touched.
- **Rationale**: Preserves responses wherever a date survives; matches the owner-confirmed option.
- **Alternatives**: Always regenerate (discards responses on unmoved dates) — rejected by the owner.

## 6. Skip vs. cancel

- **Decision**: `TrainingSessionStatus { Scheduled, Cancelled, Skipped }`. **Skip** sets `Skipped`
  (filtered out of every list/agenda, no responder notification) — a soft tombstone so regeneration
  won't resurrect the date. **Cancel** sets `Cancelled` (stays visible marked-off, blocks new/changed
  responses, notifies responders).
- **Rationale**: A tombstoned skip is safer than a hard delete under later pattern-regeneration and is
  auditable. Cancel's visibility + notification is the meaningful difference the wireframe calls out.
- **Alternatives**: Hard-delete on skip — rejected: a regenerate on the same pattern could recreate the
  skipped date.

## 7. Guests = `TrainingResponse.IsGuest`, no separate entity

- **Decision**: An outsider RSVP on a public session is a `TrainingResponse` with `IsGuest=true`. The
  guest tag, headcount inclusion, and removability all derive from it; removal is a row delete. Guests
  are never written to `TeamMembership`. `IsGuest` is computed server-side at RSVP time from "is the
  caller a member of the owning team?" — never client-supplied.
- **Rationale**: Minimal surface, matches the wireframe ("members & guests share one list; only the tag
  & ✕ differ"), and keeps the never-trust-the-client boundary trivial.
- **Reverting to team-only**: When effective visibility becomes team-only, guest responses are excluded
  from reads and the outsider loses access; the rows may be pruned lazily (excluded by the visibility
  filter regardless), so no cascade is required.

## 8. Authorization: `TrainingGuard`

- **Decision**: A single-query `TrainingGuard` mirrors `TeamMembershipGuard`/`PartyGuard`. For a
  team-scoped call it resolves the caller's `TeamRole?`; for a session-scoped call it resolves the
  session's `TeamId`, effective visibility, status, date (past?), the caller's `TeamRole?`, and whether
  the caller is a team member — enough to decide member-read, outsider-read (public only), admin-write,
  and guest-vs-member RSVP without a second round trip. Team-only sessions resolve as not-found to
  outsiders (mirrors teams/parties).
- **Rationale**: Uniform, server-side, one query — consistent with the established guard pattern.

## 9. Notifications (in-app only)

- **Decision**: Add `NotificationType.TrainingScheduled` (team heads-up on create; fan-out to team
  members via `CreateManyAsync`, link-only) and `NotificationType.TrainingUpdated` (series edit +
  session cancel; fan-out to that session's/series' responders, link-only). Both map to a new
  `NotificationCategory.Trainings`. **Skip does not notify.** No email templates are added this feature.
- **Rationale**: Reuses the 010 spine and 011 preference matrix (a new category appends without
  migrating existing sparse preference rows). In-app-only keeps scope lean and matches the wireframe's
  "heads-up"/"responders are notified" framing without introducing new transactional emails.
- **Alternatives**: New training emails — deferred; reminders/scheduled notifications are explicitly out
  of scope (spec Assumptions).

## 10. Time & date storage

- **Decision**: Store time-of-day as `TimeOnly` (`StartTime`/`EndTime`) and session dates as `DateOnly`
  (`SessionDate`); store the series `StartDate`/`EndDate` as `DateOnly` and the weekday as `DayOfWeek`.
  Cross-cutting ordering (Trainings tab, dashboard agenda) composes a UTC `DateTime` from
  `SessionDate + EffectiveStartTime` for sorting and "upcoming" filtering.
- **Rationale**: `DateOnly`/`TimeOnly` make recurrence math DST-immune and express the domain exactly;
  a composed UTC instant is only needed for ordering/upcoming windows, consistent with the rest of the
  app storing UTC.

## 11. Surfacing & the replaced placeholder

- **Decision**: The Trainings tab lives on the internal team page (`team-detail`), replacing the current
  placeholder "upcoming trainings" section that lists a team's event sign-ups (`TeamService` +
  `TrainingDto` on `TeamDtos`). Public sessions are reachable by a shareable session URL and listed for
  outsiders via a public-list endpoint the public team page can surface. The dashboard gains a
  "Your trainings" module using a `GET /me/trainings` agenda across all the member's teams + public
  sessions they joined.
- **Rationale**: Matches the wireframe (fourth tab + dashboard agenda + shareable link/public page) and
  removes the now-obsolete event-sign-up placeholder.
- **Migration note**: The existing `TrainingDto`/`upcomingTrainings` on the team payload is repurposed
  or removed in favor of real trainings; the team-detail change is coordinated so no dead field ships.
