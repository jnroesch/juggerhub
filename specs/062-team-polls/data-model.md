# Data Model: Team Polls (062)

Three new entities, one migration `AddTeamPolls`. **Generate it with a build.** 056 found that
`--no-build` emitted an empty migration, so after generating, check that the migration is not
empty. There are no new columns on existing tables.

## TeamPoll : BaseEntity

A question an admin put to one team.

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` (UUIDv7) | BaseEntity |
| `TeamId` | `Guid` | FK → `Teams`, **Cascade**: a deleted team takes its polls |
| `AuthorUserId` | `Guid` | FK → `Users`, **Restrict** (the `TeamNewsPost` precedent). The account row is never deleted, so an erased author's poll stays and projects a `null` name |
| `Question` | `string` | `varchar(200)`, required; trimmed, 1–200 (`TeamPollRules.QuestionMaxLength`) |
| `AllowsMultiple` | `bool` | one answer (`false`) or any number (`true`); editable until the first answer |
| `IsAnonymous` | `bool` | fixed at creation, **never updated by any code path** (FR-016). The edit request has no field for it |
| `ResultsAfterAnswer` | `bool` | `false` = results always visible; `true` = hidden from a member until they answer or the poll closes (FR-012a); editable until the first answer |
| `ClosesAt` | `DateTime?` | `timestamptz`, UTC; scheduled close; later than now and ≤ now + 365 days when set; movable while open (FR-022) |
| `ClosedAt` | `DateTime?` | `timestamptz`, UTC; set only by closing early (FR-021); never cleared: no reopen |
| `CreatedDate` / `ModifiedDate` | `DateTime` | audit. **Every `ExecuteUpdate` (close, edit) sets `ModifiedDate`** |

Navigations: `Team`, `Author` (`User`), `Options` (ordered by `Position`), `Votes`.

Indexes: `(TeamId, CreatedDate)`, which serves the team's list and Home's per-team scan (the
`TeamNewsPost` shape).

**Derived, never stored**:
- `IsOpen(now) = ClosedAt == null && (ClosesAt == null || ClosesAt > now)`. This is the single
  expression `TeamPollOpen.At(now)`.
- `ClosedMoment = ClosedAt ?? ClosesAt`, meaningful only when not open.
- `HasAnswers = Votes.Any()`, counting any stored vote including a former member's. It is what
  locks the content fields (FR-023).

## TeamPollOption : BaseEntity

One of a poll's two to ten fixed answers.

| Field | Type | Rules |
|---|---|---|
| `PollId` | `Guid` | FK → `TeamPolls`, **Cascade** |
| `Text` | `string` | `varchar(80)`, required; trimmed, 1–80; unique within the poll ignoring case and surrounding spaces (checked by `TeamPollRules`, not by an index) |
| `Position` | `int` | 0-based, the admin's order |

Indexes: **unique `(PollId, Position)`**. It is the order's integrity guard and the FK's index
(the 061 `TeamLink` shape).

Options are **replaced as a set** (delete + insert, new ids) only when the poll has no votes
and their text or order changed. Otherwise they are left as they are and their ids stay stable.

## TeamPollVote : BaseEntity

One chosen option of one member's answer. A multi-choice answer is several rows.

| Field | Type | Rules |
|---|---|---|
| `PollId` | `Guid` | FK → `TeamPolls`, **Cascade**. Redundant with `Option.PollId`, and kept so that "has X answered poll P" and "remove X's answer to P" are single-table statements |
| `OptionId` | `Guid` | FK → `TeamPollOptions`, **Cascade** |
| `UserId` | `Guid` | FK → `Users`, **Restrict**. Erasure deletes these rows explicitly (R13) |

Indexes:
- **unique `(OptionId, UserId)`** — the same option twice is impossible;
- `(PollId, UserId)` — the answered check, the replace-my-answer delete, and Home's
  `!Votes.Any(v => v.UserId == me)`.

**Invariants, enforced by `TeamPollService` under the poll row lock (research R3), not by the
schema**:
- all rows of one `(PollId, UserId)` belong to options of that poll;
- a one-answer poll has at most one row per `(PollId, UserId)`;
- rows change only while the poll is open.

**Counted** (research R5): a row counts iff its `UserId` currently has a `TeamMembership` on the
poll's team and is not `Banned`. It is evaluated at read time and never written.

## Changes to existing types (no schema change)

| Type | Change |
|---|---|
| `NotificationType` | `+ TeamPoll = 11`, **appended**. Stored as an integer, so it must never be inserted mid-list |
| `NotificationCategories.For` | `+ TeamPoll => TeamNews`, an explicit arm (the default arm already returns it, and that is exactly the silent-mapping trap 039 warned about) |
| `NeedsYouKind` | `+ TeamPoll`, appended (serialised by name) |
| `NeedsYouParamsDto` | `+ string? Question` |
| `AccountDeletionService.RetainedCategories` | `+ "Polls"` |

## Notification payload (jsonb, camelCase)

```text
TeamPollPayload { teamSlug, teamName, pollId, question }
```

- Dedupe key: `poll:{pollId}:{recipient}`, written by `CreateManyAsync` under the prefix
  `poll:{pollId}`.
- Actor: the author.
- **No person's name in the payload**: the recipient's row outlives the author's account
  (037 FR-023).
- `teamSlug` + `teamName` is what 061's `ReplaceTeamNameAsync` matches and rewrites.

## Lifecycle

```text
                 create (admin; team row locked; open count < 10)
                          │
                          ▼
   ┌────────────────── OPEN ──────────────────┐
   │ answer / change / withdraw (member)      │
   │ edit content (admin; only if no votes)   │
   │ move / clear close date (admin)          │
   └──────────────┬───────────────┬───────────┘
      ClosesAt ≤ now          close early (admin)
      (derived, no write)     ClosedAt := now
                  ▼               ▼
                 CLOSED (final: no answers, no edits, no reopen)

   delete (admin), from either state → poll + options + votes + Alerts rows removed together
```
