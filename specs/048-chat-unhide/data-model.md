# Phase 1 Data Model: Hiding a Chat Is Reversible

**Feature**: 048-chat-unhide | **Date**: 2026-09-09

## Summary

**No entity is added, removed or altered. There is no migration in this feature.**

If a task in `tasks.md` produces an `Add-Migration`, a new entity file, or a change to
`AppDbContext`, it is wrong — stop and re-read [research.md](./research.md) R1/R2.

The whole feature is a change to **when one existing boolean is cleared** and **who can
clear it**.

---

## D1 — The one field involved

`ConversationParticipant.IsHidden` (`backend/Entities/ConversationParticipant.cs:42`),
`boolean NOT NULL`, present since the original chat migration
(`20260716130446_AddChat`).

| Aspect | Before 048 | After 048 |
|--------|-----------|-----------|
| Set to `true` by | `PatchStateAsync` (details panel Hide) | unchanged |
| Set to `false` by | `PatchStateAsync` — reachable in the API, never sent by any client | `PatchStateAsync` (details panel toggle) **and** `SendAsync` on a member message |
| Read by | `VisibleConversations` (L606), `UnreadTotalAsync` (L196), `GetDetailAsync` (L691) | unchanged — all three |
| Meaning | undefined between "archived" and "left" | **archived**: tidied away until somebody writes |

The three readers are deliberately untouched. This feature never changes what "hidden"
*means* to a query; it changes the lifetime of the value.

## D2 — The row's own semantics are unchanged, and one of them constrains us

The entity's XML doc already records that for **Team/Party** conversations this row is
*state only* and carries no authority over access, while for **Direct/Group** the row **is**
the membership (a non-null `LeftDate` fails the membership check).

That second half is what forces the `LeftDate == null` clause in the auto-clear predicate
(research R2): without it, a message in a group would clear the hidden flag on rows belonging
to people who have already left, so rejoining would silently un-hide something they had
archived.

## D3 — State transitions

```text
                    ┌──────────────────────────────────────────┐
                    │  member sends a message (FR-007/FR-012)   │
                    │  — sender and every hidden member alike   │
                    ▼                                          │
              ┌───────────┐   Hide (details panel)      ┌───────────┐
              │  visible  │ ──────────────────────────▶ │  hidden   │
              │ IsHidden  │ ◀────────────────────────── │ IsHidden  │
              │  = false  │   Show in my messages       │  = true   │
              └───────────┘   (details panel, FR-001)   └───────────┘
                                                              │
                              system line — joined / left /    │
                              archived — changes NOTHING ──────┘
                              (FR-011, research R4)
```

**Invariants**

- **I1** — The transition is per player. No transition on one player's row implies a
  transition on another's (FR-006).
- **I2** — `IsMuted` is never written by any path in this feature (FR-005). Hide and mute
  are orthogonal, and all four combinations are reachable and meaningful.
- **I3** — No transition changes who may open, read or write the conversation. `ChatGuard`
  does not consult `IsHidden` before this feature and does not after it (FR-017).
- **I4** — A row that does not exist is equivalent to `IsHidden = false`. The auto-clear must
  therefore never *create* a row (research R2) — there is nothing to clear.

## D4 — Fields explicitly NOT touched

| Field | Why it stays put |
|-------|------------------|
| `IsMuted` | I2 — mute is the separate control; FR-005 |
| `LastReadMessageId` | Un-hiding restores a conversation with its unread state **intact** (FR-001); resetting the marker would silently mark an archive read |
| `LeftDate` | Membership, not preference — read by the auto-clear predicate, written by nothing here |
| `JoinedDate` | The join cutoff (019 FR-051) governs which messages a returned conversation shows; unrelated to hiding |
| `Conversation.LastMessageDate` | Already set by `SendAsync`; a returned conversation orders by it for free |

## D5 — Audit fields

The auto-clear uses `ExecuteUpdateAsync`, which **bypasses the change tracker**, so
`AuditFieldsInterceptor` does not run and `ModifiedDate` must be set in the same
`SetProperty` chain (constitution Principle III; research R2).

The explicit toggle goes through `PatchStateAsync`, which is a **tracked** save
(`state.IsHidden = h; await _db.SaveChangesAsync(ct);`) — the interceptor stamps
`ModifiedDate` there as it already does for mute. Two paths, two correct mechanisms; neither
needs changing beyond what R2 states.
