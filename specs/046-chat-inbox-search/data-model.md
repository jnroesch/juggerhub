# Data Model: Chat Inbox Search by People and Conversation Names

Phase 1 for [plan.md](./plan.md). **No entity, column, index or migration is added or changed.**
If a task ever produces an `Add-Migration`, something is wrong. This document records what the
search *reads*, per conversation kind, and the invariants that make the spec's rules structural.

## Entities read (all existing, feature 019 / 027)

| Entity | Used for | Notes |
|--------|----------|-------|
| `Conversation` | the row; `Kind`, `State`, `Name`, `TeamId`, `PartyId`, `EventId`, `RequesterUserId` | `Name` is stored for groups and for the frozen name of an archived chat; null otherwise |
| `ConversationParticipant` | membership for Direct / Group / Archived; hide flag | for Team / Party / inquiry kinds the row is per-user *state only* and carries no authority |
| `TeamMembership` | live roster of a Team chat; admins of a TeamInquiry | `Role == Admin` for the inquiry branch |
| `PartyMember` | live roster of a Party chat | `Status == In` only |
| `EventAdmin` | admins of an EventInquiry | |
| `PlayerProfile` | `DisplayName`, `Handle` — what a member's name *is* | carries the ban `HasQueryFilter`; always reached via `db.PlayerProfiles`, never via `User.Profile` |
| `Team`, `Event` | `Name` — what a team / inquiry chat is called | |
| `UserBlock` | DM exclusion, inherited from the inbox | unchanged |

## Name sources per kind

What "the name the inbox shows" and "the members" are, for each `ConversationKind`. The
member column is what `HasMemberNamed` reads; the name column is what `IsNamed` reads. Both
mirror the branches of `ChatGuard.IsMemberOf`, which is the point: one source of truth per kind.

| Kind | Members searched (excluding the caller) | Conversation name searched | Shown-name fallback (**not** searched) |
|------|------------------------------------------|----------------------------|-----------------------------------------|
| Direct | the other participant (`LeftDate == null`) | — | the other's display name *is* the row name; a gone profile shows the placeholder |
| Group | participants with `LeftDate == null` | `Conversation.Name` | "Group" |
| Team | `TeamMembership` rows for `TeamId` | `Team.Name` | "Team chat" |
| Party | `PartyMember` rows for `PartyId`, `Status == In` | — (a party chat has no name of its own) | "Party chat" |
| TeamInquiry | the requester, and `TeamMembership` rows with `Role == Admin` | `Team.Name` | — |
| EventInquiry | the requester, and `EventAdmin` rows | `Event.Name` | — |
| *Archived* (any kind) | participants with `LeftDate == null` (the roster snapshotted at archival) | `Conversation.Name` (frozen at archival) | — |

The inquiry admin-side label ("Requester · Team") is covered without special-casing: the
requester is a searched member, the team or event is the searched name.

## The predicate

```text
eligible(c)   = VisibleConversations(caller)              -- unchanged inbox rule:
                  IsMemberOf(caller)                       --   member (per-kind source of truth)
                  AND NOT hidden-by-caller                 --   owner decision: hidden stays hidden
                  AND NOT (Direct AND blocked either way)  --   inherited from the inbox

listed(c, q)  = eligible(c) AND (HasMemberNamed(c, q) OR IsNamed(c, q))

HasMemberNamed(c, q) = EXISTS member m of c, m ≠ caller, such that
                         EXISTS PlayerProfile pp WHERE pp.UserId = m
                           AND ( unaccent(pp.DisplayName) ILIKE unaccent('%q%')
                              OR pp.Handle ILIKE '%q%' )

IsNamed(c, q) = unaccent(c.Name)       ILIKE unaccent('%q%')   -- Group, archived
             OR unaccent(c.Team.Name)  ILIKE unaccent('%q%')   -- Team, TeamInquiry
             OR unaccent(c.Event.Name) ILIKE unaccent('%q%')   -- EventInquiry
```

Order, skip/take and the projection to `ConversationSummaryDto` are the inbox's own and are not
repeated here. When `q` has fewer than two characters after trimming, `listed(c, q) = eligible(c)`.

## Invariants (what the model guarantees, and which requirement each carries)

- **I1 — No widening.** `listed ⊆ eligible` for every term, because the name predicate is
  appended to the inbox query rather than replacing it. A conversation the inbox would not
  show cannot appear, and its existence cannot leak through `totalCount` (FR-003, SC-003).
- **I2 — No term, no change.** An absent or too-short term yields the unfiltered inbox query,
  byte-for-byte (FR-007, SC-007).
- **I3 — Membership is as of now.** Every member branch reads the same live source `IsMemberOf`
  reads, so leaving a group or a roster stops the match at the same instant it stops the
  membership (edge case "former members"). Archived chats read their snapshot, exactly as
  `IsMemberOf` does.
- **I4 — Unavailable members cannot match.** A member's name is only ever reached through
  `db.PlayerProfiles`, so a banned account (filtered) or an erased account (row deleted) has no
  name to match, and the placeholder text is never a searchable value (edge case
  "unavailable members").
- **I5 — Message text is not a source.** Nothing in `listed` reads `ChatMessages` (FR-001,
  FR-010, SC-002). The only message-related value on a result is the inbox row's own
  last-message preview, which is rendering, not matching (FR-011).
- **I6 — The caller is not a member of interest.** Every member branch excludes the caller, so a
  common own-name term does not list the whole inbox.
- **I7 — Fallback labels are not names.** "Party chat", "Group", "Team chat" and "Chat" are
  presentation defaults, not stored or chosen names, and are not matched (recorded as minor
  drift on FR-002 in the plan).

## What is deleted

| Symbol | Where | Why |
|--------|-------|-----|
| `MessageSearchHitDto` | `backend/Dtos/Chat/ChatDtos.cs` | message hits no longer exist (FR-010) |
| `ChatSearchResultDto.Messages` | same | the DTO keeps only `People` |
| `ChatSearchService.SearchMessagesAsync` | `backend/Services/Chat/ChatSearchService.cs` | the only reader of message bodies on the search path |
| `MessageSearchHit`, `ChatSearchResult.messages` | `frontend/apps/web/src/app/core/models/chat.models.ts` | mirrors the DTO |

No table, column or index is touched by any of this: the deleted code read existing data; it
owned none.
