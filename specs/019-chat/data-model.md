# Data Model: Chat (019)

Phase 1 for [plan.md](./plan.md). All entities derive from `BaseEntity` (constitution III): UUIDv7
`Id`, `CreatedDate` / `ModifiedDate` set by `AuditFieldsInterceptor`. Enums serialize as names via the
global `JsonStringEnumConverter`.

> **The load-bearing idea**: `Id` is a **UUIDv7** — timestamp-prefixed and monotonic — so `ORDER BY Id`
> *is* chronological order, and `Id > x` *is* "sent after x". Message ordering, keyset paging, unread
> counting and read receipts all fall out of that one property, with no separate sequence column, no
> client clock and no receipt table. See research §3.

---

## Enums — `Entities/ChatEnums.cs`

```csharp
public enum ConversationKind { Direct = 0, Group = 1, Team = 2, Party = 3 }
public enum ConversationState { Active = 0, Archived = 1 }
public enum ChatMessageKind { Member = 0, System = 1 }
public enum ChatLinkKind { None = 0, Player = 1, Team = 2, Event = 3, Training = 4 }
public enum ChatSystemEvent { Joined = 0, Left = 1, Removed = 2, GroupCreated = 3 }
```

`ConversationKind` is the discriminator that drives **everything** conditional in the feature:

| Kind | Membership source | Name | Leave? | Add? | Inbox tag |
|------|-------------------|------|--------|------|-----------|
| `Direct` | `ConversationParticipant` rows (exactly 2) | derived — the other person | no (block/hide instead) | no | — |
| `Group` | `ConversationParticipant` rows (2…50) | stored, required | **yes** | **yes** | — |
| `Team` | **derived** from `TeamMemberships` | derived — the team's name | no (mute/hide) | no | `TEAM` |
| `Party` | **derived** from `PartyMembers` (`Status == In`) | derived — the party's name | no (mute/hide) | no | `PARTY` |

---

## `Conversation`

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | UUIDv7, from `BaseEntity` |
| `Kind` | `ConversationKind` | discriminator (above) |
| `Name` | `string?` | **required for `Group`**, must be null for all other kinds. Direct/Team/Party derive their display name at projection time. |
| `TeamId` | `Guid?` | set **iff** `Kind == Team`; FK → `Team` |
| `PartyId` | `Guid?` | set **iff** `Kind == Party`; FK → `Party` |
| `State` | `ConversationState` | `Archived` closes it to writes (FR-027) |
| `LastMessageDate` | `DateTime?` | denormalised for inbox ordering; set on send/delete |

**Rules**

- **R1** — `Kind == Group` ⟺ `Name` is non-empty. `Kind == Direct` ⟺ `Name is null`. A `Team`/`Party`
  chat has a null `Name` **while live** (it derives one) and a frozen `Name` **once archived** (R3a).
  (FR-009)
- **R2** — While live: `Kind == Team` ⟺ `TeamId` non-null; `Kind == Party` ⟺ `PartyId` non-null; both
  null otherwise. Both are nulled on archival (R3a).
- **R3** — `State == Archived` rejects send, typing, add and all realtime emission; reads still work
  for members. Archiving is **one-way** — nothing sets it back to `Active` (FR-027, edge case
  "never becomes writable again").
- **R3a** — **Archiving an auto chat is a snapshot, not a flag.** *(Discovered during implementation —
  see the drift note below.)* Team deletion and party disband are **hard deletes** in this codebase
  (`Party` remarks: "Disband is a hard delete… there is no stored disbanded state"), and
  `TeamMemberships`/`PartyMembers` cascade away with them. Because a live auto chat **derives** its
  membership from that roster (R5), a naive archive-and-delete would leave a conversation that
  *nobody* can read — the roster it consults is gone — silently breaking FR-027's "members can still
  read the history". Archiving must therefore, **before** the team/party row is deleted:
  1. materialise the derived roster into real `ConversationParticipant` rows,
  2. freeze the display name into `Name`,
  3. null `TeamId`/`PartyId`,
  4. set `State = Archived`.

  An archived auto chat is then structurally a **read-only group**: stored membership, stored name, no
  roster link. `Kind` is deliberately **not** changed, so the inbox still tags it TEAM/PARTY.
  The FKs are **`Restrict`** on purpose: a future delete path that forgets to archive first fails
  loudly in development rather than silently orphaning an `Active` conversation whose membership
  resolves to nobody. Fails closed, not quiet.
- **R4** — `LastMessageDate` is denormalised **only** to keep the inbox's ORDER BY off a correlated
  subquery over `ChatMessages`. It is a cache of `MAX(ChatMessages.CreatedDate)`, never authoritative
  for ordering *within* a conversation (that is `Id`, always).

**Indexes**

- `IX_Conversations_TeamId` **unique**, filtered `WHERE "TeamId" IS NOT NULL` → enforces *one chat per
  team* (FR-024) in the database rather than in a service race.
- `IX_Conversations_PartyId` **unique**, filtered `WHERE "PartyId" IS NOT NULL` → same for parties.

