---
description: "Task list for City Search Relevance Ranking"
---

# Tasks: City Search Relevance Ranking

**Input**: Design documents from `specs/032-city-search-ranking/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/cities-search.md, quickstart.md

**Tests**: INCLUDED — the spec's quickstart lists the integration tests as the authoritative validation,
and this repo is integration-test-driven (`JuggerHub.Api.IntegrationTests`).

**Organization**: Grouped by user story (US1 = populous-first MVP; US2 = proximity). Backend-only.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1 / US2 for story-phase tasks; Setup/Foundational/Polish carry no story label

## Path Conventions

Backend web service. All paths are repo-relative under `backend/` (source) and
`backend/tests/JuggerHub.Api.IntegrationTests/` (tests).

> **Cross-feature note**: this branch is cut from `main`. The region-disambiguation change (PR #86,
> branch `fix/city-picker-region-disambiguation`) is **not yet merged** — so the `Springfield`
> fixtures and the "region only when ambiguous" label behavior are **not** present in this base.
> Tasks below add their own fixtures and do not depend on #86. If #86 merges first, rebase and reuse
> its fixtures instead of duplicating them (see T007).

---

## Phase 1: Setup

**Purpose**: Establish a clean baseline before touching schema or ranking.

- [X] T001 Confirm branch `032-city-search-ranking` and a green baseline: `dotnet build backend/JuggerHub.Api.csproj` and `dotnet test backend/tests/JuggerHub.Api.IntegrationTests/JuggerHub.Api.IntegrationTests.csproj --filter "FullyQualifiedName~CitySearchTests"` both pass unchanged.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Make `Population` available end-to-end (entity → schema → seed → test fixture). Both user
stories rank on population, so this must complete first.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T002 Add `Population` (`int`, non-negative, default `0`) to the `CityReference` entity in `backend/Entities/CityReference.cs`, with an XML-doc note that `0` = unknown and sorts last. (data-model.md)
- [X] T003 Create the EF Core migration adding `Population integer NOT NULL DEFAULT 0` to `CityReferences` (`dotnet ef migrations add AddCityReferencePopulation` from `backend/`); verify the generated migration + `AppDbContextModelSnapshot` under `backend/Data/Migrations/`. (depends on T002)
- [X] T004 [P] Update `backend/Data/Seed/regenerate-cities500.mjs` to emit GeoNames `cities500` population (column index 14) as a 10th tab-separated column after `Longitude`; blank/missing → `0`.
- [X] T005 [P] Extend `backend/Data/CityReferenceSeeder.cs`: add `"Population"` to the binary `COPY` column list and write the parsed population (`int.Parse`, invariant; blank → `0`) as the 10th row value; tolerate the current 9-column bundle by defaulting missing population to `0`.
- [X] T006 Regenerate the bundled seed `backend/Data/Seed/cities500.seed.tsv.gz`: download GeoNames `cities500.txt`, `countryInfo.txt`, `admin1CodesASCII.txt` into `backend/Data/Seed/`, run `node regenerate-cities500.mjs`, and verify the output has 10 columns and a row count consistent with the previous bundle. Do **not** commit the raw `.txt` downloads. (depends on T004)
- [X] T007 [P] Add a `Population` value to every row of the `TestReferenceCities` fixture in `backend/tests/JuggerHub.Api.IntegrationTests/TestReferenceCities.cs` (realistic figures, e.g. Berlin ≈ 3 700 000), and add the same-name/same-country + near/far fixtures the story tests need: a small US "Berlin" (low population) and two near/far same-named towns for the proximity test. (If PR #86 has merged, reuse its `Springfield` fixtures instead of adding duplicates.)

**Checkpoint**: `Population` exists on the entity, in the schema, in the real bundle, and in the test
fixture. Ranking work can begin.

---

## Phase 3: User Story 1 - The obvious city comes first (Priority: P1) 🎯 MVP

**Goal**: Within the preserved match-quality tier, rank options by population so the large, well-known
city of a shared name leads — for everyone, including users with no home city.

**Independent Test**: As a user with no home city, search `berlin` and confirm the high-population
Berlin is item 0 and same-name/same-country results are population-ordered, with the match tier and
name tiebreakers intact.

### Tests for User Story 1 ⚠️ (write first, ensure they fail before T014)

- [X] T008 [P] [US1] Test `Most_populous_city_ranks_first_without_home_city`: `GET /cities/search?q=berlin` as a user with no home city returns the high-population Berlin at index 0, above the small US Berlin fixture — in `backend/tests/JuggerHub.Api.IntegrationTests/Search/CitySearchTests.cs`.
- [X] T009 [P] [US1] Test `Same_name_same_country_ordered_by_population`: two same-name/same-country fixtures come back most-populous-first (region label still present per feature 030) — in `Search/CitySearchTests.cs`.
- [X] T010 [P] [US1] Test `Exact_prefix_match_still_outranks_alternate_name_match` and `Ordering_is_deterministic` (identical repeated query → identical order) — in `Search/CitySearchTests.cs`. (FR-002, FR-009)

### Implementation for User Story 1

- [X] T011 [US1] In `backend/Services/Geocoding/CityService.cs`, insert `.ThenByDescending(r => r.Population)` into the `SearchAsync` ordering **after** the existing exact-prefix match tier and **before** the existing `Name.Length`/`Name` tiebreakers, so populous cities lead and unknown (`0`) sink. Keep `AsNoTracking` + projection + `Take(limit)`. (research.md R4; makes T008–T010 pass)

**Checkpoint**: US1 fully functional and independently testable — MVP shippable.

---

## Phase 4: User Story 2 - Cities near my home rank higher (Priority: P2)

**Goal**: For a user with a stored home city, bias ranking toward nearby cities so the nearby place
they most likely mean floats above a larger, distant same-named city — while keeping population as the
next signal.

**Independent Test**: As a user whose `HomeCity` is near a small same-named town, search that name and
confirm the nearby town ranks above a larger distant same-named city; a user with no home city still
gets population-ordered results with no error and no prompt.

### Tests for User Story 2 ⚠️ (write first, ensure they fail before T014–T016)

- [X] T012 [P] [US2] Test `Nearby_city_outranks_distant_more_populous_city`: seed a user whose `PlayerProfile.HomeCity` is near the small same-named fixture; `GET /cities/search?q={name}` returns the nearby small town above the larger distant one — in `Search/CitySearchTests.cs`. (needs a helper to set the user's home city)
- [X] T013 [P] [US2] Test `No_home_city_falls_back_to_population_without_error`: a user with no home city gets ranked results (population order), HTTP 200, no distance influence — in `Search/CitySearchTests.cs`. (FR-004)

### Implementation for User Story 2

- [X] T014 [US2] Change `SearchAsync` in `backend/Services/Geocoding/ICityService.cs` to accept a nullable current-user id: `SearchAsync(string query, int limit, Guid? userId, CancellationToken ct)`. (research.md R2)
- [X] T015 [US2] In `backend/Controllers/CitiesController.cs`, add the existing `TryGetUserId` helper (JWT `sub`) to the controller, resolve the current user id in `Search`, and pass it to `SearchAsync`. Keep the controller thin (Principle II). (depends on T014)
- [X] T016 [US2] In `backend/Services/Geocoding/CityService.cs`: when `userId` is non-null, project the user's `PlayerProfile.HomeCity` `Latitude`/`Longitude` (single `AsNoTracking` query; `null` when absent). When home coords exist, insert an equirectangular squared-distance `.ThenBy(...)` — `(Latitude-lat0)*(Latitude-lat0) + kLon*(Longitude-lon0)*(Longitude-lon0)` with `kLon = cos(lat0*π/180)` as a C# constant — **between** the match tier and the population sort, so order becomes match → distance → population → name tiebreakers. Omit the term entirely when there is no home city. (research.md R1/R4; makes T012–T013 pass; depends on T014)

**Checkpoint**: US1 and US2 both independently functional; full relevance order in place.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [X] T017 Run the full `CitySearchTests` class and the `Search` collection: `dotnet test backend/tests/JuggerHub.Api.IntegrationTests/JuggerHub.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Search"`; confirm no regressions and all new tests pass.
- [X] T018 [P] Record the **one-time reseed** operational step (truncate `CityReferences` so the seeder reloads the regenerated bundle) in the migration's summary comment and in `specs/032-city-search-ranking/quickstart.md`'s prerequisites, for Dev/Prod deploy notes. (Principle V parity)
- [X] T019 [P] Run `dotnet build backend/JuggerHub.Api.csproj` and confirm 0 warnings/0 errors; verify no frontend files changed (DTO/label shape unchanged, FR-008).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: after Setup. **Blocks both user stories.** T003 depends on T002; T006 depends on T004.
- **US1 (Phase 3)**: after Foundational. The MVP.
- **US2 (Phase 4)**: after Foundational. Builds on US1's ordering by inserting the distance tier above population; T015/T016 depend on T014.
- **Polish (Phase 5)**: after the desired stories are complete.

