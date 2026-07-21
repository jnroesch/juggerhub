# Quickstart / Validation: Remove the Player-Search Opt-Out

Validates that the opt-out is gone, the directory is unconditionally inclusive
(except banned players), the owner UI has no toggle, and a legacy field is tolerated.

## Prerequisites

- Local stack up via docker-compose (Postgres + backend + frontend), per repo README.
- The `RemoveAppearInSearch` migration applied (`dotnet ef database update`, or the
  app's startup migration path).
- Dev seed data (or manually created players), including at least one player who,
  under the old model, would have had `AppearInSearch = false`.

## Automated checks

- **Backend**: `dotnet test` (Search + Admin integration suites are the ones touched).
  - `PlayerBrowseTests` now asserts every non-banned player appears (the old
    opt-in-invariant test is replaced).
  - `AccountEnforcementTests` still asserts banned excluded / suspended included,
    without seeding `AppearInSearch`.
- **Frontend**: `npx nx test web` (owner-profile component renders with no toggle).
- **Build/lint/typecheck**: backend build is analyzer-clean (warnings-as-errors);
  `npx nx lint web` passes with the field removed from the model.

## Manual end-to-end scenarios

1. **Previously-hidden player now appears** (FR-001, SC-001)
   - `GET /api/v1/profiles?q=<name>` for a player who was not opted in.
   - Expect: the player is in `items`, and `totalCount` counts them.

2. **Owner profile has no visibility control** (FR-003, SC-002)
   - Sign in, open the owner profile edit view.
   - Expect: no "appear in search" toggle and no "you appear / are hidden" text.
   - `GET /api/v1/profiles/me` response has no `appearInSearch` field (SC-003).

3. **Banned still hidden, suspended still visible** (FR-007)
   - Browse with a banned player and a suspended player present.
   - Expect: banned absent from results; suspended present.

4. **Count/results agree** (FR-005, SC-005)
   - Compare `totalCount` against the number of `items` paged through for a query.
   - Expect: they reconcile for the full non-banned matching set.

## Spec reconciliation check (FR-006, SC-006)

- `specs/007-search/spec.md` carries a dated amendment retiring the opt-in invariant;
  `specs/007-search/contracts/openapi.yaml` no longer lists `appearInSearch`.
- `specs/003-profile/spec.md` carries a dated amendment for the removed field.

## UI review (Quality Gate 7)

- Instantiate `specs/020-remove-search-optout/checklists/ui-review.md` from the
  template and verify the owner-profile edit view against DESIGN.md after the toggle
  is removed (spacing/empty-state of the surrounding section, no orphaned divider).
