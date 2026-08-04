# Feature Specification: Structured Locations for Trainings

**Feature Branch**: `042-training-locations`

**Created**: 2026-08-04

**Status**: Draft

**Input**: User description: "Structured locations for trainings. Trainings currently store a single free-text `location` string for in-person sessions, while events already capture a structured address (venue name, street, postal code) plus a canonical City selected through the shared city picker (feature 030). Bring trainings up to the same standard so trainings can later be discovered by proximity ("find trainings near me"), exactly as events can."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin captures a real address when scheduling a training (Priority: P1)

A team admin sets up the team's weekly training. On the "Where" step they choose in-person, then fill in the venue name, street and postal code and pick the training's city from the same searchable city list they already use when creating an event or a team. They cannot continue until street, postal code and a city have been supplied. If the training is virtual instead, they supply a link and see no address fields at all.

**Why this priority**: Without structured capture there is no data to render, correct or search on — every other story in this feature depends on it. It is also the only story that changes what the platform *knows*; the rest change what it *shows*.

**Independent Test**: Create a recurring training and a one-off training, both in-person, through the create wizard; confirm the venue, street, postal code and the chosen city are all stored against the training and shown back on the review step. Create a virtual training and confirm no address is captured or required.

**Acceptance Scenarios**:

1. **Given** an admin on the "Where" step with in-person selected, **When** they type at least two characters of a city name, **Then** matching cities are offered and selecting one records that city as the training's city.
2. **Given** an admin on the "Where" step with in-person selected and street, postal code or city missing, **When** they attempt to continue, **Then** the step blocks and identifies what is missing.
3. **Given** an admin on the "Where" step with virtual selected, **When** the step renders, **Then** no venue, street, postal code or city input is shown and only the link is required.
4. **Given** an admin who selected a city and then switches the training to virtual, **When** they submit, **Then** no address or city is stored against the training.
5. **Given** an admin who filled in the address, **When** they reach the review step, **Then** the venue, street, postal code and the city's display label are all shown back to them before they commit.

---

### User Story 2 - Players read one consistent location everywhere (Priority: P2)

A player looking at the team's trainings tab, opening a single training session, or scanning their dashboard agenda sees the training's location described the same way an event's location is described — anchored on the city, with the venue named where one was given — rather than whatever free text somebody typed.

**Why this priority**: This is the visible payoff of story 1 and the thing that makes trainings and events feel like one product. It is worth shipping on its own even before editing is possible.

**Independent Test**: With a training created via story 1, open the trainings tab, the session detail and the dashboard agenda, and confirm all three show the same city-anchored label, matching the wording pattern an event with the same address produces.

**Acceptance Scenarios**:

1. **Given** an in-person training with a venue name and a city, **When** its location is displayed anywhere in the product, **Then** the label names the city and the venue, in the same form an event with the same address uses.
2. **Given** an in-person training with a city but no venue name, **When** its location is displayed, **Then** the label names the city and nothing is left blank or dangling.
3. **Given** a virtual training, **When** its location is displayed, **Then** it reads as online, exactly as it does today.
4. **Given** a session whose location differs from its series, **When** its location is displayed, **Then** the session's own location is shown, not the series'.

---

### User Story 3 - Admin corrects a training's address later (Priority: P3)

The team moves its weekly training to a different hall. The admin opens the series edit form, changes the venue, street, postal code and — if the hall is in a different town — the city, using the same inputs and the same city search as when creating. The change applies to every upcoming session that still follows the series.

**Why this priority**: Addresses change, but less often than they are first entered. A team that cannot yet edit can delete and recreate; a team that cannot enter an address at all is stuck.

**Independent Test**: Edit an existing training's address and city, then confirm every upcoming non-relocated session shows the new location and past sessions are untouched.

**Acceptance Scenarios**:

1. **Given** a training with an address, **When** the admin opens the series edit form, **Then** the existing venue, street, postal code and the currently selected city are pre-filled.
2. **Given** the admin changes the address and saves, **When** upcoming sessions that follow the series are viewed, **Then** they show the new location.
3. **Given** a session that was previously relocated on its own, **When** the series address changes, **Then** that session keeps its own location.
4. **Given** the admin clears the city on an in-person training, **When** they attempt to save, **Then** the save is refused and the missing city is identified.
5. **Given** the admin switches an in-person training to virtual, **When** they save, **Then** the stored address and city are cleared.

---

### User Story 4 - Admin relocates a single session (Priority: P4)

One week the hall is booked, so the training happens elsewhere. The admin edits that one session and gives it its own venue, street, postal code and city. Only that date moves; the rest of the series is unaffected, and the relocated session keeps its own address even if the series address changes afterwards.