---

## `ConversationParticipant`

One row = one player's **state in** one conversation. For `Direct`/`Group` the row *is* the
membership. For `Team`/`Party` the row is **only state** — it is created lazily on first access and
carries no authority; deleting it would not revoke access, and its absence does not deny access.
Membership for those kinds is a roster query (research §4).

| Field | Type | Notes |
|-------|------|-------|
| `ConversationId` | `Guid` | FK → `Conversation` |
| `UserId` | `Guid` | FK → `User` |
| `LastReadMessageId` | `Guid?` | the read marker. Null = has read nothing. |
| `IsMuted` | `bool` | excluded from the nav unread total (FR-018, FR-028) |
| `IsHidden` | `bool` | excluded from the inbox list (FR-029) |
| `JoinedDate` | `DateTime` | for the member list and system lines |
| `LeftDate` | `DateTime?` | set when leaving a `Group`; the row is kept, not deleted |

**Rules**

- **R5** — Membership predicate, the single most security-relevant expression in the feature:
  ```
  Direct/Group → participant row exists AND LeftDate is null
  Team         → TeamMemberships.Any(m => m.TeamId == c.TeamId && m.UserId == me)
  Party        → PartyMembers.Any(p => p.PartyId == c.PartyId && p.UserId == me && p.Status == In)
  ```
  It lives in **one place** (`ChatGuard`) and every read, send, search scope and fan-out calls it.
  (FR-047, FR-022, FR-025, FR-035)
- **R6** — Leaving a `Group` sets `LeftDate` rather than deleting the row, so the leaver's past
  messages keep an attributable sender and the group's history stays coherent (US3 #6). A left
  participant fails R5, so they read nothing.
- **R7** — `Team`/`Party` rows are created on demand (`EnsureParticipantStateAsync`) purely so mute /
  hide / read-marker have somewhere to live. Never used to decide access.
- **R8** — Unread for a participant:
  ```
  ChatMessages.Count(m => m.ConversationId == c.Id
                       && m.SenderId != me
                       && !m.IsDeleted
                       && (p.LastReadMessageId == null || m.Id > p.LastReadMessageId))
  ```
  A keyset comparison on the PK — no receipt table, no scan. (FR-015, FR-050c)
