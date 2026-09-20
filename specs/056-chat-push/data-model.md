# Phase 1 Data Model: Chat Push Notifications

**Feature**: 056-chat-push | **Date**: 2026-09-20 | **Research**: [research.md](./research.md)

**One column, one migration, no new entity.** If a task produces a second `Add-Migration` or a new
class under `backend/Entities/`, something has gone wrong.

---

## D1. `ChatMessage.PushConsideredAt` (new column)

| | |
|---|---|
| Column | `"PushConsideredAt" timestamptz NULL` |
| Entity | `backend/Entities/ChatMessage.cs` |
| Written by | the background worker only — never by `SendAsync`, never by a request |
| Default | `NULL`, meaning "not yet looked at" |

```csharp
/// <summary>
/// When the chat push worker looked at this message (feature 056). NULL means it has not been
/// looked at yet; a value means a decision was taken — which is NOT the same as a notification
/// having been sent.
/// </summary>
/// <remarks>
/// <para>
/// <b>This records that the question was asked, never the answer.</b> Most marked messages are
/// marked without anything being delivered: everyone had read it, everyone had muted the
/// conversation, or nobody had a device. Storing the outcome would be storing who was notified
/// about what, which is a record about members that this feature has no use for.
/// </para>
/// <para>
/// <b>The worker MUST mark every message it selects, including the ones it sends nothing for.</b>
/// The partial index below covers exactly the rows where this is NULL, so a message that is
/// selected and left unmarked stays in that index for ever and the index stops being small.
/// </para>
/// <para>
/// Set through <c>ExecuteUpdateAsync</c>, which bypasses the change tracker, so
/// <c>ModifiedDate</c> is set in the same statement (constitution III). That is accurate here
/// rather than a formality — the row did change — and nothing renders it: no chat DTO carries
/// ModifiedDate and feature 019 ships no message editing.
/// </para>
/// </remarks>
public DateTime? PushConsideredAt { get; set; }
```

### Index

```text
IX_ChatMessages_PushConsideredAt_Pending
  ON "ChatMessages" ("CreatedDate")
  WHERE "PushConsideredAt" IS NULL
```

Partial on purpose. The worker's only query is *"unconsidered messages older than the quiet
delay"*, and with the mark-everything discipline the index holds roughly the last quiet-delay's
worth of messages plus whatever a stopped worker left behind. A full index on the column would
cover every message ever sent to serve a query that only ever wants the newest handful.

### Migration

`AddChatMessagePushConsideredAt` — one column, one partial index, and **one backfill**:

```sql
UPDATE "ChatMessages" SET "PushConsideredAt" = now() WHERE "PushConsideredAt" IS NULL;
```

**The backfill is load-bearing, not tidiness.** Without it every message that already exists is
`NULL` for ever: they fail the max-age filter so the worker never selects them, never marks them,
and they sit in the partial index permanently — which is the one thing the partial index exists to
prevent. It also stops the first pass after deployment from considering the entire message history.

Marking history as "already considered" is exactly right: nobody is owed a notification about a
conversation from last week.

---

## D2. `NotificationCategory.Chat` (new enum member, **no migration**)

```csharp
/// <summary>
/// New chat messages (feature 056). Unlike the four above, this category has NO
/// <see cref="NotificationType"/> mapped to it and never will: chat writes no notification rows
/// (feature 019, FR-051). It exists so a member can say "not chat, on my phone" in the same place
/// they say it about everything else, and it governs the Push channel only.
/// </summary>
Chat = 4,
```

No migration is needed and that is a property of the model, not luck: `NotificationPreference` is
**sparse** — a row exists only for a cell a member has explicitly changed, and an absent row means
on. Feature 055 relied on the same fact to add a third channel. A fifth category adds possible
cells and touches no stored row.

**Ordering**: appended, never inserted. The stored values are integers and renumbering an existing
member would silently re-point every stored preference row.

---

## D3. `NotificationCategories.Supports` (new pure function, no state)

```csharp
/// <summary>
/// Whether a category can be delivered on a channel at all. <see cref="NotificationCategory.Chat"/>
/// is Push-only: it has no in-app row by design (feature 019, FR-051) and no email producer.
/// </summary>
/// <remarks>
/// One home for the rule, three readers: the matrix says which cells exist, the controller refuses
/// to store a cell that does not, and the category's description copy explains the empty ones. A
/// stored <c>(Chat, Email)</c> row would mean nothing and would be readable back as if it meant
/// something.
/// </remarks>
public static bool Supports(NotificationCategory category, NotificationChannel channel) =>
    category != NotificationCategory.Chat || channel == NotificationChannel.Push;
```

Written as "everything is supported unless stated" so that adding a category defaults to the
permissive, visible behaviour rather than to a silently missing toggle.

---

## D4. What is deliberately **not** stored

| Not stored | Why |
|---|---|
| Who was notified, and when | `PushConsideredAt` records that a decision was taken, not its outcome (D1). A delivery log would be a per-member record of which conversations they were away from. |
| A pending/outbox row per message | R2: the column achieves the same without an insert on the hottest write path. |
| A per-participant "last pushed" marker | R2: participant rows are lazy for Team/Party, so for most members there is nowhere to put it. |
| A dedupe table | The collapse tag already makes a repeat replace rather than stack, exactly as feature 055 established (its dedupe table and Redis `SETNX` were both declined for the same reason). |
| Any message text, anywhere outside `ChatMessages.BodyCipher` | The preview is composed in memory and handed to the dispatcher. It is never persisted, never cached and never logged (FR-021c). |

---

## D5. Account deletion and retention

**Nothing to add to either, and that is worth stating because both look like they need a line.**

- `AccountDeletionService.EraseOwnedDataAsync` — no change. The new column lives on `ChatMessages`,
  which 037 already handles under its existing chat rules (messages survive attributed to "a former
  player"); the column holds a timestamp about the platform's own bookkeeping, not about a member.
  Feature 041 recorded the opposite hazard — a table that *looks* like it belongs in that method
  and must not be added. This is the mirror: a column that genuinely needs nothing.
- `IRetentionSweep` — no new sweep. The column does not accumulate anything; it is a flag on rows
  whose own retention is 019's (messages are kept indefinitely, stated there). `StalePushSubscription
  Sweep` already covers the device rows this feature delivers to.
