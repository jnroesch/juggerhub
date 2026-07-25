# Feature Specification: Structured Locations & "Near You" Discovery

**Feature Branch**: `030-structured-locations`

**Created**: 2026-07-25

**Status**: Draft

**Input**: GitHub issue #78 — "Structured locations: real cities with country, powering 'near you' search and profile display." Related feature: 029 (onboarding team search).

## Clarifications

### Session 2026-07-25

- Q: Which geocoding provider and parity model? → A: A **self-hosted OpenStreetMap-based geocoder** (e.g. Nominatim/Photon) run as a container in **all** environments (docker-compose locally, in-cluster on AKS) — no API key, no per-request billing, no user location sent to a third party, identical provider everywhere.
- Q: How should "near you" behave — sort-only or a distance cut-off? → A: **Sort-only, nearest-first, no radius cut-off**; nothing is hidden by distance. The country filter is a separate control.
- Q: On the browse screens, is proximity the default ordering or an opt-in sort? → A: **Opt-in** — browse keeps its current default ordering (name/date); "near me" is one selectable sort option. (Onboarding still auto-leads with near-you per User Story 3.)
- Q: In a proximity-sorted event list, where do virtual/location-less events go? → A: **Excluded from the proximity-sorted view** — a "near me" event sort shows only located events; virtual/location-less events are not shown until the player switches away from proximity sort.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Choose a real home city (Priority: P1)

A player setting up their profile (during first-login onboarding or later from profile edit) types the start of their city name, sees a short list of **real, disambiguated cities** ("Köln, North Rhine-Westphalia, Germany" — not just "Köln"), and picks one. Their profile then displays their location **with the country** next to it (e.g. "Köln, Germany"). They can change or clear it later.

**Why this priority**: This is the foundation the whole feature rests on. Without a structured, canonical city on the profile there is no country to display and no anchor point for "near you". It is independently valuable on its own: it upgrades every place a player's location is shown from an ambiguous free-string to a real, country-qualified place.

**Independent Test**: Complete onboarding (or edit a profile), search for a city, select it, and confirm the saved profile shows "City, Country". Fully testable without any team, event, or proximity feature existing.

**Acceptance Scenarios**:

1. **Given** a player on the onboarding city step, **When** they type at least the first characters of a city name, **Then** a debounced type-ahead list of real matching cities is shown, each labelled with enough context (region and/or country) to tell same-named cities apart.
2. **Given** the type-ahead list is shown, **When** the player selects a city, **Then** that canonical city (with its country) is attached to their profile and the free-text entry is replaced by the chosen city's label.
3. **Given** a player who has selected a city, **When** their profile is viewed anywhere location is shown, **Then** it displays the city and country together (e.g. "Köln, Germany").
4. **Given** a player editing their profile, **When** they clear their city, **Then** their profile shows no location and they are not treated as being "near" anywhere.
5. **Given** a player searching for a city that the city service cannot find, **When** no matches return, **Then** they see a clear "no matching city" state and are not forced to save an invalid location.

---

### User Story 2 - Teams and events reference a real city (Priority: P1)

A team admin (when creating or editing a team) and an event organiser (when creating or editing an event) select the team's / event's city from the same real-city picker. The team and event then display their location **with the country**, and — critically — they now carry a canonical city that "near you" can measure distance to.

**Why this priority**: "Near you" for teams and events (User Stories 3 and 4) is impossible unless teams and events themselves carry a structured city. It is also independently valuable: it makes team and event locations consistent and country-qualified, and removes the ambiguity of free-text city strings. City-teams require a city; mixteams and virtual/location-less events legitimately have none.

**Independent Test**: Create/edit a team and an event, pick a city for each, and confirm both display "City, Country" and store a canonical city reference. Testable without any proximity sorting existing.

**Acceptance Scenarios**:

1. **Given** a team admin creating or editing a city-team, **When** they pick a city from the picker, **Then** the team stores that canonical city and displays it with its country.
2. **Given** a team that is a mixteam (no home city), **When** it is displayed, **Then** no location is shown and it is not placed anywhere for proximity purposes.
3. **Given** an event organiser creating or editing an in-person event, **When** they pick a city, **Then** the event stores that canonical city and displays it with its country.
4. **Given** a virtual event (no physical location), **When** it is displayed, **Then** no city/country is shown and it is excluded from proximity-based ranking.

---

### User Story 3 - Discover teams near your city during onboarding (Priority: P2)

Immediately after (or as part of) choosing their home city in onboarding, a new player sees teams **near that city** surfaced ahead of far-away ones, so the "find your team" step (feature 029) opens with locally relevant suggestions rather than an arbitrary list.

**Why this priority**: This is the first payoff of the structured data for the player — it makes onboarding feel local and personal. It depends on US1 (player has a city) and US2 (teams have cities), so it comes after them, but before the general browse work because onboarding is the highest-intent moment.

**Independent Test**: As a player who just picked a city, open the onboarding team step and confirm nearer teams are ranked ahead of farther ones. Testable with the 029 team-search step plus US1/US2 data.

**Acceptance Scenarios**:

1. **Given** a player who selected a home city and has not yet typed a team search, **When** the onboarding team step loads, **Then** teams are ordered so that teams nearer the player's city appear before farther ones.
2. **Given** a player who has **not** set a home city, **When** the onboarding team step loads, **Then** it still shows a sensible default list (e.g. beginner-friendly teams, per feature 029) and does not error.
3. **Given** the city service is slow or unavailable when computing proximity, **When** the team step loads, **Then** it degrades to the existing default ordering rather than blocking or failing the step.

---

### User Story 4 - Browse teams and events near you (Priority: P2)

A signed-in player browsing teams or events can **sort by proximity** to their home city and/or **filter by country**, so "what's near me" and "what's in my country" are answerable in a couple of taps. Distance-aware ordering places closer results first; a country filter narrows to a single country.

**Why this priority**: This is the general-purpose discovery payoff. It is broader than onboarding and reuses the same proximity capability, so it follows US3. It is independently testable and delivers standalone value to returning users.

**Independent Test**: On the browse teams and browse events screens, choose "near me" ordering and/or a country filter and confirm results are ordered/narrowed accordingly. Testable once US1/US2 data exists.

**Acceptance Scenarios**:

1. **Given** a player with a home city on the browse-teams screen, **When** they choose the proximity sort (an opt-in option; the default ordering is unchanged), **Then** teams are listed nearest-first relative to their home city.
2. **Given** a player on the browse-events screen, **When** they choose the proximity sort, **Then** only located in-person events are shown, nearest-first, and virtual/location-less events are excluded from that view until the player switches away from the proximity sort.
3. **Given** any browse screen, **When** the player applies a country filter, **Then** only teams/events whose city is in that country are shown.
4. **Given** a player with **no** home city set, **When** they open a browse screen, **Then** the proximity sort is unavailable or clearly prompts them to set a city, and the default (non-proximity) ordering still works.

---

### Edge Cases

- **Ambiguous names**: Two cities share a name (e.g. multiple "Springfield"). The picker MUST disambiguate with region/country so the player can pick the right one, and the two are stored as distinct canonical cities.
- **City service unavailable or slow**: Type-ahead search, and any proximity computation, MUST degrade gracefully — the picker shows a transient "can't search right now" state and lets the user retry; proximity ordering falls back to default ordering. A location provider outage MUST never block onboarding, profile save (for fields other than city), or browse.
- **City service rate-limits us**: The system MUST NOT amplify the throttle into an outage (see constitution Principle VII) and MUST keep the picker usable within limits.
- **No home city set**: Proximity features are unavailable but everything else works; the player is gently prompted to set a city where relevant.
- **City cleared / changed**: Removing a city removes the player/team/event from proximity results; changing it re-anchors future "near you" results.
- **Location-less entities**: Mixteams and virtual events have no city. In a proximity-sorted view they are excluded (not treated as distance-zero, and not appended); they remain visible under the default (non-proximity) ordering.
- **Provider returns coordinates that are missing/implausible**: A selected city that cannot be resolved to usable coordinates MUST NOT silently sort as "nearest"; it is treated as unlocated for proximity while still displaying its name/country if available.
- **Very large result sets**: Proximity-ordered browse MUST remain paginated (constitution Principle III) and performant.

