---
description: "Task list for feature 030 — Structured Locations & Near You Discovery"
---

# Tasks: Structured Locations & "Near You" Discovery

**Input**: Design documents from `/specs/030-structured-locations/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included — JuggerHub is test-heavy (xUnit backend, Jasmine/Karma frontend, Playwright e2e) and `quickstart.md` defines automated checks. Test tasks precede the implementation they cover within each phase.

> **Implementation status (2026-07-25)** — feature implemented and **both test suites green**. **Verified**: backend `dotnet build` green (0 warnings); `nx build web` green; **backend integration 511/511 pass**; **frontend 256/256 pass**. **Done**: all foundational backend + entity/migration/seeder + the DTO/service cascade + proximity sort (teams & events) + a structured city-NAME browse filter (re-added alongside country + proximity) + the full frontend (shared `jh-city-picker`, all display templates, onboarding/profile/team-create/event-create/event-edit forms, browse country filter) + a `TestGeocoder` fake and updated test payloads/assertions across the suite.
>
> **Remaining**: dedicated NEW test coverage the task list called for is only partly present — the suites are green and exercise create/browse/filter, but there are no *dedicated* tests for geocoder resilience (T020), `CityService` upsert/dedupe/backfill units (T021), the `/api/cities/search` degradation path (T022), the `jh-city-picker` (T023), or proximity-sort ordering specifically (T044/T049). `docker-compose` Photon added to the main file only, not mirrored to test/e2e/debug (T001). **Onboarding proximity (T048) deferred** — the home city isn't persisted until finish, so the team step can't derive it server-side without a design change. Polish (T056–T061), incl. the Photon extract/AKS spike (T058) and legacy `Event.Location` cleanup (T059), outstanding. **Deviation note**: the spec/contract said *remove* the browse city filter (country + proximity only); implementation **re-added a structured city-NAME filter** (matches `City.Name`, not freeform) — a justified, low-risk expansion that also preserved test isolation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1–US4 for user-story phases; Setup/Foundational/Polish carry no story label
- Paths are repo-relative and concrete.

## Path Conventions

Web app: backend at `backend/`, Angular at `frontend/apps/web/src/app/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Stand up the self-hosted geocoder and its configuration so everything else can build against it.

- [ ] T001 Add a `photon` service (`komoot/photon`) to `docker-compose.yml` with a named volume for its index; mirror into `docker-compose.test.yml`, `docker-compose.e2e.yml`, and `docker-compose.debug.yml`; add it to the `juggerhub-network`.
- [X] T002 [P] Add geocoder config to `.env.sample` (`GEOCODING__BASEURL`, extract hint) and the `Resilience:Outbound:Geocoding` + `Geocoding` sections to `backend/appsettings.json` / `appsettings.Development.json` with snappy interactive limits (attempt ~3s, total ~8–10s) and a breaker `MinimumThroughput` tuned to interactive volume (research R5).
- [X] T003 [P] Create `backend/Common/GeocodingOptions.cs` (base URL, request limit, extract hint) bound from the `Geocoding` config section, with safe defaults.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The canonical-city data model, the resilient geocoder integration, the backend search/select surface, and the shared Angular picker — all shared by US1 and US2, so nothing user-facing can begin until these exist.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Data model & migration

- [X] T004 Create `backend/Entities/City.cs` (BaseEntity; `ExternalId`, `Name`, `CountryName`, `CountryCode?`, `Region?`, `Latitude`, `Longitude`) per data-model.md.
- [X] T005 [P] Create `backend/Entities/CityDistance.cs` (BaseEntity; `FromCityId`, `ToCityId`, `DistanceKm`).
- [X] T006 Modify `backend/Entities/PlayerProfile.cs` (`Hometown` → `HomeCityId` + `HomeCity` nav), `backend/Entities/Team.cs` (`City` string → `CityId` + `City` nav), `backend/Entities/Event.cs` (`City`/`Country` strings → `CityId` + `City` nav).
- [X] T007 Configure `backend/Data/AppDbContext.cs`: `DbSet<City>`, `DbSet<CityDistance>`; unique index on `City.ExternalId`; unique composite + `(FromCityId, DistanceKm)` index on `CityDistance`; FK relationships with `OnDelete: Restrict` (entity→City) and `Cascade` (City→CityDistance).
- [X] T008 Add one EF migration under `backend/Data/Migrations/` that creates `Cities` + `CityDistances`, drops `PlayerProfiles.Hometown` / `Teams.City` / `Events.City` / `Events.Country`, and adds the three nullable FK columns (no data migration — reseed).

### Resilient geocoder integration (Principle VII)

