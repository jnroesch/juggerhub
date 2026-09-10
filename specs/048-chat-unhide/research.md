# Phase 0 Research: Hiding a Chat Is Reversible

**Feature**: 048-chat-unhide | **Date**: 2026-09-09 | **Spec**: [spec.md](./spec.md)

Every finding below was established by reading the code on `main` at `89888b1`, not by
recollection. Line numbers are from that commit.

---

## R1 — The un-hide endpoint already exists and needs no change

**Decision**: Reuse `PATCH /chat/conversations/{id}/state` exactly as it is. Add **no endpoint,
no request shape, no entity, no column, no migration**.

**Rationale**: `PatchConversationStateRequest(bool? IsMuted, bool? IsHidden)`
(`backend/Dtos/Chat/ChatDtos.cs:158`) is already nullable-per-flag, and
`ChatConversationService.PatchStateAsync` (L971-1002) applies whichever flags are present:

```csharp
if (isHidden is { } h) { state.IsHidden = h; }
```

`isHidden: false` therefore un-hides today and always has. The gap reported in #222 is
**entirely** that nothing in the product ever sends `false` — it is a UI gap on the explicit
path, and a missing server behaviour only on the automatic path (R3).

**Alternatives considered**: a dedicated `POST .../unhide` action — rejected: it would be a
second way to write one boolean, and the toggle semantics FR-002 wants are exactly what a
nullable patch already expresses.

**Consequence for review**: if a task in this feature produces an `Add-Migration`, a new DTO,
or a new controller action, something has gone wrong.

---

## R2 — The hidden flag lives on a row that only exists once somebody uses it

**Decision**: Clear the flag with a narrow `ExecuteUpdateAsync` over the rows that carry it.
Do not materialise or create participant rows on the send path.

**Rationale**: `ConversationParticipant` rows are created **lazily** by
`ChatGuard.EnsureParticipantStateAsync` (L487-517) — and for Team/Party conversations the
entity's own XML doc is emphatic that the row is *state only*, carrying no authority over
access:

> **Team/Party** — the row is *state only*. It is created lazily on first access purely so the
> read marker, mute and hide flags have somewhere to live. […] Never use the presence of this
> row to decide access.

So only players who actually hid a conversation have a row with `IsHidden = true`. The
auto-un-hide is therefore not "iterate the roster and clear a flag" — it is a single
set-based update whose `WHERE` usually matches **zero** rows:

```csharp
await _db.ConversationParticipants
    .Where(p => p.ConversationId == conversationId && p.IsHidden && p.LeftDate == null)
    .ExecuteUpdateAsync(s => s
        .SetProperty(p => p.IsHidden, false)
        .SetProperty(p => p.ModifiedDate, DateTime.UtcNow), ct);
```

**Two details that are load-bearing, not decoration:**

1. **`ModifiedDate` must be set explicitly.** `ExecuteUpdateAsync` bypasses the change tracker,
   so `AuditFieldsInterceptor` does not run. This is spelled out in the constitution
   (Principle III, "Batch operations") with this exact shape as the example. Omitting it is a
   Quality Gate 2 failure.
2. **`p.LeftDate == null` is required for the spec's rejoin edge case.** The entity records
   that `LeftDate` is non-null only for `Group` (Direct/Team/Party cannot be left, FR-026), and
   the row is *kept* rather than deleted when someone leaves a group. Without this clause, a
   message in a group would clear the hidden flag of people who had already left it — so on
   rejoining they would find it un-hidden, contradicting the spec's "their hidden state is
   whatever they last left it as".

**Alternatives considered**: loading the participant rows and letting `SaveChangesAsync` write
them — rejected: it adds a `SELECT` to *every* send in order to usually find nothing, and
loses the free `ModifiedDate`-must-be-explicit reminder the constitution attaches to the
set-based form.

---

## R3 — Ordering: the flag must clear before any unread total is computed

**Decision**: Clear the flag in `ChatMessageService.SendAsync`, immediately after
`SaveChangesAsync` and **before** `PushMessageToOthersAsync`.

**Rationale**: `PushMessageToOthersAsync` (L154-177) recomputes and pushes each recipient's
badge inside its own loop:

```csharp
await _realtime.PushUnreadCountAsync(recipientId, await UnreadTotalAsync(recipientId, ct), ct);
```

and `UnreadTotalAsync` (L184-196) excludes hidden conversations:

```csharp
.Where(c => !c.Participants.Any(p => p.UserId == userId && (p.IsMuted || p.IsHidden)))
```

Clear the flag *after* that loop and FR-009 breaks in a way that is easy to miss and annoying
to diagnose: the conversation returns to the inbox carrying unread messages that the
navigation badge does not count, and stays that way until something else recomputes the total.
The correct site is a single statement between the save and the push.