**Why this priority**: A genuine but occasional need. Trainings already support relocating one session as free text, so this story raises an existing capability to the same standard rather than introducing a new one.

**Independent Test**: Relocate one upcoming session to a different address and city, then confirm only that session's location changed, that it survives a subsequent series-wide address change, and that the rest of the series is untouched.

**Acceptance Scenarios**:

1. **Given** an upcoming session that follows its series, **When** the admin gives it its own in-person address, **Then** that session alone shows the new location and every other session is unchanged.
2. **Given** a single-session relocation, **When** the admin supplies a street and postal code but no city, **Then** the save is refused and the missing city is identified.
3. **Given** a relocated session, **When** the admin later clears the relocation, **Then** the session returns to showing the series' location.
4. **Given** an in-person series, **When** the admin makes one session virtual, **Then** that session shows as online with its link and carries no address.
5. **Given** a session relocated to its own address, **When** the whole series is edited afterwards, **Then** the relocated session retains its own address.

---

### Edge Cases

- **City search unavailable.** If the city list cannot be reached while the admin is filling in the form, the admin is told the city search is temporarily unavailable and cannot complete an in-person training until it works again. The rest of the form is preserved; nothing is silently saved without a city.
- **City no longer resolvable.** If the city an admin picked can no longer be resolved when the form is submitted, the submission is refused with a message naming the city, rather than storing a training with a broken location.
- **Partial address.** A street with no postal code, a postal code with no street, or either with no city is never accepted for an in-person training. A venue name on its own is not an address and does not satisfy the requirement.
- **Address on a virtual training.** Address fields are never stored for a virtual training, and never displayed, even if values were typed before switching the kind.
- **Series address changed after a session was relocated.** The relocated session keeps its own address; it does not silently snap back to the series.
- **Two cities with the same name.** The admin picks from the search results, which distinguish same-named cities the way they already do for events and teams; the training records the city they picked, not a name match.
- **Existing trainings created before this feature.** No preserved free-text location is reinterpreted or guessed at; see Assumptions.
- **Past sessions.** Past sessions remain read-only; their recorded location is not rewritten by a later series edit.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A training MUST be able to record a venue name, a street, a postal code and a canonical city, in addition to the location kind (in-person or virtual) it records today.
- **FR-002**: The system MUST require a street, a postal code and a resolved city before an in-person training can be created or saved. A venue name remains optional.
- **FR-003**: The system MUST NOT store or display any address or city for a virtual training, and MUST clear any previously stored address when a training changes from in-person to virtual.
- **FR-004**: Admins MUST select a training's city from the same searchable, canonical city list already used when creating an event, a team, or setting a home city — never by typing a free-text city name.
- **FR-005**: The system MUST reject a submission whose selected city cannot be resolved, with a message that names the city, and MUST NOT store a partially resolved location.
- **FR-006**: A single session MUST be able to carry its own venue name, street, postal code and city that replace the series' address for that date only.
- **FR-007**: A session's address override MUST behave as one indivisible block: a session either uses the series' address entirely or its own address entirely. Mixing a session's street with the series' city MUST NOT be possible.
- **FR-008**: A session that carries its own address MUST retain it when the series address is subsequently changed, consistent with how single-session edits already behave.
- **FR-009**: Clearing a session's own address MUST return that session to showing the series' address.
- **FR-010**: The location shown for a training or session MUST be derived from the structured data using the same rule events already use — city first, then venue, falling back to any retained legacy text — and MUST read identically to an event with the same address.
- **FR-011**: The location label MUST be rendered consistently on the team's trainings tab, on a training session's detail view, and in the dashboard agenda.
- **FR-012**: The existing free-text training location MUST become a derived label maintained by the system rather than a value an admin types, mirroring how the equivalent event field is already handled.
- **FR-013**: The training create wizard and both training edit forms (whole series and single session) MUST present the same address inputs and the same city search, and MUST pre-fill the currently stored address and city when editing.
- **FR-014**: The structured address MUST be visible to exactly the audience that can see the training's location today — no wider and no narrower. In particular, making a location structured MUST NOT expose a training's address to anyone who could not already see it.
- **FR-015**: Every address and city validation rule MUST be enforced on the server. Form-level checks exist for guidance only and MUST NOT be the only barrier.
- **FR-016**: A training's recorded city MUST be stored in a form that a later proximity search can filter and sort on, equivalent to how an event's city is stored.

### Key Entities