## Requirements *(mandatory)*

### Functional Requirements

**City selection & data**

- **FR-001**: The system MUST let a user search for a real-world city by typing a partial name and MUST return a debounced type-ahead list of matching canonical cities sourced from an external location service.
- **FR-002**: Each city option MUST carry, at minimum, a canonical city name, a country, and (where available) a region/administrative area and geographic coordinates.
- **FR-003**: The type-ahead list MUST present enough context (region and/or country) to distinguish cities that share a name.
- **FR-004**: When a user selects a city, the system MUST persist a reference to a **canonical city** record (created once and reused thereafter), not a free-typed string.
- **FR-005**: The system MUST NOT allow a location to be saved as a raw free-text city; a stored location is either a reference to a canonical city or empty.
- **FR-006**: The system MUST allow a user to clear a previously selected city, leaving the location empty.

**Applying structured locations**

- **FR-007**: A player's profile home location MUST be a structured canonical city (replacing the previous free-text hometown), settable during onboarding and from profile edit.
- **FR-008**: A team's home city MUST be a structured canonical city, settable at team creation and from team edit; teams without a home city (mixteams) MUST remain valid.
- **FR-009**: An in-person event's city MUST be a structured canonical city, settable at event creation and edit; virtual/location-less events MUST remain valid with no city.
- **FR-010**: Everywhere a profile, team, or event location is displayed, the system MUST show the city together with its country (e.g. "Köln, Germany").

**"Near you" discovery**

- **FR-011**: The system MUST be able to order teams and events by proximity to a given player's home city, nearest first. Proximity is a **sort only** — no distance/radius cut-off is applied and no located result is hidden by distance.
- **FR-012**: Proximity MUST be computed at city-to-city granularity — the distance between the player's home city and each entity's city — and equal-distance ties MUST have a stable secondary ordering.
- **FR-013**: The onboarding "find your team" step MUST, when the player has a home city, surface nearer teams ahead of farther ones, and MUST fall back to the existing default ordering (feature 029) when the player has no city or proximity cannot be computed.
- **FR-014**: The browse-teams and browse-events screens MUST offer proximity ordering (nearest-first) relative to the signed-in player's home city as an **opt-in sort option**; the existing default ordering (name/date) MUST remain the default and MUST NOT change based on whether the player has a home city.
- **FR-015**: The browse screens MUST offer a filter by country, independent of the sort.
- **FR-016**: Entities with no city (mixteams, virtual events, players with no city) MUST be **excluded from the proximity-sorted view** rather than treated as distance zero or appended, and MUST still be reachable via the default (non-proximity) ordering.
- **FR-017**: Proximity-ordered and country-filtered lists MUST remain paginated and MUST NOT return unbounded result sets.

**Resilience, privacy & parity** *(constitution Principles I, V, VII)*

