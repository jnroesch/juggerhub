# Feature Specification: Browse Public Trainings

**Feature Branch**: `feat/043-browse-public-trainings`

**Created**: 2026-08-04

**Status**: Draft

**Input**: GitHub Issue #145 — "Discover public trainings: a searchable trainings browse, separate from events"

---

## Context

A training is a team's regular practice. Teams can open a training — the whole series, or one
individual session — to anyone signed in, who then attends as a guest. That capability has existed
since feature 018, but **nothing in the product ever shows an open training to someone outside the
team that owns it**. The only ways to reach one are a direct link and a chat message. A team can
open its doors, and nobody finds out.

The home screen already promises the missing route and fails to deliver it: when a player has
nothing coming up, the empty state offers a "Browse open trainings" button that navigates to the
**events** browser — a list that only ever contains tournaments and workshops. The button is a dead
end today.

Trainings are deliberately *not* events. An event carries a fee, a participation cap, a waitlist,
and a signup-and-approval flow; a training carries none of those — it recurs, it is free, and
everyone welcome simply says whether they are coming. Folding trainings into the events browser
would mix two things the product has kept separate everywhere else, and would clutter the list a
player scans when looking for a tournament.

Feature 042 supplied the last missing ingredient by giving every in-person training a canonical
city, explicitly described in the data model as the anchor that a later "trainings near me" search
would filter and sort on. This feature is that search.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Find an open training I could actually attend (Priority: P1)

A player who has just joined the platform, has no team yet, and wants to try the sport opens the
discovery area and switches to Trainings. They see a list of upcoming training sessions that teams
have opened to everyone — each showing what it is called, which team runs it, when it is, and
where. They pick one, open it, and say they are coming as a guest.

This is the whole point of the feature: it turns "a team opened its training" into "a stranger
found it". It also fixes the broken promise on the home screen — the "Browse open trainings" button
in the empty state must land here rather than in the events browser.

**Why this priority**: Without it there is no discovery at all and the advertised button stays a
dead end. Every other story in this spec is a refinement of this list.

**Independent Test**: With at least one public and one team-only training seeded, sign in as a
player who belongs to no team, open the trainings browse, and confirm the public sessions are
listed, the team-only ones are absent, and a listed row opens the session page where the player can
RSVP as a guest.

**Acceptance Scenarios**:

1. **Given** a team has a public training series with upcoming sessions, **When** a signed-in
   non-member opens the trainings browse, **Then** those sessions are listed with name, owning
   team, date, start and end time, and location.
2. **Given** a team has a team-only training series, **When** any signed-in user opens the trainings
   browse, **Then** none of its sessions appear — including for members of that team.
3. **Given** a team-only series in which a single session has been individually opened to the
   public, **When** a non-member opens the trainings browse, **Then** exactly that one session
   appears and the rest of the series does not.
4. **Given** a public series whose next session has been individually set back to team-only, **When**
   a non-member opens the trainings browse, **Then** that one session is absent and the others in
   the series are present.
5. **Given** a listed public session, **When** the viewer selects the row, **Then** the existing
   session page opens and the viewer can respond as a guest.
6. **Given** a player with nothing coming up, **When** they select "Browse open trainings" in the
   home empty state, **Then** the trainings browse opens — not the events browser.
7. **Given** a signed-out visitor, **When** they request the trainings browse, **Then** they are
   sent to sign in, exactly as the teams, events, and players browse pages behave.

---

### User Story 2 - Narrow the list down to what is relevant to me (Priority: P2)

A player who only wants trainings in their region, or within the fortnight they are visiting a
city, narrows the list: they type part of a training's name, choose a city or a country, set a date
range, and decide whether past sessions should be included. The result count and the active filters
are visible, and any filter can be cleared individually or all at once.

**Why this priority**: A single flat list of every open training everywhere is browsable at today's
volume but stops being useful as the platform grows, and the location filter is the one a travelling
or relocating player reaches for immediately. It is a refinement of US1, not a precondition for it.

**Independent Test**: Seed public sessions across at least two cities, two countries, and a spread
of dates, then confirm each filter narrows the list correctly on its own and in combination, that
the count line reflects the filtered total, and that clearing restores the full list.

**Acceptance Scenarios**:

1. **Given** public sessions in several cities, **When** the viewer filters by one city, **Then**
   only sessions in that city are listed and the count line reflects the narrowed total.
2. **Given** public sessions in several countries, **When** the viewer filters by one country,
   **Then** only sessions in that country are listed.
