---

description: "Task list for 042 — Structured Locations for Trainings"
---

# Tasks: Structured Locations for Trainings

**Input**: Design documents from `/specs/042-training-locations/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/trainings-api.md](./contracts/trainings-api.md),
[quickstart.md](./quickstart.md)

**Tests**: Included. The contract test checklist in
[contracts/trainings-api.md §6](./contracts/trainings-api.md) and the definition of done in
[quickstart.md](./quickstart.md) both call for them explicitly.

**Organization**: Grouped by user story so each is an independently testable increment.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: `US1`–`US4`, mapping to the four user stories in spec.md
- Exact file paths are given in every task

## Path Conventions

Web application per [plan.md](./plan.md): `backend/` and `frontend/apps/web/src/app/`.
Backend tests live in `backend/tests/JuggerHub.Api.IntegrationTests/`.

---

## Phase 1: Setup

**Purpose**: Put the two guards in place *before* the code they guard exists.

- [X] T001 Copy `.specify/templates/ui-review-checklist-template.md` to `specs/042-training-locations/checklists/ui-review.md` (Constitution gate 7; three forms and three read surfaces change, so instantiate it now and fill it as the UI lands)
- [X] T002 [P] Add a main-catalogue i18n key-parity spec at `frontend/apps/web/src/app/core/i18n/catalog-parity.spec.ts` comparing the flattened key sets of `frontend/apps/web/public/i18n/{en,de,es}.json`, excluding the `_meta.*` namespace — model it on the existing `legal-catalog.spec.ts`. Confirm it is **green before** any key is added (measured at plan time: 1238 / 1240 / 1240, the two extras being `_meta.status` and `_meta.review` in de/es)

**Checkpoint**: The i18n guard is green on untouched catalogues, so any later failure is this feature's own omission.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared address layer, the schema, and the reusable form group. Every user story depends on this phase.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Shared backend helpers (research R2)

- [X] T003 Create `backend/Services/Geocoding/StructuredAddress.cs` with two pure statics extracted from `EventService`: `Resolve(LocationKind, venueName, street, postalCode, virtualLink)` returning the validated parts or a user-facing reason (from `EventService.cs:497-531`), and `ResolveCityAsync(ICityService, LocationKind, LocationSelectionDto?, CancellationToken)` returning `(Guid? CityId, City? City, string? Reason)` (from `EventService.cs:473-495`). Take `ICityService` as a parameter — do **not** register a new DI service. Generalise the two reason strings so "event" is not hard-coded
- [X] T004 Refactor `backend/Services/Events/EventService.cs` to call `StructuredAddress` and delete its now-duplicate private helpers. Keep `LegacyLocationLabel` **in** `EventService` — a virtual event stores `"Online"` while a virtual training must store `null`, so it is deliberately not shared (research R2)
- [X] T005 Run `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter FullyQualifiedName~Events` and confirm every event test still passes — this is the proof that T004 changed no behaviour

### Schema

- [X] T006 [P] Add `VenueName` (≤120), `Street` (≤160), `PostalCode` (≤20), `CityId` (`Guid?`) and the `City` navigation to `backend/Entities/Training.cs`. Update the XML doc on `Location` to state it is now a **system-derived legacy label**, never assigned from a request
- [X] T007 [P] Add `VenueNameOverride`, `StreetOverride`, `PostalCodeOverride`, `CityIdOverride` and the `CityOverride` navigation to `backend/Entities/TrainingSession.cs`. Add an XML doc warning that the address override is an **indivisible block keyed on `CityIdOverride`** and must never be resolved with the `X ?? Training.X` pattern the other five overrides use (data-model.md §2)
- [X] T008 Configure both entities in `backend/Data/AppDbContext.cs` — the four max-lengths in the `Training` block (~line 870) and on the `…Override` columns in the `TrainingSession` block (~line 890), plus two `HasOne(...).WithMany().HasForeignKey(...).OnDelete(DeleteBehavior.Restrict)` city FKs matching the `Event` precedent at lines 249-252 (depends on T006, T007)
- [X] T009 Generate the migration with `dotnet ef migrations add AddTrainingStructuredLocations` run from `backend/`, then review the generated `Up`/`Down`: it must contain **only** `AddColumn`, `CreateIndex` and `AddForeignKey` — no data migration, no `DropColumn`, and `Trainings.Location` untouched (research R7). Apply with `dotnet ef database update` (depends on T008)

### Shared frontend form group (research R5)

- [X] T010 [P] Create `frontend/apps/web/src/app/shared/address-fields/address-fields.component.{ts,html,css}` — a standalone `jh-address-fields` wrapping venue name, street, postal code and `jh-city-picker`. Expose two-way-bindable `venueName` / `street` / `postalCode` inputs plus matching `…Change` outputs, an `initialCity` input (a `Location`, for edit prefill) and a `cityChange` output emitting `CityOption | null`. Keep it template-driven-compatible (`ngModel`) — the training forms are not reactive. Match the input styling used in `features/events/event-create/event-create.component.html:86-94`
- [X] T011 [P] Add `frontend/apps/web/src/app/shared/address-fields/address-fields.component.spec.ts` covering: each field emits its change output; the city output emits the selected option and `null` on clear; `initialCity` is forwarded to the picker
- [X] T012 [P] Add the new form and validation keys to `frontend/apps/web/public/i18n/{en,de,es}.json` — venue/street/postal/city labels and placeholders under the `trainings.form.*` namespace, and the three in-person validation messages. **All three catalogues in the same task**; T002 will fail the build otherwise

**Checkpoint**: Schema, shared helpers and the shared form group exist. Event behaviour is proven unchanged. User stories can now proceed.

---

## Phase 3: User Story 1 — Admin captures a real address when scheduling a training (P1) 🎯 MVP

**Goal**: An in-person training cannot be created without a street, a postal code and a resolved city; a virtual training asks for none of them.

**Independent Test**: Create a recurring and a one-off in-person training through the wizard and confirm all four values are stored and shown on the review step; create a virtual training and confirm no address is captured or required.

### Tests for User Story 1

- [X] T013 [P] [US1] Add create-path contract tests to `backend/tests/JuggerHub.Api.IntegrationTests/Trainings/TrainingApiTests.cs`: in-person with street + postal + city → `200` and all four members returned; missing street → `400`; missing postal code → `400`; no `location` → `400`; unresolvable `cityExternalId` → `400` with the city named; **virtual with an address supplied → `200` and the address stored as `null`** (FR-003). Seeded cities `TEST:köln` / `TEST:berlin` are already available via `TrainingTestSupport.cs:40`

### Implementation for User Story 1

- [X] T014 [US1] In `backend/Dtos/Trainings/TrainingDtos.cs`, change `CreateTrainingRequest`: remove `string? Location`, add `string? VenueName`, `string? Street`, `string? PostalCode` and `LocationSelectionDto? Location` (contracts §1)
- [X] T015 [US1] Update `TrainingSeriesService.CreateAsync` and `ValidateCreate` in `backend/Services/Trainings/TrainingSeriesService.cs`: drop the `string.IsNullOrWhiteSpace(r.Location)` check, call `StructuredAddress.Resolve` then `StructuredAddress.ResolveCityAsync`, assign the four address columns (all `null` when virtual), and set `Location` to the derived legacy label — `"{City.Name}, {City.CountryName}"` for in-person, **`null` for virtual** (data-model.md §5). Inject `ICityService` into the service constructor
- [X] T016 [P] [US1] Update `CreateTrainingRequest` in `frontend/apps/web/src/app/core/models/trainings.models.ts` to match T014, importing `LocationSelection` from `core/models/city.models`
- [X] T017 [US1] Update `frontend/apps/web/src/app/features/trainings/training-create/training-create.component.ts`: replace the single `location` string with `venueName` / `street` / `postalCode` fields and a `selectedCity` signal, add an `onCitySelected` handler, gate the "Where" step's Continue on street + postal + a non-null city for in-person, and build the new request body in `create()`. Import `AddressFieldsComponent`
- [X] T018 [US1] Update `frontend/apps/web/src/app/features/trainings/training-create/training-create.component.html`: replace the single location input at step 3 (lines 81-84) with `<jh-address-fields>` inside the existing `@if (locationKind === 'InPerson')` block, and show venue / street / postal / city label on the review step
- [X] T019 [US1] Run `dotnet test ... --filter FullyQualifiedName~Trainings` and `npx jest` in `frontend/`; walk quickstart.md **US1** end to end, including the negative invented-`cityExternalId` case

**Checkpoint**: In-person trainings now carry structured, resolved addresses. Nothing yet renders them differently.

---

## Phase 4: User Story 2 — Players read one consistent location everywhere (P2)

**Goal**: The trainings tab, the session detail and the dashboard agenda all show the same city-anchored label an event at that address would show.

**Independent Test**: Create an event and a training at the same address; compare the label on all three surfaces character for character.

### Tests for User Story 2

- [X] T020 [P] [US2] Add a label-parity test to `backend/tests/JuggerHub.Api.IntegrationTests/Trainings/TrainingApiTests.cs`: an event and a training created at the same address return an **identical** `locationLabel` (SC-003)
- [X] T021 [P] [US2] Add read-shape tests to `backend/tests/JuggerHub.Api.IntegrationTests/Trainings/TrainingApiTests.cs`: the row, agenda and detail responses no longer carry a free-text `location`; the detail response carries `venueName` / `street` / `postalCode` / `location` (a `LocationDto`) / `locationLabel`; a city-only training (no venue name) yields the city as its label with no stray separator

### Implementation for User Story 2

- [X] T022 [US2] In `backend/Dtos/Trainings/TrainingDtos.cs`, update the three read shapes per contracts §4: `TrainingSessionRowDto` and `AgendaSessionDto` drop `string? Location` and gain `string LocationLabel`; `TrainingSessionDetailDto` drops it and gains `VenueName`, `Street`, `PostalCode`, `LocationDto? Location` and `LocationLabel`. List rows deliberately do **not** carry street or postal code
- [X] T023 [US2] Update `RowProjection` in `backend/Services/Trainings/TrainingSeriesService.cs` (lines 326-344) to select the effective address as an **indivisible block**: `s.CityIdOverride != null ? s.<X>Override : s.Training.<X>` for venue, street, postal and city — **not** `s.<X>Override ?? s.Training.<X>` (data-model.md §2, research R1). Build `LocationLabel` from `HomeProjections.LocationLabel(city, venue, legacy)`. Add `.Include`/navigation access for `Training.City` and `CityOverride` as the projection requires
- [X] T024 [US2] Apply the same block expression and `LocationLabel` to the detail projection in `backend/Services/Trainings/TrainingSessionService.cs` (the anonymous `head` projection around lines 188-235) and to whichever agenda projection feeds `AgendaSessionDto`
- [X] T025 [US2] Update the training branch of `backend/Services/Home/HomeProjections.cs` (line 73) to use `LocationLabel(...)` like the event branch at line 53, instead of `s.Location ?? string.Empty`. The dashboard card already renders `item().locationLabel`, so no frontend change is needed for the agenda surface
- [X] T026 [P] [US2] Update `TrainingSessionRow`, `AgendaSession` and `TrainingSessionDetail` in `frontend/apps/web/src/app/core/models/trainings.models.ts` to match T022
- [X] T027 [US2] Update `frontend/apps/web/src/app/features/trainings/trainings-tab/trainings-tab.component.html` line 41 to render `s.locationLabel` instead of `s.location`
- [X] T028 [US2] Update `frontend/apps/web/src/app/features/trainings/training-session/training-session.component.html` (lines 23-29) to render `s.locationLabel`, with the venue name and street/postal shown as secondary detail when present; keep the existing virtual branch untouched
- [X] T029 [US2] Run backend and frontend tests; walk quickstart.md **US2**, including the same-address event-vs-training comparison on all three surfaces

**Checkpoint**: US1 + US2 deliver the full read/write path for a series address. This is a shippable increment.

---

## Phase 5: User Story 3 — Admin corrects a training's address later (P3)

**Goal**: A team admin can change the whole series' address and city; upcoming sessions that still follow the series pick it up.

**Independent Test**: Edit a series' address and confirm every upcoming non-relocated session shows the new location while past sessions are untouched.

### Tests for User Story 3

- [X] T030 [P] [US3] Add series-edit contract tests to `TrainingApiTests.cs`: a block replace updates every upcoming non-detached session with **no per-session write**; clearing the city on an in-person series → `400`; switching the series to virtual clears the stored address; past sessions keep their recorded location

### Implementation for User Story 3

- [X] T031 [US3] In `backend/Dtos/Trainings/TrainingDtos.cs`, update `EditSeriesRequest` per contracts §2 — the same four members replace `string? Location`
- [X] T032 [US3] Update `EditSeriesAsync` in `backend/Services/Trainings/TrainingSeriesService.cs` (lines 202-210): replace the `if (request.Location is not null)` free-text assignment with a **block replace** — when `request.Location` is present, re-resolve through `StructuredAddress` and assign all four columns together plus the derived legacy label; when absent, leave the address untouched. A `LocationKind` change re-runs resolution, which clears the address on a switch to virtual. Field-by-field patching is explicitly not supported (FR-007)
- [X] T033 [P] [US3] Update `EditSeriesRequest` in `frontend/apps/web/src/app/core/models/trainings.models.ts` to match T031
- [X] T034 [US3] Update `frontend/apps/web/src/app/features/trainings/training-edit/training-edit.component.ts`: extend `prefill()` to populate venue / street / postal and the initial city from the detail response, and build the new address block in `saveSeries()`. Import `AddressFieldsComponent`
- [X] T035 [US3] Update the series form in `frontend/apps/web/src/app/features/trainings/training-edit/training-edit.component.html` (lines 104-112) to use `<jh-address-fields>` with `[initialCity]` bound, so the currently selected city is pre-filled
- [X] T036 [US3] Run backend and frontend tests; walk quickstart.md **US3**

**Checkpoint**: Series addresses are fully editable.

---

## Phase 6: User Story 4 — Admin relocates a single session (P4)

**Goal**: One session can carry its own full address, which survives later series edits and never mixes with the series' address.

**Independent Test**: Relocate one upcoming session; confirm only that date changed, that the series' venue does not leak into it, and that it keeps its own address after a subsequent series-wide address change.

### Tests for User Story 4

- [X] T037 [P] [US4] Add the **venue-leak guard** to `TrainingApiTests.cs` — the single most important test in this feature: give the series a venue name, relocate one session to an address with **no** venue name, and assert the relocated session's `venueName` is `null` and its `locationLabel` does not contain the series' venue. A per-field `??` implementation passes every other test and fails this one (research R1)
- [X] T038 [P] [US4] Add the remaining session-override contract tests to `backend/tests/JuggerHub.Api.IntegrationTests/Trainings/TrainingApiTests.cs`: relocation changes that session only and leaves siblings unchanged; street + postal with no city → `400`; a relocated session retains its address after a later series edit (FR-008); clearing the override returns it to the series address (FR-009); **a session edited to virtual has all four address overrides `null`** (FR-003)

### Implementation for User Story 4

- [X] T039 [US4] In `backend/Dtos/Trainings/TrainingDtos.cs`, update `EditSessionRequest` per contracts §3
- [X] T040 [US4] Update `EditSessionAsync` in `backend/Services/Trainings/TrainingSessionService.cs` in the order given in data-model.md §5: (1) extend the existing `??=` freeze at lines 76-80 to the four address columns; (2) apply the request's address block after validating street + postal + city together; (3) **if the effective kind is now `Virtual`, null all four address overrides and `LocationOverride`**; (4) `Detached = true` as today. Step 3 is what keeps FR-003 true — without it, editing an in-person session to virtual leaves a frozen address behind
- [X] T041 [P] [US4] Update `EditSessionRequest` in `frontend/apps/web/src/app/core/models/trainings.models.ts` to match T039
- [X] T042 [US4] Update the single-session form in `frontend/apps/web/src/app/features/trainings/training-edit/training-edit.component.{ts,html}` to use `<jh-address-fields>` with prefill, and build the address block in `saveSingle()`
- [X] T043 [US4] Run backend and frontend tests; walk quickstart.md **US4**, giving the venue-leak check (step 4) its own explicit pass

**Checkpoint**: All four stories are independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T044 Complete `specs/042-training-locations/checklists/ui-review.md` against the full diff — three forms and three read surfaces, DESIGN.md wins on any conflict (Constitution gate 7)
- [X] T045 [P] Review the whole diff for an accidental `HttpClient`, retry policy, timeout or circuit breaker. There must be none: city resolution is a local SQL query, not an outbound integration (research R6, Constitution gate 8)
- [X] T046 [P] Grep the backend for any remaining assignment to `Training.Location` or `TrainingSession.LocationOverride` outside the derived-label helper — there must be none
- [X] T047 [P] Add or update Playwright coverage in `frontend/apps/web-e2e/` for creating an in-person training with a city, reusing the existing helper `apps/web-e2e/src/support/city.ts`
- [X] T048 Run the full verification suite: `dotnet test backend/tests/JuggerHub.Api.IntegrationTests`, `npx jest`, `npx nx lint web`, `npx nx e2e web-e2e`
- [X] T049 Walk the whole of [quickstart.md](./quickstart.md), including the edge-case table and the definition of done
- [X] T050 [P] Open a GitHub issue for the out-of-scope gap found while planning: `event-edit` re-sends the stored city id with no `jh-city-picker` in its template (`event-edit.component.ts:91-96`), so an event's city cannot be changed after creation
- [X] T051 Record any spec drift in `specs/042-training-locations/spec.md` and report it in the PR description

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately
- **Foundational (Phase 2)**: depends on Setup — **blocks all user stories**
- **US1 (Phase 3)** → **US2 (Phase 4)** → **US3 (Phase 5)** → **US4 (Phase 6)**: see below
- **Polish (Phase 7)**: depends on all desired stories

### User Story Dependencies

These stories are **not** fully independent, and the plan does not pretend otherwise — they share
one entity, one DTO file and two services:

- **US1 (P1)**: depends only on Foundational. The MVP.
- **US2 (P2)**: depends on Foundational. Testable against data created by US1, but the read
  projections can be built and unit-tested against seeded rows if US1 is not done.
- **US3 (P3)**: depends on Foundational; naturally follows US1 (it edits what US1 creates) and is
  best validated after US2, since the acceptance criteria are phrased in terms of the rendered label.
- **US4 (P4)**: depends on Foundational **and** on US2's block-rule projection (T023/T024), which is
  what makes a relocated session render correctly. US4 adds only the write side.

### Within Each User Story

- Tests are written first and must fail before implementation
- Backend DTO → backend service → frontend model → frontend component → verification
- Story complete and verified before moving to the next priority

### Parallel Opportunities

- T002 runs alongside T001
- T006 and T007 are different entity files — parallel; both must land before T008
- T010, T011 and T012 are independent of the backend chain T003→T009 and of each other
- Within each story, the `[P]` test task and the `[P]` frontend-model task are independent of the
  backend implementation chain
- T045, T046, T047 and T050 are independent of one another

---

## Parallel Example: Phase 2

```bash
# Backend schema, in parallel:
Task: "Add address columns to backend/Entities/Training.cs"                 # T006
Task: "Add override columns to backend/Entities/TrainingSession.cs"         # T007

