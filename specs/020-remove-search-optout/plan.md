# Implementation Plan: Remove the Player-Search Opt-Out

**Branch**: `020-remove-search-optout` | **Date**: 2026-07-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/020-remove-search-optout/spec.md`

## Summary

Fully remove the `AppearInSearch` player-search opt-out and every connected piece.
After this change the anonymous player directory returns **all** players (subject
only to search terms/filters), the owner profile has no visibility toggle, and no
stored or transmitted field represents a per-player directory-visibility
preference. The change is a small, mechanical deletion across one entity, one
projection/write path, one search query, two DTOs, the model configuration, an EF
migration (drop column + its partial index), the dev seed, two backend tests, and
the owner-profile UI — plus documentation amendments to features 007 and 003.

A fact discovered during planning makes this safe and cheap:
- **Banned accounts stay hidden without the gate.** A global query filter on
  `PlayerProfile` (`User.Status != Banned`) already drops banned players from every
  query ([AppDbContext.cs](../../backend/Data/AppDbContext.cs)). Removing the
  `AppearInSearch` predicate therefore cannot leak banned players. (Suspended
  players remain visible — consistent with "suspend = data stays visible".)

**No backward compatibility** is required (owner decision 2026-07-21): the frontend
and backend ship together, so no client sends the removed field. FR-004/SC-004 were
retired accordingly. There is no compatibility shim and no tolerance test.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Angular (Nx) frontend

**Primary Dependencies**: EF Core (Npgsql), Mapster, ASP.NET Identity; Angular + Tailwind

**Storage**: PostgreSQL 18 — one column drop (`PlayerProfiles.AppearInSearch`) and
its partial index (`IX_PlayerProfiles_AppearInSearch`) via an EF Core migration

**Testing**: xUnit integration tests (backend); Jest (frontend)

**Target Platform**: Web (containerized; AKS deployed)

**Project Type**: Web application (backend + frontend)

**Performance Goals**: No change. The dropped partial index only ever backed the
opt-in scan; an unfiltered browse over `PlayerProfiles` uses the same table with the
existing ordering (`DisplayName`, `Id`).

**Constraints**: No other visibility surface may change; specs 007/003 must be
reconciled with shipped behavior. (No backward-compatibility constraint — frontend
and backend deploy together.)

**Scale/Scope**: Community-scale player base; a single directory query path.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Security-First / Never Trust the Client** — PASS. This removes a *client-facing
  preference*, not a server-side authorization. The one security-relevant question —
  "does dropping the gate expose anyone who must stay hidden?" — is answered NO by the
  existing global ban filter. Suspended-but-visible is the intended model. No secrets,
  no new exception surface.
- **II. Thin Controllers, Service-Centric** — PASS. Controllers untouched; the change
  lives in `ProfileService`/`PlayerSearchService` and DTO records mapped as today.
- **III. Disciplined Data Access (EF Core + PostgreSQL)** — PASS. One reversible
  migration (Up drops index+column; Down re-adds both, `nullable: false, default false`,
  and the partial index). Reads keep `AsNoTracking()`; browse keeps `Skip/Take` +
  stable `Id` tiebreaker. Migration notes required (constitution amendment discipline).
- **IV. Auth & Session** — N/A (no auth surface touched).
- **V. Environment Parity / Reproducible Deploys** — PASS. Pure schema+code change,
  identical across local/Dev/Prod; migration runs in the normal pipeline.
- **VI. Conventions & Tooling** — PASS. Frontend keeps separate `.html`/`.css`/`.ts`;
  no new scripts (`.ps1`-only rule untouched).
- **Quality Gate 7 (UI/Design compliance)** — APPLIES. The owner-profile edit view
  loses a control; run the DESIGN.md-derived UI review checklist on the diff
  (`specs/020-remove-search-optout/checklists/ui-review.md`) before verification.

**Result**: No violations. Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/020-remove-search-optout/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (API contract delta)
├── checklists/
│   ├── requirements.md  # spec quality (done)
│   └── ui-review.md     # created during implementation (Gate 7)
└── tasks.md             # /speckit-tasks output (NOT created here)
```

### Source Code (repository root)

```text
backend/
├── Entities/PlayerProfile.cs                    # remove AppearInSearch property
├── Data/AppDbContext.cs                          # remove HasDefaultValue + partial HasIndex
├── Data/Migrations/<new>_RemoveAppearInSearch.cs # drop index + column (generated)
├── Data/DevDataSeeder.cs                         # remove AppearInSearch seeding
├── Dtos/Profile/ProfileDtos.cs                   # remove field from OwnerProfileDto + UpdateProfileRequest
├── Services/Profile/ProfileService.cs            # drop from projection, write, ProfileProjection record
├── Services/Search/PlayerSearchService.cs        # remove .Where(p => p.AppearInSearch) + doc comment
└── tests/JuggerHub.Api.IntegrationTests/
    ├── Search/SearchTestSupport.cs               # drop the appearInSearch helper param
    ├── Search/PlayerBrowseTests.cs               # flip opt-in-invariant assertions → all-inclusive
    └── Admin/AccountEnforcementTests.cs          # remove the SetProperty(AppearInSearch,true) setup

frontend/apps/web/src/app/
├── core/models/profile.models.ts                 # remove appearInSearch from OwnerProfile + UpdateProfileRequest
└── features/profile/profile-owner/
    ├── profile-owner.component.ts                # remove signal, toggle, reload/save/cancel wiring
    └── profile-owner.component.html              # remove the toggle + status text

specs/007-search/  (amendment)                    # retire the opt-in invariant; update openapi.yaml
specs/003-profile/ (amendment)                    # note removed profile field
```

**Structure Decision**: Existing web-app layout (backend `.NET` + Angular frontend).
No new projects, services, or modules — this is a deletion within established files.

## Complexity Tracking

No constitution violations; section intentionally empty.