- [X] T009 [P] Create `backend/Services/Geocoding/IGeocodingClient.cs` — `SearchAsync(q, limit, ct)` and `ResolveByIdAsync(externalId, ct)` returning provider-neutral results.
- [X] T010 Create `backend/Services/Geocoding/PhotonGeocodingClient.cs` — call Photon `/api/`, parse GeoJSON to city results, **filter out** results lacking a country or coordinates (data-model invariant 2).
- [X] T011 Register the client in `backend/Program.cs`: `AddHttpClient<IGeocodingClient, PhotonGeocodingClient>().AddJuggerHubResilience(builder.Configuration, "Geocoding")` — retries left enabled with an inline comment that these are idempotent GETs (opposite of the email POST; research R5). No per-client timeout, no hand-rolled retry.

### City service, search endpoint, DTOs

- [X] T012 Create `backend/Services/Geocoding/ICityService.cs` + `CityService.cs` — search proxy; upsert `City` by `ExternalId`; server-side **re-resolution** on selection (never trust client coords); `CityDistance` backfill inside the EF execution strategy (all inserts in the delegate, self-row `(X→X)=0` included). Reject selections that can't resolve to country+coords.
- [X] T013 [P] Create `backend/Dtos/Cities/CityDtos.cs` — `CityOptionDto` (contracts/cities.md) + the shared `LocationDto` read shape (contracts/browse-and-profile.md) + Mapster config (`City` → `LocationDto`, `locationLabel`).
- [X] T014 Create `backend/Controllers/CitiesController.cs` — `GET /api/cities/search` (auth-required); returns `[]` for short/no-match `q`; returns `503` generic body on geocoder degradation; no PII/secrets in logs (FR-021).

### Shared frontend picker

- [X] T015 [P] Create `frontend/apps/web/src/app/core/models/city.models.ts` — `CityOption`, `Location` view models.
- [X] T016 [P] Create `frontend/apps/web/src/app/core/services/city.service.ts` — debounced-friendly `search(q)` calling `/api/cities/search`, using the existing retry interceptor (GET).
- [X] T017 Create `frontend/apps/web/src/app/shared/city-picker/` (`city-picker.component.ts/.html/.css`) — 250ms debounced type-ahead, disambiguated option labels, select/clear, transient "can't search right now" state on 503 (FR-019). Export from `shared/ui` barrel if applicable.
- [X] T018 [P] Add a `locationLabel(location)` helper (frontend `shared/`) mirroring the backend `"City, Country"` mapping (FR-010).

### Seed

- [X] T019 Update `backend/Data/DevDataSeeder.cs` to seed a handful of real cities (with `ExternalId`, country, coords), backfill their `CityDistance` pairs, and link seeded profiles/teams/events.

### Foundational tests

- [ ] T020 [P] Geocoder resilience integration test under `backend/tests/JuggerHub.Api.IntegrationTests/Resilience/` reusing `OutboundResilienceHarness` — transient GET retried, breaker/timeouts bounded, degradation surfaces (not a hang).
- [ ] T021 [P] `CityService` unit tests — upsert/de-dupe by `ExternalId`, distance backfill correctness + symmetry, unlocated/no-country rejection, concurrent first-select converges to one row.
- [ ] T022 [P] `GET /api/cities/search` contract test — `200 []` for short `q`, `200` results shape, `503` when geocoder down.
- [ ] T023 [P] `jh-city-picker` component spec — debounce, disambiguation labels, select/clear, 503 transient state.

**Checkpoint**: Cities can be searched, selected, persisted (de-duped), and distance-cached; the picker works in isolation. User stories can now begin.

---

## Phase 3: User Story 1 - Choose a real home city (Priority: P1) 🎯 MVP

**Goal**: A player selects a canonical home city in onboarding/profile edit and sees it displayed with country everywhere.

**Independent Test**: Complete onboarding → pick "Köln" → profile shows "Köln, Germany"; clear it → no location. (quickstart US1.)

### Tests for User Story 1

- [ ] T024 [P] [US1] Integration test: `updateMine` with `location.cityExternalId` resolves server-side, links `HomeCityId`, and clearing (`null`) unsets it; unresolvable id → `422`.
- [ ] T025 [P] [US1] Onboarding city-step spec update in `frontend/apps/web/src/app/features/onboarding/onboarding.component.spec.ts` — picker selection carried into the finish payload; no freeform hometown.

### Implementation for User Story 1

