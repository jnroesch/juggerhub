# Data Model: The Team Page Leads Into the Team Chat (060)

**No entity, no column, no index, no migration.** If a task produces an `Add-Migration`, something
is wrong.

## Existing entities this feature reads

| Entity | Relevant fields | Role here |
|--------|-----------------|-----------|
| `Conversation` | `Id`, `Kind`, `TeamId`, `State` | The team chat is the row with `Kind = Team (2)` and `TeamId = team`. At most one per team, enforced by the unique index `IX_Conversations_TeamId` filtered on `"TeamId" IS NOT NULL AND "Kind" = 2` (`AppDbContext.cs:1041`). |
| `Conversation` (inquiry) | `Kind = TeamInquiry (4)`, `TeamId`, `RequesterUserId` | Shares `TeamId` with the team chat (feature 027). **Must never be taken for the team chat** (research R1). |
| `TeamMembership` | `TeamId`, `UserId`, `Role` | Read only through `ChatGuard` (access) and `EnsureAutoChatsForAsync` (what to create). No new read. |
| `ConversationParticipant` | `IsHidden`, `IsMuted` | Per-player flags. **Not written** by this feature (FR-012). |

## Invariants

- **I1 — one team chat per team.** Unchanged, and still enforced in the database (FR-015).
- **I2 — a team chat is found by kind AND team.** `Kind == Team && TeamId == team`, in both the
  existence check that decides creation and the lookup that returns the id (FR-013). The index
  already had it. After this feature, so does the service.
- **I3 — access is ChatGuard's alone.** The resolver's answer to "may this player open it" is
  `ChatGuard.ResolveAsync` and nothing else (FR-004).
- **I4 — a non-member's request creates nothing for the team asked about** (FR-007). Creation is
  the inbox's step, which only covers the caller's own rosters.

## New DTO

```text
TeamChatRefDto
  conversationId: Guid   // the team chat's id; never null (a missing chat is a 404, not a null)
```

Nothing else: no name, no unread count, no member list (FR-006).
