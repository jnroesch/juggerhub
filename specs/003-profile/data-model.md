# Phase 1 — Data Model: Player Profile & Public Share Link

All new entities derive from `BaseEntity` (`Id` UUIDv7, `CreatedDate`, `ModifiedDate` via the audit interceptor). `User` is the existing Identity aggregate; profile data lives in `BaseEntity`-derived domain entities to keep Identity clean. See [research.md](./research.md) for the rationale behind each choice.

## Entities

### PlayerProfile : BaseEntity
The public-facing identity for an account. **1:1 with `User`.**

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid (UUIDv7) | PK (BaseEntity) |
| `UserId` | Guid | FK → `User.Id`, **unique** (1:1), `OnDelete(Cascade)` |
| `Handle` | string | **unique index**, required, `[a-z0-9]` + single hyphens, len 3–30, **immutable** (init-only; no update path) |
| `DisplayName` | string | required, len 1–50; defaults to `Handle` at creation |
| `Hometown` | string? | len 0–80 |
| `Description` | string? | len 0–280 |

Navigations: `User User`, `ICollection<ProfilePompfe> Pompfen`, `ProfileAvatar? Avatar`, `ICollection<EventParticipation> Participations`.

Indexes: unique `Handle`; unique `UserId`.

### ProfilePompfe : BaseEntity
One selected pompfe/position for a profile (set membership).

| Field | Type | Rules |
|---|---|---|
| `ProfileId` | Guid | FK → `PlayerProfile.Id`, `OnDelete(Cascade)` |
| `Pompfe` | `Pompfe` (enum, stored int) | required |

Indexes: **unique (`ProfileId`, `Pompfe`)** — no duplicate selection.

### ProfileAvatar : BaseEntity
The profile picture bytes, kept out of the profile row so profile projections stay lean. **1:1 optional with `PlayerProfile`.**

| Field | Type | Rules |
|---|---|---|
| `ProfileId` | Guid | FK → `PlayerProfile.Id`, **unique**, `OnDelete(Cascade)` |
| `ContentType` | string | one of `image/png`, `image/jpeg`, `image/webp` (validated by magic-byte sniff, not just declared) |
| `Bytes` | byte[] (`bytea`) | ≤ `Profile:MaxAvatarBytes` (default ~2 MB) |

### Event : BaseEntity
Minimal Jugger event record. Foundation for later event features; **no management UI in this feature** (seeded in local/dev).

| Field | Type | Rules |
|---|---|---|
| `Name` | string | required, len 1–120 |
| `Date` | DateOnly | required |
| `Location` | string | required, len 1–120 |

### EventParticipation : BaseEntity
Records that a player took part in an event with a team. Basis for recent activity.

| Field | Type | Rules |
|---|---|---|
| `ProfileId` | Guid | FK → `PlayerProfile.Id`, `OnDelete(Cascade)` |
| `EventId` | Guid | FK → `Event.Id`, `OnDelete(Cascade)` |
| `TeamLabel` | string | required, len 1–80; lightweight label until a real Team model exists |

Indexes: (`ProfileId`) and composite (`ProfileId`, `EventId`) unique (a player participates in an event once). Activity query orders by joined `Event.Date` desc.

### Pompfe (enum — fixed catalog, not a table)
`Stab = 0, Langpompfe = 1, Schild = 2, QTip = 3, Kette = 4, DoppelKurz = 5, Laeufer = 6`.
Display labels (DE / EN) live in the frontend catalog: Stab/Staff, Langpompfe/Long, Schild/Shield, Q-Tip/Q-Tip, Kette/Chain, Doppel-Kurz/Double-Short, Läufer/Runner.

### Team — **not modeled** this feature
Referenced only as `EventParticipation.TeamLabel` (a string) and shown in the Teams/Badges UI stub sections. A future feature replaces the label with a real `TeamId` FK without changing DTO shapes.

## Relationships

```
User (Identity) ──1:1── PlayerProfile ──1:0..1── ProfileAvatar
                              │
                              ├──1:many── ProfilePompfe        (unique per Pompfe)
                              │
                              └──1:many── EventParticipation ──many:1── Event
```

## DTOs

**Requests**
- `RegisterRequest` (EXTENDED): `Email`, `Password`, **`Handle`** (required; format validated).
- `UpdateProfileRequest`: `DisplayName`, `Hometown?`, `Description?`, `Pompfen: Pompfe[]` (full desired set; server replaces selections). **No `Handle`** — immutable.
- Avatar upload: multipart `file` (validated server-side).

**Responses**
- `OwnerProfileDto`: `handle`, `displayName`, `hometown`, `description`, `hasAvatar`, `pompfen: Pompfe[]` (selected), `recentActivity: ActivityItemDto[]`. (Owner view; still no token material.)
- `PublicProfileDto`: `handle`, `displayName`, `hometown`, `description`, `hasAvatar`, `selectedPompfen: Pompfe[]`, `recentActivity: ActivityItemDto[]`. **No email, no account/security fields, no raw UserId.**
- `ActivityItemDto`: `eventName`, `date`, `location`, `teamLabel`.
- `HandleAvailabilityDto`: `handle`, `normalized`, `available`, `reason?`.

Public/owner DTOs are produced by **explicit projected queries** (`.Select`/`ProjectToType`) so sensitive columns are never loaded — not filtered post-hoc.

## Validation summary (server-side, enforced)

| Rule | Where |
|---|---|
| Handle format + reserved-word + length | `HandlePolicy` (registration + availability) |
| Handle uniqueness (race-safe) | unique index + pre-check |
| Handle immutable | no update path (asserted by test) |
| DisplayName 1–50 / Description 0–280 / Hometown 0–80 | DTO annotations + service guard |
| Avatar content-type (sniffed) + size cap | `ProfileService.SetAvatarAsync` |
| Owner-only edit | authenticated subject == profile.UserId |
| Public response omits sensitive fields | `PublicProfileDto` projection |
| Activity paginated + capped | `PaginationRequest` / `PagedResult<T>` |
| Pompfe selection distinct | unique (ProfileId, Pompfe) |

## Migration

Single EF migration `AddProfilesAndEvents`: creates `PlayerProfiles` (unique `Handle`, unique `UserId`), `ProfilePompfen` (unique `ProfileId,Pompfe`), `ProfileAvatars` (unique `ProfileId`), `Events`, `EventParticipations` (unique `ProfileId,EventId`). Auto-applies on startup (existing convention).

**Backfill note**: any pre-existing accounts (local/dev only — no prod users) need a one-time handle backfill; handled by a dev-seed step, not a production concern.
