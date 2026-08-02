# Phase 1 Data Model: Self-Service Account Deletion

**Feature**: 037-account-deletion | **Date**: 2026-08-01

This feature adds **one enum value** and **no new tables**. Its data model is almost entirely a *disposition inventory*: for each table referencing an account, what happens to those rows on erasure, and why.

---

## 1. Schema changes

### `AccountStatus` — one new value

```csharp
public enum AccountStatus
{
    Active = 0,
    Suspended = 1,
    Banned = 2,
    Deleted = 3,   // NEW — feature 037
}
```

`Deleted` is terminal. Unlike `Suspended` and `Banned`, which feature 013 makes fully reversible, there is no transition out of it. The admin reinstate/unban flows must refuse it rather than silently accept it ([AdminUserService.cs:123-135](backend/Services/Admin/AdminUserService.cs#L123-L135) declares its `from:` sets explicitly, so this is a matter of *not* adding `Deleted` to them).

**State transitions**:

```
Active ──────► Deleted   (self-service, this feature)
Suspended ───► ✗ refused (FR-005)
Banned ──────► ✗ refused (FR-005)
Deleted ─────► ✗ nothing; terminal (FR-029)
```

### `User` — no new columns; existing columns neutralised

| Column | After erasure | Why |
|---|---|---|
| `Email`, `NormalizedEmail` | `NULL` | Frees the address for re-registration (FR-031). Postgres allows many `NULL`s in a unique index. |
| `UserName`, `NormalizedUserName` | random non-identifying token | **Load-bearing for FR-031/FR-034, not hygiene.** Registration sets `UserName = email` ([AuthService.cs:97](backend/Services/Auth/AuthService.cs#L97)) and `NormalizedUserName` is **uniquely indexed**, so releasing only the email leaves the username colliding and re-registration fails. Cannot be null. Must **not** derive from the old handle or email — that would be a re-identification vector (FR-026). |
| `PasswordHash`, `SecurityStamp`, `ConcurrencyStamp` | regenerated / `NULL` | Credentials erased (FR-014). |
| `PhoneNumber`, `EmailConfirmed`, `TwoFactorEnabled` | cleared / `false` | Contact data (FR-014). |
| `PreferredLanguage` | `NULL` | An interface preference that exists only to serve the member (FR-017). |
| `Status` | `Deleted` | New terminal state. |
| `StatusChangedAt` | now (UTC) | Existing field; doubles as the erasure timestamp. |
| `Id` | **unchanged** | This is the point. Every `Restrict` FK keeps pointing at a row that identifies nobody. |

**The `Id` surviving is the design.** It is a UUIDv7 that no longer resolves to a person: no name, no handle, no email, no profile. Retained records stay referentially intact while becoming non-attributable.

---

## 2. Disposition inventory

Verified against [AppDbContext.cs](backend/Data/AppDbContext.cs). **Delete** = row removed. **Cascade** = removed automatically as a consequence of deleting `PlayerProfile`. **Retain** = row survives, now pointing at a neutralised account.

### Cascades away with `PlayerProfile` — no explicit code needed

| Table | FK | Notes |
|---|---|---|
| `PlayerProfile` | `User` (Cascade) | **The one explicit delete.** Everything below follows from it. |
| `ProfilePompfe` | `Profile` (Cascade) | Equipment preferences |
| `ProfileAvatar` | `Profile` (Cascade) | Photo bytes today; external object after 035 (see R8) |
| `EventParticipation` | `Profile` (Cascade) | Their activity history |
| `BadgeAward` | `PlayerProfile` (Cascade) | Awards granted **to** them |
| `AchievementAward` | `PlayerProfile` (Cascade) | Awards granted **to** them |

### Deleted explicitly — FK is `Cascade` on `User`, but the row must go before the user row is neutralised

| Table | FK behaviour | Requirement |
|---|---|---|
| `RefreshToken` | Cascade | FR-016 — **delete, not revoke**; carries `CreatedByIp` |
| `Notification` (as `Recipient`) | Cascade | FR-017 |
| `NotificationPreference` | Cascade | FR-017 |
| `TeamMembership` | Cascade | FR-018 |
| `EventSignup` | Cascade | FR-018 |
| `EventAdmin` | Cascade | FR-018 |
| `PartyMember` | Cascade | FR-018 |
| `TrainingResponse` | Cascade | FR-018 |
| `MercenaryListing` | Cascade | FR-018 |
| `MarketRequest` (as `User`) | Cascade | FR-018 |
| `TeamJoinRequest` (as `User`) | Cascade | FR-018 |
| `ConversationParticipant` | Cascade | Membership, not content |
| `UserBlock` (both directions) | **Restrict** | FR-020 — must be deleted explicitly before the user row is touched |
| Identity `UserRoles`/`Claims`/`Logins`/`Tokens` | Cascade | Credentials |

> `UserBlock` is the one row here whose FK is `Restrict`. It is deleted deliberately (FR-020) because the account it guards against no longer exists. Note the interaction with FR-031: a returning member registers a *new* account, so old blocks would not have applied to them anyway.

### Retained, re-attributed to the neutralised account

| Table | FK behaviour | Requirement | What a viewer sees |
|---|---|---|---|
| `ChatMessage` (as `Sender`) | Restrict | FR-024 | Body verbatim, sender = *"A former player"* |
| `TeamNewsPost` (as `Author`) | Restrict | FR-024 | Post verbatim, author = placeholder |
| `EventNewsPost` (as `Author`) | Restrict | FR-024 | Post verbatim, author = placeholder |
| `PartyNewsPost` (as `Author`) | Restrict | FR-024 | Post verbatim, author = placeholder |
| `AdminActionRecord` (Actor/Target) | Restrict | FR-022 | Moderation history intact |
| `BadgeAward.GrantedBy` | Restrict | FR-021 | Someone else's award keeps its provenance |
| `AchievementAward.GrantedBy` | Restrict | FR-021 | ditto |
| `TeamJoinRequest.DecidedBy` | Restrict | FR-021 | A decision about someone else |
| `Party.CreatedBy` | Restrict | FR-021 | Blocked upstream if sole admin (FR-010) |
| `Training.CreatedBy` | Restrict | FR-021 | Retained |
| `Conversation.Requester` | Restrict | FR-021 | Retained |
| `Notification.Actor` | SetNull *(never fires)* | FR-023 | See note below — behaves exactly like the `Restrict` rows |

> **Correction (verified during implementation).** The planning phase described `Notification.Actor`
> as "already nulls itself — the pattern this feature generalises". It does not. `SetNull` fires when
> the referenced **row is deleted**, and erasure never deletes the account row — it neutralises it.
> So `ActorUserId` keeps pointing at the erased account, exactly like `ChatMessage.SenderId`. This is
> correct rather than a defect: the reference resolves to an account that identifies nobody, which is
> what FR-023 asks for. It is recorded because the original claim would mislead anyone reasoning
> about which references survive.

### Invitations — deleted, though the FK restricts

| Table | FK behaviour | Requirement |
|---|---|---|
| `TeamInvitation` (`CreatedBy` / `TargetUser`) | Restrict | FR-019 — pending ones deleted explicitly |
| `EventAdminInvitation` (`CreatedBy` / `TargetUser`) | Restrict | FR-019 |
| `PartyAdminInvitation` (`CreatedBy` / `TargetUser`) | Restrict | FR-019 |

> Only **pending** invitations are deleted. An invitation already accepted has become a membership and is covered above.

---

## 3. The archived-conversation trap — **CORRECTED during implementation**

> **The trap described below does not exist as originally stated.** Verified against
> `ChatConversationService.ArchiveConversationAsync`: the frozen `Conversation.Name` resolves to a
> **team name, event name, or a literal** (`"Team chat"` / `"Party chat"` / `"Event"`) — never a
> member's display name. There are exactly two writes to that column in the codebase: the archival
> freeze, and group-chat creation.
>
> **What is actually true:**
>
> - **Archived auto-chats and inquiry threads** freeze a team/event name → no personal data, nothing
>   to scrub. A direct conversation's name is *derived per viewer* at read time and never stored, so
>   erasure routes it through the existing placeholder.
> - **Group conversations** carry a *user-typed* name, which could contain a person's name
>   ("Ada's crew"). That is user-authored content in a shared space — the same category as message
>   bodies, retained verbatim under **FR-024** and already disclosed by **FR-027**. It is not a
>   separate leak and must not be silently scrubbed, or a group would lose its name because an
>   unrelated member left.
>
> **Consequence**: T024 needs no name-scrubbing pass. T019 remains valuable, but as a
> *regression guard* proving the freeze stays impersonal — not as a fix for a live defect.

### Original (incorrect) analysis, retained for the record

`Conversation` archival ([Conversation.cs:46-55](backend/Entities/Conversation.cs#L46-L55)) **materialises the derived roster into real `ConversationParticipant` rows and freezes the display name into `Conversation.Name`**, then nulls `TeamId`/`PartyId`/`EventId`.

Two consequences for erasure (FR-028):

1. `ConversationParticipant` rows exist for archived threads and cascade on `User` — handled.
2. **`Conversation.Name` is a frozen *string***. For a direct conversation it may contain the member's display name, and no cascade or query filter can reach a string column.

This is the one place the member's identity can survive the entire disposition inventory above. It must be found and neutralised by name, not left to referential integrity.

---

## 4. Validation and invariants

| Invariant | Enforced where |
|---|---|
| Only the account holder can erase their own account | Server-side from the auth principal; never a client-supplied id (FR-002, Principle I) |
| `Suspended`/`Banned` cannot erase | Explicit status check, not the sign-in gate (FR-005) |
| Password re-verified immediately before erasure | `CheckPasswordSignInAsync` with `lockoutOnFailure: true` (FR-003) |
| No blocking obligation outstanding | Precondition query, re-run inside the transaction (FR-013) |
| Erasure is all-or-nothing | Execution strategy + single transaction (FR-038) |
| Repeat/concurrent requests are harmless | Terminal status makes the second attempt a no-op (FR-039) |
| A released address can genuinely register again | Every uniquely-indexed identifier neutralised, not just the email (FR-034) |
| A banned address still cannot register | Its row is *retained*, so registration's email lookup still finds it (FR-032) |
| No retained record can be re-attributed | No surviving column derives from erased identity (FR-026) |
| `Deleted` never becomes visible or contactable | All seven `!= Banned` predicates audited (research R3) |

---

## 5. Migration

One migration, no data movement:

1. Add `Deleted = 3` to `AccountStatus` — an `int` column already, so this is a **code-only enum extension with no schema change**.

There is genuinely nothing else: no new table, no new column, no index. The `StatusChangedAt` column already exists and carries the erasure timestamp.

**FR-041** (record that a deletion occurred without retaining who) is satisfied by the neutralised `User` row itself: `Status = Deleted` plus `StatusChangedAt` is a dated, countable, non-identifying record of the erasure. No separate audit table is warranted, and adding one that named the member would contradict the requirement.

---

## 6. Entity relationship after erasure

```
User (Id retained, all identity columns neutralised, Status = Deleted)
 │
 ├── PlayerProfile ......................... DELETED
 │    ├── ProfilePompfe .................... cascaded
 │    ├── ProfileAvatar ..................... cascaded  (+ external object reclaimed post-commit)
 │    ├── EventParticipation ................ cascaded
 │    ├── BadgeAward (received) ............. cascaded
 │    └── AchievementAward (received) ....... cascaded
 │
 ├── RefreshToken, Notification, NotificationPreference,
 │   TeamMembership, EventSignup, EventAdmin, PartyMember,
 │   TrainingResponse, MercenaryListing, MarketRequest,
 │   TeamJoinRequest, ConversationParticipant, UserBlock,
 │   pending invitations ..................... DELETED
 │
 └── ChatMessage.Sender, *NewsPost.Author,
     AdminActionRecord, *Award.GrantedBy,
     Party/Training/Conversation ownership .... RETAINED
         └── all render as "A former player"
```
