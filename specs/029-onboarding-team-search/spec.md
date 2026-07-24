# Feature Specification: Onboarding Team Search

**Feature Branch**: `feat/029-onboarding-team-search`

**Created**: 2026-07-24

**Status**: Draft

**Input**: User description: "Onboarding team step: replace the feature-004 visual placeholder with real team search and a join request (GitHub issue #74). The 'Find your team' step still shows a disabled search box, two hardcoded sample teams and a 'coming soon' note — a deliberate stub taken in feature 004 (FR-021) when no teams model existed. Feature 005 shipped real teams and 007 shipped team browse/search, so the step must catch up. Amends 004's FR-021 rather than editing it in place. Frontend wiring only — no new API, DTO, or migration. The search field becomes live; picking a team and continuing sends a join *request*, not instant membership; the escape hatches stay; onboarding must never be blocked by this step."

## Context: what this amends

This feature **supersedes [FR-021 of feature 004](../004-onboarding/spec.md)** ("the team step MUST be
presented as a clear placeholder … any selection there MUST NOT be persisted") and the
out-of-scope line "a real teams model or team search".

FR-021 is not wrong — it was correct for its moment. Feature 003 had shipped teams as a UI-only
stub and no teams model existed, so a functioning team step could not have been built. That
rationale is now **historical**: feature 005 shipped the teams model and join requests, feature 007
shipped team browse and search, and feature 009 shipped the public team page with its
"Request to join" action. 004's FR-021 remains in place as the record of that decision; this spec
is the amendment that ends it.

Everything else in 004 stands unchanged — in particular FR-008 and FR-014, which make every step
after the display name skippable. This feature strengthens, never weakens, that guarantee.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Find my team and ask to join, during onboarding (Priority: P1)

A player who has just registered reaches the "Find your team" step. It opens with a short list of
teams that welcome new players, and tells them plainly that they can search for any other team by
name. They type part of their team's name, see it appear with its city and roster size, and pick
it. An "ask to join" action appears for that team; pressing it sends the request and the step
confirms, right there, that a team admin will let them in — it never claims they are now a member.

**Why this priority**: This is the entire point of the feature and the reason issue #74 exists. A
new player's first encounter with teams is currently a control that does not work; making it work
is the whole deliverable. It is independently valuable on its own: even without the polish of the
other stories, a player who finds their team and gets a pending request out of onboarding has
gained something real.

**Independent Test**: Enter onboarding as a new account, reach the team step, confirm real teams
are listed, type a query and confirm the results change to match, pick a team, ask to join, and
confirm a pending join request exists for that team from that account — and that the account is
*not* a member of it.

**Acceptance Scenarios**:

1. **Given** the team step opens, **When** it first renders, **Then** it lists teams that welcome
   beginners and makes clear, in visible copy, that any other team can be found by searching by name.
2. **Given** the team step, **When** the player types a query, **Then** the list shows teams matching
   that query drawn from **all** teams, not only beginner-friendly ones.
3. **Given** a list of results, **When** each team row renders, **Then** it shows the same
   information as the teams browse list: the team's initial, its name, its city and player count,
   and a marker when it welcomes beginners.
4. **Given** results are listed, **When** the player picks a team, **Then** that row is visibly
   selected, no other row is, and an ask-to-join action for that team becomes available.
5. **Given** a team is picked, **When** the player uses the ask-to-join action, **Then** a join
   request is created for that team on behalf of the signed-in player, and the step confirms in
   plain words, on the step itself, that an admin still has to approve it.
6. **Given** a join request has just been sent, **When** the confirmation is shown, **Then** it does
   not state or imply that the player is now a member of the team.
7. **Given** the player continues without having asked to join anything, **When** the flow advances,
   **Then** no join request is sent and nothing about teams is written — including when a team is
   merely selected.

---

### User Story 2 - Never be trapped by this step (Priority: P1)

Whatever goes wrong — the search fails, the network wobbles, the join request is refused — the
player can always move on. The step tells them quietly what happened and lets them continue, skip,
or go back. Nothing about teams can hold a brand-new player inside the wizard.

**Why this priority**: Equal to P1 because this is the **first screen after registration**. A player
stuck here is a player lost before they ever see the app, so the escape guarantee is not polish —
it is a hard constraint on the primary story and must ship with it.

**Independent Test**: Force the team search to fail and confirm the player still sees a Continue,
"I'm not on a team yet", and Back that all work. Force the join request to fail and confirm the
player is told, is not stuck, and can still complete onboarding and reach the app.

**Acceptance Scenarios**:

1. **Given** the team search fails, **When** the failure is shown, **Then** the player is offered a
   retry **and** every way out of the step (continue, "I'm not on a team yet", Back) still works.
2. **Given** the join request fails, **When** the failure is shown, **Then** the player is told the
   request did not go through, and continuing still advances — the failure never blocks onboarding.
3. **Given** any failure on this step, **When** the message is shown, **Then** it is quiet and
   plain-worded and reveals no system internals.
4. **Given** the player chooses "I'm not on a team yet" or Skip, **When** the flow advances,
   **Then** no join request is sent and no team data is written, exactly as before this feature.
5. **Given** the player finishes onboarding after this step, **When** the profile is saved,
   **Then** the saved profile is identical to what it would have been without this feature — the
   pick itself is never part of the profile.

---

### User Story 3 - A calm, honest step that matches the rest of the app (Priority: P2)

The step looks and behaves like the rest of JuggerHub: the same team rows as the browse screen, the
standard loading line rather than a spinner, and states that read differently depending on what
actually happened — "no teams match that" invites another try; a failure offers a retry.

**Why this priority**: The step is usable without this consistency, so it follows P1, but a first
impression that visibly differs from the rest of the product undercuts the feature's purpose.

**Independent Test**: Compare the step's rows, loading line, empty state, and error state against
the teams browse screen and DESIGN.md, and confirm a search that returns nothing and a search that
fails are visibly and verbally distinct.

**Acceptance Scenarios**:

1. **Given** a search is running, **When** the player waits, **Then** a single quiet loading line is
   shown — never a spinner — consistent with every other loading state in the app.
2. **Given** a query that matches no team, **When** results return empty, **Then** the player sees a
   "no teams match that" message that invites them to try another search, with no retry action.
3. **Given** a search that fails, **When** the failure is shown, **Then** the player sees a distinct
   failure message offering a retry — never the empty-results wording.
4. **Given** the player types continuously, **When** they pause, **Then** the search runs once for
   what they typed rather than once per keystroke.
5. **Given** the step on a phone and on a desktop, **When** it renders, **Then** it is legible and
   usable at both sizes per DESIGN.md.
6. **Given** the step in any state, **When** it renders, **Then** no placeholder artefact from
   feature 004 remains: no disabled field, no sample teams, no "coming soon" note.

---

### Edge Cases

- **The player types, then clears the field**: the step returns to its opening state — the
  beginner-friendly list — rather than showing stale results for a query that is no longer there.
- **The player picks a team, then changes their mind and picks another**: only the last pick counts,
  and the ask-to-join action follows the new selection. Nothing was sent for the abandoned pick.
- **The player asks to join, then goes Back and returns to the step**: the request is remembered as
  already sent, so the same team cannot be asked twice from this flow.
- **The player asks to join one team, then picks another and asks again**: both requests stand.
  Belonging to several teams is allowed (feature 005); the step does not pretend otherwise.
- **The player picks a team they already belong to, or has already asked to join**: the system
  refuses the duplicate; the player is told plainly and is not blocked. (Rare during first-login
  onboarding, but reachable by a player who accepted an invite before onboarding.)
- **Search is slow**: the loading line reassures rather than looking stalled, and Continue/Skip
  remain available throughout — a slow search never disables the way out.
- **No teams exist at all, or none welcome beginners**: the opening list is empty and says so
  without implying an error, and the search field still works.
- **The player leaves onboarding right after the request is sent**: the join request stands on its
  own — it does not depend on the player finishing onboarding.
- **Onboarding is dismissed from the Welcome screen**: the team step is never reached and no team
  request is possible, unchanged from before.

## Requirements *(mandatory)*

### Functional Requirements

#### Search & results

- **FR-001**: The team step MUST offer a working search field over real teams; the field MUST NOT be
  disabled and MUST NOT present sample or fabricated teams.
- **FR-002**: On first display, the step MUST show teams that welcome beginners, and MUST state
  visibly that any other team can be found by searching by name.
- **FR-003**: Entering a query MUST search **all** teams by name, not only those that welcome
  beginners; clearing the query MUST return the step to its opening beginner-friendly list.
- **FR-004**: Searching MUST be debounced so that a pause in typing triggers one search, not one per
  keystroke.
- **FR-005**: Each result MUST present the same team information as the teams browse list — initial,
  name, city, player count, and a beginners-welcome marker where applicable.
- **FR-006**: The step MUST distinguish four states with distinct treatment: loading, results,
  no matches for the current query, and search failure.
- **FR-007**: The loading state MUST use the application's standard loading treatment (a single
  quiet line, never a spinner).
