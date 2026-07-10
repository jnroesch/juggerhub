# Phase 1 Data Model: Admin catalogue management

**No schema migration.** Every persisted shape below already exists. This feature
only adds *derived/projected* fields to response DTOs and new read-only team DTOs.

## Existing entities (unchanged)

### BadgeDefinition / AchievementDefinition : BaseEntity

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid (UUIDv7) | from `BaseEntity` |
| Name | string (≤60) | free text; not unique |
| Description | string (≤280) | |
| AppliesToPlayers | bool | ≥1 of players/teams must be true |
| AppliesToTeams | bool | |
| IsRetired | bool | retire sets true; **reinstate sets false** |
| CreatedDate | DateTime | from `BaseEntity`; surfaced as `CreatedAt` |
| ModifiedDate | DateTime | audit interceptor |
| Icon (nav) | BadgeIcon? / AchievementIcon? | presence ⇒ `HasIcon` |

### BadgeAward / AchievementAward : BaseEntity (unchanged)

Referenced only for the **grant count** (COUNT where `DefinitionId == d.Id` and
`Status == AwardStatus.Active`) and for team grant/revoke (existing paths). Carries
`PlayerProfileId?` / `TeamId?`, `Status`, and (achievements) `ContextYear` /
`ContextLabel`.

### BadgeIcon / AchievementIcon (unchanged)

`{DefinitionId, Bytes, ContentType}`. **Remove-icon** deletes the row (⇒ `HasIcon`
false; public icon endpoint 404s → placeholder fallback).

### Team : BaseEntity (unchanged)

`Slug` (unique, immutable — the address), `Name` (free text), `Type`, `City?`,
`BeginnersWelcome`, `Memberships`. Admin teams search filters `Name`/`City`; routes
and award calls use `Slug`.

## DTO changes

### BadgeDefinitionDto / AchievementDefinitionDto — ADD two fields

Existing: `(Id, Name, Description, AppliesToPlayers, AppliesToTeams, IsRetired, HasIcon)`.

Add:

| Field | Type | Source |
|-------|------|--------|
| GrantedCount | int | projected COUNT of active awards for the definition |
| CreatedAt | DateTime | `BaseEntity.CreatedDate` |

Populated inside `ListDefinitionsAsync` projections (and returned by create/update
where sensible: a new type has `GrantedCount = 0`). Achievements DTO mirrors badges.

### AdminTeamListItemDto — NEW (mirrors `AdminUserListItemDto`)

`(string Slug, string Name, string? City, TeamType Type, int MemberCount, int AwardCount)`
— one row of the admin teams list.

### AdminTeamDetailDto — NEW (mirrors `AdminUserDetailDto`, minimal)

`(Guid TeamId, string Slug, string Name, string? City, TeamType Type, int MemberCount, DateTime CreatedAt)`
— identity for the team admin detail header. Current awards come from the existing
`GET /admin/teams/{slug}/awards` (`AdminSubjectAwardsDto`), not duplicated here.

## Frontend model changes

- `RecognitionDefinition` (`recognition.models.ts`): add `grantedCount: number`,
  `createdAt: string` (ISO).
- `admin.models.ts`: add `AdminTeamListItem` and `AdminTeamDetail` mirroring the
  DTOs above.

## Lifecycle / state

Definition status is binary: **Active** (`IsRetired = false`) ⇄ **Retired**
(`IsRetired = true`), toggled by retire (`DELETE {id}`) and reinstate
(`POST {id}/reinstate`). Awards are unaffected by either transition (retired types
stay on holders; never deleted). No other states.

## Validation rules (unchanged, server-enforced)

- Upsert: `Name` required ≤60, `Description` required ≤280, at least one applies-to
  (existing `IValidatableObject` on the request records).
- Icon: magic-byte sniff PNG/JPEG/WebP + `MaxIconBytes` cap; unsupported/oversize ⇒
  400, existing icon unchanged.
- Grant: exactly one of `playerHandle`/`teamSlug`; retired ⇒ 400; type-mismatch ⇒
  400; duplicate ⇒ 409 (all existing).
