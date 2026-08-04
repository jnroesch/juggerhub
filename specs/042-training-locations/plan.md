# Implementation Plan: Structured Locations for Trainings

**Branch**: `feat/042-training-locations` | **Date**: 2026-08-04 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/042-training-locations/spec.md`

## Summary

Trainings currently hold one free-text `Location` string. Events already hold `VenueName` +
`Street` + `PostalCode` + a canonical `City` FK, selected through the shared `jh-city-picker`
(feature 030) and resolved server-side from a `CityExternalId`. This feature copies that model
onto `Training`, adds a matching **per-session override block** to `TrainingSession`, converts
`Training.Location` into a derived legacy label (exactly as `Event.Location` already is), and
routes every training location label through the existing shared
`HomeProjections.LocationLabel(city, venue, legacy)`.

The two decisions that shape the whole implementation:

1. **The session address override is an indivisible block keyed on `CityIdOverride`.** Every
   other 018 override uses `X ?? Training.X`. Applying that per-field to an address is a defect:
   a session relocated to a venue-less address would inherit the *series'* venue name, and a
   session with its own street could render under the series' city. The effective address is
   therefore selected as a whole — `s.CityIdOverride != null ? (session block) : (series block)`
   — in every projection.
2. **The label is computed server-side, once.** Read DTOs carry a `LocationLabel` string built by
   the same helper events use. SC-003 ("character-for-character identical to an event at the same
   address") becomes structural rather than something two client templates have to agree on.

No new outbound network call is introduced: city resolution is a local SQL lookup against the
seeded `CityReference` table (030 research R8), not an external geocoder.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Angular 22.1 (frontend)

**Primary Dependencies**: Entity Framework Core, ASP.NET Core; Nx 23.1, Tailwind CSS, Transloco

**Storage**: PostgreSQL 18 — four new nullable columns on `Trainings`, four on `TrainingSessions`,
two `Restrict` FKs to the existing `Cities` table. One EF migration, no data migration.

**Testing**: xUnit integration tests (`backend/tests/JuggerHub.Api.IntegrationTests`, real HTTP +
seeded `TEST:*` cities), Jest component specs (frontend), Playwright e2e (`web-e2e`)

**Target Platform**: Linux containers — local compose, AKS on Dev/Prod

**Project Type**: Web application (separate `backend/` and `frontend/`)

**Performance Goals**: No new query patterns. Session list/agenda projections gain one extra
`LEFT JOIN` to `Cities` per address source (series + session override); both are already-loaded
navigations on indexed FKs.

**Constraints**: Frontend and backend ship together — the training request/response contract
changes shape (free-text `location` out, structured block in) with no compatibility window. This
follows the precedent set by feature 020.

**Scale/Scope**: 3 backend services, 6 DTO shapes, 1 migration, 3 Angular forms, 3 read surfaces,
3 i18n catalogues.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Assessment |
|------|------------|
| **1. Architecture** — thin controllers, DI'd services, no repository, DTOs via explicit `.Select` | **PASS.** No new controller logic; `TrainingsController` forwards unchanged. The address block is added to the existing `RowProjection` expression and the detail projection as explicit `.Select` members. No object mapper introduced. |
| **2. Data access** — pagination, projections, `AsNoTracking`, `BaseEntity` | **PASS.** No new entity, so no new key generation. Existing paginated list paths are untouched in shape. Reads stay `AsNoTracking` + projected. No `ExecuteUpdateAsync` path is added. |
| **3. Security** — OWASP, never trust the client | **PASS, and load-bearing.** The client sends a `CityExternalId`, never a city name, coordinates or a resolved id: the server resolves it through `ICityService.ResolveAndUpsertAsync` and rejects an unknown id (FR-005). Street/postal/city presence is enforced in the service, not only the form (FR-015). Address visibility rides on the existing `TrainingGuard` / visibility gates — no new read path is created (FR-014). |
| **4. Auth** | **PASS.** Untouched. |
| **5. Conventions** — separate `.html`/`.css`/`.ts`, `.ps1` only | **PASS.** New `shared/address-fields/` component ships as three files. Migration is added via `dotnet ef`, no new scripts. |
| **6. Environment parity** | **PASS.** No environment-specific configuration. The `CityReference` seed already exists in all three environments. |
| **7. UI/Design compliance** | **REQUIRED.** Three forms and three read surfaces change. `.specify/templates/ui-review-checklist-template.md` must be instantiated as `specs/042-training-locations/checklists/ui-review.md` and run against the diff before verification. |
| **8. Resilience** — Principle VII | **NOT ENGAGED, and this must stay true.** City resolution is a local database query against the seeded `CityReference` table (030 R8), not an outbound HTTP call. No `HttpClient`, retry policy, timeout or breaker belongs in this feature. A reviewer seeing the word "geocoding" should not add one. |

**Result: no violations.** Complexity Tracking is therefore omitted.

**Post-design re-check (after Phase 1).** The design added exactly two new artifacts, both
re-evaluated against the gates: `StructuredAddress` is a static class of pure functions in an
existing namespace, adding no DI lifetime, interface or indirection (gate 1 — and it *removes*
duplication rather than adding a layer); `jh-address-fields` ships as separate `.ts`/`.html`/`.css`
(gate 5). No endpoint, entity, migration-time data change, outbound call or configuration key was
introduced beyond what the table above assessed. Gates unchanged: **no violations**.

## Project Structure

### Documentation (this feature)

```text
specs/042-training-locations/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── trainings-api.md # Phase 1 output — request/response deltas
├── checklists/
│   ├── requirements.md  # Spec quality (complete)
│   └── ui-review.md     # Instantiated during implementation (gate 7)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
backend/
├── Entities/
│   ├── Training.cs                      # + VenueName, Street, PostalCode, CityId, City
│   └── TrainingSession.cs               # + VenueNameOverride, StreetOverride,
│                                        #   PostalCodeOverride, CityIdOverride, CityOverride
├── Dtos/Trainings/TrainingDtos.cs       # 6 shapes: free-text Location out, structured in
├── Data/
│   ├── AppDbContext.cs                  # Training + TrainingSession config: lengths, 2 FKs
│   └── Migrations/                      # + AddTrainingStructuredLocations
├── Services/
│   ├── Geocoding/
│   │   ├── StructuredAddress.cs         # NEW — shared pure helpers, extracted from EventService
│   │   └── LocationLabels.cs            # unchanged
│   ├── Events/EventService.cs           # refactored to call the extracted helpers (no behaviour change)
│   ├── Trainings/
│   │   ├── TrainingSeriesService.cs     # create + series edit + RowProjection
│   │   └── TrainingSessionService.cs    # single-session edit (freeze + block override) + detail
│   └── Home/HomeProjections.cs          # training agenda item uses LocationLabel(...)
└── tests/JuggerHub.Api.IntegrationTests/Trainings/TrainingApiTests.cs

