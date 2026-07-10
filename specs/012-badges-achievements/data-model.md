# Data Model: Badges & Achievements

Feature 012. All entities derive from `BaseEntity` (`Guid Id = Guid.CreateVersion7()`, `CreatedDate`, `ModifiedDate` via the audit interceptor). Postgres 18. One migration: **`AddBadgesAndAchievements`**.

Badges and achievements are **two separate families** with a parallel shape (see [research.md](./research.md) D1). Shared enums live in `RecognitionEnums.cs`.

---

## Enums (`Entities/RecognitionEnums.cs`)

```
AwardSource   : Manual = 0, Automatic = 1   // v1 only ever writes Manual; Automatic reserved for deferred US3
AwardStatus   : Active = 0, Revoked = 1
SubjectType   : Player = 0, Team = 1        // used in DTOs/validation; storage uses the two nullable FKs
```

> A global `JsonStringEnumConverter` is already registered (EF/backend gotcha), so enums serialize by name.

---

## Badge family

### `BadgeDefinition : BaseEntity`
| Field | Type | Notes |
|---|---|---|
| `Name` | string | required, max 60, the catalog label |
| `Description` | string | required, max 280 |
| `AppliesToPlayers` | bool | at least one of players/teams true (service-validated) |
| `AppliesToTeams` | bool | |
| `IsRetired` | bool | default false; retiring hides from grant pickers but preserves existing awards |
| `Icon` | `BadgeIcon?` | optional 1:1 nav |
| `Awards` | `ICollection<BadgeAward>` | |

### `BadgeIcon : BaseEntity`
| Field | Type | Notes |
|---|---|---|
| `BadgeDefinitionId` | Guid | unique (1:1) |
| `ContentType` | string | max 64, magic-byte validated (png/jpeg/webp) |
| `Bytes` | byte[] | Postgres `bytea`, size-capped on upload |

*(Mirrors `ProfileAvatar`; kept in a side table so catalog/award projections never pull the blob.)*

### `BadgeAward : BaseEntity`
| Field | Type | Notes |
|---|---|---|
| `BadgeDefinitionId` | Guid | FK → BadgeDefinition (Restrict delete — see integrity) |
| `PlayerProfileId` | Guid? | polymorphic subject — exactly one of these two set (DB CHECK) |
| `TeamId` | Guid? | |
| `Source` | AwardSource | `Manual` in v1 |
| `Status` | AwardStatus | `Active` / `Revoked` |
| `EarnedAt` | DateTime | UTC, set at grant |
| `GrantedByUserId` | Guid | the admin who granted (FK → User, Restrict) |
| `RevokedAt` | DateTime? | |
| `RevokedByUserId` | Guid? | |
| `RevokedReason` | string? | max 280 |

---

## Achievement family

### `AchievementDefinition : BaseEntity`
Same fields as `BadgeDefinition` (`Name`, `Description`, `AppliesToPlayers`, `AppliesToTeams`, `IsRetired`, `Icon` → `AchievementIcon?`, `Awards`).

### `AchievementIcon : BaseEntity`
Same shape as `BadgeIcon`, keyed by `AchievementDefinitionId` (unique).

### `AchievementAward : BaseEntity`
Same fields as `BadgeAward`, **plus** accomplishment context (research D8):
| Field | Type | Notes |
|---|---|---|
| `ContextYear` | int? | e.g., 2026 |
| `ContextLabel` | string? | max 120, e.g., "National Championship" |

---

## Relationships & delete behavior

- `BadgeDefinition 1—0..1 BadgeIcon` — cascade (icon deleted with its definition). Same for achievements.
- `BadgeDefinition 1—* BadgeAward` — **Restrict**: a definition cannot be hard-deleted while awards exist; admins **retire** (`IsRetired = true`) instead (spec edge case: retiring must not break earned awards). Same for achievements.
- `PlayerProfile 1—* BadgeAward` (via `PlayerProfileId`) — **Cascade**: deleting a profile removes its awards (edge case: subject deletion, no orphans). Same for `Team` and for achievements.
- `User 1—* BadgeAward` (via `GrantedByUserId`) — **Restrict**: preserve who granted; admins aren't deleted casually.

## Indexes & constraints

Per family (badges shown; achievements identical):

- **CHECK** `(PlayerProfileId IS NOT NULL) <> (TeamId IS NOT NULL)` — exactly one subject set.
- **Filtered unique** index `(BadgeDefinitionId, PlayerProfileId) WHERE Status = 0 (Active)` — no duplicate active player award (FR-006); re-grant allowed after revoke.
- **Filtered unique** index `(BadgeDefinitionId, TeamId) WHERE Status = 0 (Active)` — same for teams.
- Index on `PlayerProfileId` and on `TeamId` (backs the embedded display query: active awards for one subject).
- `BadgeIcon`: unique index on `BadgeDefinitionId`.
- Property max-lengths configured in `OnModelCreating` (matching the table above), consistent with existing entity config.

## DbSets (add to `AppDbContext`)

```
BadgeDefinitions, BadgeIcons, BadgeAwards,
AchievementDefinitions, AchievementIcons, AchievementAwards
```

## Validation rules (enforced in services, not controllers)

- Definition: `Name`/`Description` required within bounds; `AppliesToPlayers || AppliesToTeams` must be true.
- Grant: definition not retired; target subject exists; subject type ∈ definition applicability (FR-005); no existing **active** award for that (definition, subject) (FR-006) — DB index is the backstop.
- Revoke: award exists and is `Active`; sets `Status=Revoked`, `RevokedAt`, `RevokedByUserId`, optional reason (uses a targeted update; set `ModifiedDate` explicitly if bypassing the tracker).
- Icon upload: size cap + magic-byte content-type sniff (png/jpeg/webp), server-side.

## Read shape (embedded in existing pages)

For a given subject, the display query selects **active** awards only, projecting `{ definitionId, name, description, hasIcon, earnedAt, (achievements: contextYear, contextLabel) }` via `AsNoTracking` + `Select`. Lists are bounded (see research D6). Icon bytes are fetched separately via the icon endpoint by `definitionId`.

## Deferred (US3, not built)

An `AwardCriterion` concept (rule type + parameters) would attach to definitions to drive `Source = Automatic`. The reserved `Source` field and durable award history mean adding it later needs no change to existing rows.
