# Phase 1 — Data Model: Events

All new entities derive from `BaseEntity` (`Id` UUIDv7, `CreatedDate`, `ModifiedDate` via the audit interceptor). New enums serialize as **names** (global `JsonStringEnumConverter`). See [research.md](./research.md) for rationale. The existing `EventParticipation` (profile activity) is **unchanged** and deliberately separate from `EventSignup` (research §2).

> **Naming glossary (avoid the three-way confusion).** The spec's Key Entity **"Event Participant"** is implemented as the **`EventSignup`** entity (a live registration: joined / awaiting-approval / waitlisted, subject = user **or** team). It is **distinct** from the pre-existing **`EventParticipation`** (a historical profile-attended-event record that backs recent-activity, unchanged by this feature). Wherever the spec says "participant" for the sign-up flow, the code entity is `EventSignup`.

## Enums (stored as int, serialized as name)

- **`EventType`**: `Tournament = 0`, `Workshop = 1`, `Other = 2`. (`CustomTypeLabel` required iff `Other`.)
- **`LocationKind`**: `InPerson = 0`, `Virtual = 1`.
- **`ParticipantMode`**: `Teams = 0`, `Individuals = 1`.
- **`EventStatus`**: `Published = 0`, `Cancelled = 1`.
- **`SignupStatus`**: `Joined = 0`, `AwaitingApproval = 1`, `Waitlisted = 2`. (Occupied = `Joined` + `AwaitingApproval`.)
- **Reused** (from 005): `InvitationKind` (`Link`/`Targeted`), `InvitationStatus` (`Pending`/`Accepted`/`Declined`/`Revoked`; *Expired* derived from `ExpiresDate`).

## Entities

### Event : BaseEntity  *(EXTENDED — was minimal Name/Date/Location)*

The root object: a hostable event.

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid (UUIDv7) | PK; **the URL identity** `/events/{id}` (research §9) |
| `Name` | string | required, len 3–120 |
| `Type` | `EventType` | required |
| `CustomTypeLabel` | string? | required & len 1–40 **iff** `Type == Other`; else null |
| `Description` | string | required, len 1–4000 |
| `StartsAt` | DateTime (UTC) | required *(replaces old `Date DateOnly`)* |
| `EndsAt` | DateTime (UTC) | required, **`EndsAt >= StartsAt`** (multi-day allowed) |
| `LocationKind` | `LocationKind` | required |
| `VenueName` | string? | in-person optional, len ≤120 |
| `Street` | string? | in-person required, len ≤160 |
| `PostalCode` | string? | in-person required, len ≤20 |
| `City` | string? | in-person required, len ≤120 |
| `Country` | string? | **in-person required**, len ≤80 |
| `VirtualLink` | string? | **virtual required**, absolute URL, len ≤500 |
| `ParticipantMode` | `ParticipantMode` | required; **immutable once any signup exists** (service-guarded) |
| `ParticipationLimit` | int | required, **> 0**; may increase; **may not drop below current occupied count** |
| `IsPaid` | bool | required |
| `FeeAmount` | decimal? | paid optional, ≥ 0 |
| `FeeCurrency` | string(3)? | paid optional, ISO-4217, default `EUR` |
| `FeeRecipientName` | string? | **paid required**, len ≤120 |
| `FeeIban` | string? | **paid required**, len ≤34 (format-checked) |
| `FeePaymentDeadline` | DateOnly? | paid optional; **informational** (no auto-expiry, research §7) |
| `Status` | `EventStatus` | `Published` initially; `Cancelled` is terminal |
| `CancelledDate` | DateTime? (UTC) | set when cancelled |

Navigations: `ICollection<EventSignup> Signups`, `ICollection<EventAdmin> Admins`, `ICollection<EventAdminInvitation> Invitations`, `ICollection<EventContact> Contacts`, `ICollection<EventNewsPost> News`, `ICollection<EventParticipation> Participations` *(existing — activity)*.
Indexes: `StartsAt` (kept, activity ordering).
Location rule (service-guarded): in-person requires `Street`+`PostalCode`+`City`+`Country` and null `VirtualLink`; virtual requires `VirtualLink` and null address parts.

