# Phase 0 Research: Structured Locations for Trainings

**Feature**: 042-training-locations | **Date**: 2026-08-04

The Technical Context carried no `NEEDS CLARIFICATION` markers — the reference implementation
(events, feature 030) is in the repository and was read directly. The research below records the
decisions that reading it forced, each with the file and line the evidence came from.

---

## R1 — How is a session's effective address selected?

**Decision**: As an **indivisible block, keyed on `CityIdOverride`**. In every projection:

```text
hasOwnAddress = s.CityIdOverride != null
venue  = hasOwnAddress ? s.VenueNameOverride : s.Training.VenueName
street = hasOwnAddress ? s.StreetOverride    : s.Training.Street
postal = hasOwnAddress ? s.PostalCodeOverride: s.Training.PostalCode
city   = hasOwnAddress ? s.CityOverride      : s.Training.City
```

**Rationale**: Every existing 018 override uses `X ?? Training.X`
(`TrainingSeriesService.cs:333-337`). Applied per-field to an address that pattern produces two
concrete defects:

- A session relocated to a venue with no name, under a series that has one, renders the *series'*
  venue name against the *session's* street.
- A session that overrides only its street renders that street under the series' city — the exact
  failure FR-007 names.

Keying on the city is reliable because an in-person address is invalid without a city (FR-002), so
`CityIdOverride != null` is equivalent to "this session carries its own address". A virtual session
has no address on either side, so the branch is inert.

**Alternatives considered**:

- *Per-field `??`* — rejected above. It is the shape a reviewer will reflexively expect, which is
  why the rule is repeated in the entity XML doc and pinned by a dedicated test.
- *A separate `HasAddressOverride` boolean* — a second source of truth that can disagree with the
  columns. The city FK already carries the signal exactly.
- *A shared owned type / EF complex type for the address* — attractive on paper, but `Training` and
  `TrainingSession` need different nullability and `Event` already models the same four fields as
  plain columns. Diverging here would make the training model harder to compare with its reference.

---

## R2 — Where does the shared address logic live?

**Decision**: Extract the two **pure** helpers from `EventService` into a new static
`backend/Services/Geocoding/StructuredAddress.cs`:

- `Resolve(kind, venueName, street, postalCode, virtualLink)` → the validated result or a
  user-facing reason (today `EventService.cs:497-531`)
- `ResolveCityAsync(ICityService, kind, LocationSelectionDto?, ct)` → `(CityId, City, Reason)`
  (today `EventService.cs:473-495`), taking the interface as a parameter rather than becoming a
  DI-registered service of its own

`EventService` is refactored to call them; the existing event tests prove no behaviour change.

**Rationale**: SC-003 requires a training and an event at the same address to render identically.
Two copies of the same 60 lines guarantee they drift. A static class with pure functions adds no
DI layer, no interface, and no indirection — consistent with Principle II's "lean" stance and with
`LocationLabels.cs`, which is already exactly this shape in the same namespace.

**Alternatives considered**:

- *Copy the helpers into the training services* — the drift SC-003 forbids.
- *A DI-registered `IAddressResolver`* — an interface and a lifetime for two pure functions plus
  one call that already receives `ICityService`. Rejected as unjustified indirection.
- *Leave them private and have trainings call `EventService`* — a service-to-service dependency
  between two unrelated domains.

**Not extracted**: the legacy-label helper. `Event.LegacyLocationLabel` returns `"Online"` for a
virtual event (`EventService.cs:534-537`); a virtual training must keep `Location = null`, because
`RowProjection` already nulls the location for a virtual session and the client renders "Online"
from the kind (`TrainingSeriesService.cs:336`, `training-session.component.html:24-28`). Sharing
one function across the two would have to be parameterised into meaninglessness. Each service
composes the city label itself; `LocationLabels.Display(name, countryName)` stays the shared part.

---

## R3 — What does a single-session edit do to the address?

**Decision**: Follow the existing freeze, then guard the virtual case.

1. Freeze inherited values into overrides exactly as `TrainingSessionService.cs:76-80` already does
   for time, kind, location and link — extended to the four address columns.
2. Apply the request.
3. **If the resulting effective kind is `Virtual`, null all four address overrides.**
4. Set `Detached = true` (unchanged).

**Rationale**: Step 1 is 018's stated semantics — "a single-session edit sets the relevant
overrides and `Detached = true` so the session no longer follows subsequent whole-series edits"
(`TrainingSession.cs:6-9`). Extending the freeze to the address is what makes FR-008 hold without
new machinery. A consequence to be explicit about: after a *time-only* single-session edit,
`CityIdOverride` is non-null even though the admin never touched the address. That is correct — the
session is detached, and its address is now genuinely its own.

Step 3 is what keeps FR-003 true. Without it, editing an in-person session to virtual leaves a
frozen address sitting in the override columns for a session that has no address.

**Alternatives considered**:

- *Only write address overrides when the request contains an address* — leaves a detached session
  still tracking the series address, contradicting both FR-008 and 018's detach semantics.
- *Clear the overrides when the request omits the address* — makes an unrelated time edit silently
  relocate the session back to the series address.

---

## R4 — How do the edit requests carry the address?