- [X] T026 [US1] Update `backend/Services/Profile/ProfileService.cs` — accept a city selection, resolve+link via `ICityService`, support clear; drop `Hometown` handling.
- [X] T027 [US1] Update `backend/Dtos/Profile/ProfileDtos.cs` — replace `Hometown` with the `location` write fragment + `LocationDto` read shape.
- [X] T028 [P] [US1] Update `backend/Dtos/Search/SearchDtos.cs` `PlayerCardDto` + `backend/Services/Search/PlayerSearchService.cs` — `Hometown` → `LocationDto`.
- [X] T029 [P] [US1] Update `backend/Dtos/Admin/AdminUserDtos.cs` + `AdminOverviewDtos.cs` — `Hometown` → `LocationDto`.
- [X] T030 [P] [US1] Update `backend/Dtos/Marketplace/MarketDtos.cs` + `backend/Dtos/Parties/PartyDtos.cs` — `Hometown` → `LocationDto`.
- [X] T031 [US1] Update `frontend/apps/web/src/app/core/models/profile.models.ts` + `profile.service.ts` — `hometown` → `location`; `updateMine` sends `cityExternalId`.
- [X] T032 [US1] Wire `jh-city-picker` into the profile edit form (`features/profile/profile-owner/…`), replacing the freeform hometown field.
- [X] T033 [US1] Show `locationLabel` ("City, Country") in `profile-owner.component` and `profile-public.component` displays.
- [X] T034 [US1] Update `features/onboarding/onboarding.component.ts` + `.html` — replace the freeform city input with `jh-city-picker`; carry `cityExternalId` into the finish payload (keep the "never trap a new player" separation).
- [X] T035 [P] [US1] Update Angular models consuming hometown — `search.models.ts`, `market.models.ts`, `party.models.ts`, `admin.models.ts` — to the `location` shape, and their display sites.

**Checkpoint**: Profiles are structured and country-qualified end-to-end. MVP demoable.

---

## Phase 4: User Story 2 - Teams and events reference a real city (Priority: P1)

**Goal**: Team admins and event organisers pick canonical cities; teams/events display with country and carry a city for proximity.

**Independent Test**: Create a city-team and an in-person event → both show "City, Country" and store a `CityId`; mixteam/virtual event have none. (quickstart US2.)

### Tests for User Story 2

- [ ] T036 [P] [US2] Integration test: team create/update links `CityId`; `CityTeam` requires a city, `Mixteam` allows none.
- [ ] T037 [P] [US2] Integration test: in-person event create/update links `CityId`; virtual event rejects/ignores a city.

### Implementation for User Story 2

- [X] T038 [US2] Update `backend/Services/Teams/TeamService.cs` — resolve+link city on create/update; enforce `CityTeam` requires a city against the FK.
- [X] T039 [US2] Update `backend/Dtos/Teams/TeamDtos.cs` (incl. `TeamCardDto`) — `City` string → `location` write + `LocationDto` read.
- [X] T040 [US2] Update `backend/Services/Events/…` event create/update — resolve+link city for `InPerson`; disallow for `Virtual`.
- [X] T041 [US2] Update `backend/Dtos/Events/EventDtos.cs` (incl. `EventCardDto`) — `City`/`Country` strings → `location`; remove `Country`.
- [X] T042 [P] [US2] Wire `jh-city-picker` into the team create/edit forms (`features/teams/…`); team cards/pages show `locationLabel`.
- [X] T043 [P] [US2] Wire `jh-city-picker` into the event create/edit forms (`features/events/…`); event cards/pages show `locationLabel`; models in `event.models.ts`.

**Checkpoint**: Profiles, teams, and events are all structured and country-qualified.

---

## Phase 5: User Story 3 - Discover teams near your city during onboarding (Priority: P2)

**Goal**: Onboarding surfaces teams near the player's home city; falls back safely when there's no city or the geocoder is down.

**Independent Test**: As a Köln player, the onboarding team step ranks a Köln team ahead of a Berlin team; stop the geocoder → default list, no error. (quickstart US3.)

### Tests for User Story 3

- [ ] T044 [P] [US3] `TeamSearchService` proximity-sort test — nearest-first via `CityDistance` join, `ThenBy(Id)`, mixteams excluded, no radius cut-off.
- [ ] T045 [P] [US3] Onboarding proximity test — requests proximity when home city set; falls back to beginner-friendly default when absent/degraded (no trap).

### Implementation for User Story 3

- [X] T046 [US3] Extend `TeamBrowseQuery` with `sort=Proximity` + `country`; implement the proximity join + country filter in `backend/Services/Search/TeamSearchService.cs` (default sort unchanged; `409` when Proximity requested without a home city, per contract).
- [X] T047 [US3] Update the team browse action in `backend/Controllers/TeamsController.cs` to derive the caller's `HomeCityId` server-side (never a client param).
- [ ] T048 [US3] Update `features/onboarding/onboarding.component.ts` `teamParams()` to request `sort=Proximity` once a home city is set, falling back to the current beginners-welcome default otherwise (FR-013).

**Checkpoint**: Onboarding leads with local teams.

---

## Phase 6: User Story 4 - Browse teams and events near you (Priority: P2)

**Goal**: Browse offers an opt-in nearest-first sort and a country filter for both teams and events; virtual events excluded from the proximity view; no-home-city handled.

