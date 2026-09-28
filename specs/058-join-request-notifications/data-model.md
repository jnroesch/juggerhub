# Data Model: Join Requests Reach the People Who Decide Them

**Feature**: 058 · **Spec**: [spec.md](./spec.md) · **Research**: [research.md](./research.md)

**No new entity, no new column, no migration.** `NotificationType` is stored as an integer and the
preference table is sparse, so two appended enum values change no stored row. If a task produces an
`Add-Migration`, something is wrong.

---

## 1. `TeamJoinRequest` (existing — behaviour only)

Fields unchanged: `TeamId`, `UserId` (the player), `Status`, `DecidedByUserId`, `DecidedDate`,
plus `BaseEntity`'s `Id`/`CreatedDate`/`ModifiedDate`. The partial unique index "one Pending
request per (team, player)" is unchanged and still what makes FR-003's simultaneous double request
announce once.

### Waiting (R3, FR-015)

A request is **waiting** when all three hold:

| Clause | Why |
|--------|-----|
| `Status == Pending` | not answered |
| the player is not `Banned` (`Users.Status`) | a ban hides the player everywhere (013); lifting it restores the request (FR-021) |
| the player is not a member of the team | ended by joining another way (FR-020); safety net for R6 |

One expression, `JoinRequestWaiting.Predicate(db)`, correlated subqueries only, used by the queue,
Home, the alerts' *Resolved* state and the answer statements.

### Transitions

```text
                 ┌──────── approve (claim WHERE waiting) ───────► Approved  + TeamMembership(Member)
                 │                                                  └─► answer notice (accepted)
 (request) ──► Pending ─── decline (claim WHERE waiting) ────────► Declined
                 │                                                  └─► answer notice (declined)
                 ├──── withdraw (delete WHERE Pending) ────────────► (row gone) + admins' alerts removed
                 ├──── player joins by invitation (R6) ────────────► (row gone) + admins' alerts removed
                 ├──── player erases account (037) ────────────────► (row gone); alerts stay, name no one
                 └──── team deleted ───────────────────────────────► (row gone); alerts stay, not waiting
```

- **Exactly once** (R4): Approved and Declined are reached only by a conditional `ExecuteUpdate`
  that matches the row only while it is waiting. Every `ExecuteUpdate` sets `ModifiedDate`
  explicitly (constitution III — the interceptor does not run).
- **Withdrawal** deletes conditionally (`WHERE Status = Pending`), never by key alone (R5).
- A ban is not a transition: the row stays `Pending` and simply stops satisfying *waiting*.

---

## 2. Notification types (existing enum, two values appended)

```text
NotificationType
  …
  EventCancelled          = 8
  TeamJoinRequest         = 9    // to each admin; link-only; actor = the player
  TeamJoinRequestAnswered = 10   // to the player; link-only; no actor
```

`NotificationCategories.For`: both → `InvitesAndRoster`. **Appended, never inserted** — the values
are stored as integers.

### Payloads (camelCase JSON in `Notification.Payload`)

| Type | Payload | Never contains |
|------|---------|----------------|
| `TeamJoinRequest` | `TeamJoinRequestPayload(Guid RequestId, string TeamSlug, string TeamName)` | the player's name or handle (R1 — the player is `ActorUserId`) |
| `TeamJoinRequestAnswered` | `TeamJoinRequestAnsweredPayload(string TeamSlug, string TeamName, bool Accepted)` | any admin's identity (FR-011); an enum (R8) |

### Rows written

| Type | Recipients | `ActorUserId` | `DedupeKey` | Written by |
|------|-----------|---------------|-------------|------------|
| `TeamJoinRequest` | every admin of the team at the moment of the request, each by their *Invites & roster → In-app* setting | the player | `join-request:{requestId}:{adminId}` (prefix `join-request:{requestId}`) | `CreateManyAsync` |
| `TeamJoinRequestAnswered` | the player, by their *In-app* setting | `null` | `join-answer:{requestId}` | `CreateAsync` |

### Read-time state

`NotificationDto.Resolved` — today TeamInvite-only — also covers `TeamJoinRequest`: **true when the
request is no longer waiting** (R10). `NotificationDto.ActorDisplayName` for a `TeamJoinRequest` row
is the player's *current* name, or `null` once banned or erased (R1).

### Removal