- **FR-008**: A search failure MUST offer a retry; an empty result set MUST NOT — it MUST instead
  invite the player to try a different search.
- **FR-009**: The step MUST NOT surface system internals, stack traces, or raw failure detail.

#### Selection & joining

- **FR-010**: The player MUST be able to select at most one team at a time, with the selection
  clearly visible; selecting another team MUST replace the previous selection.
- **FR-011**: Selecting a team MUST reveal an explicit ask-to-join action naming that team. Using it
  MUST create a **join request** for that team on behalf of the signed-in player, using the same
  capability as the team page's "Request to join" action.
- **FR-012**: Sending a join request MUST be the *only* way this step writes anything. Continuing,
  choosing "I'm not on a team yet", skipping, and going Back MUST never send a join request — not
  even when a team is selected.
- **FR-013**: The step MUST confirm a sent request in words that make the pending nature explicit —
  that an admin will let the player in — and MUST NOT state or imply membership has been granted.
- **FR-014**: The confirmation MUST appear on the team step itself; the Done screen MUST NOT claim
  anything about teams.
- **FR-015**: A team already requested during this flow MUST be shown as already asked, and MUST NOT
  be askable a second time from the step.
- **FR-016**: A refused request (already a member, or otherwise rejected) MUST be reported plainly to
  the player and MUST NOT be retried automatically.