# Independently, the frontend shared group and i18n:
Task: "Create shared/address-fields component"                              # T010
Task: "Add address-fields component spec"                                   # T011
Task: "Add trainings.form.* keys to en/de/es"                               # T012
```

---

## Implementation Strategy

### MVP First (US1 only)

1. Phase 1 Setup — the i18n guard green on untouched catalogues
2. Phase 2 Foundational — **critical**; T005 must prove event behaviour is unchanged before anything else builds on `StructuredAddress`
3. Phase 3 US1
4. **STOP and VALIDATE**: quickstart US1, including the negative city case
5. At this point in-person trainings capture a resolved city — SC-001 and SC-006 are already satisfied even though nothing renders differently yet

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. + US1 → structured capture (MVP)
3. + US2 → consistent labels everywhere; **this is the natural first shippable pair**
4. + US3 → series addresses editable
5. + US4 → per-session relocation, with the venue-leak guard

### Suggested single-PR scope

US1 + US2 + US3 + US4 in one PR. The contract change is breaking (free-text `location` leaves six
DTOs) and frontend and backend ship together, so splitting the stories across PRs would leave
`main` with a broken training contract between merges.

---

## Notes

- `[P]` = different files, no dependencies on incomplete tasks
- The one rule most likely to be got wrong is the **block override** (T007, T023, T037). If a
  reviewer sees `s.VenueNameOverride ?? s.Training.VenueName` anywhere, it is a defect
- The second most likely is the **virtual guard** in T040 step 3
- Commit after each task or logical group; reference `#<issue>` where one exists
- Verify tests fail before implementing them away