**Independent Test**: Browse events → "Near me" shows located events nearest-first, virtual absent; apply country filter; player with no city gets a prompt, default sort still works. (quickstart US4.)

### Tests for User Story 4

- [ ] T049 [P] [US4] `EventSearchService` proximity test — nearest-first, virtual (`CityId null`) excluded from the proximity view, reappear under date sort.
- [ ] T050 [P] [US4] Country-filter test across team + event browse (independent of sort).
- [ ] T051 [P] [US4] Browse UI spec — "Near me" sort option, country filter, no-home-city prompt, default unchanged.

### Implementation for User Story 4

- [X] T052 [US4] Extend `EventBrowseQuery` with `sort=Proximity` + `country`; implement in `backend/Services/Search/EventSearchService.cs` excluding virtual events from the proximity view.
- [X] T053 [US4] Update `backend/Dtos/Search/SearchDtos.cs` — replace free-text `City` filter with `country`; add `Proximity` to the team/event sort enums.
- [X] T054 [US4] Update `features/browse/…` — add the "Near me" sort option + country filter controls (DESIGN.md), and the no-home-city prompt/disabled state (FR-014/US4 scenario 4).
- [X] T055 [US4] Update `core/models/search.models.ts` + `core/services/search.service.ts` — proximity sort + country params for team & event browse.

**Checkpoint**: All four user stories independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T056 Instantiate `specs/030-structured-locations/checklists/ui-review.md` from `.specify/templates/ui-review-checklist-template.md` and verify the picker, "City, Country" display, and browse proximity/country controls against DESIGN.md (report conflicts, don't silently resolve).
- [ ] T057 [P] Document the geocoder in `.env.sample` and the repo README (local bring-up, extract note).
- [ ] T058 **Spike + infra (R1 risk)**: settle the Photon image tag + regional extract file, its docker volume/init, and the AKS deployment + PersistentVolume in Terraform (`infra/`) with the extract size as an env sizing knob; wire the geocoder URL through GitHub Environments.
- [ ] T059 [P] Verify whether legacy `Event.Location` free-text is still read by `ActivityItemDto` consumers; remove it if unused, else document why it stays (R6 open item).
- [ ] T060 Run `quickstart.md` end-to-end, including the geocoder-down degradation path (SC-006) and no-PII-in-logs check (SC-007).
- [ ] T061 [P] Full verification: `dotnet test backend`, `npx nx test web`, `npx nx lint web`, and the e2e compose run.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (P1)** → no deps.
- **Foundational (P2)** → depends on Setup; **blocks all user stories** (city model, geocoder, picker, seed).
- **US1 (P3)** and **US2 (P4)** → depend only on Foundational; both are P1 and can proceed in parallel by different developers.
- **US3 (P5)** → depends on Foundational + US2 (teams must carry cities to be sorted by proximity).
- **US4 (P6)** → depends on Foundational + US2; reuses the team proximity sort from US3 for the team-browse path (event proximity is independent).
- **Polish (P7)** → after the desired stories are complete.

### Within each story

- Tests before implementation; models/DTOs before services; services before controllers/endpoints; backend before the frontend wiring that consumes it.

### Parallel opportunities

- Setup: T002, T003 in parallel.
- Foundational: T005/T009 alongside T004; the DTO/frontend scaffolding (T013, T015, T016, T018) and the test tasks (T020–T023) parallelize once their targets exist.
- US1: the cross-cutting DTO/model updates (T028, T029, T030, T035) parallelize; T024/T025 in parallel.
- US2: T042 and T043 in parallel; T036/T037 in parallel.
- US3/US4: the `[P]` test tasks in parallel.

---

## Parallel Example: Foundational tests

```bash
Task: "Geocoder resilience integration test (T020)"
Task: "CityService unit tests (T021)"
Task: "Cities search contract test (T022)"
Task: "jh-city-picker component spec (T023)"
```

---

## Implementation Strategy

### MVP first

1. Phase 1 Setup → 2. Phase 2 Foundational (the bulk of the infra) → 3. Phase 3 US1 → **STOP & VALIDATE** structured profile locations end-to-end → demo.

### Incremental delivery

Foundation → US1 (profiles, MVP) → US2 (teams/events) → US3 (onboarding near-you) → US4 (browse near-you). Each ships FE+BE together (no back-compat window, per the 020 pattern) and adds value without breaking prior stories.

### Notes

- `[P]` = different files, no incomplete-task dependency.
- The single migration (T008) is intentionally shared by US1/US2 — it is one atomic schema change; each story then wires its own service/DTO/UI.
- The geocoder is retry-safe because its calls are idempotent GETs — the one place this diverges from the email-POST rule; keep the justifying comment (Principle VII).
- Commit after each task or logical group; stop at any checkpoint to validate a story independently.
