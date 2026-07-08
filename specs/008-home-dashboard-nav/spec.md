# Feature Specification: Home dashboard & top-level navigation

**Feature Branch**: `008-home-dashboard-nav`

**Created**: 2026-07-07

**Status**: Draft

**Input**: User description: "Home dashboard and top-level navigation rework — the logged-in entry point. Replace the entity-type sidebar and the walking-skeleton dashboard with a task/person navigation model and a real, agenda-led Home dashboard (up next · team · news · tournaments), plus a new-player empty state. Based on wireframes/Dashboard Wireframes (offline).html and DESIGN.md."

## Overview

Today a signed-in player lands on a placeholder dashboard (a backend health check) and navigates through a drawer that lists things by data type — "Browse teams", "Browse events", "Browse players", "Create team", "Create event". This feature makes the signed-in experience navigate by **what you do** rather than **what kind of record it is**, and turns the landing page into a useful **agenda** that leads with the player's next trainings and matches.

Two changes ship together:

1. **A new navigation shell** — Home · Browse · My team, a notifications (Alerts) destination, and the player's profile/account under their avatar. One top bar on desktop; a thumb-friendly bottom tab bar on mobile. The same shell wraps every in-shell screen.
2. **A real Home dashboard** — a warm greeting and an ordered set of modules (up next with one-tap RSVP, your teams' recent activity, news, and tournaments), with a distinct low-pressure empty state for players who have not joined a team yet.

## Clarifications

### Session 2026-07-07

- Q: The desktop team snapshot shows a W/L record, but match/results modeling is deferred — what should it show? → A: Drop the record; the snapshot shows only real, existing data (team name, next fixture, "view team"). No W/L until a future results feature.
- Q: How should a team-mode event the player's team entered appear to an individual member in Up next? → A: Show it marked "your team is going" (the team is the participant); no personal RSVP toggle. Individual RSVP applies only to individuals-mode events.
- Q: On an Up next item the player is already going to, does tapping "Going" let them withdraw from Home? → A: Yes — it's a toggle: RSVP to join, tap again to withdraw, guarded by a brief confirm.
- Q: A player can be on more than one team — which team drives the team-scoped modules? → A: None in particular — all of the player's teams are shown in aggregate (news merged newest-first and tagged by team; the snapshot lists each team with its next fixture). There is no single "primary team".

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Navigate by task, not by data type (Priority: P1)

A signed-in player uses one consistent shell across the whole app. On desktop a single top bar exposes Home, Browse, and My team, a notifications bell with an unread count, and an avatar menu (Profile, Account, Sign out). On mobile the same destinations live in a fixed bottom tab bar (Home · Browse · My team · Alerts) with a slim top strip carrying the wordmark and avatar. The active destination is always highlighted; wherever the player is, the primary destinations are one tap away.

**Why this priority**: The shell is the frame every other screen lives in and the prerequisite for the dashboard being "Home". It delivers value on its own by making the app faster to move around and matching how players think ("check what's on", "go looking", "manage my team").

**Independent Test**: Sign in and confirm the top bar (desktop) and bottom tab bar (mobile) expose Home/Browse/My team and the Alerts and avatar destinations; navigating to each highlights the correct destination; Browse continues to reach the existing teams/events/players search; the old sidebar drawer is gone.

**Acceptance Scenarios**:

1. **Given** a signed-in player on a wide screen, **When** the app loads, **Then** a single sticky top bar shows the brand, Home, Browse, My team, a notifications bell (with an unread count when non-zero), and an avatar — and no left sidebar drawer is present.
2. **Given** a signed-in player on a narrow screen, **When** the app loads, **Then** a fixed bottom tab bar shows Home, Browse, My team, and Alerts, and a slim top strip shows the wordmark and avatar.
3. **Given** the player is on any in-shell screen (Browse, My team, a profile, an event), **When** they look at the navigation, **Then** the destination matching the current screen is visually marked active (and Profile, reached via the avatar, marks no primary destination active).
4. **Given** the player opens the avatar menu, **When** it expands, **Then** it offers Profile, Account, and Sign out; "create team"/"create event" are not top-level destinations.
5. **Given** the player taps Browse, **When** it opens, **Then** they reach the existing search experience (teams, events, players) unchanged in behavior.

---

### User Story 2 - See and act on what's next (Priority: P1)

Immediately after signing in, the player lands on Home and sees a warm greeting and an **Up next** module: their upcoming trainings, matches, and events in chronological order, each showing the date, title, place, time, and remaining spots, with a one-tap control to RSVP or show they're already going. "See all" leads to the full list.

**Why this priority**: This is the core reason a returning player opens the app — "what have I got on, and am I in?" It turns the landing page from a placeholder into a daily-useful agenda and is the single most valuable module.

**Independent Test**: As a player signed up to (or on a team signed up to) one or more upcoming events, load Home and confirm those events appear soonest-first with correct date/place/time and a working RSVP / "going" control; a player with nothing upcoming sees a friendly empty prompt instead.

**Acceptance Scenarios**:

1. **Given** a player who has upcoming events (their own individuals-mode sign-ups, plus events their team(s) entered if they are on a team), **When** Home loads, **Then** an Up next module lists those events soonest-first, each with date, title, location, time, and spots remaining.
2. **Given** an individuals-mode Up next item the player has not yet confirmed, **When** they tap RSVP, **Then** their attendance is recorded and the control switches to a "going" state without leaving Home.
3. **Given** an individuals-mode Up next item the player is already going to, **When** they tap the "going" control, **Then** they are asked to confirm and, on confirming, are withdrawn and the control returns to an RSVP prompt — all without leaving Home.
4. **Given** an event one of the player's teams entered (team-mode), **When** Home loads, **Then** the item shows a "your team is going" state and offers no personal RSVP/withdraw control (the team, not the individual, is the participant).
5. **Given** the module can hold more than fits, **When** the player taps "See all", **Then** they reach the full list of their upcoming events.
6. **Given** a player with no upcoming events, **When** Home loads, **Then** Up next shows a low-pressure empty prompt instead of an empty box.

---

### User Story 3 - Catch up on teams, news, and tournaments (Priority: P2)

Below Up next, Home shows recent activity from the player's team(s) (with links into each team space), a News module whose items are tagged by source (team or event) with timestamps, and a promoted tournaments module. On a wide screen a slim right rail carries a snapshot for each team the player is on (next fixture, "view team") and the tournament card so the main column stays focused.

**Why this priority**: These modules make Home a genuine home base for staying current, but they are catch-up rather than act-now, so they follow the agenda.

**Independent Test**: As a player on one or more teams that have posted news and whose events have news, and with an upcoming tournament in the system, load Home and confirm the aggregated team activity, source-tagged news items (newest first), and the tournament card all render with correct data and working links; each degrades to a friendly empty state when its data is absent.

**Acceptance Scenarios**:

1. **Given** a player on one or more teams with recent news, **When** Home loads, **Then** a "Your teams" module shows recent activity across all their teams, merged newest-first, each item tagged with its team and carrying an "open team" link into that team's space.
2. **Given** the player is connected to teams/events that have posted news, **When** Home loads, **Then** a News module lists items newest-first, each tagged with its source (team or event) and a relative timestamp, with a "see all" affordance.
3. **Given** an upcoming tournament exists, **When** Home loads, **Then** a Tournaments module promotes it with name, place, date, spots, and a "view" link to its page.
4. **Given** a wide screen, **When** Home loads, **Then** a right rail shows a snapshot for each of the player's teams (next fixture + "view team", no win/loss record) plus the tournament card; on a narrow screen these appear inline in the single column.
5. **Given** any of these modules has no data, **When** Home loads, **Then** that module shows a friendly empty state and never a broken or blank card.

---

### User Story 4 - A warm first run for players without a team (Priority: P2)

A player who has not joined a team yet gets a distinct Home: the greeting becomes "Welcome, {name} — let's find you a crew", Up next and Your team are replaced by low-pressure prompts to find a team ("many lend gear and welcome newcomers"; primary "find a team near you", secondary "browse open trainings"), and an "Open to everyone" module surfaces trainings and events anyone can attend. News still shows.

**Why this priority**: New players are the most fragile audience and the growth engine of a community app; a blank or team-centric dashboard would strand them. It rides on the same dashboard, so it follows the core modules.

**Independent Test**: As a signed-in player with no team membership, load Home and confirm the welcome variant: find-a-team prompts in place of Up next/Your team, an "open to everyone" module of open events, and News still present; joining a team flips Home to the standard variant.

**Acceptance Scenarios**:

1. **Given** a signed-in player who is on no team, **When** Home loads, **Then** the greeting welcomes them to find a team and the Up next / Your team areas become warm prompts ("find a team near you" → Browse teams; "browse open trainings" → open events).
2. **Given** the same player, **When** Home loads, **Then** an "Open to everyone" module lists trainings/events anyone can attend, each with the same one-tap RSVP.
3. **Given** the same player, **When** Home loads, **Then** the News module still appears.
4. **Given** the player later joins a team, **When** they return to Home, **Then** Home shows the standard team-member variant (Up next, Your team, etc.).

---

### User Story 5 - A place for alerts and account (Priority: P3)

The navigation exposes a notifications/Alerts destination (a bell on desktop with an unread count, an Alerts tab on mobile) and the player's Profile and Account under the avatar. In this feature the Alerts destination is present and reachable but shows a friendly "nothing here yet" placeholder, with the unread count at zero — the real notifications system arrives later.

**Why this priority**: The destinations must exist so the shell is complete and stable, but the backing notification system is deferred, so this is the lowest-priority slice of the shell.

**Independent Test**: Confirm the Alerts destination is reachable from both desktop and mobile navigation and shows a placeholder screen; the unread indicator reads zero; Profile and Account are reachable from the avatar menu.

**Acceptance Scenarios**:

1. **Given** a signed-in player, **When** they open Alerts, **Then** they see a friendly placeholder ("you're all caught up" / "nothing here yet") rather than an error or blank screen.
2. **Given** notifications are not yet implemented, **When** the navigation renders, **Then** the unread count is zero / hidden and never shows a misleading number.
3. **Given** the avatar menu, **When** the player selects Profile or Account, **Then** they reach their existing profile and account screens.

---

### Edge Cases

- **Loading**: Each module shows a loading state on first paint and never a layout jump when data arrives.
- **Partial failure**: If one module's data fails to load, the rest of Home still renders; the failed module shows a retry affordance, not a whole-page error.
- **Not signed in**: Home is a signed-in screen; an unauthenticated visitor hitting it is routed to sign-in (existing behavior), not shown an empty dashboard.
- **Player on multiple teams**: the Your teams module and the right rail show all of the player's teams in aggregate — team activity is merged newest-first and tagged by team, and the rail shows one snapshot per team. A player on many teams sees a bounded, sensibly-capped list, not an unbounded wall.
- **RSVP contention**: Tapping RSVP on an event that has just filled shows a clear "full / waitlisted" result rather than silently failing.
- **Timezones / relative times**: "2h ago", "Sat 14:00" render consistently for the viewer; a training later today vs. next week is unambiguous.
- **Long content**: Long team, event, or place names truncate gracefully within cards without breaking the layout at any viewport.
- **Small screens**: The bottom tab bar stays reachable and does not obscure content or the RSVP controls; safe-area insets are respected.

## Requirements *(mandatory)*

### Functional Requirements

**Navigation shell**

- **FR-001**: The signed-in app MUST present a single navigation shell with the primary destinations Home, Browse, and My team, plus an Alerts (notifications) destination and an avatar/account menu, and MUST wrap every in-shell screen.
- **FR-002**: On wide screens the shell MUST render as one sticky top bar (brand, primary destinations, notifications bell, avatar); on narrow screens it MUST render the primary destinations plus Alerts as a fixed bottom tab bar with a slim top strip (wordmark + avatar).
- **FR-003**: The shell MUST visually mark the destination corresponding to the current screen as active; screens reached via the avatar (e.g. Profile) mark no primary destination active.
- **FR-004**: Browse MUST reach the existing teams/events/players search experience unchanged; the previous per-type sidebar entries and the "create team"/"create event" entries MUST NOT appear as top-level destinations.
- **FR-005**: "My team" MUST take a player who is on a team to their team space and MUST present a "find a team" prompt to a player who is on no team.
- **FR-006**: The avatar menu MUST expose Profile, Account, and Sign out.
- **FR-007**: The navigation MUST keep no fewer than all primary destinations reachable at every supported viewport, with touch targets meeting the design system's minimum size.

**Home dashboard — common**

- **FR-008**: Home MUST be the default landing screen after sign-in (the app's root in-shell route) and MUST require an authenticated session.
- **FR-009**: Home MUST greet the player by name with a short agenda line, and MUST present its modules in the order: Up next, Your team, News, Tournaments (team-member variant).
- **FR-010**: On wide screens Home MUST use a main column plus a slim right rail (team snapshot + tournament); on narrow screens all modules MUST stack in one column.
- **FR-011**: Every module MUST have defined loading, empty, and error states; a failure in one module MUST NOT prevent the others from rendering.
- **FR-012**: Home MUST show only data the signed-in player is entitled to see; all filtering to that entitlement MUST be enforced server-side and responses MUST carry only public/permitted fields.

**Up next**

- **FR-013**: Up next MUST list the player's upcoming events — their own individuals-mode sign-ups and, if they are on a team, the events their team(s) entered — in soonest-first order, each showing date, title, location, time, and spots remaining.
- **FR-014**: Up next MUST exclude past and cancelled events.
- **FR-015**: Each individuals-mode Up next item MUST offer a one-tap toggle: RSVP to join when not going, and — when already going — a "going" control that, after a brief confirmation, withdraws the player; acting MUST update the item in place without leaving Home. A team-mode item (an event the player's team entered) MUST instead show a non-interactive "your team is going" state with no personal RSVP/withdraw control.
- **FR-016**: RSVP from Home MUST respect the event's existing sign-up rules (capacity, waitlist, approval) and surface the resulting state (going / waitlisted / full) to the player.
- **FR-017**: Up next MUST offer a "see all" path to the player's full upcoming-events list and MUST show a friendly empty prompt when the player has nothing upcoming.

**Your team, News, Tournaments**

- **FR-018**: The Your teams module MUST show recent activity across all of the player's teams, merged newest-first, each item tagged with its team and carrying a link into that team's space; when no activity exists it MUST show a friendly empty state.
- **FR-019**: The News module MUST aggregate news the player is connected to (from their teams and the events they are involved with), newest-first, each item tagged by its source (team or event) with a relative timestamp, and MUST offer a "see all" affordance.
- **FR-020**: The Tournaments module MUST promote upcoming tournament events with name, place, date, spots, and a link to the tournament's page.
- **FR-021**: On wide screens the right rail MUST show a compact snapshot for each of the player's teams (team name, next fixture, "view team" — no win/loss record) and the tournament card; these MUST appear inline in the single column on narrow screens.
- **FR-022**: Any list module MUST be bounded (paginated / capped) and MUST NOT return or render an unbounded collection.

**New-player empty state**

- **FR-023**: For a signed-in player on no team, Home MUST show the welcome variant: a "let's find you a crew" greeting, find-a-team prompts replacing Up next and Your team (primary "find a team near you" → Browse teams; secondary "browse open trainings" → open events), and an "Open to everyone" module of events anyone can attend.
- **FR-024**: The News module MUST still appear in the new-player variant.
- **FR-025**: When a player joins a team, Home MUST switch to the team-member variant on next load.

**Alerts & account (placeholder scope)**

- **FR-026**: The Alerts/notifications destination MUST be reachable from both desktop and mobile navigation and MUST show a friendly placeholder screen in this feature; the unread indicator MUST read zero / be hidden and MUST NOT display a misleading count.
- **FR-027**: Profile and Account MUST be reachable from the avatar menu and MUST reach the player's existing profile and account screens.

**Voice, visual, and quality**

- **FR-028**: All copy MUST follow the JuggerHub voice (sentence case, warm, encouraging, low-pressure empty states, no emoji) and all visuals MUST follow the design system (warm sand/coral palette, rounded shapes, line icons, scores/times in the mono style).
- **FR-029**: Home and the shell MUST be usable and correct at both mobile and desktop viewports, meet the design system's contrast and touch-target rules, and expose navigation and controls to assistive technology (labels, active/selected state, keyboard reachability).

### Key Entities *(include if feature involves data)*

- **Player (viewer)**: the signed-in user; determines the greeting, team membership (which Home variant shows), and entitlement to every module's data.
- **Upcoming event item**: a projection of an existing event the player or one of their teams is signed up to — date, title, location, time, spots remaining, and an attendance state that is either the player's own (individuals-mode: not going / going, toggleable) or "team is going" (team-mode: read-only) — used by Up next and the "open to everyone" module.
- **Team activity item**: a recent happening in one of the player's teams, sourced from existing team news; carries a title/summary, the owning team (for tagging + link), and a timestamp. Aggregated across all of the player's teams. (Richer activity — roster joins, results — is a later feature.)
- **News item**: a team- or event-sourced post the player is connected to, carrying a title, a source tag (team/event), and a timestamp. (An official "league" source is a later feature.)
- **Tournament item**: an upcoming tournament event — name, place, date, spots, link — for the Tournaments module and right-rail card.
- **Team snapshot**: a compact summary of one team the player is on — team name and next fixture (no win/loss record) — shown once per team in the desktop right rail.
- **Notification (deferred)**: the Alerts destination anticipates a notification concept, but no notification data is created or stored in this feature; the unread count is a fixed zero.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A returning player can see their next scheduled event and confirm attendance from the landing screen in a single tap, without navigating away from Home.
- **SC-002**: From any in-shell screen, a player can reach any primary destination (Home, Browse, My team, Alerts) in one tap on both mobile and desktop.
- **SC-003**: Home's most important content (greeting + Up next) is visible within about one second of the page settling under normal conditions, and no module failure blanks the page.
- **SC-004**: A brand-new player with no team is never shown an empty or broken dashboard; Home always offers at least one clear next step (find a team, browse open trainings, or RSVP to an open event).
- **SC-005**: 100% of Home's modules render a defined loading, empty, and error state (no blank boxes, no whole-page error on a single module's failure).
- **SC-006**: A player only ever sees events, team activity, and news they are entitled to — verified by data-access tests that a player cannot see another player's private sign-ups or another team's internal activity via Home.
- **SC-007**: Home and the shell pass design-system and accessibility checks (contrast, ≥44px touch targets, labeled/active navigation, keyboard reachability) at mobile and desktop viewports.

## Assumptions

- **Reuses existing systems**: authentication/session, the events + sign-up model, team memberships and team news, event news, and the Browse (search) feature are all in place and reused; this feature adds read/aggregation surfaces and a dashboard/shell, not new core domains.
- **Real vs. deferred data (per product decision)**: Up next and Tournaments read real events/sign-ups; News aggregates existing team + event news; **Notifications/Alerts, an official "League" news source, and a unified activity feed (roster joins, match results) are deferred** — their destinations/modules appear but render placeholder/empty states seeded for later features.
- **No new event types**: the wireframe's "training" and "match" labels map onto existing event data; **no new event-type values or team-vs-team match modeling** are introduced here.
- **All teams aggregated**: for players on more than one team, the team-scoped modules show every team the player is on in aggregate (activity merged and tagged by team; one snapshot per team) — there is no single "primary team". The aggregate list is bounded/capped for players on many teams.
- **No team win/loss record**: the team snapshot shows only real, existing data (team name + next fixture). A win/loss record is not shown because match/result modeling is deferred (see Out of Scope).
- **RSVP semantics**: acting on Up next uses the existing event sign-up rules (capacity, waitlist, approval, out-of-band payment) unchanged; Home only surfaces the outcome. Individual RSVP/withdraw applies only to individuals-mode events; team-mode events are shown read-only as "team is going".
- **Near-me/geo** remains a locked placeholder (as in Browse); "find a team near you" links to Browse teams rather than a geo search.
- **Scope of this feature is signed-in players**; the public/marketing entry points and the signed-out experience are unchanged.

## Out of Scope

- A Notifications backend (entity, delivery, real unread counts) and any notification content.
- An official/"League" news source and a unified team activity feed beyond existing team news.
- New event types (Training, Match) and team-vs-team match/result modeling.
- Near-me / geographic search or ranking.
- Multi-team aggregated dashboards.
- Any change to the signed-out / marketing experience.
