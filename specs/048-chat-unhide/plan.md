# Implementation Plan: Hiding a Chat Is Reversible

**Branch**: `048-chat-unhide` | **Date**: 2026-09-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/048-chat-unhide/spec.md` · GitHub issue **#222**

## Summary

Hiding a conversation is one-way today: there is no un-hide control anywhere in the product,
and nothing on the send path clears the flag — so a hidden **group, team, party or
admin-contact** thread is unreachable through the interface for as long as it stays set.

The owner settled the definition that 019 left open: **hide is an archive** ("tidy away until
something happens") and **mute is "don't bother me"**. Each control gets exactly one job, and
019 FR-026's premise — hide as the stand-in for leaving a roster chat — is rejected outright:
a player who wants out leaves the team, which removes them from the chat by the roster rule.

Two mechanisms deliver it, both small:

1. **Automatic** — a member message clears the hidden flag for every member who had set it,
   the sender included, **before** any unread total is recomputed.
2. **Explicit** — the details panel's Hide becomes a two-state toggle, mirroring mute/unmute.

**No entity, no column, no migration, no endpoint, no DTO field, no realtime event, no
dependency.** The scope is one `ExecuteUpdateAsync` in `SendAsync`, one truthiness fix in
`ChatService.setState`, one component method and one template branch, one i18n key × 3, and
an amendment to 019. A "Hidden chats" inbox surface (#222 option 3) is explicitly out of scope.

**Three facts make this feature much smaller than the issue implies**, all established by
reading the code (see [research.md](./research.md)):

- **The backend can already un-hide.** `PatchStateAsync` applies `isHidden: false` today and
  always has; nothing in the product ever sent it (R1).
- **The client already re-seeds the inbox for a conversation it does not know about.**
  `bumpConversationWithMessage` takes a `!found` branch straight into `loadInbox()` — written
  for 022's lazy DMs, and it describes the un-hide case exactly. FR-008 and FR-010 hold by
  construction with **zero** new frontend code (R5).
- **FR-011 is satisfied by not writing code.** `WriteSystemMessageAsync` already neither
  pushes realtime nor bumps `LastMessageDate`; leaving it alone is the implementation (R4).

## Technical Context

**Language/Version**: C# / .NET 10 (backend) · TypeScript / Angular 22, zoneless (frontend)

**Primary Dependencies**: EF Core + Npgsql, SignalR, Transloco. **No dependency is added.**

**Storage**: PostgreSQL 18 — existing `ConversationParticipants.IsHidden` column, **no schema
change** (see [data-model.md](./data-model.md))

**Testing**: xUnit + Testcontainers (`JuggerHub.Api.IntegrationTests`) · Jest via Nx (`web`)

**Target Platform**: Web (responsive, 375px up)

**Project Type**: Web application — .NET API + Angular SPA

**Performance Goals**: One additional `UPDATE` per message send, `WHERE ConversationId = … AND
IsHidden` on the indexed prefix, matching **zero rows** in the common case (R2, R11)

**Constraints**: The flag must clear **before** `PushMessageToOthersAsync` recomputes each
recipient's badge, or a conversation returns to the inbox with unread messages the nav total
does not count (FR-009, R3)

**Scale/Scope**: ~6 files changed. 1 backend service method, 1 frontend service method, 1
component + template, 3 i18n catalogues, 2 test files, plus the 019 amendment.

## Constitution Check

*GATE: evaluated before Phase 0 and re-evaluated after Phase 1 design. Constitution v1.4.0.*

| Gate | Verdict | Notes |
|------|---------|-------|
| **I. Security-first, never trust the client** | ✅ Pass | No authorisation change. `ChatGuard` never consulted `IsHidden` and still does not (FR-017, I3). The auto-clear is scoped to one `ConversationId` resolved server-side from the message being sent, never from client input. Hidden state stays a per-player preference, not a security boundary. |
| **II. Thin controllers, service-centric** | ✅ Pass | No controller change at all — `PatchState` already forwards to the service. New logic lives in `ChatMessageService`. No object mapper. |
| **III. Disciplined data access** | ⚠️ Pass **with an obligation** | The auto-clear uses `ExecuteUpdateAsync` per the batch-operations rule, which **bypasses the change tracker** — so `ModifiedDate` **must** be set explicitly in the same `SetProperty` chain (D5, R2). This is the single most likely gate failure in the diff. No new entity, no unbounded list, no pagination surface added. |
| **IV. Auth & sessions** | ✅ Pass | Untouched. |
| **V. Environment parity** | ✅ Pass | No config, no secret, no infrastructure, no migration. Identical across local/Dev/Prod by having nothing to differ. |
| **VI. Conventions & tooling** | ✅ Pass | Frontend keeps `.html`/`.css`/`.ts` separate (editing existing files). No `.sh` script added. |
| **VII. Resilient by default** | ✅ **Not engaged** | No outbound call and no new browser→backend call is added. Reaching for `AddJuggerHubResilience`, retry, or a breaker here would wrap a local `UPDATE` and is **review-rejectable** (R10). |
| **Gate 7 — UI/design compliance** | ✅ **Engaged** | The details panel gains a conditional label and a new string in 3 catalogues → instantiate `checklists/ui-review.md` from the template and verify against the diff. |
| **Gate 8 — Resilience review** | ✅ N/A | See VII. |

**Post-Phase-1 re-evaluation**: unchanged. The design added no entity, no endpoint, no
outbound call and no configuration, so no gate moved. The one live obligation is the Principle
III `ModifiedDate` requirement, which is now written into three places (research R2,
data-model D5, and the task that implements it).

**Complexity Tracking**: not required — no violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/048-chat-unhide/
├── plan.md                        # This file
├── spec.md                        # Requirements (FR-001…FR-017, SC-001…SC-007)
├── research.md                    # Phase 0 — R1…R12
├── data-model.md                  # Phase 1 — no schema change; D1…D5
├── quickstart.md                  # Phase 1 — validation scenarios
├── contracts/
│   └── chat-hide-state.md         # Phase 1 — unchanged HTTP contract + C1…C10
├── checklists/
│   ├── requirements.md            # Spec quality — 16/16
│   └── ui-review.md               # Gate 7 — instantiated during implementation
└── tasks.md                       # Phase 2 — /speckit-tasks, NOT created here
```