#### Never blocking

- **FR-017**: No state of this step — loading, failed search, in-flight or failed join request — may
  disable or remove Continue, "I'm not on a team yet", or Back. The player MUST always be able to
  leave the step in both directions.
- **FR-018**: Because Continue never sends anything (FR-012), leaving the step MUST be instantaneous
  and MUST NOT depend on any network call. A failed join request MUST NOT prevent the flow from
  advancing, MUST NOT prevent onboarding from completing, and MUST NOT prevent the player reaching
  the app.
- **FR-019**: The step's outcome MUST NOT change what onboarding saves to the player's profile; the
  selection itself MUST NOT be persisted to the profile — the join request is the only persistence
  this step produces.

#### Removal of the placeholder

- **FR-020**: All feature-004 placeholder artefacts MUST be removed: the disabled field, the
  hardcoded sample teams, the "coming soon" note, and the non-persisted selection state that
  supported them.
- **FR-021**: The rest of the onboarding flow — step order, progress indication, Back behaviour,
  required display name, and the finish payload — MUST be unchanged by this feature.

#### Security & authorization

- **FR-022**: Whether a join request may be created MUST be decided server-side for the signed-in
  account; nothing on this step is a security boundary.
- **FR-023**: The step MUST only show teams the signed-in player is permitted to see, on the same
  terms as the teams browse screen.

