# Feature Specification: Search / Browse

> **⚠️ Superseded in part by [feature 026](../026-authenticated-only-access/spec.md) (2026-07-22):**
> player/team/event browse & search are now **authenticated-only** — the "anonymous browse"
> invariant is reversed. Discovery is direct-link only; there is no anonymous search surface. The
> filtering/sorting/pagination behavior otherwise stands.

**Feature Branch**: `007-search`

**Created**: 2026-07-07

**Status**: Draft

**Input**: User description: "Search / Browse — a shared discovery surface for the JuggerHub community app. A Browse area with three sibling pages (Teams, Events, Players) that share one shell and behave identically; only the filter set and default sort differ per entity. Live search, filters on demand (bottom sheet on mobile, drawer on desktop), active-filter chips, result counts, compact list rows, empty + no-results states, and a locked 'Near me' coming-soon hook. Players are opt-in. Implemented from the Search Wireframes design handoff; styled per DESIGN.md; backend per the constitution."

## Amendments

> **Amended by feature 020 (2026-07-21) — player search opt-in removed.** The
> per-player "Appear in search" opt-in described below — **FR-041, FR-042, FR-044,
> SC-003**, the User Story 3 "opt-in only" framing, and the data-model
> `AppearInSearch` field — has been **retired**. The Players directory now returns
> every non-banned player unconditionally: there is no opt-in setting, no stored
> `AppearInSearch` field, and no opt-in note on the page. Banned accounts remain
> excluded by the global account-status query filter (feature 013); suspended
> accounts stay visible. See `specs/020-remove-search-optout/`. The historical text
> below is preserved for provenance but no longer reflects shipped behavior.

## Clarifications

### Session 2026-07-07

- Q: How should a team count as "active" (so "Active teams only" can hide dormant teams)? → A: A team is active if it has participated in at least one event within the last 12 months. (Deliberately a single, simple rule for v1; more rules may be added later.)
- Q: The wireframe shows "Recruiting" and "Beginners welcome" team chips/filters — no backing fields exist. How should these work? → A: Add **"Beginners welcome" only** as a self-managed team flag; drop the separate "Recruiting" chip and filter for v1.
- Q: The Players page filters by "Looking for a team" and "Experience" — neither exists today. How should they be backed? → A: **Drop both** filters for v1. Players browse ships with the opt-in gate, position, city, and name search only.
- Q: How should the Players "Position" filter be sourced? → A: **Derive position from the player's already-declared pompfen** (Stab / Q-Tip / Kette / Schild / Langpompfe / Doppel-Kurz) plus Läufer/Runner; the filter uses the real canonical pompfen/position set, not the wireframe's English placeholders. No new position field is added.

## User Scenarios & Testing *(mandatory)*

A visitor wants to **discover** the JuggerHub community — find a team to join, an event to attend, or a player to connect with — from one consistent, predictable place. The three discovery pages share a single interaction model so that learning one teaches all three.

### User Story 1 - Browse and search Teams (Priority: P1)

A person new to Jugger (or new to the area) opens the Teams browse page to find a club to join. Before typing anything they already see a scrollable list of teams with current filters applied. They type part of a name or city and the list narrows live. They open Filters to keep only teams that are active and welcome beginners, pick their city, and apply. Each row links through to that team's public page.

**Why this priority**: Finding a team is the single most valuable discovery action for a growing community app — it directly converts visitors into members. It is also the reference implementation of the shared shell; every other page reuses it.

**Independent Test**: Load `/browse/teams` with seeded teams; confirm the default list appears without typing, that typing a query narrows results server-side, that opening Filters and toggling options changes the result set and count, that active filters appear as removable chips, and that a row navigates to the team page. Delivers a fully usable team-discovery experience on its own.

**Acceptance Scenarios**:

1. **Given** the Teams browse page with teams in the system, **When** the page loads before any input, **Then** a paginated list of teams is shown with the default filters and default sort (A–Z) already applied, and a count line states how many teams match.
2. **Given** the Teams browse page, **When** the user types text into the search field, **Then** the results narrow to teams whose name or city matches, updating shortly after the user stops typing, with the count line reflecting the new total.
3. **Given** the Teams browse page, **When** the user opens Filters, **Then** a bottom sheet (mobile) or slide-over drawer (desktop) appears showing the team filter set, a Reset control, and a primary action labelled with a live count of how many teams the pending selection would show.
4. **Given** an open filter panel with pending changes, **When** the user taps the primary "Show N teams" action, **Then** the panel closes, the results reflect the applied filters, the Filters button shows a badge with the number of active filters, and each active filter appears as a removable chip above the results.
5. **Given** active-filter chips are shown, **When** the user removes one chip (its ✕) or taps "Clear all", **Then** that filter (or all filters) is dropped, the results and count update accordingly, and the Filters badge decreases.
6. **Given** a team list row, **When** the user activates it, **Then** they are taken to that team's public page.
7. **Given** any team filter panel, **When** it is open, **Then** a "Near me / distance" option is visible but shown as a locked "Coming soon" affordance that cannot be toggled.

---

### User Story 2 - Browse and search Events (Priority: P1)

Someone looking for a tournament or training to attend opens the Events browse page. By default only upcoming events are shown, soonest first. They search by event name, narrow by a date range and city, and open an event that interests them.

**Why this priority**: Event discovery is the other half of the community's core loop and directly drives attendance. It reuses the exact shell from Teams, differing only in its filter set (hide-past, date range, type) and default sort (soonest upcoming).

**Independent Test**: Load `/browse/events` with seeded past and future events; confirm past events are hidden by default, that the default order is soonest-upcoming-first, that a search narrows by name, that the date-range and type filters change the set, and that a row opens the event page.

**Acceptance Scenarios**:

1. **Given** the Events browse page with both past and future events, **When** the page loads, **Then** only events that have not already ended are listed by default, ordered soonest-first, and cancelled events are excluded.
2. **Given** the Events browse page, **When** the user types into search, **Then** results narrow to events whose name matches, live.
3. **Given** the Events filter panel, **When** the user turns off "Hide past events", **Then** events that have already ended also appear.
4. **Given** the Events filter panel, **When** the user sets a from/to date range and/or an event type and applies, **Then** only events within that range and of that type are shown, and the choices appear as active-filter chips.
5. **Given** an event list row, **When** the user activates it, **Then** they are taken to that event's public page.
6. **Given** the Events filter panel, **When** it is open, **Then** the same locked "Near me / distance — Coming soon" affordance is present.

---

### User Story 3 - Browse and search Players, opt-in only (Priority: P2)

A team recruiter wants to find players in their area. They open the Players browse page. Only players who have chosen to appear in search are ever listed. They filter by position and city, and reach out via a player's public profile.

**Why this priority**: Player discovery closes the recruiting loop (teams find players, not just players find teams), but it depends on a privacy opt-in that must ship with it, so it follows the two core pages. It must never expose a player who has not opted in.

**Independent Test**: With a mix of opted-in and not-opted-in players seeded, load `/browse/players`; confirm only opted-in players ever appear (including with no query and with any filter/sort combination), that a not-opted-in player cannot be surfaced by name search, that the player filter set (position, city) narrows results, and that a row opens the player's public profile. Toggle a player's opt-in off and confirm they disappear.

**Acceptance Scenarios**:

1. **Given** players who have and have not opted into search, **When** anyone (signed in or not) browses or searches players by any query, filter, or sort, **Then** only opted-in players are ever returned; a non-opted-in player can never be surfaced.
2. **Given** a player viewing their own profile settings, **When** they enable "Appear in search", **Then** they become discoverable on the Players browse page; when they disable it (the default state), they are not.
3. **Given** the Players browse page, **When** the user filters by one or more positions and/or a city and applies, **Then** results narrow accordingly with matching active-filter chips and count.
4. **Given** the Players browse page, **When** it is shown, **Then** a note communicates that only players who chose to appear in search are listed.
5. **Given** a player list row, **When** the user activates it, **Then** they are taken to that player's public profile.
6. **Given** the Players filter panel, **When** it is open, **Then** the same locked "Near me / distance — Coming soon" affordance is present.

