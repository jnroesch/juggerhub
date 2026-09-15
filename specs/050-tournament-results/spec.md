# Feature Specification: Tournament Results

**Feature Branch**: `050-tournament-results`

**Created**: 2026-09-15

**Status**: Draft

**Input**: User description: "Tournament results (GH #295). Tournament-type events can carry their results: the final ranking (placements, who won) and, where available, individual match results; team pages show a team's placement history across tournaments. JuggerHub works together with Tugeny (the tournament-day bracket/schedule tool most Jugger tournaments use): (1) event admins record a final ranking on a tournament event, choosing signed-up teams or naming a team not on JuggerHub; (2) event admins can paste the JSON produced by Tugeny's "Export Ranking for JTR" to fill in the ranking; (3) event admins can copy the event's confirmed team list in a form Tugeny's "Import Team Names" dialog accepts; (4) a tournament event can be linked to a finalized Tugeny tournament and its final ranking and match results imported from Tugeny's public MIT-licensed API; (5) team placement history on the team page. Out of scope: cross-tournament rankings/league standings, anonymous access, automatic achievements, bracket/schedule tooling, and any automated read of turniere.jugger.org (JTR). Team identity matching between Tugeny names and JuggerHub teams must be a human decision, never automatic name matching."

## Context

Today a tournament event in JuggerHub ends when its date passes: nothing records who won or where each team placed. That information exists — most Jugger tournaments are run on the day with **Tugeny**, a desktop bracket and schedule tool with a public website — but it lives outside JuggerHub, and a team's history of placements is nowhere in the product.

This feature gives tournament events a **results** section and gives teams a **placement history**. It is built to sit alongside Tugeny rather than compete with it: JuggerHub handles the time **before** a tournament (sign-ups) and **after** it (results, history); Tugeny handles the day itself (brackets, schedule, live scores). The two hand-offs between them — the team list going in, the results coming out — are what this feature makes cheap.

Facts about Tugeny that shape the requirements (verified 2026-09-14):

- Tugeny publishes **final rankings and match results only for tournaments the organizer has "finalized"** — a one-time step in the desktop app that requires every match to have a result. Many tournaments are never finalized, often because the final's score was never entered. So a Tugeny import cannot be the only route to results; hand entry must stand on its own.
- Tugeny's desktop app can already produce a **ranking export** (menu *Export → Export Ranking for JTR*) for any finished tournament, finalized or not, and can **import a team list** (*Import Team Names*), which accepts plain text with one team per row.
- Team names inside Tugeny brackets are frequently short names or contain typos ("Ecplise" for "Eclipse"). Deciding which JuggerHub team a Tugeny name refers to is therefore **a person's decision**, never an automatic name match.

## Clarifications

### Session 2026-09-15

