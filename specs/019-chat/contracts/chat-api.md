# Contract: Chat API (019)

Phase 1 for [plan.md](../plan.md). REST base `/api/v1/chat`. All endpoints require authentication
(JWT in an httpOnly cookie); all are authorized **server-side** against current membership via
`ChatGuard` (data-model R5). Lists are paginated with the shared `PaginationRequest` /
`PagedResult<T>` envelope, except message history which is **keyset**-paged (research §9).

**Refusal policy (FR-048)**: a request for a conversation the caller is not a member of returns
**`404 Not Found`**, never `403` — a `403` would confirm the conversation exists. The same applies to
a message id in a conversation the caller cannot see. Bodies carry only the generic problem shape from
the existing global exception middleware; no stack traces, no internals.

---

## Conversations

### `GET /api/v1/chat/conversations`

The inbox. Returns the caller's conversations, `LastMessageDate` descending.

Query: `skip`, `take` (default 20, max 100).

Excludes: hidden conversations (FR-029), and direct conversations with a player the caller has
blocked (FR-031/R19).

```jsonc
{
  "items": [{
    "id": "0198...",
    "kind": "Direct",              // Direct | Group | Team | Party
    "name": "Ben R.",              // derived for Direct/Team/Party, stored for Group
    "avatar": { "kind": "User", "userId": "0198...", "url": "…" },
    "lastMessage": {
      "preview": "grabbing the chain, omw",   // "" when the last message is deleted
      "at": "2026-07-16T19:38:02Z",
      "senderName": "Ben R.",                 // null for a system line or own message
      "isOwn": false,
      "isSystem": false
    },
    "unreadCount": 2,
    "isMuted": false,
    "state": "Active",             // Active | Archived
    "teamId": null, "partyId": null
  }],
  "totalCount": 5, "skip": 0, "take": 20
}
```

### `GET /api/v1/chat/conversations/unread-count`

Drives the nav badge. Sums unread over non-muted, non-hidden conversations (data-model R9).

```jsonc
{ "unreadCount": 8 }
```

### `POST /api/v1/chat/conversations`

Start a conversation. **One person → `Direct`; two or more → `Group`** (FR-007).

Rate-limited: `chat-start`, 10/min per user (research §7).

```jsonc
// request
{ "participantUserIds": ["0198..."], "name": null }
```

| Condition | Response |
|-----------|----------|
| 1 id, no existing direct | `201` + conversation |
| 1 id, direct already exists | **`200`** + the existing conversation (FR-008 — idempotent, not a duplicate) |
| ≥2 ids, non-empty name | `201` + group |
| ≥2 ids, missing/blank name | `400` — a group must be named (FR-009) |
| >50 ids | `400` — group cap (research §8) |
| only the caller's own id | `400` — no self-chat (edge case) |
| either party has blocked the other | `403` — refused (FR-031, R16) |
| rate limit exceeded | `429` |

### `GET /api/v1/chat/conversations/{id}`

One conversation's header + details: kind, name, avatar, state, and the caller's own mute/hide flags.
`404` if not a member.

### `GET /api/v1/chat/conversations/{id}/members`

Paginated member list, the caller marked `isYou`. For `Team`/`Party` this projects the **roster**
(data-model R5), not participant rows.

```jsonc
{ "items": [{ "userId": "0198...", "displayName": "Ada K.", "handle": "ada-k",
              "avatarUrl": "…", "isYou": true, "viaMarket": false }],
  "totalCount": 3, "skip": 0, "take": 20 }
```

### `POST /api/v1/chat/conversations/{id}/members`

Add people to a **`Group`** only (FR-044).

```jsonc
{ "userIds": ["0198..."] }
```