---

### User Story 4 - Consistent shell, empty and no-results states (Priority: P2)

A user who searches too narrowly, or visits before any data exists, is never left staring at a blank screen. Every browse page shows a friendly empty state (nothing exists yet) or no-results state (nothing matched, with a one-tap way to clear the query/filters), and all three pages look and behave identically apart from their filter set.

**Why this priority**: These states are what make the shell feel finished and trustworthy across all three entities; without them a valid narrow search reads as a broken page.

**Independent Test**: On each page, apply a query/filter combination that matches nothing and confirm a no-results message with a clear-and-retry action; point a page at an empty dataset and confirm a distinct empty state. Confirm the three pages share the same header, search field, Filters/Sort controls, chip row, and count line.

**Acceptance Scenarios**:

1. **Given** any browse page whose current query and filters match nothing, **When** the results resolve, **Then** a no-results message is shown together with a way to clear the search and/or reset filters, and using it restores results.
2. **Given** any browse page with no underlying data at all, **When** it loads, **Then** a distinct empty state is shown (not a spinner and not the no-results message).
3. **Given** any of the three browse pages, **When** compared side by side, **Then** they present the same shell — title + "Browse" caption, search field, Filters button (with active-count badge), Sort control, active-filter chip row, and result-count line — differing only in filter set, default sort, and row contents.
4. **Given** a slow or failed results request, **When** it is loading, **Then** a loading indication is shown; **When** it fails, **Then** an error state with a retry is shown rather than a blank list.

---

### Edge Cases

- **Whitespace / very short queries**: a query of only spaces is treated as no query (browse all); leading/trailing whitespace is ignored.
- **Diacritics & casing**: searches are case-insensitive and match German names/cities regardless of accent (e.g. "koln" matches "Köln").
- **Pagination boundary**: scrolling to the end of a long result set loads the next page seamlessly and stops cleanly at the true end without duplicating rows; changing the query or a filter resets to the first page.
- **Filter + query interaction**: filters remain applied while the user types; the count line reflects query **and** filters combined.
- **Teams without a city** (Mixteams): appear in browse but are excluded by a specific city filter; searching still finds them by name.
- **Events exactly at "now"**: an event whose end is in the past is "past"; an in-progress event (started, not ended) counts as not-past and shows by default.
- **Player opts out mid-session**: a player who disables their opt-in stops appearing on subsequent searches; a stale link to their profile still resolves per existing profile visibility rules (unchanged by this feature).
- **Sort with ties**: equal sort keys fall back to a stable secondary order so pagination never drops or repeats a row.
- **Near me**: the locked coming-soon control can never be enabled and never affects results.

## Requirements *(mandatory)*

### Functional Requirements

#### Shared browse shell (all three pages)

