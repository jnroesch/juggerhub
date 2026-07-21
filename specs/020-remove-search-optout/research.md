# Phase 0 Research: Remove the Player-Search Opt-Out

All questions below were resolvable from the codebase; no external research or
open NEEDS CLARIFICATION remain.

## R1. Does removing the browse gate expose accounts that must stay hidden?

**Decision**: Safe to remove `.Where(p => p.AppearInSearch)`; no additional guard needed.

**Rationale**: `PlayerProfile` carries a **global query filter**
`HasQueryFilter(p => p.User.Status != AccountStatus.Banned)`
([AppDbContext.cs:128](../../backend/Data/AppDbContext.cs)). It applies to every
non-`IgnoreQueryFilters` query — including `PlayerSearchService.BrowseAsync`. So
banned (soft-deleted, feature 013) players are already excluded independent of
`AppearInSearch`. The only accounts newly appearing are ordinary and
**suspended** players. Suspension is defined as "login block only; data stays
visible" (feature 013), so a suspended player showing in the directory is correct,
not a regression.

**Alternatives considered**: Adding an explicit account-state filter to browse —
rejected as redundant with the global filter and out of scope (this feature does
not change account-state semantics).

## R2. Backward compatibility for a legacy `appearInSearch` on save

**Decision**: Not required — no shim, no tolerance test (owner decision
2026-07-21). Simply remove the field from `UpdateProfileRequest`.

**Rationale**: The frontend and backend deploy together, so no client sends the
removed field. FR-004/SC-004 were retired. (Incidental, not relied upon: MVC JSON is
configured only with a `JsonStringEnumConverter`
([Program.cs:37-38](../../backend/Program.cs)), leaving `UnmappedMemberHandling` at
its default `Skip`, so a stray unknown field would be ignored rather than error — but
this feature neither guarantees nor tests that.)

**Alternatives considered**: Adding tolerance handling / a compatibility test —
rejected as unnecessary given synchronized deploys.

## R3. Migration shape (drop column + partial index)

**Decision**: One EF Core migration, generated after the entity/model edits, that
drops the partial index `IX_PlayerProfiles_AppearInSearch` and the
`PlayerProfiles.AppearInSearch` column; `Down` restores both.

**Rationale**: The column was added by `20260707094... AddDiscoveryFields` together
with a **partial index** `HasFilter("\"AppearInSearch\"")`
([AppDbContext.cs:138](../../backend/Data/AppDbContext.cs);
[migration](../../backend/Data/Migrations/20260707122721_AddDiscoveryFields.cs)).
EF will emit both `DropIndex` and `DropColumn` once the property and its
`HasIndex`/`HasDefaultValue` config are removed. Generating (not hand-writing) the
migration keeps the model snapshot consistent. `AddDiscoveryFields` also created the
`unaccent` extension and a `BeginnersWelcome` column/`IX_Events_Status` — those are
unrelated and MUST remain untouched.

**Alternatives considered**: Hand-writing the migration — rejected; risks snapshot
drift. Keeping the column "just in case" — rejected; the spec mandates full removal
(FR-002, SC-003).

## R4. Data-side removal points in `ProfileService`

**Decision**: Remove `AppearInSearch` from the `GetOwnerAsync` projection and the
`OwnerProfileDto` construction, from `UpdateAsync` (the `profile.AppearInSearch =
request.AppearInSearch` write), and from the private `ProfileProjection` record.
`GetPublicAsync` already omits it (uses the 8-arg projection) — no change there.

**Rationale**: These are the only reads/writes of the property in the service
([ProfileService.cs:69,83,169,316](../../backend/Services/Profile/ProfileService.cs)).
With the DTO param gone, both owner and public projections converge on the same
shape.

## R5. Documentation reconciliation (FR-006 / SC-006)

**Decision**: Amend rather than rewrite. Add a dated "Amended by 021" note to
`specs/007-search/spec.md` retiring the player opt-in invariant (its FR-041/FR-042
and the 100%-invariant SC-003), update `specs/007-search/contracts/openapi.yaml` to
drop the `appearInSearch` field from the profile schema(s), and add a matching note
to `specs/003-profile/spec.md` for the removed profile field.

**Rationale**: 007 and 003 are shipped features with historical value; a targeted
amendment note keeps provenance while making the source-of-truth match behavior.
The live contract (`openapi.yaml`) is corrected outright since it describes the
current API.

**Alternatives considered**: Deleting the 007 invariant text entirely — rejected;
an amendment note preserves the decision history and explains the reversal.

## R6. Test impact

**Decision**: Update exactly the tests that assert the opt-in behavior.

- `Search/SearchTestSupport.cs` — the `appearInSearch` helper parameter/assignment is
  removed; helper just ensures a profile exists/searchable by default.
- `Search/PlayerBrowseTests.cs` — assertions that a non-opted-in player is **absent**
  flip to expecting the player **present**; the "opt-in is a hard invariant" test
  (SC-003 of 007) is removed/replaced by an "all players appear" test.
- `Admin/AccountEnforcementTests.cs` — remove the
  `SetProperty(p => p.AppearInSearch, true)` setup ([line 76](../../backend/tests/JuggerHub.Api.IntegrationTests/Admin/AccountEnforcementTests.cs));
  the player is searchable by default, so the banned/suspended visibility assertions
  keep their intent unchanged.

**Rationale**: Grep confirms these are the only backend tests referencing the flag;
`ProfileTests` does not. No frontend spec references it (onboarding included).

## R7. Frontend removal points

**Decision**: Remove the `appearInSearch` signal, `toggleAppearInSearch()`, its
read in `reload()`/`cancelEdit()`, and its inclusion in the `updateMine(...)` save
payload in `profile-owner.component.ts`; remove the toggle button + "You appear /
are hidden from player search" status text in `profile-owner.component.html`; and
drop `appearInSearch` from `OwnerProfile` and `UpdateProfileRequest` in
`profile.models.ts`. No other component references it.

**Rationale**: Confirmed by grep; the owner profile is the only surface with the
control. Removing it from the model keeps the save payload from carrying the field.