- **R9** — The nav total sums unread across non-muted, non-hidden conversations the player is a member
  of by R5, and the *displayed* value is capped by the existing `badgeText()` helper at "9+"
  (FR-018, reusing feature 010's convention rather than inventing a second badge rule).

**Indexes**

- `IX_ConversationParticipants_ConversationId_UserId` **unique** → one state row per player per
  conversation; makes the duplicate-add edge case a no-op at the database level.
- `IX_ConversationParticipants_UserId` → drives "my inbox".

---

## `ChatMessage`

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | UUIDv7 — **the sort key and the read cursor** |
| `ConversationId` | `Guid` | FK → `Conversation` |
| `SenderId` | `Guid?` | **null for `System`** messages (FR-013 — attributable to no sender) |
| `Kind` | `ChatMessageKind` | `Member` \| `System` |
| `Body` | `string` | ≤ 2 000 chars; **plain text, never markup** (FR-014) |
| `IsDeleted` | `bool` | tombstone (FR-050) |
| `SystemEvent` | `ChatSystemEvent?` | set iff `Kind == System`; the *client* renders the wording |
| `SystemSubjectUserId` | `Guid?` | who the system line is about ("Nia B. joined the team") |
| `LinkKind` | `ChatLinkKind` | `None` unless a JuggerHub link was parsed out at send |
| `LinkTargetId` | `Guid?` | the target's id — **never a snapshot of its fields** |

**Rules**

- **R10** — Ordering is `ORDER BY Id` (research §3). Never `CreatedDate` — ties within a tick would
  give two viewers different orders, violating FR-011.
- **R11** — `Body` is validated non-empty-after-trim and ≤ 2 000 chars **server-side** (FR-010). The
  client's own check is UX only.
- **R12** — Deleting sets `IsDeleted = true` and **clears `Body`, `LinkKind` and `LinkTargetId`** — the
  content is genuinely gone from the row, not merely hidden behind a flag a future query might forget.
  The row survives to hold its place in the order and render the tombstone (FR-050, FR-050c).
- **R13** — `Kind == System` ⟺ `SenderId is null`. A system line can never be authored by a member,
  and a member message can never be forged as a system line.
- **R14** — Only `SenderId == me` may delete, enforced in the service (FR-050a). There is no moderator
  delete in this feature.
- **R15** — `(LinkKind, LinkTargetId)` is parsed from `Body` at send by `ChatLinkParser` using
  **route-shape matching only** (research §5). The card is built at *read* time per viewer by
  `ChatLinkResolver`, which re-checks that viewer's permission and returns null (→ plain link) on fail
  or on a missing target (FR-040, FR-041). **No snapshot is stored** — that is precisely what makes
  per-viewer permission enforceable rather than frozen at send time.

**Indexes**

- `IX_ChatMessages_ConversationId_Id` → the history keyset page (`WHERE ConversationId = x AND Id < cursor ORDER BY Id DESC`) and the unread count, both on one composite.
- `IX_ChatMessages_SenderId` → delete authorization and the deleted-user sweep.

---

## `UserBlock`

| Field | Type | Notes |
|-------|------|-------|
| `BlockerUserId` | `Guid` | FK → `User` — the person who blocked |
| `BlockedUserId` | `Guid` | FK → `User` — the person blocked |

**Rules**

- **R16** — Blocks are **directional**. A blocks B does not imply B blocks A. The check for "may A and
  B hold a direct conversation" is symmetric though: a block in *either* direction closes it, so a
  blocked player cannot open a fresh DM to their blocker (FR-049b).
- **R17** — Enforced on **three** paths, all server-side (FR-031, FR-033):
  1. start a direct conversation → refused,
  2. send to an existing direct conversation → refused,
  3. people search → the counterpart is filtered out.
- **R18** — A block **never** touches `Group` / `Team` / `Party` conversations. The check is scoped by
  `Kind == Direct` and nothing else (FR-032).
- **R19** — The blocker's inbox filters out the direct conversation with the blocked player (FR-031);
  history is retained, so unblocking restores it intact (FR-030).
- **R20** — `BlockerUserId != BlockedUserId` — you cannot block yourself.

**Indexes**

- `IX_UserBlocks_BlockerUserId_BlockedUserId` **unique** → idempotent blocking; a double-block is a no-op.
- `IX_UserBlocks_BlockedUserId` → the "am I blocked by them" direction of R16.

---

## Relationships

```text
User ──< ConversationParticipant >── Conversation ──< ChatMessage
                                          │                 │
                                          ├── TeamId? ──> Team ──< TeamMembership >── User   (derived membership)
                                          └── PartyId? ─> Party ──< PartyMember >── User     (derived membership)

User ──< UserBlock >── User        (directional; Direct conversations only)

ChatMessage ──(LinkKind, LinkTargetId)──> PlayerProfile | Team | Event | TrainingSession
             (a loose reference by id, resolved per viewer — deliberately NOT a foreign key,
              so a deleted target degrades to a plain link instead of cascading into the thread)
```

## Lifecycle

**Conversation**

```text
Direct : created on first "start a chat" (or on demand)  ──> Active ──> (never archived)
Group  : created explicitly by a player                  ──> Active ──> (never archived)
Team   : EnsureForTeamAsync on first access              ──> Active ──> Archived  (team deleted)
Party  : EnsureForPartyAsync on first access             ──> Active ──> Archived  (party disbands)

                                    the Team/Party ──> Archived transition is R3a's snapshot:
                                    derived roster ──> stored participant rows
                                    derived name   ──> frozen into Name
                                    TeamId/PartyId ──> null
                                    State          ──> Archived
                                    …all BEFORE the team/party row is hard-deleted.
```

`Archived` is terminal (R3). No transition leaves it.

> **Spec drift found during implementation (2026-07-16)**
> The plan assumed archival could be a simple state flag. It cannot: party disband and team delete are
> hard deletes whose rosters cascade, and derived membership dies with them. R3a is the correction.
> No spec requirement changed — FR-027 still reads exactly as written — but the *mechanism* behind it
> did, and the archive hook (T065) is now materially more work than "set a flag". Recorded here rather
> than absorbed silently.

**Message**

```text
sent ──> Member/System ──> (sender deletes) ──> IsDeleted, Body & link cleared, place in order kept
```

Terminal. There is no edit transition (FR-050b) and no un-delete.

## Uniqueness & concurrency

- **One direct conversation per pair** (FR-008): enforced by a unique index on the *ordered pair* of
  participant ids — a `Conversations` row for `Kind == Direct` gets a computed
  `DirectPairKey = $"{min(a,b)}:{max(a,b)}"` with a unique filtered index
  `WHERE "Kind" = 0`. Ordering the pair means (A,B) and (B,A) collide, so the duplicate-start race
  from two clients resolves to one row **in the database**, not in a service check-then-insert that
  can interleave. The service catches the unique violation and returns the existing row.
- **Duplicate add** to a group collides on `IX_ConversationParticipants_ConversationId_UserId` and is
  treated as a no-op (edge case: "the second add is a no-op").
- **Concurrent sends** are ordered by UUIDv7 `Id` (R10), which is monotonic — no tie is possible.
- Inserts use `DbSet.Add` with client-generated UUIDv7 keys — per the known EF gotcha, a client-GUID
  nav-property insert can be misclassified, so participants are added via `DbSet.Add` explicitly.

## Deleted / banned users (feature 013)

A soft-deleted or banned user's `ChatMessage` rows are **left intact**; projection renders a neutral
placeholder identity from the existing profile projection rather than rewriting history. They fail R5
everywhere (no roster, no live participant row), so no new messages arrive from them, and a direct
conversation with them cannot be started. No chat-specific deletion logic is added.
