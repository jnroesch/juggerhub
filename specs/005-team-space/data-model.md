# Phase 1 — Data Model: Team Space & Member Handling

All new entities derive from `BaseEntity` (`Id` UUIDv7, `CreatedDate`, `ModifiedDate` via the audit interceptor). New enums serialize as **names** (global `JsonStringEnumConverter`). See [research.md](./research.md) for rationale.

## Enums (fixed catalogs, stored as int, serialized as name)

- **`TeamType`**: `CityTeam = 0`, `Mixteam = 1`.
- **`TeamRole`**: `Member = 0`, `Admin = 1`.
- **`InvitationKind`**: `Link = 0`, `Targeted = 1`.
- **`InvitationStatus`**: `Pending = 0`, `Accepted = 1`, `Declined = 2`, `Revoked = 3`. (*Expired* is derived from `ExpiresDate`, not stored.)

## Entities

### Team : BaseEntity
The group players belong to.

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid (UUIDv7) | PK (BaseEntity) |
| `Slug` | string | **unique index**, required, `^[a-z0-9]+(?:-[a-z0-9]+)*$`, len 3–30, reserved-set excluded, **immutable** (init-only; no update path) |
| `Name` | string | required, len 2–50; **not unique** |
| `Type` | `TeamType` | required |
| `City` | string? | len 1–80; **required when `Type == CityTeam`**, **null when `Mixteam`** (service-guarded) |

Navigations: `ICollection<TeamMembership> Memberships`, `ICollection<TeamInvitation> Invitations`, `ICollection<TeamNewsPost> News`.
Indexes: **unique `Slug`**.
(Deferred: `LogoAvatar`/cover — not modeled this iteration.)

### TeamMembership : BaseEntity
A user's membership + role in a team.

| Field | Type | Rules |
|---|---|---|
| `TeamId` | Guid | FK → `Team.Id`, `OnDelete(Cascade)` |
| `UserId` | Guid | FK → `User.Id`, `OnDelete(Cascade)` |
| `Role` | `TeamRole` | required; a team always has ≥ 1 `Admin` (last-admin guard) |
| `JoinedDate` | DateTime (UTC) | set at creation |

Navigations: `Team Team`, `User User` (→ `User.Profile` for display).
Indexes: **unique (`TeamId`, `UserId`)**; index (`TeamId`, `Role`) for admin-count checks; index (`UserId`) for "my teams".

### TeamInvitation : BaseEntity
A shared link or a targeted invite to join a team.

| Field | Type | Rules |
|---|---|---|
| `TeamId` | Guid | FK → `Team.Id`, `OnDelete(Cascade)` |
| `Kind` | `InvitationKind` | `Link` or `Targeted` |
| `Token` | string | **unique index**, opaque high-entropy URL-safe (≥128-bit base64url), stored raw (capability; §4) |
| `Status` | `InvitationStatus` | `Pending` initially |
| `ExpiresDate` | DateTime (UTC) | issued + 7 days; *usable* iff `Pending && ExpiresDate > now` |
| `CreatedByUserId` | Guid | FK → `User.Id` (issuing admin), `OnDelete(Restrict/NoAction)` |
| `TargetUserId` | Guid? | FK → `User.Id` (targeted only; null for link), `OnDelete(Cascade)` |

Navigations: `Team Team`, `User CreatedBy`, `User? TargetUser`.
Indexes: **unique `Token`**; **partial unique (`TeamId`) WHERE `Kind == Link && Status == Pending`** (≤ 1 active link/team); **partial unique (`TeamId`, `TargetUserId`) WHERE `Kind == Targeted && Status == Pending`** (no duplicate pending targeted invite); index (`TeamId`).

### TeamNewsPost : BaseEntity
A team update shown in the read-only News feed (composer deferred).

| Field | Type | Rules |
|---|---|---|
| `TeamId` | Guid | FK → `Team.Id`, `OnDelete(Cascade)` |
| `AuthorUserId` | Guid | FK → `User.Id`, `OnDelete(Restrict/NoAction)` |
| `Body` | string | required, len 1–1000 |

Navigations: `Team Team`, `User Author`. Author's **role** rendered from their current `TeamMembership`. Ordered by `CreatedDate` desc. Index (`TeamId`, `CreatedDate`).

### EventParticipation : BaseEntity — **EXTENDED**
Add a real team reference; keep the label as a display snapshot.

| Field | Type | Rules |
|---|---|---|
| `ProfileId` | Guid | (existing) FK → `PlayerProfile.Id`, `OnDelete(Cascade)` |
| `EventId` | Guid | (existing) FK → `Event.Id`, `OnDelete(Cascade)` |
| `TeamLabel` | string | (existing) len 1–80 — now a **display snapshot** (survives team deletion) |
| `TeamId` | Guid? | **NEW** FK → `Team.Id`, **`OnDelete(SetNull)`** (preserve history — FR-036) |

Indexes: (existing) unique (`ProfileId`, `EventId`); **NEW** index (`TeamId`) for team activity queries.
`Event` is unchanged.

