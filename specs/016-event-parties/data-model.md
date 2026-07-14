# Phase 1 Data Model — Event Parties

All entities derive from `BaseEntity` (`Id` UUIDv7, `CreatedDate`, `ModifiedDate` via the audit
interceptor). One EF Core migration (`AddParties`) creates the four tables, adds `Events.RosterCap`,
and applies the indexes/constraints below. Enums serialize by name (global
`JsonStringEnumConverter`) and are stored as `int`.

## New enums (`Entities/PartyEnums.cs`)

```csharp
public enum PartyStatus        { Open = 0, Applied = 1 }          // Disband = hard delete (no state)
public enum PartyMemberStatus  { In = 0, Declined = 1 }           // "NoResponse" is derived, never stored
public enum PartyMemberRole    { Member = 0, Admin = 1 }
```

`InvitationKind` / `InvitationStatus` are **reused** from `TeamEnums.cs` (as events already do).
Two new `NotificationType` members are appended in `NotificationEnums.cs` and mapped in
`NotificationCategories.For(...)`: `PartyRequest` → `NotificationCategory.InvitesAndRoster`, and
`PartyNews` → `NotificationCategory.TeamNews` (both reuse existing categories — no settings-matrix
change).

## Entity: Party (`Parties`)

The aggregate root — a temporary subset of one **team** for one **event**.