**Decision**: Mirror the event contract exactly. `CreateTrainingRequest`, `EditSeriesRequest` and
`EditSessionRequest` drop `string? Location` and gain
`string? VenueName`, `string? Street`, `string? PostalCode`, `LocationSelectionDto? Location`
(`EventDtos.cs:23-27`, `51-55`). On the series edit, the address is replaced as a block whenever
`Location` is present — not patched field by field, for the R1 reason.

**Rationale**: The name `Location` is reused with a new type rather than invented fresh, so the
training and event contracts read the same. `LocationSelectionDto` already carries
`CityExternalId` + a name hint, which is the never-trust-the-client shape (Principle I): the
client cannot supply coordinates or a resolved id.

**Alternatives considered**:

- *Keep `string? Location` alongside the structured fields for compatibility* — two sources of
  truth for the same value, and the free-text one is the forgeable one. There is no external API
  consumer to keep compatible; frontend and backend ship together (feature 020 precedent).
- *A distinct field name such as `City`* — gratuitous divergence from the event contract.

---

## R5 — Reactive vs template-driven forms, and does a shared component earn its place?

**Decision**: Keep the training forms **template-driven** (`ngModel`) and add a new standalone
`jh-address-fields` component (`shared/address-fields/`) wrapping venue + street + postal code +
`jh-city-picker`, exposing two-way-bindable inputs so `[(venueName)]` works.

**Rationale**: The event wizard is reactive (`event-create.component.ts:60-62`); both training
forms are template-driven (`training-create.component.ts:41-43`,
`training-edit.component.ts:53-55`). Converting trainings to reactive forms is a large,
behaviour-preserving rewrite that this feature does not need — "prefer existing project patterns"
(CLAUDE.md). `jh-city-picker` is form-API agnostic: `@Input() initial`, `@Input() placeholder`,
`@Output() selectedChange` (`city-picker.component.ts:30-36`), already consumed by four call sites.

The wrapper earns its place on count alone: three training forms (create step 3, series edit,
single-session edit) need the identical four-field group. Without it the markup and its validation
messages are copy-pasted three times inside one feature.

**Alternatives considered**:

- *Convert the training wizards to reactive forms first* — a refactor larger than the feature.
- *Inline the four fields three times, as `event-create` does once* — triples the i18n and
  accessibility surface for no benefit.
- *Also migrate `event-create` onto the new component* — out of scope ("no change to events") and
  would put an events refactor on a training feature's critical path.

---

## R6 — Does this feature engage Principle VII (resilience)?

**Decision**: **No.** No timeout, retry, backoff or circuit breaker belongs in this feature.

**Rationale**: The name "geocoding" is misleading here. `CityService` resolves against the
**bundled, seeded `CityReference` table** — "a local SQL query, not an external geocoder"
(`CityService.cs:9-14`, feature 030 research R8). Feature 030's Photon geocoder was never deployed
(recorded in the 036 plan). The only failure mode is a database fault, which the EF execution
strategy already covers. Adding an `HttpClient`, a resilience pipeline or a breaker here would be
inventing an integration that does not exist.

**Alternatives considered**: none — this entry exists to stop a resilience review from
manufacturing a requirement, in the same spirit as gate 8 in the Constitution Check.

---

## R7 — Migration shape and existing data

**Decision**: One migration, `AddTrainingStructuredLocations`, adding eight nullable columns and
two `Restrict` FKs. **No data migration, no backfill, no destructive step.**

**Rationale**: All accounts and trainings in every environment are test data (owner statement,
carried into the spec's Assumptions). Existing rows get nulls and keep their free-text
`Trainings.Location`, which the display rule already falls back to
(`HomeProjections.LocationLabel`, city → venue → legacy). So pre-existing trainings keep rendering
their old text instead of showing a blank, and no row is deleted.

`Restrict` on both FKs matches the event and team precedent (`AppDbContext.cs:249-252`) — a city
that trainings reference is not deletable.

**Alternatives considered**:

- *Backfill by geocoding existing free text* — unreliable by construction, and pointless against
  test data.
- *Delete the legacy column in the same migration* — would blank every existing training's
  location and break the fallback the display rule depends on. `Trainings.Location` stays,
  becoming a system-derived value.
- *Make `CityId` non-nullable* — impossible: virtual trainings have no city, and the column must
  also accept existing rows.

---

## R8 — i18n guard

**Decision**: Add a key-parity Jest spec for the main `en`/`de`/`es` catalogues, excluding the
`_meta.*` namespace.

**Rationale**: 031 sets `useFallbackTranslation: true` with `fallbackLang: 'en'`, so a missing
`de` key renders English with no visible signal — the same hazard 036 guarded for the legal
catalogue with `legal-catalog.spec.ts`. That guard covers only the legal scope; the main
catalogues have none, and this feature adds form labels and validation messages to all three.

Measured before committing to it: `en` 1238 keys, `de` 1240, `es` 1240 — full parity apart from
`_meta.status` and `_meta.review`, which exist only in `de`/`es` by design (translation-status
metadata). The guard is therefore green today and safe to add.

**Alternatives considered**:

- *Change the global fallback* — 036 already rejected this; it would alter behaviour app-wide.
- *Skip the guard* — leaves this feature's Spanish and German form labels able to ship as English
  without failing anything.
