# Quickstart: Hiding a Chat Is Reversible

**Feature**: 048-chat-unhide | **Date**: 2026-09-09

How to run and prove this feature. Details live in [contracts/chat-hide-state.md](./contracts/chat-hide-state.md)
and [data-model.md](./data-model.md); this file is the validation guide.

## Prerequisites

Nothing new. **No migration to apply, no new environment variable, no new service** — the
whole feature runs on the stack as it already stands (research R12).

```powershell
docker compose up -d          # postgres, redis, mailpit (redis is required — chat fails closed without it)
cd backend; dotnet run
cd frontend; npm run start    # → http://localhost:4200
```

## Automated checks

```powershell
# Backend — chat suite (the new facts live in ChatHideTests)
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~IntegrationTests.Chat"

# Frontend — chat components/service + the i18n key-parity guard
cd frontend
npx nx test web --watch=false --testPathPattern="chat|catalog-parity"
npm run lint
npm run build
```

**The parity guard is expected to be red until all three catalogues carry
`chat.details.unhide`** (research R8). That is the guard doing its job — add the key to
`en.json`, `de.json` and `es.json` together, do not silence it.

---

## Scenario 1 — An archived conversation comes back on its own (US1, P1)

*Two accounts, A and B, both on the same team.*

1. As **A**, open the team chat → details panel → **Hide from my messages**.
2. Confirm it is gone: the team chat is absent from A's inbox, and the nav chat badge does not
   count it.
3. As **B**, send a message to that team chat.
4. **Expected — as A, with the app open and no refresh:** the team chat is back in the inbox,
   at the top, showing B's message as its last line, with an unread badge; the nav total has
   gone up by the same amount.

**What this proves**: FR-007, FR-008, FR-009, FR-010 — and it is the scenario that makes a
hidden **team** chat recoverable at all, which is the reported bug in #222.

**If the row appears but the nav badge does not move**, the clear is running *after*
`PushMessageToOthersAsync` instead of before it (research R3).

### 1a — Hidden *and* muted

Repeat with A muting as well as hiding. **Expected**: the conversation returns to the inbox,
and the nav total does **not** move. Mute is a separate choice and survives (FR-005).

### 1b — A system line does not bring it back (FR-011)

With the team chat hidden by A, have a third player join the **team** (not the chat). The
chat records "X joined". **Expected**: A's inbox is unchanged — the conversation stays hidden.

### 1c — The sender's own send (FR-012)

As A, hide a conversation, then navigate to it directly at `/chat/{id}` and send a message.
**Expected**: it is back in A's own inbox. (Before this feature, A could post a message
invisible in their own inbox.)

---

## Scenario 2 — Put a conversation back by hand (US2, P2)

1. As **A**, hide any conversation. Note its `/chat/{id}` URL first — for a group or team chat
   that link is the only route back while a hidden-chats surface stays out of scope
   (spec Assumptions).
2. Navigate to that URL and open the details panel.
3. **Expected**: the control reads **"Show in my messages"**, not "Hide" (FR-002).
4. Click it.
5. **Expected**: you stay on the conversation (FR-004), and the conversation is present in the
   inbox rail immediately — **without reloading the page** (FR-003).

**If it only reappears after a reload**, the `setState` truthiness defect is unfixed
(research R6) — `{isHidden: false}` is falling through both branches.

6. Click again. **Expected**: it reads "Hide from my messages", hiding works, and you are
   returned to `/chat` — unchanged from today.

### 2a — Convergence across sessions

With A signed in in two browsers, un-hide in one. **Expected**: the other converges, exactly
as mute already does (FR-003).

---

## Scenario 3 — Nothing else moved (regression)

1. As a player who has hidden **nothing**, compare the inbox before and after this feature:
   same conversations, same order, same unread total (SC-006).
2. Confirm a hidden conversation is still openable by its direct link, and still shows its
   full history and members (FR-017, SC-005).
3. Confirm a **blocked** DM that is also hidden does not return to the blocker's inbox, and
   that the blocked player still cannot send (019 FR-031).
4. Confirm mute/unmute is untouched in both label and behaviour.

---

## Manual UI review (Quality Gate 7)

The details panel gains a conditional label, so Gate 7 is engaged. Work through
[`checklists/ui-review.md`](./checklists/ui-review.md) against the diff, at **375px** and
desktop, in light and dark:

- the toggle sits with `toggle-mute` and matches it in height, padding, border and focus ring;
- the longest string — German **"Wieder in meinen Nachrichten anzeigen"** — does not wrap the
  row into an unreadable shape or push the icon off-axis;
- the icon reads as *show* in the un-hidden state rather than staying the crossed-out eye;
- keyboard focus order and the visible focus ring are unchanged;
- DESIGN.md wins on any conflict.

---

## Definition of done

- [ ] Backend chat suite green, including the new `ChatHideTests` facts (C1–C10 in the contract)
- [ ] Frontend chat + `catalog-parity` suites green; lint and production build clean
- [ ] Scenarios 1 (incl. 1a–1c), 2 (incl. 2a) and 3 walked in the browser
- [ ] `checklists/ui-review.md` completed
- [ ] `specs/019-chat/spec.md` carries the 048 amendment callout; FR-026 and FR-029 updated;
      `specs/019-chat/contracts/chat-api.md` points here (research R9)
- [ ] No migration, no new entity, no new endpoint, no new dependency in the diff (research R12)