| Field | Type | Notes |
|---|---|---|
| `TeamId` | `Guid` | FK → `Teams`; cascade delete. |
| `EventId` | `Guid` | FK → `Events`; cascade delete. |
| `RosterCap` | `int` | Snapshot of `Event.RosterCap` at formation (≥ 5). |
| `Message` | `string?` | Optional message to the team; max 500. |
| `Status` | `PartyStatus` | `Open` → `Applied` (on apply) → back to `Open` (on withdraw). |
| `EventSignupId` | `Guid?` | FK → `EventSignups`; set when `Applied`, else null. `OnDelete: Restrict` (party controls the signup's lifecycle explicitly). |
| `CreatedByUserId` | `Guid` | The forming team admin (first party admin). FK → Users, Restrict. |

Navigations: `Team`, `Event`, `EventSignup?`, `ICollection<PartyMember> Members`,
`ICollection<PartyNewsPost> News`, `ICollection<PartyAdminInvitation> Invitations`.

**Indexes / constraints**
- Partial-unique `(TeamId, EventId)` → **one party per team per event** (race-safe backstop).
- Unique partial `(EventSignupId)` where `EventSignupId IS NOT NULL` → 1:1 applied↔signup link.
- Index `(TeamId)` for the team-space discovery read.

**Validation (service-side)**
- Formable only by a **team admin** of `TeamId`, only for a **teams-only, non-cancelled, not-ended**
  event, and only if no party already exists for `(TeamId, EventId)`.
- `RosterCap` copied from the event's `RosterCap` (which is ≥ 5).

## Entity: PartyMember (`PartyMembers`)

A team member's relationship to a party. Rows exist **only** for `In` and `Declined`.

| Field | Type | Notes |
|---|---|---|
| `PartyId` | `Guid` | FK → `Parties`; cascade delete. |
| `UserId` | `Guid` | FK → Users; cascade delete. |
| `Status` | `PartyMemberStatus` | `In` or `Declined`. |
| `Role` | `PartyMemberRole` | `Member` or `Admin`. Creator/co-admins = `Admin`. |
| `CreatedDate` (base) | `DateTime` | Arrival order for first-come reopen (In rows). |

Navigations: `Party`, `User`.

**Indexes / constraints**
- Unique `(PartyId, UserId)` → at most one row per member per party.
- Index `(PartyId, Status)` → roster group reads + In-count.

**Derived groups** (not stored):
- **In** = rows with `Status = In` **joined to a current `TeamMembership`** (drops ex-team-members).
- **Declined** = rows with `Status = Declined` joined to current membership.
- **No response** = current `TeamMembership` users with **no** `PartyMember` row (minus none — the
  creator always has a row).

**State transitions**

| Action | Actor | Effect |
|---|---|---|
| Form party | team admin | Insert creator as `In + Admin`. |
| "I'm in" | team member | Upsert `In` (capacity-checked under party-row lock); from `Declined` → `In`. |
| "Can't make it" | team member | Upsert `Declined` (no capacity check). |
| Leave | the member (In) | Delete row → back to no-response; last-admin guarded. |
| Remove | party admin | Delete target row; last-admin guarded; team membership untouched. |
| Accept co-admin invite | invited team member | Upsert `In + Admin` (cap-checked; refuse if full). |

## Entity: PartyNewsPost (`PartyNewsPosts`)

A party update, **private to party members**, deleted on disband (cascade).

| Field | Type | Notes |
|---|---|---|
| `PartyId` | `Guid` | FK → `Parties`; cascade delete. |
| `AuthorUserId` | `Guid` | FK → Users, Restrict. |
| `Body` | `string` | Required, max 1000 (matches `TeamNewsPost`). |

**Indexes**: `(PartyId, CreatedDate)` for newest-first paging.

**Validation**: compose = party admin only; read = **crew only** (an `In` `PartyMember` row) —
decliners, non-member team members, and outsiders → 404/forbidden.

**Side effect**: posting fans out `NotificationType.PartyNews` (in-app + email) to every `In`
member except the author, via `INotificationService.CreateManyAsync` (dedupe `party-news:{postId}`)
and `PartyEmailService`.

## Entity: PartyAdminInvitation (`PartyAdminInvitations`)

Co-admin invite — a shared **link** or a **targeted** invite bound to one **team member**. Mirrors
`EventAdminInvitation` field-for-field; accepting grants `Admin` role on the invitee's `PartyMember`.

| Field | Type | Notes |
|---|---|---|
| `PartyId` | `Guid` | FK → `Parties`; cascade delete. |
| `Kind` | `InvitationKind` | `Link` or `Targeted`. |
| `Token` | `string` | Opaque, URL-safe, max 64; unique. |
| `Status` | `InvitationStatus` | `Pending`/`Accepted`/`Declined`/`Revoked` (Expired derived from `ExpiresDate`). |
| `ExpiresDate` | `DateTime` | Issued + TTL (reuse `EventOptions.InviteLinkTtlDays`, default 7). |
| `CreatedByUserId` | `Guid` | Issuing party admin. FK → Users, Restrict. |
| `TargetUserId` | `Guid?` | Targeted only; must be a **current member of the party's team**. FK → Users, Restrict. |

**Indexes** (identical shape to `EventAdminInvitation`)
- Unique `(Token)`.
- Index `(PartyId)`.
- Unique partial `(PartyId)` where `Kind = 0 AND Status = 0` → one active link per party.
- Unique partial `(PartyId, TargetUserId)` where `Kind = 1 AND Status = 0` → one pending targeted invite per (party, user).

**Validation**: issue/revoke = party admin; target must be a team member and not already a party
admin; acceptance is team-membership-checked and cap-checked.

## Modified entity: Event (`Events`)

| New field | Type | Notes |
|---|---|---|
| `RosterCap` | `int?` | Players-per-team cap for **teams-only** events; set at creation, default 8, **minimum 5**. Null for individuals-only events. No DB check for the ≥ 5 floor (service-enforced), consistent with how other event bounds are validated. |

The event-create wizard's "who can join" step captures it when mode = teams; existing event fields
are unchanged. `EventSignupService.SignupAsync`'s teams-only branch is **removed** — a team subject on
the public signup endpoint is refused with guidance to form a party.

## Relationship summary

```text
Team 1───* Party *───1 Event
                │
                ├──* PartyMember *──1 User        (In/Declined; NoResponse derived from TeamMembership)
                ├──* PartyNewsPost                (private; cascade delete)
                ├──* PartyAdminInvitation         (team-scoped co-admin)
                └──0..1 EventSignup(TeamId)       (the applied entry; feature 006 flow)
```

## Migration notes (`AddParties`)

1. `alter table "Events" add column "RosterCap" integer null;`
2. Create `Parties`, `PartyMembers`, `PartyNewsPosts`, `PartyAdminInvitations` with the FKs, cascade
   rules, and the partial-unique indexes above.
3. No data backfill (no teams-only sign-ups exist yet — see spec Assumptions / clarification).
4. Follow the repo convention: generate via `dotnet ef migrations add AddParties` and copy
   `Directory.Build.props` + `.editorconfig` before `dotnet restore` (per branch 027 fix noted in
   recent history) if building the migration in a fresh tooling context.
