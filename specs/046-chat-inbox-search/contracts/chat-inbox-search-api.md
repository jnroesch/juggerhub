# Contract: Chat Inbox Search (046)

Amends the **Conversations** and **Search** sections of
[specs/019-chat/contracts/chat-api.md](../../019-chat/contracts/chat-api.md). Everything not
listed here is unchanged. Both changes are **breaking for the client** (a new optional query
parameter on one endpoint, a removed property on another); frontend and backend ship together,
as for features 020 and 042.

All endpoints require the signed-in JWT (httpOnly cookie), as before.

---

## Conversations

### `GET /api/v1/chat/conversations` — the inbox, now optionally filtered by name

Query:

| Parameter | Type | Rules |
|-----------|------|-------|
| `q` | string, optional | Trimmed. Fewer than 2 characters ⇒ treated as absent (the plain inbox is returned, never an error). Matched case- and accent-insensitively as a substring. |
| `skip`, `take` | int | The shared `PaginationRequest` (default 20, hard max 100), unchanged. |

Response: `PagedResult<ConversationSummaryDto>` — **unchanged shape**. Each item is the same
inbox row (`id`, `kind`, `name`, `avatar`, `lastMessage`, `unreadCount`, `isMuted`, `state`,
`teamId`, `partyId`), in the same order (most recently active first). `totalCount` counts the
matching conversations only.

With `q` present, an item is included when the conversation is one the inbox would show
**and** at least one of these holds:

- a **current member other than the caller** has a display name or handle containing `q` —
  the other participant of a DM; a group's participants; a team chat's roster; a party chat's
  members with status *In*; an admin-contact thread's requester or current admins;
- the conversation's **name** contains `q` — a group's name, the team's name (team chat and
  team admin-contact thread), the event's name (event admin-contact thread), or the frozen
  name of an archived chat.

Not matched, by design: message text; the caller's own name; the fallback labels "Party chat",
"Group", "Team chat"; members whose profile is unavailable (banned or erased).

Eligibility is exactly the inbox's: member of, not hidden by the caller, and — for DMs — not
with a player either side has blocked. A term that matches only a conversation the caller cannot
see returns `items: []` and `totalCount: 0`.

```http
GET /api/v1/chat/conversations?q=lena&skip=0&take=20
```

```jsonc
{
  "items": [
    { "id": "0198…", "kind": "Team",   "name": "Hamburg Jugger",  /* …inbox row… */ },
    { "id": "0198…", "kind": "Group",  "name": "Tournament trip", /* … */ },
    { "id": "0198…", "kind": "Direct", "name": "Lena B.",         /* … */ }
  ],
  "totalCount": 3, "skip": 0, "take": 20
}
```

---

## Search

### `GET /api/v1/chat/search` — people only

Query: `q` (≥ 2 chars, otherwise an empty result), `skip`, `take` — unchanged.

Response — the `messages` property is **removed**; the `people` envelope is unchanged:

```jsonc
{
  "people": {
    "items": [{ "userId": "0198…", "displayName": "Kofi O.", "handle": "kofi-o",
                "avatarUrl": "…", "existingConversationId": null }],
    "totalCount": 1, "skip": 0, "take": 20
  }
}
```

Semantics of `people` are unchanged: any player on the platform except the caller and anyone
blocked in either direction (019 FR-033, FR-049); `existingConversationId` is set when a DM
already exists (019 FR-008). Its callers — the new-chat picker, compose-by-handle (022) and the
profile Message action (021) — need no change.

There is no message-text search anywhere in the API after this feature. A client that still
sends a term expecting `messages` receives the object above without that property.

---

## Client models (`frontend/apps/web/src/app/core/models/chat.models.ts`)

- `ChatSearchResult` becomes `{ people: { items: PersonHit[]; totalCount: number } }`.
- `MessageSearchHit` is deleted.
- `Conversation` is unchanged; `ChatService.searchInbox(term, take?)` returns
  `PagedResult<Conversation>`.