### Source code (repository root)

```text
backend/
├── Services/Chat/
│   ├── ChatMessageService.cs              # ★ SendAsync — the auto-clear (the only backend edit)
│   ├── ChatConversationService.cs         #   PatchStateAsync already un-hides — UNCHANGED
│   └── ChatGuard.cs                       #   membership only — UNCHANGED
├── Entities/ConversationParticipant.cs    #   UNCHANGED (no migration)
└── tests/JuggerHub.Api.IntegrationTests/Chat/
    └── ChatHideTests.cs                   # ★ new — C1…C10

frontend/apps/web/
├── src/app/core/services/
│   ├── chat.service.ts                    # ★ setState truthiness fix (R6)
│   └── chat.service.spec.ts               # ★ un-hide path facts
├── src/app/features/chat/chat-details/
│   ├── chat-details.component.ts          # ★ hide() → toggleHide()
│   ├── chat-details.component.html        # ★ conditional label + testid
│   └── chat-details.component.spec.ts     # ★ toggle facts
└── public/i18n/{en,de,es}.json            # ★ chat.details.unhide — all three together

specs/019-chat/
├── spec.md                                # ★ Amendments callout; FR-026, FR-029
└── contracts/chat-api.md                  # ★ pointer to this feature
```

**Structure Decision**: the established two-project layout (`backend/` .NET API +
`frontend/apps/web` Angular SPA). This feature adds **no new directory and no new file**
outside tests and its own spec folder — every production edit lands in a file that already
exists.

---

## Implementation approach

### Backend — one statement, in one place, in the right order

In `ChatMessageService.SendAsync`, between `await _db.SaveChangesAsync(ct);` (L131) and the
`ProjectOneAsync` / `PushMessageToOthersAsync` calls:

```csharp
// A message is "something happening": it returns this conversation to the inbox of everyone
// who had archived it — the sender included (FR-007/FR-012). Ordered BEFORE the push below,
// because PushMessageToOthersAsync recomputes each recipient's badge and UnreadTotalAsync
// excludes hidden conversations — clear it afterwards and the row comes back uncounted.
//
// Deliberately NOT shared with WriteSystemMessageAsync: a system line does not return an
// archived conversation (FR-011), just as it already neither pushes nor bumps LastMessageDate.
await _db.ConversationParticipants
    .Where(p => p.ConversationId == conversationId && p.IsHidden && p.LeftDate == null)
    .ExecuteUpdateAsync(s => s
        .SetProperty(p => p.IsHidden, false)
        .SetProperty(p => p.ModifiedDate, DateTime.UtcNow), ct);
```

Four things in that statement are load-bearing:

| Clause | Why |
|--------|-----|
| `&& p.IsHidden` | Keeps the update narrow — it matches zero rows in the common case (R2). |
| `&& p.LeftDate == null` | A group's leaver keeps their row; without this, rejoining silently un-hides what they archived (D2, spec Edge Cases). |
| `SetProperty(ModifiedDate)` | `ExecuteUpdateAsync` bypasses the interceptor — constitution Principle III (D5). |
| Its **position** | Before the push, per FR-009 (R3). |

No other backend file changes. `PatchStateAsync`, `VisibleConversations`, `UnreadTotalAsync`
and `GetDetailAsync` are all correct as they stand.

### Frontend — a real fix and a real toggle

**`ChatService.setState`** — replace the truthiness test with explicit checks and independent
`if`s (R6):

```ts
if (patch.isHidden === true)  { /* drop the row, as today */ }
if (patch.isHidden === false) { this.loadInbox().subscribe({ error: () => undefined }); }
if (patch.isMuted !== undefined) { this.patchConversation(conversationId, { isMuted: patch.isMuted }); }
```

Re-seeding on un-hide reuses the same mechanism R5 relies on rather than inventing a
client-side insert — the server's inbox projection stays the one place a row's shape is
decided.

**`ChatDetailsComponent`** — `hide()` becomes `toggleHide()`, modelled on `toggleMute()`, with
the one asymmetry FR-004 requires: hiding navigates to `/chat` (unchanged), un-hiding updates
the local `detail` signal and stays put.

**Template** — the label becomes `(d.isHidden ? 'chat.details.unhide' : 'chat.details.hide')`,
matching the mute row exactly; `data-testid` becomes `toggle-hide` (verified safe — the old
`hide-chat` id appears only in this template, in no test).

**i18n** — `chat.details.unhide` added to all three catalogues in one change, or
`catalog-parity.spec.ts` goes red (R8).

### Specs — amend 019 the way 022 and 046 did

An `Amended by feature 048` callout in the Amendments section, FR-026 reworded to name **mute**
as the leave substitute, FR-029 given its reversibility clause, and a pointer added to
`contracts/chat-api.md`. **FR-018 is deliberately not touched** — what "hidden" means to the
badge is unchanged; only when a conversation stops being hidden is new (R9).

## Recorded residuals

Accepted, with reasons, so review does not re-litigate them:

1. **The clear is not transactional with the send** (R11). A failure between the two leaves a
   delivered message in a still-hidden conversation — the status quo, self-correcting on the
   next message. Making it atomic would wrap chat's hottest write path in a retriable
   transaction to protect a cosmetic flag.
2. **One extra `UPDATE` per send**, usually matching zero rows (R2, R11).
3. **A hidden conversation is still only reachable by direct link** for the explicit toggle.
   User Story 1 is what serves the player with no link; the fuller answer is #222 option 3,
   deliberately out of scope (FR-016).
4. **An archived conversation accumulating only system lines stays hidden**, and its unread
   count rises unseen (FR-011, spec Edge Cases). That is the point of the decision.
5. **No sweep on release** — conversations hidden before this ships come back on the next
   message or by the toggle.
6. **Whether system lines count toward unread at all** is 019 behaviour, unexamined here and
   deliberately not "fixed" in this feature (R4).

## Follow-ups

- **#242** (was #222 option 3) — a "Hidden chats" list or inbox filter, if hidden chats are observed to
  accumulate. File after this merges, referencing FR-016.