- **FR-001**: The system MUST provide a Browse area with three sibling pages — Teams, Events, and Players — each at its own address and each with its own search + filter surface.
- **FR-002**: All three pages MUST present an identical shell: a title with a "Browse" caption, a full-width search field, a Filters button showing a numeric badge of active filters, a Sort control, an active-filter chip row, and a result-count line.
- **FR-003**: On first load, before any input, each page MUST show a paginated list of all matching entities with that page's default filters and default sort already applied (never a blank "start typing" state).
- **FR-004**: The system MUST update results live as the user types, applying the search server-side after a short debounce so the client never receives non-matching rows.
- **FR-005**: All filtering, searching, and sorting MUST be performed server-side; the client MUST NOT filter, hide, or re-rank rows locally.
- **FR-006**: Filters MUST open on demand in a panel — a bottom sheet on mobile, a slide-over drawer on desktop — containing that page's filter set, a Reset control, and a primary action whose label reflects a **live count** of results the pending (not-yet-applied) selection would produce.
- **FR-007**: Applied filters MUST appear as removable chips above the results; each chip MUST be individually removable, and a "Clear all" control MUST remove them all at once. Removing chips MUST update results, the count line, and the Filters badge.
- **FR-008**: The result-count line MUST state the total number of matches and summarize the active filters in words (e.g. "3 teams · active · beginners welcome").
- **FR-009**: Results MUST be shown as compact list rows; each row MUST link to that entity's existing detail page.
- **FR-010**: Result lists MUST be paginated and continue loading further pages on scroll, stopping cleanly at the end without duplicating rows; changing the query or any filter MUST reset to the first page.
- **FR-011**: Every filter panel MUST include a "Near me / distance" affordance rendered as a locked "Coming soon" item that cannot be toggled and never affects results.
- **FR-012**: Each page MUST show a distinct empty state (no data exists), no-results state (query/filters matched nothing, with a control to clear the search and/or reset filters), loading indication, and error-with-retry state.
- **FR-013**: All browse and search reads MUST be available to anonymous (signed-out) visitors, except where a per-entity privacy rule restricts visibility (see Players).
- **FR-014**: Browse responses MUST expose only public fields for each entity; no private, admin-only, or contact detail (e.g. user email addresses) may be returned through browse.

#### Teams page

- **FR-020**: The Teams page MUST search team **name and city**, default-sort **A–Z**, and show rows containing a logo/initial, team name, city, player count, and an optional "Beginners" status chip where the team has flagged itself beginners-welcome. (No "Recruiting" chip in v1 — see Clarifications.)
- **FR-021**: The Teams filter set MUST include: an "Active teams only" toggle (default ON), a "Beginners welcome" toggle, a City/region selector, and the locked Near-me item.
- **FR-022**: With "Active teams only" ON (the default), the system MUST hide dormant teams. For v1, a team is **active** if it has participated in at least one event within the last 12 months; all others are dormant. (This rule is intentionally simple and may be extended later.)
- **FR-023**: The system MUST expose a per-team, admin-managed "Beginners welcome" flag (default OFF) editable in team settings; it backs both the "Beginners" row chip and the "Beginners welcome" filter.

#### Events page

- **FR-030**: The Events page MUST search event **name**, default-sort by **soonest upcoming**, and show rows containing event name, date, location/city, and type.
- **FR-031**: The Events filter set MUST include: a "Hide past events" toggle (default ON), a from/to **date range**, an **event type** selector, a City/region selector, and the locked Near-me item.
- **FR-032**: With "Hide past events" ON (the default), the system MUST list only events that have not already ended; cancelled events MUST be excluded from browse regardless of this toggle.

#### Players page (privacy-critical)

- **FR-040**: The Players page MUST search player **display name**, default-sort **A–Z**, and show rows containing an avatar, display name, city, and position. Position is **derived from the player's declared pompfen** (Läufer/Runner plus their pompfen weapons); no separate position field is introduced.
- **FR-041**: The system MUST expose a per-player "Appear in search" opt-in setting that defaults to **OFF**; a player is discoverable on the Players page only while this setting is ON.
- **FR-042**: The Players browse endpoint MUST exclude every player who has not opted in, server-side, for all queries, filters, and sorts, and regardless of whether the requester is signed in — a non-opted-in player MUST NOT be surfaceable by any means through this feature.
- **FR-043**: The Players filter set MUST include: a **Position** multi-select (over the canonical pompfen/position set), a City/region selector, and the locked Near-me item. (No "Looking for a team" or "Experience" filters in v1 — see Clarifications.)
- **FR-044**: The Players page MUST display a note stating that only players who chose to appear in search are listed.
- **FR-045**: The player "Appear in search" setting MUST be editable by the player from their own profile settings and MUST take effect for subsequent searches.

### Key Entities *(include if feature involves data)*