- **FR-018**: Calls to the external location service MUST have bounded time limits, retry only transient faults with jittered backoff (honouring any provider `Retry-After`), and have a stop-condition — never amplifying a provider slowdown or throttle into an outage.
- **FR-019**: A location-service outage MUST degrade gracefully: city search shows a retryable transient error; proximity ordering falls back to default ordering; no unrelated flow (onboarding completion, profile/team/event save of non-city fields, browse) is blocked.
- **FR-020**: The location integration MUST use the **same self-hosted OpenStreetMap-based geocoder in every environment** (a container in docker-compose locally and in-cluster on Dev/Prod) — identical in shape and provider across local/Dev/Prod, with no dependency on a paid or internet-only third-party service and no per-request billing.
- **FR-021**: Any credentials for the location service MUST be sourced from environment configuration (never committed), and no secrets, credentials, or personal data (including a user's exact location query tied to their identity) MUST appear in resilience/telemetry logs.
- **FR-022**: The system SHOULD avoid re-querying the external service for city data it already holds (canonical cities are cached/persisted on first use), to reduce cost, latency, and data sent to the third party.

### Key Entities *(include if feature involves data)*

- **Canonical City**: A real-world city resolved from the external location service and stored once for reuse. Attributes: a stable external identifier (for de-duplication and re-lookup), canonical name, country (name and/or code), region/administrative area, and geographic coordinates (latitude/longitude) where available. Referenced by profiles, teams, and events. Two same-named cities in different regions/countries are distinct records.
- **Player Profile location**: Reference from a player's profile to a Canonical City (nullable). Replaces the former free-text hometown.
- **Team location**: Reference from a team to a Canonical City (nullable — required for city-teams, absent for mixteams).
- **Event location**: Reference from an in-person event to a Canonical City (nullable — absent for virtual events). Coexists with the event's existing street/postal address fields.
- **City-to-city distance (derived/cached)**: The distance between a pair of canonical cities, a pure function of their coordinates. Conceptually cacheable so repeated "near you" computations reuse prior results rather than recomputing per row. Not user-facing.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A player can find and select their real city during onboarding in under 15 seconds, seeing type-ahead suggestions within a moment of typing.
- **SC-002**: 100% of newly saved profile, team, and event locations resolve to a canonical city with an associated country (no free-text-only locations remain possible).
- **SC-003**: Every place a location is displayed shows the country alongside the city.
- **SC-004**: On the onboarding team step, a player with a home city sees at least one nearby team ranked ahead of a demonstrably farther team whenever nearby teams exist.
- **SC-005**: Proximity-ordered browse returns its first page within the same responsiveness budget as today's browse (no perceptible slowdown for typical data volumes).
- **SC-006**: When the external location service is made unavailable in testing, onboarding can still be completed, profiles/teams/events can still be saved (city left unset), and browse still works with default ordering — zero hard failures attributable to the outage.
- **SC-007**: No location-service credential appears in the repository or in logs, verified by review.

## Assumptions

- **Auth-only app (feature 026)**: All of this is behind sign-in; there is no anonymous location search or anonymous "near you". A signed-in player is the actor throughout.
- **No data migration (owner decision)**: The database currently holds only test data, so existing free-text `Hometown` (profile), `City` (team), and `City`/`Location` (event) values are **replaced outright** — there is no best-effort matching or backward-compatibility path. Existing test rows may be reset/reseeded.
- **Self-hosted geocoder (clarified 2026-07-25)**: City data comes from a **self-hosted OpenStreetMap-based geocoder** (e.g. Nominatim or Photon) run as a container in **every** environment — not a bundled dataset and not a hosted third-party SaaS. It is an external HTTP integration from the backend's perspective (so constitution Principle VII still applies) but needs no API key, incurs no per-request cost, and never sends a user's location to a third party. Coordinates come from the geocoder. The specific geocoder image, its data extract, and refresh cadence are a planning detail.
- **True distance at city granularity (owner decision)**: "Near you" uses real geographic distance, but only ever between whole cities (not per-address), which makes city-to-city distances a small, cacheable set rather than a per-row computation.
- **Trainings deferred**: Trainings keep their free-text `Location` for now; retrofitting them to structured locations is a follow-up, out of scope here.
- **Proximity is sort-only (clarified 2026-07-25)**: "Near you" is sort-by-ascending-distance with **no radius cut-off**; the country filter is a separate control. On browse it is an **opt-in** sort (default ordering unchanged); onboarding auto-leads with it. Virtual/location-less events are excluded from the proximity-sorted event view.
- **Existing patterns reused**: The city picker reuses the debounced-search interaction already used on browse/onboarding; browse proximity/country controls extend the existing feature-007 browse filters and `PagedResult` pagination.
- **DESIGN.md governs the UI**: The city-picker component, location display format ("City, Country"), and "near you" browse controls follow DESIGN.md; conflicts are reported, not silently resolved.