## Relationships

```
User (Identity) ──1:1── PlayerProfile ──1:many── EventParticipation ──many:1── Event
   │                                                     │
   │                                                     └── many:0..1 ── Team   (TeamId nullable, SetNull)
   │                                                                       │
   ├──1:many── TeamMembership ───────────────────────────many:1───────────┤ (unique TeamId,UserId; ≥1 Admin)
   ├──1:many── TeamInvitation (CreatedBy)  ─────────────  many:1 ──────────┤ (≤1 active Link; ≤1 pending Targeted/user)
   ├──0:many── TeamInvitation (TargetUser) ─────────────  many:1 ──────────┤
   └──1:many── TeamNewsPost (Author) ──────────────────── many:1 ──────────┘
```

## DTOs

**Requests**
- `CreateTeamRequest`: `Name` (2–50), `Slug` (format), `Type` (`TeamType`), `City?` (required iff CityTeam).
- `CreateTargetedInviteRequest`: `UserId`.
- `SetMemberRoleRequest`: `Role` (`TeamRole`).
- (Link create/rotate, revoke, accept, decline carry no body beyond the route token/id.)

**Responses**
- `TeamDetailDto` (members-only header): `slug`, `name`, `type`, `city?`, `memberCount`, `myRole` (`TeamRole`).
- `TeamPublicDto` (anonymous): `slug`, `name`, `type`, `city?`, `memberCount`. *(No roster/news.)*
- `TeamMemberDto` (roster item): `userId`, `handle`, `displayName`, `role`, `hasAvatar`, `pompfen: Pompfe[]`.
- `TeamNewsDto`: `authorDisplayName`, `authorHandle`, `authorRole` (`TeamRole`), `createdDate`, `body`.
- `ActivityItemDto` (**reused, unchanged**): `eventName`, `date`, `location`, `teamLabel`.
- `TeamInvitationDto` (admin list): `id`, `kind` (`InvitationKind`), `targetDisplayName?`, `createdDate`, `expiresDate`, `status` (`InvitationStatus`).
- `InviteLinkDto`: `url` (`{FrontendBaseUrl}/join/{slug}/{token}`), `token`, `expiresDate`.
- `InvitableUserDto` (user search): `userId`, `handle`, `displayName`, `city?` (hometown), `relation` (`Invitable`/`Invited`/`Member`).
- `InvitePreviewDto` (anonymous): `teamName`, `teamSlug`, `type`, `city?`, `memberCount`, `inviterDisplayName`, `state` (`Usable`/`Expired`/`Invalid`).
- `AcceptInviteResultDto`: `teamSlug` (on success).
- `SlugAvailabilityDto`: `slug`, `normalized`, `available`, `reason?`.

All list responses use the shared `PagedResult<T>` envelope; all reads use `.Select`/`ProjectToType` + `AsNoTracking`. Public/preview DTOs are produced by **projected queries** so internal fields (roster/news) are never loaded for public callers.

## Validation summary (server-side, enforced)

| Rule | Where |
|---|---|
| Slug format + reserved + length | `TeamSlugPolicy` (create + availability) |
| Slug uniqueness (race-safe) | unique index + pre-check |
| Slug immutable | no update path (asserted by test) |
| Name 2–50 (non-unique) | DTO annotations + guard |
| City required iff CityTeam / absent iff Mixteam | `TeamService.CreateAsync` guard |
| Invite creation / revoke / role change / remove / delete / news read | membership + `Admin` role checks (server-side) |
| Team-internal reads (roster/news) refused to non-members | member check → 404 for non-members |
| ≥ 1 admin always remains | transactional admin-count guard → 409 `LastAdmin` |
| No duplicate membership | unique (TeamId, UserId) |
| ≤ 1 active link / team | partial unique index |
| ≤ 1 pending targeted invite / user / team | partial unique index |
| Invite usable iff Pending && not expired | derived check on accept/preview |
| All lists paginated | `PaginationRequest` / `PagedResult<T>` |
| Event history preserved on delete | `EventParticipation.TeamId` `OnDelete(SetNull)` |

## Migration

Single EF migration `AddTeams`:
- Create `Teams` (unique `Slug`), `TeamMemberships` (unique `TeamId,UserId`; index `TeamId,Role`; index `UserId`), `TeamInvitations` (unique `Token`; partial unique active-link; partial unique pending-targeted; index `TeamId`), `TeamNewsPosts` (index `TeamId,CreatedDate`).
- Alter `EventParticipations`: add nullable `TeamId` FK (`OnDelete SetNull`) + index.
- Auto-applies on startup (existing convention).

**Seeding (Development only)**: extend `DevDataSeeder` to create a couple of demo teams (one CityTeam "Rheinfeuer" + one Mixteam), enroll seeded profiles as members with a mix of roles, backfill `EventParticipation.TeamId` to a demo team so team **Activity** is demonstrable, and add a few `TeamNewsPost` rows so the **News** feed renders. Never runs outside Development.