On withdrawal and on joining by invitation: `DeleteManyAsync(TeamJoinRequest, "join-request:{id}")`,
in the same transaction as the request's deletion; unread badges refreshed after commit (R5, R6).

---

## 3. Home *Needs you* (existing DTO, reshaped — breaking)

```text
NeedsYouKind   TeamInvite | PartyRequest | PartyCoAdminInvite | MarketInvite | MarketApplication
               | JoinRequest                                   ← appended

NeedsYouItemDto(
    NeedsYouKind Kind,
    string Id,                 // the action key — unchanged per kind
    NeedsYouParamsDto Params,  // NEW — replaces Title + Context (English prose, removed)
    string? LinkTarget,        // unchanged per kind; JoinRequest = the player's handle
    DateTime OccurredAt)

NeedsYouParamsDto { TeamName?, TeamSlug?, EventName?, PlayerName? }   // names only, never prose
```

| Kind | `Id` | `LinkTarget` | `Params` used | English (verbatim from today) |
|------|------|--------------|---------------|-------------------------------|
| TeamInvite | invitation token | team slug | TeamName | "{{team}} invited you" · "to join the team" |
| PartyCoAdminInvite | invitation token | event id | EventName, TeamName | "Co-admin a party for {{event}}" · team |
| PartyRequest | party id | event id | TeamName, EventName | "{{team}} is fielding a party" · event |
| MarketInvite | market request id | event id | TeamName, EventName | "{{team}} want you" · event |
| MarketApplication | market request id | event id | TeamName, EventName | "You applied to {{team}}" · event |
| **JoinRequest** | join request id | player handle | PlayerName, TeamName, TeamSlug | "{{player}} wants to join" · team |

Join-request items: the viewer's **current** admin teams × R3's predicate, `OrderByDescending
(CreatedDate)`, merged with the other kinds by `OccurredAt` and capped at `HomeOptions.NeedsYouCap`,
exactly like the rest. Stores nothing.

---

## 4. Rate-limit policy (configuration in code, like chat's)

| Policy | Limit | Window | Partition | Applies to |
|--------|-------|--------|-----------|------------|
| `join-request` | 10 | 1 hour, fixed | authenticated user | `POST /teams/{slug}/join-requests` only |

Counters in Redis (`rl:join-request:{userId}:{window}`), fail-closed; in-memory in Development/tests.

---

## 5. Email (templates + localizer keys)

| Template (`en`/`de`/`es`) | To | Variables |
|---------------------------|----|-----------|
| `join-request.html` | each admin with *Invites & roster → Email* on | `EMAIL_TITLE`, `RECIPIENT_NAME`, `PLAYER_NAME`, `TEAM_NAME`, `TEAM_URL`, `FOOTER_REASON` |
| `join-request-accepted.html` | the player, if on | `EMAIL_TITLE`, `RECIPIENT_NAME`, `TEAM_NAME`, `TEAM_URL`, `FOOTER_REASON` |
| `join-request-declined.html` | the player, if on | `EMAIL_TITLE`, `RECIPIENT_NAME`, `TEAM_NAME`, `BROWSE_URL`, `FOOTER_REASON` |

`EmailLocalizer` keys × 3 languages: `subject.joinRequest` ({0} player, {1} team),
`subject.joinRequestAccepted` ({0} team), `subject.joinRequestDeclined` ({0} team), and the matching
`title.*` and `footer.*`. Shared URLs (dashboard, settings, privacy, imprint) come from
`AddSharedUrls` as for every template.

---

## 6. Push copy (`PushLocalizer`, × en/de/es)

`teamJoinRequest.body` ({0} = player), `teamJoinRequest.bodyAnonymous`,
`teamJoinRequestAccepted.body`, `teamJoinRequestDeclined.body`. Titles are the team name (the
subject). See research R13 for the table.

---

## 7. Frontend models

```ts
type NotificationType = … | 'TeamJoinRequest' | 'TeamJoinRequestAnswered';
interface TeamJoinRequestPayload { requestId: string; teamSlug: string; teamName: string }
interface TeamJoinRequestAnsweredPayload { teamSlug: string; teamName: string; accepted: boolean }

type NeedsYouKind = … | 'JoinRequest';
interface NeedsYouParams { teamName: string | null; teamSlug: string | null;
                           eventName: string | null; playerName: string | null }
interface NeedsYouItem { kind; id; params: NeedsYouParams; linkTarget: string | null; occurredAt }
```
