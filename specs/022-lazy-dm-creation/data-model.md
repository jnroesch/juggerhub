# Phase 1 Data Model: Lazy Direct-Message Creation

**No schema change, no migration, no new entity.** This feature changes *when* an
existing entity is created, not its shape.

## Entities (unchanged shape)

- **Conversation** — unchanged. A `Direct` conversation is now inserted on the first
  message rather than on open. Its uniqueness is still the filtered unique index on
  `DirectPairKey` (one DM per ordered pair), which also provides the create-on-send race
  safety (insert → `DbUpdateException` → resolve to winner).
- **ConversationParticipant** — unchanged; the two participant rows are still written
  when the conversation is created (now at first send).
- **ChatMessage** — unchanged; the first message is the trigger that materialises the
  conversation.

## Lifecycle change (Direct conversations)

| Moment | Before | After |
|--------|--------|-------|
| Open a chat with a new person | Conversation + participants inserted; appears in inbox empty | Nothing persisted; transient compose view only |
| Send first message | Message inserted into pre-created conversation | Conversation + participants + message inserted atomically (ensure-or-get, then send) |
| Open a chat with an existing DM | Returns existing conversation | Returns existing conversation (unchanged) |
| Leave without sending | Empty conversation lingers in inbox | Nothing exists to leave behind |

Group / Team / Party conversations: lifecycle unchanged.

## Client view-models (component-local)

- **Compose target** (`ChatComposeComponent`): `{ handle, userId?, displayName?, hasAvatar? }`
  — derived from router state (from the 021 action / new-message picker) or re-resolved
  from `:handle` via chat search on reload. Not persisted. `userId` is required at send
  time; resolved lazily if only the handle is known.

## DTO additions (contracts)

| DTO | Shape | Purpose |
|-----|-------|---------|
| `DirectMessageSentDto` | `{ conversationId: Guid, message: MessageDto }` | Response of the first-message endpoint — the newly ensured conversation id + the created message, so the client can navigate into the real thread. |

Request reuses the existing `SendMessageRequest { body }`. No other DTO changes; the
`ConversationSummaryDto`, `MessageDto`, and inbox shapes are unchanged.

See [contracts/](contracts/) for the endpoint delta.
