# API Contract Delta: Lazy Direct-Message Creation

One endpoint is **added**; one existing endpoint's behavior is **narrowed**. No shapes
are removed. Message send/history/inbox contracts are otherwise unchanged.

## NEW — `POST /api/v1/chat/direct/{targetUserId}/messages`

Create-or-get the direct conversation with `targetUserId` and post the first message,
atomically. This is the only path that creates a DM.

- **Auth**: authenticated (JWT).
- **Rate limit**: `ChatStart` policy (the open-reach abuse guard — this is the
  conversation-creation surface now).
- **Request body**: `SendMessageRequest` — `{ "body": "string" }` (reused).
- **Success**: `201 Created` →

  ```json
  {
    "conversationId": "uuid",
    "message": { /* MessageDto, exactly as returned by POST .../messages today */ }
  }
  ```

- **Errors** (as ProblemDetails, mirroring existing chat outcomes):
  - `400` — target invalid/unavailable (unknown or banned), or empty / too-long body.
  - `403` — blocked in either direction (a block cannot be circumvented by composing).
  - `429` — rate limited.

- **Idempotency / races**: if a DM with the pair already exists, it is reused (no
  duplicate). Concurrent first sends resolve to a single conversation (unique
  `DirectPairKey`), and each message lands in it.

## UNCHANGED — `POST /api/v1/chat/conversations` (Start)

Left as-is (revised during implementation, see research R4): single-participant Start
still create-or-gets a DM, groups unchanged. It is refactored only to share the
`EnsureDirectAsync` create-or-get with the endpoint above (no behavior change). The
**client** simply stops calling Start for a new DM — it routes to compose and creates via
the first-message endpoint. FR-009 holds because no empty DM is created through the UI;
the residual API-only create path is rate-limited and out of normal use.

## UNCHANGED

- `POST /api/v1/chat/conversations/{id}/messages` — send within an existing conversation.
- `GET .../conversations` (inbox), `GET .../conversations/{id}` (detail),
  `GET .../messages`, typing, read, mute/hide, blocks — all unchanged.
- Group, team, and party conversation creation/materialisation — unchanged (FR-007).

## Client consumption (frontend)

- `ChatService.sendDirect(targetUserId, body)` → the new endpoint; on `201`, navigate
  (`replaceUrl`) to `/chat/{conversationId}`.
- The 021 profile **Message** action and the chat **new-message** picker: existing DM
  (`existingConversationId` present) → open `/chat/{id}`; otherwise → `/chat/compose/{handle}`.
- Groups from the new-message picker still use `POST /conversations` via `ChatService.start`.
