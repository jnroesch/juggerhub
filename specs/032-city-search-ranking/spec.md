# Feature Specification: City Search Relevance Ranking

**Feature Branch**: `032-city-search-ranking`

**Created**: 2026-07-27

**Status**: Draft

**Input**: User description: "Rank city-picker search options by relevance — proximity to the user's stored home city and population — so the city people usually mean appears first."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The obvious city comes first (Priority: P1)

A person searching for a city types its name and, among several same-named places, sees the one most people mean at the top of the list. When they have a home city on record, the nearest match to home is preferred; otherwise the largest, best-known city of that name leads.

**Why this priority**: This is the whole point of the feature — a search that surfaces "Berlin, Germany" (population ~3.7M) above tiny like-named villages removes the most common source of mis-selection. It is independently valuable even before any personalization.

**Independent Test**: Search a name shared by many places (e.g. "berlin") as a user with no home city and confirm the largest/best-known city leads, with obscure same-named places pushed down.

**Acceptance Scenarios**:

1. **Given** a signed-in user with no stored home city, **When** they search "berlin", **Then** the large German Berlin appears above smaller villages of the same name.
2. **Given** two results share the same name and country, **When** they are shown, **Then** the more populous one is listed first (and the region label still disambiguates them, per feature 030).
3. **Given** a search term, **When** results are ranked, **Then** an exact name/prefix match still ranks above a match that only hit an alternate/exonym name (existing behavior is preserved).

---

### User Story 2 - Cities near my home rank higher (Priority: P2)

A person who has already set a home city searches for a place. Results are biased toward cities close to their home, so the nearby place they most likely mean floats to the top even when a larger, more distant city shares the name.

**Why this priority**: Personalization by proximity is a meaningful quality-of-life win, but it depends on the user having a home city on record — a smaller audience than P1 and only useful after onboarding. It builds on P1 rather than replacing it.

**Independent Test**: As a user whose home city is set to a location near a smaller same-named city, search that name and confirm the nearby smaller city ranks above the distant larger one.

**Acceptance Scenarios**:

1. **Given** a user whose home city is near a smaller same-named place, **When** they search that name, **Then** the nearby place ranks above a larger but far-away place of the same name (within the same match-quality tier).
2. **Given** a user with a stored home city, **When** two candidate cities are otherwise equal in match quality, **Then** the one geographically closer to their home city ranks first.
3. **Given** a user with no stored home city, **When** they search, **Then** ranking falls back to population (then existing tiebreakers) with no error and no prompt.

---

### Edge Cases

- **No home city (e.g. during onboarding)**: the proximity signal is simply absent; results rank by match quality → population → existing name tiebreakers. No location prompt is ever shown.
- **Reference rows with unknown/zero population**: sort last within their tier rather than being hidden.
- **Ties**: when two candidates are equal on match quality, distance, and population, the existing deterministic name-length/name tiebreakers keep ordering stable.
- **Home city with no coordinates on record**: treated the same as "no home city" — the distance tier is skipped.
- **Result cap**: the same maximum number of options is returned as today; relevance ranking must be applied across the full candidate set so the cap does not drop a more-relevant option before ranking.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The city-picker search MUST order results within each existing match-quality tier by, in order: (1) distance from the signed-in user's stored home city ascending, (2) population descending, (3) the current name-length then name tiebreakers.
- **FR-002**: The system MUST preserve the existing match-quality tiering — exact name / ASCII-name prefix matches rank above matches that only hit an alternate/exonym name.
- **FR-003**: The proximity signal MUST come only from the signed-in user's stored home city (feature 030), resolved server-side from their profile. The feature MUST NOT introduce browser geolocation, a location-permission prompt, or any new client-supplied location parameter on the search request.
- **FR-004**: When the user has no stored home city (or it has no usable coordinates), the system MUST skip the distance factor and rank by population then the existing tiebreakers, returning results normally with no error.
- **FR-005**: The system MUST rank same-name/same-country cities by population so the more populous city leads, while the region disambiguation label from feature 030 is unchanged.
- **FR-006**: Population MUST be available for every reference city, sourced from the existing bundled city dataset; cities with unknown population are treated as the lowest population and sorted last within their tier.
- **FR-007**: Relevance ranking MUST be computed across the full set of candidate matches before the result list is truncated to its display cap, so the cap never discards a more-relevant option.
- **FR-008**: The change MUST be limited to the ordering of the city-picker search options. The selected/canonical city model, the option display labels (aside from the ordering they appear in), and the browse country filter MUST be unchanged.
- **FR-009**: Search results MUST remain deterministic for a given user and query (stable ordering across identical requests).

### Key Entities *(include if feature involves data)*

- **City reference record**: a searchable reference city. Gains a **population** attribute (a count of inhabitants) used for ranking, in addition to its existing name, country, region, and coordinates.
- **User home city**: the signed-in user's stored canonical home city (feature 030), whose coordinates provide the proximity origin. Optional — many users, and all users mid-onboarding, will not have one.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a search term shared by many places (e.g. "berlin"), a user with no home city sees the most populous matching city in the first position 100% of the time.
- **SC-002**: For a user with a stored home city, when a same-named city exists within their region, that nearby city appears above larger distant same-named cities in the ranked list.
- **SC-003**: Users with no home city receive ranked results with zero location prompts and zero added errors compared to today.
- **SC-004**: Perceived search responsiveness is unchanged from today's city picker (no user-noticeable slowdown introduced by the new ranking).
- **SC-005**: Exact-name matches never rank below alternate-name/exonym matches for the same query (0 regressions of the existing match-quality tiering).

## Assumptions

- The bundled reference dataset already carries a population figure per city, so no new external data source is required — only that the existing bundled seed is regenerated to include population and the reference data reseeded.
- The signed-in user's home city, when set, has coordinates on record sufficient for a city-granularity distance comparison (feature 030 stores these).
- "Distance" means straight-line geographic distance between the user's home city and each candidate city; city-level granularity is sufficient (street-level precision is not required).
- The search endpoint is already restricted to signed-in users (feature 026), so the current user's home city can be resolved server-side without any new client input.
- Reseeding the reference table is acceptable operationally (the reference table is seed-once-per-environment and is not user-authored data), consistent with how feature 030 introduced it.
- The result display cap stays at its current value; only the ordering within the returned set changes.
