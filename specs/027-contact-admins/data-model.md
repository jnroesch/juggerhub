# Data Model: Contact the Admins (027)

Extends the feature-019 chat schema. **No new tables.** Two enum members, two nullable columns on
`Conversation`, three index changes, two foreign keys. One EF migration.

## E1 — `ConversationKind` (enum, extend)

Add two members after `Party = 3` (append-only; never renumber — the enum serializes by **name** via
the global `JsonStringEnumConverter`, but existing rows store the integer):

```csharp
public enum ConversationKind
{
    Direct = 0,
    Group = 1,
    Team = 2,
    Party = 3,
    TeamInquiry = 4,   // a player's thread to a team's admins; membership = requester + live team admins
    EventInquiry = 5,  // a player's thread to an event's admins; membership = requester + live event admins
}
```

Both are **mirrored** kinds (like `Team`/`Party`): membership is derived, not stored; participant rows
are per-user state only. Both are non-leavable and non-addable.

## E2 — `Conversation` (entity, extend)

Existing columns unchanged (`Kind`, `Name`, `TeamId`, `PartyId`, `State`, `LastMessageDate`,
`DirectPairKey`). Add:

| Column | Type | Meaning |
|--------|------|---------|
| `EventId` | `Guid?` | The target event for an `EventInquiry`; null for every other kind. **Nulled on archival** (R3a). |
| `RequesterUserId` | `Guid?` | The fixed non-admin player who started the inquiry (`TeamInquiry`/`EventInquiry`); null otherwise. |

Reused for `TeamInquiry`: existing **`TeamId`** = the target team. (No new team column.)

Navigation: add `public Event? Event { get; set; }` and `public User? Requester { get; set; }`.

### Column population per kind

| Kind | `TeamId` | `EventId` | `RequesterUserId` | `Name` | `DirectPairKey` |
|------|---------|-----------|-------------------|--------|-----------------|
| Direct | – | – | – | – | set |
| Group | – | – | – | set | – |
| Team | set | – | – | – (derived) | – |
| Party | – (PartyId) | – | – | – (derived) | – |
| **TeamInquiry** | **set** | – | **set** | – (derived) | – |
| **EventInquiry** | – | **set** | **set** | – (derived) | – |
| *any Archived* | nulled | nulled | retained | frozen | as-was |

## E3 — Indexes (in `AppDbContext.Entity<Conversation>`)

**R1 — Re-scope the existing one-chat-per-team index by kind.** Today:

```csharp
entity.HasIndex(c => c.TeamId).IsUnique().HasFilter("\"TeamId\" IS NOT NULL");
```

`TeamInquiry` rows also carry `TeamId`, so this would wrongly forbid a second inquiry for the same
team. Tighten to the team **chat** only:

```csharp
entity.HasIndex(c => c.TeamId).IsUnique().HasFilter("\"TeamId\" IS NOT NULL AND \"Kind\" = 2");
```

(The `PartyId` and `DirectPairKey` indexes are unchanged — no inquiry kind uses those columns.)

**R2 — One inquiry thread per (player, target).** Two new unique **filtered** indexes; the loser of a
concurrent create collides here and resolves to the winner (service catches `DbUpdateException`):

```csharp
// TeamInquiry = 4
entity.HasIndex(c => new { c.TeamId, c.RequesterUserId })
      .IsUnique().HasFilter("\"Kind\" = 4");
// EventInquiry = 5
entity.HasIndex(c => new { c.EventId, c.RequesterUserId })
      .IsUnique().HasFilter("\"Kind\" = 5");
```

**R3 — Foreign keys, `Restrict` (fail-closed, mirroring the Team/Party FKs).**

```csharp
entity.HasOne(c => c.Event).WithMany()
      .HasForeignKey(c => c.EventId).OnDelete(DeleteBehavior.Restrict);
entity.HasOne(c => c.Requester).WithMany()
      .HasForeignKey(c => c.RequesterUserId).OnDelete(DeleteBehavior.Restrict);
```