3. **Given** public sessions spread across months, **When** the viewer sets a date range, **Then**
   only sessions falling inside the range are listed.
4. **Given** a training named "Anfängertraining", **When** the viewer searches for "anfanger"
   without the accent or the umlaut, **Then** the training is found, matching how the other browse
   pages already treat search text.
5. **Given** filters are applied, **When** the viewer clears one chip, **Then** only that filter is
   removed and the list re-runs with the rest still applied.
6. **Given** the list has loaded with the default filters, **When** the viewer looks at the results,
   **Then** only upcoming sessions are shown, and past sessions appear only after the viewer
   explicitly asks for them.
7. **Given** a combination of filters that matches nothing, **When** the list re-runs, **Then** a
   no-results state is shown that offers to clear the filters — distinct from the empty state shown
   when no public trainings exist at all.

---

### User Story 3 - Show me the closest ones first (Priority: P3)

A player who has set a home city switches the ordering from soonest-first to nearest-first, so
trainings within reach appear before ones across the country. A player who has not set a home city
is not offered the option, and is told what to do if they ask for it another way.

**Why this priority**: This is the "trainings near me" the home screen has been pointing at, and the
data model was explicitly prepared for it — but the list is usable in date order without it, and it
depends on the viewer having supplied a home city.

**Independent Test**: With a home city set on the viewer's profile and public trainings seeded in
cities at different distances, switch to nearest-first and confirm the ordering follows distance;
then repeat with no home city set and confirm the option is not offered.

**Acceptance Scenarios**:

1. **Given** a viewer with a home city and public trainings in cities at differing distances,
   **When** they choose nearest-first, **Then** the sessions are ordered by the distance from their
   home city to the session's city, closest first.
2. **Given** a viewer with no home city, **When** they open the trainings browse, **Then** the
   nearest-first option is not offered.
3. **Given** a viewer with no home city who requests nearest-first anyway, **When** the request is
   made, **Then** it is refused with a message telling them to set a home city — never silently
   answered with a differently-ordered list.
4. **Given** virtual (online) public trainings exist, **When** the viewer chooses nearest-first,
   **Then** virtual trainings are absent from the list, matching how the events browse already
   treats a distance ordering; and **When** they switch back to soonest-first, **Then** the virtual
   trainings reappear.

---

### Edge Cases

- **A session was relocated.** A single session moved to a different address must be listed and
  filtered by **its own** address, never the series' — a session moved to a venue that has no name,
  under a series whose venue does have one, must not display the series' venue name, and a session
  moved to another city must not be returned by a filter on the series' city.
- **A training predates the structured address.** Trainings created before feature 042 have only a
  free-text location and no city. They must still be listed and still show a readable location; they
  simply cannot match a city filter or appear in the nearest-first ordering.
- **A public session is cancelled.** It disappears from the browse — there is nothing to discover —
  even though it stays visible, marked off, to the team that owns it.
- **A public session is skipped.** It never appears, matching the quiet soft-tombstone behaviour it
  has everywhere else.
- **A public session is virtual.** It is listed and reads as online rather than showing an address,
  it matches no city or country filter, and it drops out of the nearest-first ordering.
- **A session that is happening right now**, having started but not yet ended, counts as upcoming
  rather than past, so the default filters do not hide a training a player could still walk into.
- **The viewer is a member of the team that owns a listed public training.** The session is listed —
  the list is defined by the training being public, not by the viewer being an outsider — and the
  viewer's existing response, if any, is unaffected by anything on this surface.
- **No public trainings exist anywhere yet.** The empty state explains that teams have not opened
  any trainings rather than reading as a broken page, and does not blame the viewer's filters.
- **A team runs a public series every week for six months.** Under soonest-first this interleaves
  harmlessly with every other team's sessions by date. Under nearest-first it does not — see
  FR-024.
- **The viewer's home city, or a training's city, has no cached distance between them.** The
  nearest-first ordering must not drop the training silently in a way that makes an existing
  training invisible with no explanation.

---

## Requirements *(mandatory)*

### Functional Requirements

**The list and what belongs in it**

- **FR-001**: The system MUST provide a browse surface listing training sessions that teams have
  opened to everyone, drawn from every team on the platform.
- **FR-002**: One row MUST represent one dated session, matching how the events browse presents one
  row per event. Rows MUST NOT be grouped or collapsed by series.
- **FR-003**: A session MUST be listed only when its effective visibility is public, where a
  session's own visibility setting overrides the series default in both directions — a public
  session inside a team-only series is listed, and a team-only session inside a public series is
  not.
