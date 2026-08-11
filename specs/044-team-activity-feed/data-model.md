# Phase 1 Data Model: Team-internal "What's happening" section

**Feature**: 044 | **Date**: 2026-08-11 | **Plan**: [plan.md](./plan.md)

> **No persisted model changes.** No entity, column, index, constraint, or migration is added.
> Everything here is a **read model** projected from rows that already exist. If implementation
> produces an `Add-Migration`, stop — something has gone wrong.

## 1. The read model (new DTOs)

`backend/Dtos/Teams/TeamHappeningDtos.cs`

### `TeamHappeningKind` (enum, serialized by name)

| Member | Meaning |
|---|---|
| `MemberJoined` | A player joined the team. |
| `RecognitionAwarded` | A badge or achievement was awarded **to the team**. |
| `TrainingSeriesCreated` | A training series was added to the team's schedule. |
| `TrainingSessionCancelled` | A dated session of a team training was called off. |

Closed set of four (spec FR-004…FR-007). Departures and role changes are deliberately absent
(D1/FR-009). Events played are deliberately absent — they belong to the event card (FR-008).

### `TeamHappeningParamsDto`

Only the fields the entry's kind uses are populated; the rest stay `null`. Names are user data
and are **never translated**; the connecting prose is a client-side key (FR-021).

| Field | Type | Populated for | Notes |
|---|---|---|---|
| `ActorName` | `string?` | `MemberJoined` | `null` when the profile is suppressed (banned) or neutralised (deleted). The client substitutes a **translated** stand-in — see research R2. |
| `RecognitionName` | `string?` | `RecognitionAwarded` | The badge or achievement definition's name. |
| `TrainingName` | `string?` | `TrainingSeriesCreated`, `TrainingSessionCancelled` | `Training.Name` is non-nullable, so no unnamed fallback key is needed. |
| `SessionDate` | `DateOnly?` | `TrainingSessionCancelled` | *Which* session was called off — the reader needs this, not just the training's name. |

### `TeamHappeningDto`

```
TeamHappeningDto(
    TeamHappeningKind Kind,
    TeamHappeningParamsDto Params,
    string? LinkTarget,
    DateTime OccurredAt)
```

Mirrors `ActivityEntryDto`'s shape without sharing its type (research R1).

## 2. Sources — one row per kind

All reads are `AsNoTracking()` with explicit `.Select` projections, filtered to the team, bounded
by the window **and** the cap.

| Kind | Source table | Team filter | `OccurredAt` | Extra predicate |
|---|---|---|---|---|
| `MemberJoined` | `TeamMemberships` | `TeamId == teamId` | **`JoinedDate`** | — |
| `RecognitionAwarded` | `BadgeAwards` | `TeamId == teamId` | **`EarnedAt`** | `Status == AwardStatus.Active` |
| `RecognitionAwarded` | `AchievementAwards` | `TeamId == teamId` | **`EarnedAt`** | `Status == AwardStatus.Active` |
| `TrainingSeriesCreated` | `Trainings` | `TeamId == teamId` | **`CreatedDate`** | — |
| `TrainingSessionCancelled` | `TrainingSessions` | `TeamId == teamId` (denormalised) | **`CancelledDate`** | `Status == TrainingSessionStatus.Cancelled` |

Five queries, two of which produce the same kind. **The `OccurredAt` column differs per kind and
is not `CreatedDate` uniformly** — see research R5 for why that matters.

### Projection details

- **`ActorName` / handle**: sub-projected as
  `_db.PlayerProfiles.Where(p => p.UserId == m.UserId).Select(p => …).FirstOrDefault()`, never
  navigated as `m.User.Profile!.…`. `PlayerProfiles` carries a ban query filter
  (`AppDbContext.cs:149`); the sub-projection degrades to `null`, the navigation does not
  degrade predictably. (R2)
- **`RecognitionName`**: `Definition.Name` on either award entity.
- **`TrainingName`**: `Training.Name` (via the session's navigation for the cancelled kind).
- **`SessionDate`**: `TrainingSession.SessionDate`.

## 3. Bounds

Two compile-time constants, declared once in `TeamHappeningService` (spec FR-011, FR-012):

| Constant | Value | Applied |
|---|---|---|
| `WindowDays` | `30` | `cutoff = DateTime.UtcNow.AddDays(-WindowDays)`, as a predicate on **each** query |
| `MaxEntries` | `10` | `Take(MaxEntries)` on **each** query, then again on the merged list |

Not configuration, not per-team, not overridable (owner decision D4). Applying the cap per query
*and* after the merge bounds the in-memory set at 50 rows regardless of distribution.

## 4. Ordering

Total and repeatable (FR-015):

1. `OccurredAt` **descending**
2. `Kind` (ascending, enum order) — breaks the realistic collision of a series created and one
   of its sessions cancelled in the same batch
3. A stable per-entry key (`LinkTarget` or the source row id rendered as text)

Then `Take(MaxEntries)`.

## 5. Link targets

| Kind | `LinkTarget` | Client route |
|---|---|---|
| `MemberJoined` | player handle | `['/u', handle]` |
| `RecognitionAwarded` | `null` | none — the standing-collection card is on the same page |
| `TrainingSeriesCreated` | `null` | `['/t', slug, 'trainings']`, built client-side from the component's slug input (no per-series route exists) |
| `TrainingSessionCancelled` | session id | `['/trainings/sessions', id]` |

Where the target has since disappeared, the entry renders as plain text rather than a dead link
(FR-022).

## 6. Access

| Caller | Result |
|---|---|
| Anonymous | `401` — global `FallbackPolicy` (feature 026); no `[AllowAnonymous]` |
| Signed-in non-member | `404` via `TeamNotFound()` |
| Unknown slug | `404` — **identical response**, so team existence is not disclosed |
| Member / admin | `200` with the list; admins get nothing extra |

Enforced by `TeamMembershipGuard.ResolveAsync`, whose contract is *"non-members are
indistinguishable from unknown teams to callers"*. The service returns `null` for both.

## 7. Self-correcting behaviour (do not optimise away)

Because entries are derived rather than stored, three spec edge cases resolve with no extra code:

| Change | Effect on the feed | Why |
|---|---|---|
| A member leaves or is removed | Their join entry disappears | The `TeamMemberships` row is gone |
| An award is revoked | Its entry disappears | `Status` is no longer `Active` |
| A member is banned or deletes their account | Their name collapses to a translated stand-in | The `PlayerProfiles` ban filter / 037 neutralisation |

A persisted activity table would reintroduce all three as bugs (research R10).

## 8. Existing entities referenced (all unchanged)

| Entity | Fields used | File |
|---|---|---|
| `TeamMembership` | `TeamId`, `UserId`, `JoinedDate` | `backend/Entities/TeamMembership.cs` |
| `PlayerProfile` | `UserId`, `DisplayName`, `Handle` | ban query filter at `AppDbContext.cs:149` |
| `BadgeAward` | `TeamId`, `Status`, `EarnedAt`, `Definition.Name` | `backend/Entities/BadgeAward.cs` |
| `AchievementAward` | `TeamId`, `Status`, `EarnedAt`, `Definition.Name` | `backend/Entities/AchievementAward.cs` |
| `Training` | `TeamId`, `Name`, `CreatedDate` | `backend/Entities/Training.cs` |
| `TrainingSession` | `TeamId`, `Status`, `CancelledDate`, `SessionDate`, `Training.Name` | `backend/Entities/TrainingSession.cs` |
| `Team` | `Id`, `Slug` (via the guard) | `backend/Entities/Team.cs` |
