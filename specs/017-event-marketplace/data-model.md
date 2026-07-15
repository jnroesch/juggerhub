# Phase 1 Data Model: Event Marketplace (Mercenaries)

One EF Core migration — **`AddEventMarketplace`** — adds two tables and two column-sets. All new
entities derive from `BaseEntity` (`Id` UUIDv7, `CreatedDate`, `ModifiedDate` via the audit
interceptor). Enums serialize as their **name** (global `JsonStringEnumConverter`) but store as `int`.
Positions are Postgres `int[]` of `Pompfe`.

## New entity: `MercenaryListing`

A free agent's public post for one event. At most **one live listing per (user, event)** (hard-deleted
on take-down or on join).

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | BaseEntity (UUIDv7) |
| `EventId` | `Guid` | FK → `Events`. `OnDelete(Cascade)`. |
| `UserId` | `Guid` | FK → `AspNetUsers`. `OnDelete(Cascade)`. |
| `Positions` | `int[]` (`List<Pompfe>`) | Positions the player offers; 1..7 values, deduped. |
| `Pitch` | `string` | Required, ≤ 280 (matches profile bio length). |
| `Event` | nav | |
| `User` | nav | |

**Indexes / constraints**

- **Unique** `(UserId, EventId)` — one listing per user per event (race-safe backstop).
- Index `(EventId)` — the board's free-agents read scans an event's listings.

## New entity: `MarketRequest`

The two-way handshake between a party and a user. `Direction` sets who accepts vs. revokes; terminal
rows are retained so the inbox can show "declined".

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | BaseEntity |
| `PartyId` | `Guid` | FK → `Parties`. `OnDelete(Cascade)` (disband cascades requests). |
| `UserId` | `Guid` | The mercenary. FK → `AspNetUsers`. `OnDelete(Cascade)`. |
| `Direction` | `MarketRequestDirection` | `Application` (user→party) / `Invite` (party→user). |
| `Positions` | `int[]` (`List<Pompfe>`) | What the applicant would play / the invite's asked position(s). |
| `Status` | `MarketRequestStatus` | `Pending` / `Accepted` / `Declined` / `Revoked`. |
| `CreatedByUserId` | `Guid` | Initiator (the applicant, or the inviting admin). FK → `AspNetUsers` `Restrict`. |
| `Party` | nav | |
| `User` | nav | |

**Indexes / constraints**

- **Filtered unique** `(PartyId, UserId) WHERE "Status" = 0` (Pending) — at most one active request per
  pair; a fresh request after a decline/revoke is allowed. Mirrors the 016 targeted-invite filter.
- Index `(PartyId, Status)` — the recruiting inbox reads a party's pending applications/invites.
- Index `(UserId, Status)` — the mercenary inbox and the dashboard summary read a user's requests.

## Enums: `MarketEnums.cs`

```csharp
public enum MarketRequestDirection { Application = 0, Invite = 1 }
public enum MarketRequestStatus   { Pending = 0, Accepted = 1, Declined = 2, Revoked = 3 }
```

`Revoked` covers both an initiator's explicit revoke/withdraw and the system auto-cancel of a joiner's
other pending requests (research §6). No `Cancelled` value is added — both read as "dropped off".

## Edited entity: `Party` (+ recruiting)

New nullable/defaulted columns (no impact on existing rows; default not-recruiting):

| Field | Type | Notes |
|---|---|---|
| `IsRecruiting` | `bool` | Default **false**. Public board visibility. |
| `SpotsAdvertised` | `int` | Default 0. The admin's stated "looking for N" (display; real gate is `RosterCap − InCount`). Clamp 0..RosterCap. |
| `RecruitBlurb` | `string?` | ≤ 500. Optional board copy. |
| `PositionsNeeded` | `int[]` (`List<Pompfe>`) | Positions the party needs (board filter + card). |

Recruiting is togglable only while the party exists and the event is open (not cancelled/ended).

## Edited entity: `PartyMember` (+ guest marker)

| Field | Type | Notes |
|---|---|---|
| `ViaMarket` | `bool` | Default **false**. True for an accepted mercenary (a guest — In, `Role = Member`, not a team member). Renders the "guest · via market" tag and is OR-ed into the In-count/roster predicate. |

No new index — existing `(PartyId, Status)` covers roster reads; `ViaMarket` is a projected flag.

## Edited enum: `NotificationType` (+ `MarketInvite`)

Append `MarketInvite = 5` and map it in `NotificationCategories.For(...)` to
`NotificationCategory.InvitesAndRoster` (alongside `TeamInvite`/`PartyRequest`). Extensible append — no
migration of existing sparse preference rows (feature 011).

## Guest reconciliation (read-path edits, no schema change)

- `PartyService.ProjectAsync`: `InCount`/`DeclinedCount` predicate becomes
  `m.Status == … && (m.ViaMarket || Team.Memberships.Any(tm => tm.UserId == m.UserId))`.
- `PartyRosterService.ListGroupAsync` (In/Declined branches) and `LoadMineAsync`: same predicate; add
  `m.ViaMarket` to the `PartyMemberDto` projection. `NoResponse` branch unchanged (team-derived).
- `PartyMemberDto` gains `bool ViaMarket` (existing rows serialize `false`).

## State transitions

**MercenaryListing**: (none) → **live** (post) → edited (edit) → **removed** (take-down *or* owner
joins any party for the event). Removed is a hard delete.

**MarketRequest**: → **Pending** (apply / invite) → **Accepted** (recipient accepts, seats the guest)
| **Declined** (recipient declines) | **Revoked** (initiator revokes *or* system auto-cancel on the
user joining another party *or* party disband cascade-delete). Only `Pending` is actionable; a new
`Pending` may follow a terminal one while the user stays eligible.

**Party.IsRecruiting**: false ⇄ true (party admin toggle); forced-inert when the event is
cancelled/ended or the party is disbanded.

## Migration notes (`AddEventMarketplace`)

1. `CreateTable("MercenaryListings")` + unique `(UserId, EventId)` + index `(EventId)`.
2. `CreateTable("MarketRequests")` + filtered-unique `(PartyId, UserId) WHERE "Status" = 0` + indexes
   `(PartyId, Status)`, `(UserId, Status)`.
3. `AddColumn` on `Parties`: `IsRecruiting bool NOT NULL DEFAULT false`, `SpotsAdvertised int NOT NULL
   DEFAULT 0`, `RecruitBlurb text NULL`, `PositionsNeeded integer[] NOT NULL DEFAULT '{}'`.
4. `AddColumn` on `PartyMembers`: `ViaMarket bool NOT NULL DEFAULT false`.
5. No data backfill (feature ships before any real listings exist). `int[]` columns default to `'{}'`.

Filtered unique indexes are declared in `AppDbContext.OnModelCreating` with `.HasFilter("\"Status\" =
0")`, exactly like `PartyAdminInvitation`'s pending-invite filters.