`Restrict` on `EventId`/`TeamId` means a delete path that forgets to archive-first fails loudly rather
than orphaning an Active conversation whose membership resolves to nobody. `RequesterUserId` is
`Restrict` too; account deletion is out of this feature's scope and already gated elsewhere.

## M1 — Membership rule (`ChatGuard`)

Extend the single membership predicate — the same expression is used by `ResolveAsync`,
`VisibleConversations` (inbox), and search, so all three stay in lockstep.

`IsMemberOf(userId)` gains two branches (evaluated after the archived-snapshot branch, before/around
the Team/Party branches):

```
TeamInquiry  → c.RequesterUserId == userId
               || db.TeamMemberships.Any(m => m.TeamId == c.TeamId
                                             && m.UserId == userId
                                             && m.Role == TeamRole.Admin)
EventInquiry → c.RequesterUserId == userId
               || db.EventAdmins.Any(a => a.EventId == c.EventId && a.UserId == userId)
```

Archived inquiries fall through the **first** branch (participant snapshot), identical to archived
Team/Party — which is why archival must snapshot before the roster is gone (R3a).

`ChatAccess` record gains `Guid? EventId` and `Guid? RequesterUserId`, plus a helper
`bool IsInquiry => Kind is ConversationKind.TeamInquiry or ConversationKind.EventInquiry`.

## M2 — Participant/fan-out resolution (`ResolveParticipantUserIdsAsync`)

Add cases (used for realtime fan-out, unread recipients, member list):

```
TeamInquiry  → { RequesterUserId } ∪ { team admins (TeamMembership.Role == Admin) }
EventInquiry → { RequesterUserId } ∪ { event admins (EventAdmin) }
Archived     → participant snapshot (existing branch, unchanged)
```

De-duplicate (a requester who is somehow also an admin appears once; but note the requester is by
FR-002 never offered the action when they are an admin).

## M3 — Join cutoff (`ResolveJoinCutoffAsync` + batched)

```
TeamInquiry/EventInquiry, viewer == RequesterUserId → requester's ConversationParticipant.JoinedDate
TeamInquiry, viewer is admin                        → their TeamMembership.JoinedDate
EventInquiry, viewer is admin                       → their EventAdmin.AddedDate
Archived                                            → null (no cutoff; snapshot readable in full)
```

Implements FR-019 (admin sees history from their grant) and FR-051 (requester sees from thread start).

## R3a — Archival snapshot invariant (generalized)

An **Active** inquiry derives membership from the roster; archiving must therefore **snapshot before
detach**, exactly as team/party chats do. Generalize the existing `ArchiveAutoAsync` into a
per-conversation `ArchiveConversationAsync(conversationId)` that, for any derived-membership kind:

1. Resolve the derived roster (`ResolveParticipantUserIdsAsync`) → upsert `ConversationParticipant`
   rows (revive `LeftDate` if present) so membership survives the roster's deletion.
2. Freeze `Name` (team name / event title / player display name as appropriate — a stable label).
3. Null `TeamId` and `EventId` (clears the `Restrict` FK so a subsequent hard delete is not blocked).
4. Set `State = Archived`. Leave `Kind` and `RequesterUserId` intact so the inbox still tags it and the
   history reads as what it was. One-way and idempotent.

Callers:
- `ArchiveForTeamAsync(teamId)` — archive the team chat **and** all `TeamInquiry` for the team.
- New `ArchiveInquiriesForEventAsync(eventId)` — archive all `EventInquiry` for the event.

## State transitions

`Active → Archived` only (one-way), triggered by team delete or event cancel. No inquiry-specific
lifecycle beyond the shared `ConversationState`.

## Validation rules (enforced server-side)

- Requester MUST NOT be an admin of the target at send time (FR-002) — checked in
  `SendFirstInquiryAsync` before create.
- Target team/event MUST exist and be visible/non-cancelled at *first send* (a cancelled event rejects
  new inquiries, consistent with archived = closed to writes).
- Message body validation, length, rate limiting: unchanged from `IChatMessageService.SendAsync` +
  `ChatStart` policy.