- **Team (existing, extended)**: A club players belong to, addressed by an immutable slug, with a display name, type (city team / mixteam), optional home city, and membership. Browse exposes name, city, member count, and beginners-welcome status. This feature adds one new attribute: a self-managed **"Beginners welcome" flag** (default off). The "active" state is **derived**, not stored — a team is active if it has participated in an event within the last 12 months (via existing event-participation records).
- **Event (existing)**: An ownable Jugger event with a name, type, start/end schedule, location (in-person city/country or virtual), participation, fee, and lifecycle status. Browse exposes name, date, city/location, and type; only non-cancelled events are listed, and past events are hidden by default.
- **Player Profile (existing, extended)**: The public-facing identity for an account, with an immutable handle, display name, hometown, and declared pompfen. This feature adds exactly one new attribute: a self-managed **"Appear in search" opt-in** (default off). Position shown/filtered is **derived** from the player's existing declared pompfen; no looking-for-a-team or experience attributes are added in v1.
- **Browse query (conceptual)**: The combination of free-text query, the active per-entity filters, the chosen sort, and the pagination position that together determine one page of results. Not persisted (saved searches are out of scope).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A first-time visitor can reach a relevant team, event, or player from the corresponding browse page in **under 30 seconds** without instruction, because results are visible immediately and narrowing is live.
- **SC-002**: Live results appear within **1 second** of the user pausing typing, for typical community-scale datasets.
- **SC-003**: **100%** of players who have not opted into search are absent from Players browse results across every query, filter, and sort combination — verified as a hard invariant, not a sampled metric.
- **SC-004**: The three browse pages are **behaviourally identical** apart from their filter set, default sort, and row contents — a user who has used one can operate the other two without relearning (measured by consistent presence of search, Filters+badge, Sort, chips, count, and the four list states on all three).
- **SC-005**: Every browse result set is **bounded** — no request returns an unbounded list; additional results load only on demand as the user scrolls.
- **SC-006**: On a narrow search that matches nothing, **100%** of the time the user is shown a no-results state with a working one-action path back to results (clear query / reset filters), never a blank page.
- **SC-007**: The "Near me / distance" control is present on all three filter panels and is **non-functional** (cannot be enabled, never changes results) in this release.

## Assumptions

- **Reused detail pages**: Team public pages, event pages, and player public profiles already exist; browse rows link to them and this feature adds no new detail pages.
- **Pagination contract**: The existing paginated-list contract used elsewhere in the product is reused for all browse endpoints; "infinite scroll" is presentation over that same paging.
- **City/region selector**: The city filter matches the free-text city/hometown already stored on teams, events, and profiles; a curated region taxonomy is **not** introduced in this release (a plain city match/selector is sufficient). Teams/players without a city are simply excluded by an active city filter.
- **Search matching**: Search is a case- and accent-insensitive substring match on the stated fields (team name/city, event name, player display name). Full-text relevance ranking is out of scope; default sort governs order.
- **Sort options**: Each page ships at least its stated default sort; a small set of alternates (e.g. newest, nearest date) may be offered but the default is the guaranteed behaviour. Ties break on a stable secondary key so paging is consistent.
- **Anonymous browse**: All three pages are readable signed-out; only the Players opt-in rule restricts *which rows* appear, and it applies equally to signed-out and signed-in requesters.
- **Player opt-in default OFF**: Existing players are not retroactively made searchable; they remain hidden until they explicitly opt in. This is the privacy-safe default.
- **Data-model additions (resolved in clarification)**: This feature adds exactly two stored fields — a team **"Beginners welcome"** flag and a player **"Appear in search"** opt-in (both default OFF). Team **"active"** and player **"position"** are derived from existing data (event participation within 12 months; declared pompfen). The wireframe's "Recruiting" chip, player "Looking for a team" toggle, and player "Experience" filter are **deferred out of v1**.

## Out of Scope (future extensions)

- A single **global search** across teams, events, and players at once (parked for later).
- **Map / geo / "near me" / distance** ranking — the control is a locked placeholder only.
- **Saved searches**, search history, and alerts/notifications on new matches.
- Relevance/full-text ranking beyond the stated substring search and default sort.