- **FR-004**: The visibility rule MUST be the sole determinant of what is listed. The system MUST
  NOT widen the list for a viewer based on their team memberships, and MUST NOT narrow it because
  the viewer is a member of the owning team.
- **FR-005**: Cancelled and skipped sessions MUST never be listed, under any filter or ordering.
- **FR-006**: By default the list MUST show only sessions that have not yet ended. Past sessions
  MUST be reachable only by the viewer explicitly turning the default off.
- **FR-007**: The surface MUST require a signed-in viewer, consistent with the rest of the discovery
  area.

**What a row shows**

- **FR-008**: Each row MUST show the training's name, the team that runs it, the session's date,
  its start and end time, its location, and whether it belongs to a recurring series or is a
  one-off.
- **FR-009**: The location text MUST be composed by the system, using the same rule the rest of the
  product already uses, so that a training and an event at the same address read character-for-
  character identically.
- **FR-010**: For a session carrying its own address, every displayed and filtered address element
  MUST come from that session's address as a whole; for every other session they MUST all come from
  the series. The two MUST never be mixed within one row.
- **FR-011**: A virtual session MUST read as online instead of displaying an address.
- **FR-012**: Selecting a row MUST open the existing session page, where the viewer can respond as a
  guest. This feature MUST NOT change that page or how a guest joins.

**Finding and narrowing**

- **FR-013**: Viewers MUST be able to search by the training's name, with the same accent- and
  case-insensitive partial matching the other browse pages use.
- **FR-014**: Viewers MUST be able to filter by city.
- **FR-015**: Viewers MUST be able to filter by country.
- **FR-016**: Viewers MUST be able to filter by a date range, with either bound usable on its own.
- **FR-017**: The surface MUST show the number of matching sessions, the filters currently in
  effect, a way to remove each one individually, and a way to clear them all.
- **FR-018**: Results MUST be paginated, with further results loaded on demand rather than all at
  once.

**Ordering**

- **FR-019**: The default ordering MUST be soonest-first, and MUST be stable so that paging through
  the list never repeats or skips a session.
- **FR-020**: Viewers who have set a home city MUST be able to switch to a nearest-first ordering,
  measured from their home city to the session's city.
- **FR-021**: Viewers who have not set a home city MUST NOT be offered the nearest-first ordering,
  and a request for it made without one MUST be refused with a message explaining that a home city
  is needed — never silently answered with a different ordering.
- **FR-022**: The nearest-first ordering MUST exclude sessions with no city, including every virtual
  session, matching the established behaviour of the events browse.
- **FR-023**: A session whose distance from the viewer's home city is not known MUST NOT vanish
  without trace under the nearest-first ordering; it MUST either be ordered last or be covered by
  the same exclusion notice as virtual sessions.
- **FR-024**: Changing the sort MUST change only the ordering. The nearest-first view MUST NOT
  impose a date range, a result cap, or any other implicit filter of its own; the date range stays
  entirely the viewer's to set. Within a given distance, sessions are ordered by date.

  > **Owner decision, 2026-08-04** — this reverses an earlier resolution. The concern was that
  > distance-only ordering lets the closest city's whole schedule fill the first page, and the
  > implementation briefly defaulted the range to two weeks ahead under this sort. The owner
  > rejected that: a sort control that silently applies a filter is surprising, and someone asking
  > for the closest trainings wants the closest trainings. This is option (c) from the original
  > question — accept the density, keep the control honest.

**Entry points and presentation**

- **FR-025**: The trainings browse MUST sit alongside teams, events, and players as a fourth
  destination in the discovery area, reachable by the same switch the other three use.
- **FR-026**: Adding the fourth destination MUST NOT degrade the switch on small screens; it MUST
  remain legible and operable at the narrowest supported width in every supported language.
- **FR-027**: The home empty state's "Browse open trainings" action MUST navigate to the trainings
  browse instead of the events browser.
- **FR-028**: The surface MUST present loading, empty, no-results, and error states consistent with
  the other browse pages, and the empty state MUST read differently from the no-results state.
- **FR-029**: All viewer-facing text introduced by this feature MUST be available in English, German
  and Spanish, with no key present in one language catalogue and missing from another.

**Boundaries**

- **FR-030**: This feature MUST NOT change the events browse, the event model, or anything about
  events.
- **FR-031**: This feature MUST NOT make trainings visible to signed-out visitors.
- **FR-032**: This feature MUST NOT change how a training is created, edited, made public, skipped,
  or cancelled, and MUST NOT add any new visibility setting.

### Key Entities

