# Phase 1 Data Model: Trainings

All entities derive from `BaseEntity` (`Id` UUIDv7, `CreatedDate`, `ModifiedDate` via the audit
interceptor). New tables: `Trainings`, `TrainingSessions`, `TrainingResponses`. Enum members append to
`NotificationType`/`NotificationCategory` (no notification-table change). One migration: `AddTrainings`.

## Enums (`Entities/TrainingEnums.cs`)

```csharp
public enum TrainingInterval { Weekly = 0, BiWeekly = 1, Monthly = 2 }      // series only
public enum TrainingVisibility { TeamOnly = 0, Public = 1 }
public enum TrainingSessionStatus { Scheduled = 0, Cancelled = 1, Skipped = 2 }
public enum TrainingRsvp { Going = 0, Maybe = 1, Cant = 2 }
```

`LocationKind` (InPerson/Virtual) is reused from `Entities/EventEnums.cs`. All enums serialize by name
(global `JsonStringEnumConverter`) and are stored as `int` (existing convention; confirm against
`AppDbContext` enum config).

## Entity: `Training` (parent / template)

The recurring template, or a one-off container. One per create-wizard completion. Owned by one team.

| Field | Type | Notes |
|---|---|---|
| `TeamId` | `Guid` | FK → `Teams`. Indexed. |
| `Name` | `string` | Required, trimmed, max ~120. |
| `Description` | `string?` | Optional, max ~2000. |
| `LocationKind` | `LocationKind` | InPerson or Virtual. |
| `Location` | `string?` | In-person free-text location (e.g. "Sportpark Müngersdorf, Köln"); required when InPerson. |
| `VirtualLink` | `string?` | Required when Virtual. |
| `IsRecurring` | `bool` | true = series (Series badge); false = one-off (One-off badge). |
| `Weekday` | `DayOfWeek?` | Series only; null for one-off. |
| `Interval` | `TrainingInterval?` | Series only; null for one-off. |
| `StartTime` | `TimeOnly` | Default start time-of-day for sessions. |
| `EndTime` | `TimeOnly` | Default end time-of-day; MUST be after `StartTime`. |
| `StartDate` | `DateOnly` | First occurrence date (one-off: the single date). |
| `EndDate` | `DateOnly?` | Series only, inclusive; null for one-off. |
| `Visibility` | `TrainingVisibility` | Series-level default (TeamOnly by default). |
| `CreatedByUserId` | `Guid` | FK → `Users` (the admin who created it). |

Navigation: `Team`, `CreatedBy`, `ICollection<TrainingSession> Sessions`.

**Validation** (service boundary): `Name` non-empty; `EndTime > StartTime`; InPerson ⇒ `Location`
present, Virtual ⇒ `VirtualLink` present; if `IsRecurring`: `Weekday`, `Interval`, `EndDate` present and
`EndDate >= StartDate` and expansion yields ≥ 1 date; if not recurring: `Weekday`/`Interval`/`EndDate`
null. No `Status` field — deleting a whole training is out of scope (skip/cancel operate per session).

## Entity: `TrainingSession` (dated occurrence)

One row per generated date. Members/guests respond to these.

| Field | Type | Notes |
|---|---|---|
| `TrainingId` | `Guid` | FK → `Trainings`. Indexed. Required (one-offs included). |
| `TeamId` | `Guid` | Denormalized from `Training.TeamId` for cross-team agenda queries + scoping. Indexed. |
| `SessionDate` | `DateOnly` | The occurrence date. |
| `StartTimeOverride` | `TimeOnly?` | null ⇒ inherit `Training.StartTime`. |
| `EndTimeOverride` | `TimeOnly?` | null ⇒ inherit `Training.EndTime`. |
| `LocationKindOverride` | `LocationKind?` | null ⇒ inherit. |
| `LocationOverride` | `string?` | Per-session in-person location ("just this week"). |
| `VirtualLinkOverride` | `string?` | Per-session virtual link. |
| `VisibilityOverride` | `TrainingVisibility?` | null ⇒ inherit `Training.Visibility`. Per-session public toggle. |
| `Detached` | `bool` | true ⇒ excluded from whole-series in-place edits & pattern regeneration. |
| `Status` | `TrainingSessionStatus` | Scheduled / Cancelled / Skipped. |
| `CancelledDate` | `DateTime?` | Set (UTC) when cancelled. |

Navigation: `Training`, `ICollection<TrainingResponse> Responses`.