**Note on `ExecuteUpdateAsync` and the change tracker**: the update runs directly against the
database, so the *subsequent* `UnreadTotalAsync` query — which is `AsNoTracking()` and reads
fresh — sees the cleared flag. There is no staleness hazard in this ordering.

---

## R4 — FR-011 is satisfied by *not* writing code, and that needs saying out loud

**Decision**: Put the clear in `SendAsync` **only**. Do not put it in
`WriteSystemMessageAsync`, and do not factor the two together.

**Rationale**: `WriteSystemMessageAsync` (L538-558) is already a deliberately minimal path —
it adds a row and saves. It does **not** push realtime, and it does **not** update
`Conversation.LastMessageDate` (compare `SendAsync` L127-128, which does both). So a system
line already does not move a conversation's inbox position or notify anybody. Making it
un-hide would be inconsistent with everything else that method declines to do.

**The risk this records**: the two methods are adjacent, both "write a message", and a future
change that unifies them — or that adds the clear to a shared helper — would silently take
FR-011 with it. The guard is a test (`ChatHideTests`: a system line leaves a hidden
conversation hidden) plus a comment at the `SendAsync` call site saying why it is not shared.

**Unresolved-but-harmless**: whether a system line contributes to the unread count at all
depends on EF Core's null-semantics rewriting of `m.SenderId != callerId` (system lines have
`SenderId = null`, L547). EF Core preserves C# semantics here, so it very likely *does* count.
This does not change any decision in this feature — FR-011 keeps hidden conversations hidden
through system lines either way — and the spec records the consequence (an archived
conversation's unread count can rise unseen) as accepted. **Do not** attempt to "fix" unread
counting in this feature; it is 019 behaviour and out of scope.

---

## R5 — The live return to the inbox is already implemented (do not build it)

**Decision**: Write **no** new frontend code for FR-008/FR-010. The existing inbox re-seed
covers a conversation arriving that the client does not know about.

**Rationale**: `ChatService.bumpConversationWithMessage`
(`frontend/apps/web/src/app/core/services/chat.service.ts:482-506`) already handles exactly
this case:

```ts
const found = cs.find((c) => c.id === conversationId);
if (!found) {
  // A conversation we don't know about yet (someone just started one with us) — re-seed.
  this.loadInbox().subscribe({ error: () => undefined });
  return cs;
}
```

A hidden conversation is absent from `_conversations`, so a message in it takes the `!found`
branch and re-fetches the inbox. Because the server cleared the flag in the same request that
produced the push (R3), the conversation is in that response — with its correct preview,
position and unread count, straight from the inbox projection. FR-008 and FR-010 hold **by
construction**, and they hold for the sender too: both call sites reach this method — the
caller's own send (L292) and the socket handler (L473).

This was written for feature 022's lazy DMs ("someone just started one with us") and turns out
to describe the un-hide case exactly. It is the single biggest reason this feature is small.

**Consequence for review**: a task that adds an inbox-insert path, a `conversationUnhidden`
realtime event, or a client-side hidden-list is building something that already works.

---

## R6 — `ChatService.setState` has a real defect on the explicit path

**Decision**: Fix it as part of this feature; it is the only reason User Story 2 would not
work end-to-end.

**Rationale**: the tap is written as a truthiness test (L203-212):

```ts
if (patch.isHidden) {
  this._conversations.update((cs) => cs.filter((c) => c.id !== conversationId));
} else if (patch.isMuted !== undefined) {
  this.patchConversation(conversationId, { isMuted: patch.isMuted });
}
```

`{ isHidden: false }` fails the first test and then fails the second (`isMuted` is
`undefined`), so an explicit un-hide updates **nothing** — the conversation stays absent from
the inbox signal until a full reload, which is precisely what FR-003 forbids.

**The fix**: test `isHidden === true` / `=== false` explicitly and make the two flags
independent `if`s rather than an `if/else if`. On `false` the conversation is not in
`_conversations` and its inbox row is not held anywhere on the client, so the correct action
is `loadInbox()` — the same re-seed R5 relies on, reused rather than reimplemented.

**Secondary defect fixed by the same edit**: the `else if` means a patch carrying *both* flags
would silently drop the mute. No caller sends both today; independent `if`s make that
unrepresentable rather than merely unused.

---

## R7 — The details panel already knows the state; only the label and the branch are missing

**Decision**: Turn `hide()` into `toggleHide()`, mirroring `toggleMute()` (L124-140 of
`chat-details.component.ts`) rather than inventing a pattern.

**Rationale**: `ConversationDetailDto` already ships `IsHidden`
(`ChatConversationService.GetDetailAsync` L691, `row.Me?.IsHidden ?? false`) and the client
model already declares it (`chat.models.ts:60`). The panel therefore knows which state it is
in and simply never branched on it.

The two directions are **not** symmetric, and FR-004 says so:

- **Hide** → navigate to `/chat` (current behaviour, unchanged — you have just said you want
  it out of your list).
- **Un-hide** → stay on the conversation and update the local `detail` signal, the way
  `toggleMute()` already does. Navigating away from a chat you just asked to see more of
  would be perverse.

**Naming**: `data-testid="hide-chat"` becomes `toggle-hide`, matching the existing
`toggle-mute`. Verified safe: those two testids appear **only** in
`chat-details.component.html` — no e2e spec or unit test references either.

---

## R8 — One new i18n key in three catalogues, and the parity guard is already watching

**Decision**: Add `chat.details.unhide` to `en.json`, `de.json` and `es.json` **in the same
change**.

**Rationale**: measured, not assumed — `chat.details` currently holds `mute` / `unmute` /
`hide` and no counterpart:

| Catalogue | `hide` | line |
|-----------|--------|------|
| `en.json` | "Hide from my messages" | 528 |
| `de.json` | "Aus meinen Nachrichten ausblenden" | 532 |
| `es.json` | "Ocultar de mis mensajes" | 532 |

`frontend/apps/web/src/app/core/i18n/catalog-parity.spec.ts` enforces identical key sets, so
adding the key to `en.json` alone turns the suite red. This is the guard working as intended
(feature 042 added it); it is not an obstacle to route around.

**Wording follows FR-015** — hide is an archive, so the counterpart says *show in my messages
again*, not *unhide* and not *restore*. The existing `hide` strings already read as tidying
("from my messages"), so they stand as they are and only the new key is authored.

---

## R9 — Amending 019 follows the precedent 022 and 046 set

**Decision**: Add an `Amended by feature 048` callout to `specs/019-chat/spec.md`'s
**Amendments** section, mark FR-026 and FR-029 as superseded in place, and add a pointer in
`specs/019-chat/contracts/chat-api.md`.

**Rationale**: 019 already carries two such callouts, both in the same shape
(`spec.md:13` for 022, `spec.md:21` for 046), and 046's plan records this as the house style.
The two requirements to touch:

- **FR-026** (L287) — "…mute and hide MUST be offered in place of leave." The owner rejected
  its premise: leaving the team leaves the chat, so hide was never needed as a leave
  substitute. Amend to name **mute**.
- **FR-029** (L293) — "A player MUST be able to hide a conversation from their inbox." Amend to
  add reversibility, both manual and automatic.

**FR-018** (L272) is deliberately **left alone**: "the navigation unread total MUST exclude
muted and hidden conversations" stays exactly true — this feature changes *when a conversation
stops being hidden*, never what hidden means for the badge.

---

## R10 — Principle VII is NOT engaged

**Decision**: Add no resilience wrapping. No `AddJuggerHubResilience`, no retry, no breaker,
no timeout configuration.

**Rationale**: this feature adds **no outbound network call and no new browser→backend call**.
The auto-un-hide is one local `UPDATE` on a connection the request already holds; the explicit
toggle reuses an endpoint the client already calls for mute. Constitution Quality Gate 8 is
triggered by "any change that adds a network call or an outbound integration" — neither
applies.

Wrapping a local `UPDATE` in retry/breaker is **review-rejectable**, consistent with the same
finding in features 042, 043, 044, 045, 046 and 047.

**The one Principle VII clause that *is* worth a sentence**: `SendAsync` now performs two
sequential database operations (`SaveChangesAsync`, then the `ExecuteUpdateAsync`) outside a
transaction. That is deliberate — see R11.

---

## R11 — Recorded residual: the clear is not transactional with the send

**Decision**: Accept it. Do **not** introduce a transaction or an execution-strategy delegate.

**Rationale**: if the message saves and the flag-clear then fails, the outcome is a delivered
message in a conversation that stayed hidden for its recipients — the status quo before this
feature, recoverable by the very next message or by the manual toggle. Nothing is corrupted
and nothing is lost.

Making it atomic would mean wrapping both in `CreateExecutionStrategy().ExecuteAsync(...)`
with all mutation inside the delegate (Principle VII). That converts the hottest write path in
chat into a retriable transaction to protect a cosmetic flag — a materially worse trade than
the failure it prevents.

**Also accepted**: one extra `UPDATE` round trip per message send, whose `WHERE` matches zero
rows in the common case, on the indexed `ConversationId` prefix.

---

## R12 — Bounds, restated so the plan can be checked against them

| Question | Answer |
|----------|--------|
| New entity / column / migration? | **No** (R1, R2) |
| New endpoint or DTO field? | **No** (R1) |
| New realtime event? | **No** (R5) |
| New dependency? | **No** |
| Hidden-chats inbox surface? | **No** — spec FR-016, issue #222 option 3, follow-up |
| Does access control change? | **No** — spec FR-017; `ChatGuard` never consulted `IsHidden` and still does not |
| Unread/badge semantics change? | **No** — 019 FR-018 stands unamended (R9) |