### EventSignup : BaseEntity  *(NEW)*

One live registration by a user (individuals-only) **or** a team (teams-only).

| Field | Type | Rules |
|---|---|---|
| `EventId` | Guid | FK → `Event.Id`, `OnDelete(Cascade)` |
| `UserId` | Guid? | FK → `User.Id`, `OnDelete(Cascade)`; set **iff** individuals-only |
| `TeamId` | Guid? | FK → `Team.Id`, `OnDelete(Cascade)`; set **iff** teams-only |
| `Status` | `SignupStatus` | `Joined` / `AwaitingApproval` / `Waitlisted` |
| `PaymentConfirmedDate` | DateTime? (UTC) | set when an admin approves a paid signup |

Navigations: `Event Event`, `User? User`, `Team? Team`.
Constraints: **`CHECK ((UserId IS NULL) <> (TeamId IS NULL))`** (exactly one subject, research §3).
Indexes: **partial unique `(EventId, UserId) WHERE UserId IS NOT NULL`**; **partial unique `(EventId, TeamId) WHERE TeamId IS NOT NULL`** (no duplicate entry); index `(EventId, Status)` (occupied count + group reads). Waitlist order = `CreatedDate` (arrival; promotion is manual so no stored position — research §4).

### EventAdmin : BaseEntity  *(NEW)*

A user's admin grant over an event. All admins are equal (research §5).

| Field | Type | Rules |
|---|---|---|
| `EventId` | Guid | FK → `Event.Id`, `OnDelete(Cascade)` |
| `UserId` | Guid | FK → `User.Id`, `OnDelete(Cascade)` |
| `AddedDate` | DateTime (UTC) | set at creation |

Navigations: `Event Event`, `User User`.
Indexes: **unique `(EventId, UserId)`**; index `(EventId)` for admin-count (last-admin guard); index `(UserId)` for "events I administer".

### EventAdminInvitation : BaseEntity  *(NEW — mirrors `TeamInvitation`)*

A shared link or targeted invite to **co-administer** an event.

| Field | Type | Rules |
|---|---|---|
| `EventId` | Guid | FK → `Event.Id`, `OnDelete(Cascade)` |
| `Kind` | `InvitationKind` | `Link` or `Targeted` |
| `Token` | string | **unique index**, opaque high-entropy URL-safe (base64url of 32 bytes), stored raw (capability) |
| `Status` | `InvitationStatus` | `Pending` initially |
| `ExpiresDate` | DateTime (UTC) | issued + `InviteLinkTtlDays` (7); *usable* iff `Pending && ExpiresDate > now` |
| `CreatedByUserId` | Guid | FK → `User.Id` (issuing admin), `OnDelete(Restrict)` |
| `TargetUserId` | Guid? | FK → `User.Id` (targeted only; null for link), `OnDelete(Restrict)` |

Navigations: `Event Event`, `User CreatedBy`, `User? TargetUser`.
Indexes: **unique `Token`**; **partial unique `(EventId) WHERE Kind==Link && Status==Pending`** (≤1 active link/event); **partial unique `(EventId, TargetUserId) WHERE Kind==Targeted && Status==Pending`** (no duplicate pending targeted); index `(EventId)`.

### EventContact : BaseEntity  *(NEW)*

A free-form public point of contact.

| Field | Type | Rules |
|---|---|---|
| `EventId` | Guid | FK → `Event.Id`, `OnDelete(Cascade)` |
| `Name` | string | required, len 1–120 |
| `Role` | string | required (free text, e.g. "Location host"), len 1–80 |
| `Phone` | string? | len ≤40; **at least one of Phone/Email required** (service-guarded) |
| `Email` | string? | len ≤256; basic email shape |

Navigations: `Event Event`.
Indexes: index `(EventId)`.

### EventNewsPost : BaseEntity  *(NEW — mirrors `TeamNewsPost`)*

An admin-authored update shown newest-first on the public page.

| Field | Type | Rules |
|---|---|---|
| `EventId` | Guid | FK → `Event.Id`, `OnDelete(Cascade)` |
| `AuthorUserId` | Guid | FK → `User.Id` (an admin at post time), `OnDelete(Restrict)` |
| `Body` | string | required, len 1–2000 |

