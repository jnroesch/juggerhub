# Phase 1 Data Model: Remove the Player-Search Opt-Out

This feature **removes** state; it introduces no new entities, fields, or
relationships.

## Changed entity: `PlayerProfile`

| Aspect | Before | After |
|--------|--------|-------|
| `AppearInSearch` (bool, NOT NULL, default false) | present; owner-editable; gates browse | **removed** |
| Partial index `IX_PlayerProfiles_AppearInSearch` (`HasFilter("\"AppearInSearch\"")`) | present; backed the opt-in scan | **removed** |
| Global ban filter `User.Status != Banned` | present | **unchanged** (still hides banned players everywhere) |
| All other columns / indexes (`Handle` unique, `UserId` unique, relationships, `OnboardingCompletedAt`, etc.) | — | **unchanged** |

### Migration

- **Name (suggested)**: `RemoveAppearInSearch`
- **Up**: `DropIndex IX_PlayerProfiles_AppearInSearch`; `DropColumn AppearInSearch` on `PlayerProfiles`.
- **Down**: `AddColumn AppearInSearch (boolean, nullable:false, defaultValue:false)`; recreate the partial index `HasFilter("\"AppearInSearch\"")`.
- Generated via `dotnet ef migrations add RemoveAppearInSearch` **after** the entity + `AppDbContext` edits, so the model snapshot updates in lockstep.
- Must NOT touch the unrelated artifacts from `AddDiscoveryFields` (the `unaccent` extension, `Teams.BeginnersWelcome`, `IX_Events_Status`).
- Data effect: previously-hidden players become directory-visible automatically once the gate is gone (no data backfill needed).

## Derived read: player directory listing

- **Source query** (`PlayerSearchService.BrowseAsync`): was
  `PlayerProfiles.Where(p => p.AppearInSearch)` → becomes `PlayerProfiles` (with the
  global ban filter still implicitly applied). Ordering (`DisplayName`, then `Id`)
  and `Skip/Take` pagination are unchanged.
- **Invariant retired**: feature 007's "only opted-in players are ever returned"
  (SC-003) no longer holds by design; replaced by "all non-banned players are
  returned".

## Affected DTOs (contract shapes)

| DTO | Change |
|-----|--------|
| `OwnerProfileDto` | drop trailing `bool AppearInSearch` |
| `UpdateProfileRequest` | drop trailing `bool AppearInSearch` (incoming legacy value ignored by default JSON binding) |
| `PublicProfileDto` | none (never carried it) |
| `PlayerCardDto` (browse result) | none (never carried it) |

See [contracts/](contracts/) for the request/response deltas.
