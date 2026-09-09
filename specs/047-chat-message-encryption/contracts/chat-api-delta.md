# Contract delta: chat API

**Feature**: 047 | Amends [`specs/019-chat/contracts/chat-api.md`](../../019-chat/contracts/chat-api.md)

**No endpoint is added, removed, renamed or re-shaped.** Routes, verbs, status codes,
pagination, cursors, ordering, rate limits and authorisation are all exactly as 019/022/027/046
left them. One response field is added.

## `MessageDto` — one added field

```jsonc
{
  "id": "0198…",
  "kind": "Member",
  "senderId": "…",
  "senderName": "Nia B.",
  "isOwn": false,
  "body": "meet at the north pitch at 18:00",
  "sentAt": "2026-09-09T17:02:11Z",
  "isDeleted": false,
  "isUnavailable": false,   // ← NEW
  "readState": null,
  "systemEvent": null,
  "systemSubjectName": null,
  "linkCard": null
}
```

| Field | Meaning |
|---|---|
| `isUnavailable` | The stored text could not be decrypted. `body` is `""`. The client renders a neutral placeholder (`chat.conversation.messageUnavailable`) in the reader's language. |

Rules:

- `isUnavailable` and `isDeleted` are **never both true** — a deleted row holds no ciphertext
  to fail on.
- When either is true, `body` is `""` and `linkCard` is `null`. For the unavailable case this is
  suppression, not absence: the link columns survive the failure, and resolving a card beside a
  placeholder would advertise the content of the very message the reader is being told cannot be
  shown.
- `isUnavailable` is `false` for every `Kind = System` line: system lines carry no text.

Appears on every response that already carries a `MessageDto`:

| Path | Change |
|---|---|
| `GET /chat/conversations/{id}/messages` | items gain the field |
| `POST /chat/conversations/{id}/messages` | response gains the field (always `false` — a message just encrypted decrypts) |
| SignalR `ChatHub` → `MessageReceived` | payload gains the field |

## `LastMessageDto` — unchanged

The inbox preview is **not** given the flag. An unreadable last message previews as an empty
string, exactly as a deleted one already does. The row still shows its sender and timestamp;
opening the conversation shows the placeholder. Recorded as a deliberate simplification
(research §5), not an oversight.

## Frontend model

`frontend/apps/web/src/app/core/models/chat.models.ts` — `ChatMessage` gains
`readonly isUnavailable: boolean;` beside `isDeleted`. `ChatService`'s optimistic
delete patch (`chat.service.ts` L386) is unaffected: it sets `isDeleted: true, body: ''` and
never touches the new flag.

## Backward compatibility

None is offered and none is needed: frontend and backend ship together, as in features 020
and 042. A client that ignores the field renders an empty bubble instead of a placeholder —
degraded, not broken.