### Key Entities *(include if feature involves data)*

- **Team (existing, feature 005)**: Read-only here. The step searches and lists teams; it creates,
  changes, and deletes nothing about them.
- **Join Request (existing, feature 005/009)**: A pending request from a player to a team, approved
  or declined by a team admin. This step creates one; it is the sole persistence the step produces.
- **Player Profile (existing, feature 003)**: Untouched by this step. The team selection is never
  written to it, so the onboarding finish payload is unchanged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A player in onboarding who knows their team's name can find it and send a join request
  in under 30 seconds, without leaving the flow.
- **SC-002**: 100% of teams reachable from the teams browse screen are reachable from this step's
  search; 0% of listed teams are fabricated.
- **SC-003**: In 100% of failure cases — search failure, join-request failure, or both — the player
  can still complete onboarding and reach the app.
- **SC-004**: 0% of confirmations shown after a join request state or imply that membership has been
  granted; 100% state that approval is still pending.
- **SC-005**: A player who skips the step — or who selects a team but never asks to join — ends
  onboarding with exactly the same saved profile and the same number of join requests (zero) as
  before this feature existed.
- **SC-009**: Advancing past the step issues zero network requests, in every state of the step.
- **SC-006**: A search that returns nothing and a search that fails are distinguishable by both
  wording and available action in 100% of cases.
- **SC-007**: Continuous typing produces at most one search per typing pause, not one per keystroke.
- **SC-008**: No placeholder artefact from feature 004 (disabled field, sample teams, "coming soon"
  note) is present in any state of the step.

## Assumptions

- **Amends, does not rewrite, feature 004.** 004's FR-021 stays in its spec as the record of a
  decision that was right at the time; this spec supersedes it. No other 004 requirement changes.
- **No new server capability.** Team search and join requests already exist and are already used by
  the browse and team-page screens; this feature wires the existing capabilities into the step. No
  new endpoint, contract, stored field, or migration.
- **The join request is the persistence.** Because the request itself is durable, the selection does
  not need to be carried into the onboarding finish payload, which is therefore unchanged.
- **The step opens with beginner-friendly teams** because the player is, by definition, brand new —
  but the copy makes searching for any team explicit, so this framing never becomes a cage. Once a
  query is entered, the beginners framing is dropped entirely and all teams are searched.
- **One selection at a time, but asking is not capped.** The list is single-select, so the step
  reads as "find *your* team". Nothing stops a player asking one team, then picking another and
  asking again — feature 005 places no limit on membership, and pretending otherwise would be a lie
  in the interface.
- **Asking is a deliberate, separate press.** The request fires only from an explicit ask-to-join
  action, never from Continue. This is what makes "the step can never trap you" structural rather
  than careful: Continue is pure navigation with no network call behind it, so there is no state in
  which it can be slow, fail, or need disabling. It also guarantees the pending-request confirmation
  is actually seen, since it is the direct result of a press the player chose to make.
- **Duplicate protection is server-side and the step is honest about it.** The join endpoint is
  idempotent while a request is pending and refuses an existing member; the step reports that
  outcome rather than second-guessing it. The in-flow "already asked" state (FR-015) is a courtesy
  on top, not the guarantee.
- **Reuses the existing browse row treatment and the shared loading primitive** so this step is
  visually indistinguishable from the rest of the app.
- **The Done screen is untouched.** Its copy stays generic, since it cannot honestly speak to a
  request whose outcome is unknown.
- **Out of scope**: creating a team from within onboarding; team invitations or invite links in the
  step; filters or sorting beyond the query (city, active-only, sort order); paging through results
  beyond the first page; showing or tracking the request's later outcome inside onboarding;
  notifying the player when an admin approves (feature 010 already covers that); re-running the team
  step later from settings.