- Q: Which tournaments can have results — only ones organized in JuggerHub, or also past tournaments added after the fact? → A: **Past tournaments too.** A tournament event may be created with dates in the past so its results (entered by hand, pasted, or imported from Tugeny's finalized archive) fill team histories from day one.
- Q: Which teams may a placement be attached to, and by whom? → A: **JuggerHub's platform admins link placements to teams; until a platform admin does, a placement stays a plain name.** Teams can never claim a placement themselves: names differ slightly between platforms, and a team-side claim would let the wrong team take an entry. The one link an event admin may make is to a team with a **confirmed JuggerHub sign-up for that same event**, which cannot go wrong because the team signed itself up. Many teams that really played have no such sign-up: every team of a past tournament added after the fact, teams whose sign-up ran through another channel, guest teams added on the day. Their placements are connected by a platform admin after checking by hand.
- Q: Should a platform admin's connection for a Tugeny team carry over to that team's placements in other imported tournaments? → A: **No.** Every placement is checked and connected by hand, one at a time. Wrong data spread by a bulk shortcut costs more than the manual work it saves.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Record a tournament's final ranking (Priority: P1)

After a tournament, one of its event admins opens the event and records the final ranking: which team placed first, second, third, and so on. For each placement they pick one of the teams that entered the event, or type the name of a team that isn't on JuggerHub (a mixed team, a guest team from abroad). Everyone who can see the event then sees the ranking, with the winner shown prominently.

**Why this priority**: It is the core of the feature and the only route that works for every tournament — including the many that are never finalized in Tugeny. Everything else either fills this ranking in faster (Stories 2 and 3) or reads from it (Story 4).

**Independent Test**: Create a teams tournament with four entered teams, let its start time pass, record a ranking as an event admin, and confirm the event page shows the four placements with the winner highlighted.

**Acceptance Scenarios**:

1. **Given** a tournament event whose start time has passed and I am one of its admins, **When** I open its results, **Then** I can record a ranking by assigning placements to the event's entered teams and to named teams that are not on JuggerHub.
2. **Given** a recorded ranking, **When** any signed-in person who can see the event opens it, **Then** they see the placements in order, the winning team called out, and the number of teams ranked.
3. **Given** two teams that shared a placement (e.g. both third), **When** I record them, **Then** both are shown at that placement and the next placement continues correctly (the following team is fifth, not fourth).
4. **Given** a recorded ranking, **When** I correct it later (reorder, rename a guest team, remove a placement), **Then** the change is visible immediately and the event shows when the results were last changed.
5. **Given** I am not an admin of the event, **When** I view it, **Then** I can see its results but cannot change them.
6. **Given** a tournament event that has not started yet, or that has been cancelled, **When** an admin opens it, **Then** no results can be recorded.

---

### User Story 2 - Fill in the ranking from Tugeny's export (Priority: P2)

The organizer ran the tournament in Tugeny. Instead of typing twenty-four placements, they use Tugeny's *Export Ranking for JTR* action, copy the text it produces, and paste it into the event's results. JuggerHub reads the placements and team names out of it and shows them as a draft. For each team name the organizer confirms which entered team it is, or keeps it as a plain name, and then saves.

**Why this priority**: It turns the longest manual step into a copy-paste, and it works for tournaments that were never finalized in Tugeny — the very case the import (Story 3) cannot cover.

**Independent Test**: Paste a real Tugeny ranking export into a tournament event, match the names to entered teams, save, and confirm the saved ranking equals the one Tugeny showed.

**Acceptance Scenarios**:

1. **Given** a valid ranking export from Tugeny, **When** I paste it, **Then** I see a draft ranking with every placement and team name from the export, and nothing is saved yet.
2. **Given** the draft, **When** a pasted name matches nothing obvious, **Then** it stays a plain name until I choose an entered team for it; no name is ever linked to a JuggerHub team without my choosing it.
3. **Given** text that is not a Tugeny ranking export, **When** I paste it, **Then** I get a clear message saying it could not be read, and the existing ranking (if any) is unchanged.
4. **Given** the event already has a ranking, **When** I save a draft from a paste, **Then** I am warned first that it replaces the current ranking.

---

### User Story 3 - Hand the team list to Tugeny (Priority: P2)

Before the tournament, the organizer has collected sign-ups in JuggerHub and now sets up the bracket in Tugeny. From the event they copy the list of confirmed teams in a form Tugeny's *Import Team Names* dialog accepts, and paste it there — no retyping.

**Why this priority**: It is the other half of working with Tugeny, costs little, and gives organizers a reason to take sign-ups in JuggerHub. It also means the names that come back in the ranking export are the JuggerHub names, which makes Story 2's matching trivial.

**Independent Test**: Copy the team list of an event with confirmed teams, paste it into Tugeny's *Import Team Names* dialog, and confirm Tugeny accepts it with every team present.

**Acceptance Scenarios**:

1. **Given** a teams tournament with confirmed team entries, **When** an event admin copies its team list for Tugeny, **Then** the copied text contains exactly the confirmed teams, one per row, and none of the pending or waitlisted ones.
2. **Given** two confirmed teams with the same name, **When** I copy the list, **Then** I am told which names clash, because Tugeny refuses duplicate names.
3. **Given** an event with no confirmed teams, **When** I look for the action, **Then** it explains there is nothing to copy yet instead of copying an empty list.

---

### User Story 4 - Import results from a finalized Tugeny tournament (Priority: P3)

An event admin links the tournament event to its Tugeny tournament. While the tournament is on, the event page points people to Tugeny's live view. Once the organizer has finalized it in Tugeny, the event admin imports the results: the final ranking and every match with its set scores. As in Story 2, each Tugeny team name is matched to an entered team by the admin, or kept as a name. The event then shows the full match list alongside the ranking.

**Why this priority**: It is the richest source — the only one with match-by-match scores — but only works once a tournament has been finalized in Tugeny, which many never are. Stories 1–2 must already stand on their own.

**Independent Test**: Link a tournament event to a finalized Tugeny tournament, import, match the teams, and confirm the ranking and match count equal what Tugeny publishes for that tournament.

**Acceptance Scenarios**:

1. **Given** a tournament event, **When** an admin links it to a Tugeny tournament, **Then** the event shows the Tugeny tournament's name and date so the admin can confirm it is the right one, and the event page links to Tugeny's live view.
2. **Given** a linked Tugeny tournament that is not finalized, **When** the admin tries to import, **Then** they are told results are not available from Tugeny until the organizer finalizes it there, and are pointed to Stories 1–2 as the alternative.
3. **Given** a linked, finalized Tugeny tournament, **When** the admin imports, **Then** a draft shows its ranking and matches; after matching team names and saving, the event shows the ranking and every match with its set scores.
4. **Given** results were imported, **When** the admin corrects a placement by hand, **Then** the correction sticks and the event no longer claims the results are exactly as imported.
5. **Given** Tugeny cannot be reached, **When** the admin imports, **Then** they get a clear "try again later" message and the event's existing results are untouched.

---

### User Story 5 - A team's placement history (Priority: P3)

On a team's page, signed-in players see the tournaments the team has placed in — tournament, date, and placement out of how many teams (e.g. "3rd of 24") — newest first, each linking to the tournament's results.

**Why this priority**: It is the payoff that makes results worth recording, and it needs nothing beyond Stories 1–4 having data.

**Independent Test**: Record rankings on two tournaments that both include the same team, then open that team's page and confirm both placements appear, newest first, with links to the events.

**Acceptance Scenarios**:

1. **Given** a team that has placements on recorded rankings, **When** a signed-in person opens the team page, **Then** they see each placement with tournament name, date and "placement of N", newest first.
2. **Given** a team with no placements, **When** its page is opened, **Then** the section says the team has no recorded tournament results yet.
3. **Given** a team with many placements, **When** the page is opened, **Then** the most recent ones are shown and older ones can be loaded on request.
4. **Given** a placement is removed or re-assigned on the event, **When** the team page is opened, **Then** the history reflects the change.

---

### User Story 6 - Connect results to JuggerHub teams (Priority: P3)

Rankings recorded for past tournaments, or pasted and imported from Tugeny, arrive full of plain team names. A JuggerHub platform admin works through the placements that are not yet connected to a team, and for each decides which JuggerHub team it is, or leaves it as a name. From then on the placement counts toward that team's history. Teams themselves cannot connect anything.

**Why this priority**: Without it, results of past tournaments never reach team pages, and team pages are where Story 5's history lives. It is kept to platform admins so that one careful, accountable person decides every link, and no team can claim an entry that isn't theirs.

**Independent Test**: Record a past tournament with plain team names, link two of them to JuggerHub teams as a platform admin, and confirm both placements appear in those teams' histories while the others stay plain names.

**Acceptance Scenarios**:

1. **Given** I am a platform admin, **When** I open the list of placements not yet connected to a team, **Then** I see each one with its tournament, date, placement and name, and can connect it to an existing JuggerHub team.
2. **Given** a placement I connected by mistake, **When** I disconnect it, **Then** it becomes a plain name again and leaves that team's history.
3. **Given** I connect one placement, **When** the same team name appears in other tournaments, **Then** those placements stay unconnected until I check and connect each of them.
4. **Given** I am a team admin, a team member, or an event admin whose event that team has no confirmed JuggerHub sign-up for, **When** I look at a plain-name placement, **Then** I have no way to connect it to a team.
5. **Given** a connection was made, **When** anyone later asks who connected it, **Then** the system can say which admin did and when.

### Edge Cases

- **Tied placements**: several teams can share a placement; the ranking shows the tie and skips accordingly (two thirds → next is fifth).
- **Partial rankings**: an organizer may only know the top few places; a ranking does not have to include every entered team.
- **Unfinished final**: an admin who knows the finalists but not the winner can record both at the same shared placement rather than guess.
- **Individuals-mode tournaments**: players signed up individually and teams were formed on site. Placements are recorded as plain names, which only a platform admin can connect. Copying a team list for Tugeny does not apply.
- **Past tournaments**: a past-dated event has no entries, so every placement on it starts as a plain name, and only a platform admin can connect it.
- **The same past tournament added twice**: linking a Tugeny tournament that is already linked to another event warns about it. Otherwise duplicates are not detected automatically (see Assumptions).
- **A team entered twice**: it cannot hold two placements in the same ranking.
- **A team is deleted later**: its placements stay on the event under the name it had, shown without a link.
- **A team is renamed later**: its placements show its current name.
- **An event is cancelled after results were recorded**: cancellation already freezes the event; its recorded results remain visible as they were.
- **The Tugeny tournament is linked to the wrong event**: the admin can change or remove the link; removing it does not delete results already saved.
- **The same Tugeny tournament linked to two events**: allowed but warned, since it is usually a mistake (e.g. a duplicate event).
- **A Tugeny tournament finalized again with different results**: re-importing shows the new draft and warns that it replaces what was saved.
- **Tugeny changes its export format**: a paste that cannot be read fails with a clear message (Story 2, scenario 3) rather than saving a wrong ranking.

## Requirements *(mandatory)*

### Functional Requirements

**Recording results**

- **FR-001**: Admins of a tournament-type event MUST be able to record a final ranking for it once the event's start time has passed, and MUST NOT be able to for an event that has not started or has been cancelled.
- **FR-002**: A placement in a ranking MUST name either one of the event's teams (see FR-004) or a plain team name typed by the admin; it MUST NOT reference a player.
- **FR-003**: Placements MUST allow ties, and the displayed order MUST skip placements after a tie (1, 2, 3, 3, 5).
- **FR-004**: A placement MUST stay a plain name until it is connected to a JuggerHub team by one of exactly two actors: (a) an admin of the event, and only to a team with a confirmed JuggerHub sign-up for that same event; or (b) a JuggerHub platform admin, to any existing team (FR-024). The platform admin covers teams that took part without a JuggerHub sign-up, and checks by hand that the team really played. Team admins and team members MUST NOT be able to connect, claim or request a placement.
- **FR-005**: A team MUST NOT hold more than one placement in the same ranking.
- **FR-006**: A ranking MAY be partial; it MUST NOT be required to include every entered team.
- **FR-007**: Event admins MUST be able to correct or clear a recorded ranking at any time; the event MUST show when its results were last changed.
- **FR-008**: Results are shown to everyone who can already see the event, on the event's page, with the winner (or winners, if tied) called out. Non-admins MUST NOT be able to change them.

**Working with Tugeny**

- **FR-009**: Event admins MUST be able to paste the text Tugeny's *Export Ranking for JTR* action produces and receive a draft ranking containing every placement and team name in it. Nothing is saved until the admin confirms.
- **FR-010**: Any text that cannot be read as a Tugeny ranking export MUST be rejected with a clear message and MUST leave the existing ranking unchanged.
- **FR-011**: No team name from Tugeny, pasted or imported, may be connected to a JuggerHub team unless someone permitted by FR-004 explicitly chooses that team. The system MAY list the event's entered teams first, but MUST NOT pre-select or auto-confirm one.
- **FR-012**: Saving a draft (pasted or imported) over an existing ranking MUST first warn that it replaces the current one.
- **FR-013**: Event admins of a teams tournament MUST be able to copy the event's confirmed teams — and only those — in a form Tugeny's *Import Team Names* dialog accepts. Duplicate team names MUST be flagged before copying, because Tugeny refuses them.
- **FR-014**: Event admins MUST be able to link a tournament event to a Tugeny tournament (by its address on tugeny.org), see that tournament's name and date to confirm the link, change it, or remove it. Removing a link MUST NOT delete saved results.
- **FR-015**: A linked event MUST show a link to the Tugeny tournament's live view.
- **FR-016**: For a linked tournament that Tugeny publishes as finalized, event admins MUST be able to import its final ranking and all its matches (teams, round/stage name, set scores, winner) as a draft, match team names (FR-011), and save. For one that is not finalized, the import MUST explain why it is unavailable and point to FR-001/FR-009.
- **FR-017**: Imported results MUST record that they came from Tugeny and when; after any hand correction the event MUST no longer present them as unchanged from Tugeny.
- **FR-018**: If Tugeny cannot be reached or answers with something unusable, the import MUST fail with a "try again later" message and MUST leave saved results untouched. Only an event admin's action triggers a request to Tugeny; nothing is fetched on page views.
- **FR-019**: The system MUST NOT read, copy or otherwise process any content from turniere.jugger.org automatically.

**Match results**

- **FR-020**: When an event has match results (from a Tugeny import), its page MUST list them grouped by stage (e.g. group, quarter-final), each with both teams, the set scores, and the winner. Hand entry of individual match results is out of scope.

**Placement history**

- **FR-021**: A team's page MUST show its placements across all recorded rankings — tournament name, date, placement and number of teams ranked — newest first, each linking to the event's results, loading older entries on request.
- **FR-022**: The history MUST reflect corrections and removals on events immediately, and MUST show nothing for plain-name placements, which belong to no JuggerHub team.

**Scope of tournaments**

- **FR-023**: A tournament event MAY be created with dates entirely in the past, so that the results of a tournament held before it was on JuggerHub can be recorded, pasted or imported like any other. A past-dated event MUST NOT open sign-ups, parties or the mercenary board, and MUST NOT send notifications as if it were upcoming.

**Connecting results to teams**

- **FR-024**: JuggerHub platform admins MUST be able to list placements not yet connected to a team (newest tournament first, paged), connect any of them to an existing JuggerHub team, and disconnect a connection again.
- **FR-025**: Connections MUST be made one placement at a time. Connecting a placement MUST NOT connect, suggest or pre-fill a connection for any other placement, whether in the same ranking, another tournament, or a later import. That holds even when the team name or Tugeny's own team identity is the same.
- **FR-026**: Every connection MUST record who made it and when. A team's placement history MUST count only connected placements.
- **FR-027**: Changing a placement's team through the ranking editor (FR-007) is subject to the same rules as FR-004. An event admin MUST NOT be able to connect a plain name to a team without a confirmed JuggerHub sign-up for the event, even when editing. A connection a platform admin already made MUST survive an event admin's edit of the ranking, as long as that row is kept.

### Key Entities

- **Tournament result**: the results of one tournament event — its ranking, optional match list, where it came from (entered by hand, pasted from Tugeny's export, imported from Tugeny), when it was last changed and by whom.
- **Placement**: one line of a ranking. It has a placement number (shared by tied teams) and the name as recorded. It may carry a connection to a JuggerHub team, with who made the connection and when.
- **Match result**: one match of an imported tournament — stage/round name, the two sides (JuggerHub team or plain name), the per-set scores, and the winner.
- **Tugeny link**: the association between a JuggerHub tournament event and a Tugeny tournament, with the Tugeny tournament's name and date as last seen.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An event admin records the full ranking of a 24-team tournament in under 5 minutes by hand, and in under 1 minute from a Tugeny paste or import once team names are matched.
- **SC-002**: Importing a finalized Tugeny tournament reproduces 100% of its published placements and match scores; checked against at least one real finalized tournament.
- **SC-003**: 0 placements are linked to a JuggerHub team without an event admin having chosen that team.
- **SC-004**: A team list copied from JuggerHub is accepted by Tugeny's *Import Team Names* dialog without edits whenever the team names are unique.
- **SC-005**: A recorded or corrected placement appears in the team's placement history the next time the team page is opened.
- **SC-006**: A failed Tugeny import or unreadable paste never changes an event's saved results.
- **SC-007**: Every connected placement traces back to one deliberate connecting action by a named admin. 0 placements become connected as a side effect of connecting another (FR-025).
- **SC-008**: 0 placements are connected by anyone other than an admin of that event (to teams with a confirmed JuggerHub sign-up for it) or a platform admin.

## Assumptions

- **Visibility stays sign-in-only.** Results and placement history are visible exactly where events and team pages are today (signed-in users; 026). Showing them to signed-out visitors is out of scope.
- **Event admins write results; platform admins connect them.** Co-admins count as event admins. "Platform admins" are the configured JuggerHub administrators (feature 013). They connect and disconnect placements but do not edit rankings.
- **Anyone who may create an event may create a past-dated tournament.** That is the existing event-creation rule, unchanged. Duplicate or bogus past events are not moderated by this feature: the product has no content-removal tool today (see 041), and platform admins simply never connect such an event's placements, which keeps them out of every team history.
- **Team-side corrections go through people.** A team that believes a placement is wrong contacts the event's admins (027's contact-the-admins) or a platform admin. There is no claim or dispute button.
- **Hand entry covers the ranking only.** Individual match results arrive only through the Tugeny import.
- **Import is on demand, not a sync.** Tugeny is contacted only when an event admin links or imports; there is no background polling.
- **The Tugeny ranking export's exact text shape is an open item for planning**: it must be taken from a real export produced by Tugeny's desktop app (e.g. the finished example tournament bundled with it), never inferred from another platform.
- **Tugeny's data is open data** (its public data interface is published under the MIT licence); imported results are credited to Tugeny on the event page.
- **Tugeny has no short-name field to fill from JuggerHub**: JuggerHub teams have no short name, so the copied team list carries full names; organizers shorten long names in Tugeny as they do today.
- **Out of scope**: cross-tournament rankings or league standings; automatic achievements or badges from results; notifications when results are published; player-level tournament history; bracket or schedule tooling; any integration with turniere.jugger.org; any way for a team to claim, request or dispute a placement itself.