**Effective fields** (computed in projections/services, never stored):
`EffectiveStartTime = StartTimeOverride ?? Training.StartTime`; likewise EndTime, LocationKind,
Location, VirtualLink, Visibility. `StartsAtUtc = SessionDate.ToDateTime(EffectiveStartTime)` as UTC for
ordering/upcoming filters. `IsPast = StartsAtUtc < now`. `IsOneOff = !Training.IsRecurring`.

**Index**: `(TeamId, SessionDate)` for the tab list; `(TrainingId, SessionDate)` unique-ish for
reconciliation lookups (not enforced unique — a detached session could coincide, but generation keeps
one non-detached row per date).

## Entity: `TrainingResponse` (RSVP)

One person's current answer to one session. Guests are `IsGuest=true`.

| Field | Type | Notes |
|---|---|---|
| `TrainingSessionId` | `Guid` | FK → `TrainingSessions`. Indexed. |
| `UserId` | `Guid` | FK → `Users`. The responder (always a signed-in user). |
| `Answer` | `TrainingRsvp` | Going / Maybe / Cant. |
| `IsGuest` | `bool` | true ⇒ responder is NOT a member of the owning team (outsider on a public session). Computed server-side. |

Navigation: `Session`, `User`.

**Constraint**: **unique `(TrainingSessionId, UserId)`** — one current response per person per session
(upsert on change). No history retained (out of scope).

## Notification enum additions (`Entities/NotificationEnums.cs`)

```csharp
// NotificationType (append)
TrainingScheduled = 6,   // team heads-up on create (fan-out to team members, link-only)
TrainingUpdated  = 7,    // series edit or session cancel (fan-out to responders, link-only)

// NotificationCategory (append)
Trainings = 2,           // maps the two types above; appears in the 011 preference matrix

// NotificationCategories.For(...) — add both new types → NotificationCategory.Trainings
```

Payloads (camelCase `jsonb`): `TrainingScheduled` → `{ teamSlug, trainingId, trainingName, sessionId?, isRecurring }`;
`TrainingUpdated` → `{ teamSlug, sessionId, trainingName, kind: "seriesEdit"|"cancelled", sessionDate }`.
Dedupe keys: `training-scheduled:{trainingId}` and `training-updated:{sessionId}:{kind}`.

## State & lifecycle

- **Training**: created Active; no terminal state (no whole-series delete in scope). Editing mutates the
  template (in place) or triggers session regeneration (pattern/end-date change).
- **TrainingSession**: `Scheduled` → `Cancelled` (terminal; visible, no responses) or `Scheduled` →
  `Skipped` (terminal; hidden). `Detached` flips once on a single-session edit and stays true. Past
  sessions are read-only regardless of status.
- **TrainingResponse**: upserted while the session is `Scheduled`, in the future, and accessible to the
  caller (member, or outsider on an effectively-public session). Guest rows deleted on guest removal or
  ignored once the session reverts to team-only.

## Reconciliation rules (whole-series edit)

1. **In-place fields** (start/end time, location, description, visibility): update `Training`; no session
   rows written for inherited fields. Notify responders of all upcoming non-detached sessions.
2. **Pattern/end-date change**: recompute dates from `max(today, StartDate)` via `RecurrenceExpander`.
   - Date present before & after → keep session + responses.
   - New date → `Add` a fresh `Scheduled`, non-detached session.
   - Future non-detached `Scheduled` session whose date disappeared → `ExecuteDelete` (set nothing else;
     it's a drop, not a cancel). `Skipped`/`Cancelled`/`Detached` and past sessions untouched.
   - Notify responders of surviving sessions of the change.

## Migration: `AddTrainings`

- Create `Trainings`, `TrainingSessions`, `TrainingResponses` with the columns above.
- Indexes: `Trainings(TeamId)`; `TrainingSessions(TeamId, SessionDate)`, `TrainingSessions(TrainingId)`;
  `TrainingResponses(TrainingSessionId)`, unique `TrainingResponses(TrainingSessionId, UserId)`.
- FKs: `TrainingSessions.TrainingId → Trainings` (cascade delete), `TrainingResponses.TrainingSessionId
  → TrainingSessions` (cascade delete), user/team FKs `Restrict` (mirror existing conventions).
- No data backfill. `NotificationType`/`NotificationCategory` are `int` enums — new values need no schema
  change.
- `DevDataSeeder`: one weekly public-capable series (a few generated sessions with mixed responses), one
  one-off, and one public session carrying a guest response, for the demo team.