- **Training (series)**: the recurring or one-off training a team schedules. Gains a venue name, street and postal code, and a reference to a canonical city. Its existing free-text location becomes a system-derived label.
- **Training session**: a single dated occurrence of a training. Gains its own optional venue name, street, postal code and city reference, which together replace the series' address for that date. Continues to inherit everything it does not override.
- **City**: the existing canonical city record shared by events, teams and player home cities. Unchanged by this feature; trainings become a new referrer.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of in-person trainings created after this feature ships carry a resolved city; none can be created without one.
- **SC-002**: An admin can complete the location step of the training create wizard in under 60 seconds, including finding their city.
- **SC-003**: The location text shown for a training and for an event at the same address is character-for-character identical across the trainings tab, session detail and dashboard agenda.
- **SC-004**: Relocating a single session changes the displayed location for that date only — 0 other sessions in the series are affected — and survives a subsequent series-wide address change.
- **SC-005**: 0 in-person trainings exist with a street or postal code but no city, in any environment, at any point after this feature ships.
- **SC-006**: Every training created after this feature ships is discoverable by its city without any further data capture — i.e. a proximity search built later needs no backfill.
- **SC-007**: Admins report no new steps in the virtual-training path: creating a virtual training requires exactly the same inputs as before.

## Assumptions

- **Address overrides are a block, keyed on the city.** A session is treated as having its own address exactly when it has its own city. This is the rule that makes FR-007 checkable and avoids a session showing one street with another city.
- **No data migration or backfill.** All accounts and trainings in every environment — local, Dev and Prod — are test data (owner statement). Existing free-text locations are not parsed, geocoded or guessed into structured addresses; existing trainings are recreated. This mirrors how feature 030 handled the same transition for events and teams.
- **Address visibility follows the training's existing visibility.** Structuring the address does not change who can see it. A public training's address is as visible as its free-text location is today; a team-only training's is not.
- **The city search behaviour is inherited unchanged.** Search minimum length, result ranking, same-name disambiguation and the unavailable state come from the existing shared city selection used by events, teams and onboarding (features 030 and 032). This feature adds a consumer, not a variant.
- **Proximity search is out of scope.** This feature only makes the data structured. Finding, filtering or sorting trainings by distance is separate work, as is any change to how trainings are browsed.
- **Events are not changed.** Event capture, storage and display are the reference model here and are left exactly as they are.
- **Country is not captured separately.** A training's country comes from its city, as it already does for events and teams.
- **A venue name alone is not an address.** It is a label for a place, not a locatable one, and never satisfies the in-person requirement on its own.

## Implementation drift

Recorded during implementation (2026-08-04). No functional requirement changed; every item below is
a correction to a design-artifact assumption, found by building the thing.

1. **Create returns `201 Created`, not `200`.** The contract draft said `200`;
   `TeamTrainingsController` returns `Created`. [contracts/trainings-api.md](./contracts/trainings-api.md) §1 corrected.
2. **`LocationDto` uses `externalId` and `label`**, not `cityExternalId`/`displayLabel` — those are
   the *write* fragment's names. Contract corrected against `backend/Dtos/Cities/CityDtos.cs`.
3. **The row projection had to become two steps.** [data-model.md](./data-model.md) assumed
   `HomeProjections.LocationLabel(...)` could be called inside the EF `Select`. It cannot — it is a
   C# method and EF has no translation for it. The projection now selects the raw parts and composes
   the label after materialization, which is exactly what the event agenda already does, so SC-003's
   "one implementation" is preserved (arguably strengthened).
4. **`DevDataSeeder` needed updating** and was not in the task list. It seeded trainings with
   free-text locations only, which would have left every seeded dev training without a city — the
   reseed *is* the migration path here, so this mattered.
5. **The create wizard needed `data-testid` attributes** before the e2e in T047 could drive it; it
   had almost none.
6. **Two i18n keys were orphaned** by the refactor (`trainings.form.location`,
   `trainings.create.locationPlaceholder`) and were removed from all three catalogues.
7. **`whereComplete` must read signals.** The first implementation was a `computed()` over plain
   fields; in a zoneless app that never recomputes, so "Continue" stayed permanently disabled. The
   e2e caught it — no unit test would have.
8. **`jh-city-picker` reads its `initial` in `ngOnInit`**, so a value arriving later never reaches
   the chip. Hosts must render the address group only after their data has loaded. Documented on
   `AddressFieldsComponent.initialCity` and pinned by a spec.

Two out-of-scope defects found and filed rather than fixed here: **#136** (an event's city cannot be
changed after creation — `event-edit` has no city picker) and **#137** (`mt-2xs`/`py-3xs` are used in
templates but absent from the Tailwind spacing scale, so they emit nothing).