| Condition | Response |
|-----------|----------|
| group, caller is a member, room under the cap | `204` + a `Joined` system line per added user |
| user already a member | `204` — **no-op**, no duplicate row, no second system line (edge case) |
| conversation is `Direct`/`Team`/`Party` | `400` — membership is not addable (FR-026, US3 #9) |
| would exceed 50 members | `400` |
| conversation is `Archived` | `409` |

### `DELETE /api/v1/chat/conversations/{id}/members/me`

Leave a **`Group`** (FR-044). Sets `LeftDate` (data-model R6) and writes a `Left` system line.
The group survives for the others, including when the leaver created it (US3 #6, #7).

| Condition | Response |
|-----------|----------|
| group member | `204` |
| `Team`/`Party` conversation | **`400`** — cannot leave; mute or hide instead (FR-026, US4 #5) |
| `Direct` conversation | `400` — hide or block instead |

### `PATCH /api/v1/chat/conversations/{id}/state`

The caller's own per-conversation flags. Available for **every** kind, including `Team`/`Party` —
this is what stands in for "leave" there (FR-026).

```jsonc
{ "isMuted": true, "isHidden": false }   // either field optional
```

`204`. Creates the lazy participant-state row for `Team`/`Party` if absent (data-model R7).

### `POST /api/v1/chat/conversations/{id}/read`

Mark read up to a message. Converges the badge across the player's other sessions (FR-016) by
pushing `chatUnreadCountChanged` to the caller's **own** user group.

```jsonc
{ "lastReadMessageId": "0198..." }
```

`204`. Idempotent, and **never moves the marker backwards** — a late request from a stale tab cannot
resurrect unread messages.

---

## Messages

### `GET /api/v1/chat/conversations/{id}/messages`

History, newest first, **keyset**-paged on the UUIDv7 `Id` (research §9).

Query: `before` (message id cursor, optional), `take` (default 30, max 100).

```jsonc
{
  "items": [{
    "id": "0198...",
    "kind": "Member",                  // Member | System
    "senderId": "0198...",             // null for System
    "senderName": "Ben R.",            // null for System / deleted user → placeholder
    "isOwn": false,
    "body": "wait — we're in Hall B tonight",
    "sentAt": "2026-07-16T19:40:11Z",
    "isDeleted": false,
    "readState": null,                 // own messages in a Direct convo: "Sent" | "Read"
    "systemEvent": null,               // Joined | Left | Removed | GroupCreated
    "systemSubjectName": null,
    "linkCard": {                      // null → render body as plain text
      "kind": "Training",              // Player | Team | Event | Training
      "targetId": "0198...",
      "title": "Tuesday Training",
      "subtitle": "19:00 · Sportpark",
      "href": "/trainings/sessions/0198...",
      "avatarUrl": null
    }
  }],
  "nextBefore": "0198..."              // null when the history is exhausted
}
```

**A deleted message** returns `isDeleted: true`, `body: ""`, `linkCard: null` — the client renders the
tombstone (FR-050). **`linkCard` is resolved per viewer** — the same message returns a card for a
member and `null` (plain link in `body`) for someone who may not see the target (FR-040, research §5).

### `POST /api/v1/chat/conversations/{id}/messages`

Send. Rate-limited: `chat-send`, 30/min per user.

```jsonc
{ "body": "got it, Hall B" }
```

`201` + the created `MessageDto`. Fans out `chatMessageCreated` to the other participants' user groups
and `chatUnreadCountChanged` to each (research §1).

| Condition | Response |
|-----------|----------|
| empty / whitespace-only body | `400` (FR-010) |
| body > 2 000 chars | `400` (research §8) |
| not a member | `404` (FR-048) |
| direct conversation, either party blocked | `403` (FR-031) |
| conversation `Archived` | `409` — read-only (FR-027) |
| rate limit exceeded | `429` (FR-049a) |

### `DELETE /api/v1/chat/messages/{id}`

Delete **your own** message (FR-050). Clears body + link, keeps the row (data-model R12), pushes
`chatMessageDeleted`.

| Condition | Response |
|-----------|----------|
| caller is the sender | `204` |
| caller is a member but **not** the sender | **`403`** (FR-050a) |
| caller is not a member | `404` |
| message is a system line | `403` |

> There is **no** `PATCH /messages/{id}` — a sent message is immutable (FR-050b).

### `POST /api/v1/chat/conversations/{id}/typing`

Signal typing. Client debounces to ≤ 1 call / 3 s. Rate-limited: `chat-typing`, 30/min per user.
Pushes `chatTyping` to the **other** participants with a 5 s expiry (research §2). Persists nothing.

`204`. `409` if the conversation is `Archived`.

---

## Search

### `GET /api/v1/chat/search`

Query: `q` (required, ≥ 2 chars), `skip`, `take`.

Returns both groups in one response, matching the wireframe's "IN YOUR MESSAGES" / "PEOPLE" split.

```jsonc
{
  "messages": {
    "items": [{ "messageId": "0198...", "conversationId": "0198...",
                "conversationName": "Rheinfeuer", "conversationKind": "Team",
                "snippet": "…just RSVP'd to Tuesday Training", "sentAt": "…",
                "senderName": "Ben R." }],
    "totalCount": 2, "skip": 0, "take": 20
  },
  "people": {
    "items": [{ "userId": "0198...", "displayName": "Kofi O.", "handle": "kofi-o",
                "avatarUrl": "…", "existingConversationId": null }],
    "totalCount": 2, "skip": 0, "take": 20
  }
}
```

- **Message results are scoped to the caller's own conversations in the query itself** (FR-035,
  data-model R5) — never post-filtered. A term living only in someone else's conversation returns
  nothing, and no count leaks its existence.
- Deleted messages never match (FR-050c).
- People the caller has blocked are excluded (FR-033).
- Matching is `ILike` + `Unaccent`, per the existing convention (research §6).

---

## Blocks

### `GET /api/v1/chat/blocks` — paginated list of players the caller has blocked.

### `POST /api/v1/chat/blocks` — `{ "userId": "0198..." }` → `204`. Idempotent (data-model R18); `400` on self.

### `DELETE /api/v1/chat/blocks/{userId}` — unblock → `204`. History intact (FR-030).

---

## Realtime — hub `/hubs/chat`

`ChatHub` mirrors `NotificationHub` exactly: `[Authorize]` JWT-bearer, connection joins **only**
`user:{sub}` from the *validated* token, and there are **no client-invokable server methods** — the
hub is push-only (research §1). Typing goes over REST, not the socket (research §2).

Server → client events:

| Event | Payload | When |
|-------|---------|------|
| `chatMessageCreated` | `{ conversationId, message: MessageDto }` | a message is sent to a conversation the recipient is in |
| `chatMessageDeleted` | `{ conversationId, messageId }` | a sender deletes their message |
| `chatUnreadCountChanged` | `{ unreadCount }` | the recipient's total changed (send, read, mute) |
| `chatConversationUpserted` | `{ conversation: ConversationSummaryDto }` | a new/updated inbox row (new chat, added to a group, last-line change) |
| `chatTyping` | `{ conversationId, userId, displayName, expiresInMs: 5000 }` | another member signalled typing |

**Every one of these is best-effort and duplicated by a REST read** (FR-023): `chatMessageCreated` ⟷
`GET …/messages`, `chatUnreadCountChanged` ⟷ `GET …/unread-count`, `chatConversationUpserted` ⟷
`GET /conversations`. A client that never connects is stale, never wrong. `chatTyping` is the sole
exception — it is ephemeral by design and has no REST equivalent, which is correct: there is no such
thing as stale typing.

Fan-out resolves participants **server-side** at send time and pushes only to each participant's own
validated user group, so a non-member never receives an event (FR-022) and no client can subscribe
itself into a conversation's stream.