### User Story Dependencies

- **US1 (P1)**: independent — needs only `Population` (Foundational). Ships alone as MVP.
- **US2 (P2)**: independent to test, but its ranking sits above US1's population sort; implement after US1 for a clean incremental diff. No shared-file conflict beyond `CityService.cs` (T011 then T016 edit the same ordering block — sequence them).

### Within Each User Story

- Tests first (they must fail), then implementation.
- US2: interface change (T014) → controller (T015) + service (T016).

### Parallel Opportunities

- Foundational: T004, T005, T007 are `[P]` (different files); T002 before T003; T004 before T006.
- US1 tests T008–T010 are `[P]` (same file, but independent cases — if authored together, treat as one edit).
- US2 tests T012–T013 are `[P]`.
- Polish T018, T019 are `[P]`.

---

## Parallel Example: Foundational

```bash
# After T002 (entity) lands, these touch different files and can proceed together:
Task T004: "Emit GeoNames population (col 14) in backend/Data/Seed/regenerate-cities500.mjs"
Task T005: "Add Population to the COPY in backend/Data/CityReferenceSeeder.cs"
Task T007: "Add Population + near/far fixtures in TestReferenceCities.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational (Population end-to-end) → 3. Phase 3 US1 (population ranking)
→ 4. **STOP & VALIDATE**: search `berlin` with no home city shows the big Berlin first → 5. Ship.

### Incremental Delivery

- Foundational → US1 (populous-first, MVP) → US2 (proximity on top) → Polish. Each step is a green,
  independently testable increment; US2 never regresses US1 (it only adds a higher-priority tier).

### Notes

- `[P]` = different files, no incomplete dependency.
- `CityService.SearchAsync` is edited twice (T011 population, then T016 distance) — sequence, don't parallelize those two.
- Tests use the `TestReferenceCities` fixture (EF insert), so US1/US2 tests do **not** depend on the big bundle regeneration (T006); T006 is required for real environments to carry population data.
- Commit per task or logical group; keep the frontend untouched.
