# Quickstart / Validation: Lazy Direct-Message Creation

Validates that a DM is created only on first send, and never leaves an empty thread.
No migration; backend + frontend change.

## Prerequisites

- Local stack up (frontend + backend + Postgres + Redis + Mailpit).
- Two signed-in players (A, B) with no existing conversation; optionally a block.

## Automated checks

- **Backend**: `dotnet test` (chat suites) — new/updated cases:
  - Opening/ensuring without a message creates **no** conversation.
  - First send creates exactly one conversation containing the message.
  - Concurrent first sends resolve to one conversation (race).
  - Blocked pair: first send is refused and creates nothing.
  - Existing DM: reused, not duplicated.
  - Single-participant Start no longer creates (existing-only).
- **Frontend**: `npx nx test web` — `chat-compose.component.spec.ts` (send creates +
  navigates; block/error surfaces), and updated 021 `profile-quick-actions` spec
  (new-DM branch routes to compose, existing opens the thread).
- **Lint/build**: `npx nx lint web`; `dotnet build` analyzer-clean.

## Manual end-to-end scenarios

1. **Open + leave = nothing** (US1, FR-001/FR-004): as A, open a chat with B (profile
   **Message**, or new-message picker) — you land in a compose view. Leave without
   sending. Return to the inbox → unchanged. Sign in as B → no thread, no notification.
2. **First send creates + delivers** (US2, FR-002): as A, compose to B and send a
   message → you land in a real conversation with the message; it tops your inbox. As B,
   open the inbox → the conversation is present with A's message and an unread mark.
3. **Existing opens directly** (US3, FR-003): as A (already messaging B), use **Message**
   on B's profile → you land in the existing thread with history; no new conversation.
4. **Blocked at send** (FR-005): with A/B blocked, compose to B and try to send → a
   friendly refusal; no conversation created.
5. **No empty threads anywhere** (FR-009): after all of the above, confirm no
   message-less DM appears in either inbox.
6. **Race** (FR-006): send the first message to B from two of A's devices/tabs at once →
   exactly one conversation exists afterward, holding both messages.
7. **Groups unaffected** (FR-007): create a named group from the new-message picker (2+
   people) → it is created and appears as today.

## UI review (Quality Gate 7)

- Instantiate `specs/022-lazy-dm-creation/checklists/ui-review.md` and verify the
  compose view against DESIGN.md (it reuses the conversation composer): header names the
  recipient, empty-thread affordance is calm, send states + error states styled to the
  system, one primary send control.

## Docs

- `specs/019-chat/` carries an amendment note: direct conversations are created on first
  message send, not on open.