Navigations: `Event Event`, `User Author`.
Indexes: index `(EventId, CreatedDate)` (newest-first feed).

## Relationships (summary)

```
User 1───* EventAdmin *───1 Event
User 0..1─* EventSignup *─0..1 Team          (exactly one of User/Team per row)
Event 1───* EventSignup / EventContact / EventNewsPost / EventAdminInvitation
Event 1───* EventParticipation  (existing — activity, untouched)
```

Event delete cascades Signups, Admins, Invitations, Contacts, News. `EventParticipation` rows reference `Event` with the existing cascade and are independent of sign-ups.

## Key DTOs (client-facing; Mapster from entities)

- **`CreateEventRequest`** — type + customLabel?, name, description, startsAt, endsAt, locationKind, address parts / virtualLink, participantMode, participationLimit, isPaid, fee fields.
- **`EditEventRequest`** — editable subset (name, description, dates, location, fee, type); **not** participantMode when signups exist; limit ≥ occupied (server-validated).
- **`SignupRequest`** — `teamId?` (required for teams-only; omitted for individuals-only — subject is the caller).
- **`CreateContactRequest`** / **update** — name, role, phone?, email?.
- **`CreateTargetedInviteRequest`** — `targetUserId`.
- **`EventDetailDto` / `EventPublicDto`** — public event fields + `openSpots`/`isFull` + fee block; `EventPublicDto` is the anonymous projection (identical public fields — no admin internals).
- **`ViewerRelationDto`** — for a signed-in viewer: `isAdmin`, `mySignupStatus?` (joined/awaiting/waitlisted/none), `teamsICanEnter[]` (teams-only). Anonymous → all false/empty.
- **`SignupDto`** — id, subject (user handle/displayName **or** team slug/name), status, joinedAt (`CreatedDate`).
- **`EventContactDto`** — id, name, role, phone?, email?.
- **`EventNewsDto`** — id, author displayName, body, createdDate.
- **`EventAdminDto`** — userId, handle, displayName.
- **`EventInvitationDto`** / **`InviteLinkDto`** / **`InvitableUserDto`** / **`InvitePreviewDto`** — mirror the 005 shapes (invitation list item, active-link url+expiry, user-search row with `UserRelation`, anonymous accept preview + `InviteState`).
- **`EventSummaryDto`** — id, name, type, startsAt, locationKind, city/virtual, isFull — reserved for a future events index (not surfaced this iteration).

## Migration — `AddEvents`

Single migration:
1. **Alter `Events`**: drop `Date`; add `StartsAt`, `EndsAt`, `Type`, `CustomTypeLabel`, `Description`, `LocationKind`, `VenueName`, `Street`, `PostalCode`, `City`, `Country`, `VirtualLink`, `ParticipantMode`, `ParticipationLimit`, `IsPaid`, `FeeAmount`, `FeeCurrency`, `FeeRecipientName`, `FeeIban`, `FeePaymentDeadline`, `Status`, `CancelledDate`. **`Name` and `Location` are retained** — `Location` stays as the legacy free-text field that `ActivityItemDto` reads for profile/team activity (the new structured address is separate and used only by the event page); it is **not** folded or dropped.
2. **Create** `EventSignups`, `EventAdmins`, `EventAdminInvitations`, `EventContacts`, `EventNewsPosts` with the indexes/uniques/CHECK above.
3. Auto-applies on startup (existing behaviour). No Prod event data exists; Dev/local reseed via `DevDataSeeder`.

## Seeding (`DevDataSeeder`, Dev/local only)

Regenerate demo events covering the matrix: **in-person paid teams-only** (e.g. a tournament, limit 16, some joined + awaiting + waitlist), **virtual free individuals-only** (a workshop), and a **cancelled** example; plus creator `EventAdmin`, a couple of `EventContacts` (location host, caterer), and `EventNewsPost`s. Keep existing profile/team **activity** working by seeding `EventParticipation` against seeded events (now ordered by `StartsAt`).