- **Training (series)**: a team's recurring practice or one-off, carrying the name, description,
  default times, default visibility, and — since feature 042 — the structured address whose
  canonical city anchors location filtering and distance ordering.
- **Training session**: one dated occurrence of a training. Carries its own status (scheduled,
  cancelled, skipped), an optional visibility that overrides the series default, and an optional
  address that replaces the series address as an indivisible whole.
- **Team**: the owner of a training; identifies the training in a result row and gives a viewer
  somewhere to go next.
- **City**: the canonical place a training happens, shared with events, teams, and player home
  cities; both the city filter and the distance ordering resolve through it.
- **Viewer's home city**: the profile-level city that anchors the nearest-first ordering. Its
  absence is what makes that ordering unavailable.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A signed-in player who belongs to no team can go from the home screen to a listed open
  training and record that they are coming, in under 60 seconds and without being told a direct
  link.
- **SC-002**: 100% of sessions whose effective visibility is public, that are scheduled and not yet
  ended, appear in the unfiltered list; and 0% of team-only, cancelled, or skipped sessions appear
  in it, for every viewer regardless of team membership.
- **SC-003**: A training and an event held at the same address produce identical location text,
  character for character, on their respective browse lists.
- **SC-004**: A session relocated to its own address is listed and filtered under that address in
  100% of cases, with no element of the series' address appearing in its row.
- **SC-005**: The trainings browse offers the same search, filter, sort, count, chip, paging, and
  state behaviours as the teams, events, and players browse pages, differing only in the filter set
  and what a row contains.
- **SC-006**: The "Browse open trainings" action on the home empty state lands on a list of
  trainings, with zero events present.
- **SC-007**: A viewer with a home city sees, in the first page of the nearest-first ordering, only
  trainings closer to them than any training excluded from that page.
- **SC-008**: The discovery switch presents four destinations legibly and operably at the narrowest
  supported screen width in each of the three supported languages, with no clipped or overlapping
  labels.
- **SC-009**: Every user-facing string added by this feature exists in all three language
  catalogues, verified by the existing parity check rather than by inspection.
- **SC-010**: No behaviour of the events browse changes, verified by its existing tests continuing
  to pass unmodified.

---

## Assumptions

- **Density is managed by filters, not by grouping.** The owner chose one row per session over one
  row per series, accepting that a frequently-recurring public series produces many similar rows.
  Under the default soonest-first ordering this interleaves harmlessly with other teams' sessions;
  under nearest-first it does not, which is why FR-024 exists as the one open question.
- **No default upper bound on the date window.** The list defaults to "not yet ended" with no
  far-future cutoff, matching the events browse. A viewer wanting a narrower window sets the date
  range.
- **Distance ordering reuses the existing city-to-city distance data** that already powers the teams
  and events proximity sorts. No new distance calculation, and no map, geocoding, radius input, or
  "use my current location" is introduced.
- **Viewers are signed in.** Feature 026 made all discovery authenticated-only and this feature does
  not revisit that.
- **The session page already handles outsiders.** Any signed-in user can already open a public
  session and respond as a guest, so this feature only has to get them there.
- **A team-only training remains reachable to its own members** through the team's trainings tab and
  the home agenda, both unchanged. This surface deliberately duplicates neither.
- **Trainings have no fee, cap, waitlist, or approval**, so no row shows capacity, price, or
  remaining places — the concepts do not exist for a training.
- **Distance is used for ordering, not display.** Rows do not show a distance figure, matching the
  events browse, which sorts by proximity without displaying kilometres.
- **The public/team-only split is the only privacy control needed here.** No per-training "hide from
  search" setting is introduced; a team that does not want to be found leaves the training
  team-only.

---

## Dependencies

- **Feature 018 (Trainings)** — supplies the training and session model, the public/team-only
  visibility with its per-session override, and the guest-response flow this list leads into.
- **Feature 042 (Training locations)** — supplies the canonical city and structured address, without
  which there is nothing to filter or sort on, and the shared server-side location label that
  SC-003 depends on.
- **Feature 030 (Structured locations)** — supplies the city reference data and the city-to-city
  distance cache the nearest-first ordering reads, and the precedent that a distance ordering
  excludes placeless items.
- **Feature 007 (Browse/search)** — supplies the shared discovery behaviour the fourth destination
  must match rather than reimplement.
- **Feature 026 (Authenticated-only access)** — sets the sign-in requirement for the whole discovery
  area.
- **Feature 031 (i18n)** — supplies the three language catalogues and the parity check SC-009 relies
  on.
