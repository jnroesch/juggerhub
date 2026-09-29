# Data Model: The Party Page Leads Into the Party Chat (063)

**No entity, no column, no index, no migration.** If a task produces an `Add-Migration`, something
is wrong.

## Existing entities this feature reads

| Entity | Relevant fields | Role here |
|--------|-----------------|-----------|
| `Conversation` | `Id`, `Kind`, `PartyId`, `State` | The party chat is the row with `Kind = Party` and `PartyId = party`. At most one per party, enforced by the unique index `IX_Conversations_PartyId` filtered on `"PartyId" IS NOT NULL` (`AppDbContext.cs:1129`). No other kind sets `PartyId` (research R2). |
| `Conversation` (archived) | `State = Archived`, `PartyId = null` | What a disbanded party's chat becomes (019 data-model R3a). Its `PartyId` is cleared, so a lookup by party finds nothing and the answer is 404. It stays readable from the inbox. |
| `PartyMember` | `PartyId`, `UserId`, `Status`, `Role`, `ViaMarket` | The crew is the `Status = In` rows, guests included. Read only through `ChatGuard` (access) and `EnsureAutoChatsForAsync` (what to create). No new read. |
| `ConversationParticipant` | `IsHidden`, `IsMuted` | Per-player flags. **Not written** by this feature (FR-012). |

## Invariants

- **I1 — one party chat per party.** Unchanged, and still enforced in the database (FR-013).
- **I2 — the order lives in one place.** Ensure (caller-scoped) → find (by kind and owner) → guard,
  in the shared `OpenAutoChatAsync`, for teams and parties alike (research R1).
- **I3 — access is ChatGuard's alone.** The resolver's answer to "may this player open it" is
  `ChatGuard.ResolveAsync` and nothing else (FR-004).
- **I4 — a request from outside the crew creates nothing for the party asked about** (FR-007).
  Creation is the inbox's step, which only covers the caller's own crews.

## New DTO

```text
PartyChatRefDto
  conversationId: Guid   // the party chat's id; never null (a missing chat is a 404, not a null)
```

Nothing else: no name, no unread count, no member list (FR-006).