frontend/apps/web/src/app/
├── shared/address-fields/               # NEW — venue + street + postal + jh-city-picker
├── core/models/trainings.models.ts      # request/response models follow the contract
├── features/trainings/
│   ├── training-create/                 # step 3 "Where"
│   ├── training-edit/                   # series form + single-session form
│   ├── trainings-tab/                   # renders locationLabel
│   └── training-session/                # renders locationLabel + structured address
└── ../../public/i18n/{en,de,es}.json    # new form + validation keys
```

**Structure Decision**: The existing web-application split is used as-is. The only new directories
are `backend/Services/Geocoding/StructuredAddress.cs` (a single static class, not a service — see
research R2) and `frontend/apps/web/src/app/shared/address-fields/` (justified by three call sites
inside this feature alone — see research R5).

## Key Risks

| Risk | Mitigation |
|------|-----------|
| **The `??` override reflex.** `TrainingSession` already has five `X ?? Training.X` overrides. Writing the address the same way silently mixes a session's street with the series' city. | The block rule is stated in the entity XML doc, in `data-model.md`, and covered by a dedicated integration test that relocates a session to a **venue-less** address under a series that **has** a venue and asserts the venue does not leak. |
| **`EditSessionAsync`'s freeze step.** Lines 76–80 copy inherited values into overrides on *any* single-session edit. Copying the address too is correct (018 semantics: a single-session edit detaches everything) but means `CityIdOverride != null` after a time-only edit. | Intended and documented. The follow-on rule matters more: when the effective kind ends up `Virtual`, the four address overrides are **nulled**, so no virtual session ever stores an address (FR-003). |
| **`Training.Location` still writable.** It becomes derived; any code path still assigning user text to it reintroduces the free-text model. | The field is removed from `CreateTrainingRequest`, `EditSeriesRequest` and `EditSessionRequest`, so there is no longer an input to assign. It is set only by the legacy-label helper. |
| **Silent i18n fallback.** `useFallbackTranslation: true` + `fallbackLang: 'en'` (031) means a missing `de`/`es` key renders English with no signal, and **no key-parity guard exists for the main catalogues** (only `legal-catalog.spec.ts`, which covers the legal scope). | Verified today: en/de/es are at full parity apart from the deliberate `_meta.*` keys in de/es (1238 / 1240 / 1240). A parity spec for the main catalogues that excludes `_meta` is therefore safe to add now and is included as a task. |
| **Contract break with no compatibility window.** Free-text `location` disappears from six DTOs. | Frontend and backend ship in one PR (precedent: feature 020). The e2e suite and the training component specs are the backstop. |
| **Scope creep into events.** The event edit form has no city picker at all, so an event's city cannot be changed after creation. | Explicitly out of scope. Recorded as a follow-up issue; `EventService` is touched **only** to call the extracted helpers, with the existing event tests proving no behaviour change. |

## Complexity Tracking

Not applicable — Constitution Check passed with no violations.
