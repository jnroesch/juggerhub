# Feature Specification: "My team" home for teamless players

**Feature Branch**: `023-my-team-home`

**Created**: 2026-07-21

**Status**: Draft

**Input**: User description: "A 'My team' home/empty state for players who are not on any team yet. Today the top-bar and bottom-bar 'My team' nav reroutes teamless players straight to Browse teams, so the destination never lights up and feels broken. Instead, teamless players should land on a dedicated /my-team empty state that helps them get onto a team three ways: (1) search for / discover a team (links into existing Browse teams), (2) see and act on pending team invitations addressed to them — accept or decline inline — which requires a new backend endpoint to list invitations targeting the current user, and (3) create their own team (links into existing /teams/new). Players already on one or more teams keep today's behavior."

## Clarifications

### Session 2026-07-21

- Q: After a teamless player accepts a pending invitation on the "My team" home, what should happen next? → A: Auto-navigate straight into the joined team's space (single-team destination), matching the emailed-invite accept flow.
- Q: Who should see the in-app pending-invitations list? → A: The zero-team "My team" home only for v1; players already on one or more teams are not shown the list (they use the emailed link as today).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A teamless player lands on a real "My team" home (Priority: P1)

A newly registered player who is not yet on any team clicks the "My team" destination in the navigation. Instead of being silently bounced to the general Browse page (where "My team" does not even appear active), they arrive on a dedicated "My team" home that clearly explains they aren't on a team yet and offers two immediate ways forward: find a team to join, or create their own team.

**Why this priority**: This is the core defect being fixed — the "My team" destination currently feels broken for the exact users (newcomers) who most need guidance. Even with nothing else, a landing page that orients a teamless player and routes them to discover or create a team delivers the primary value and is a shippable MVP.

**Independent Test**: Sign in as a player on zero teams, click "My team" from both the desktop top bar and the mobile bottom bar, and confirm you land on the "My team" home with the destination shown as active, and that "Find a team" and "Create a team" both lead to the existing discovery and creation flows.

**Acceptance Scenarios**:

1. **Given** a signed-in player on zero teams, **When** they activate the "My team" navigation destination, **Then** they land on the "My team" home page and the "My team" destination is highlighted as the current location.
2. **Given** a teamless player on the "My team" home, **When** they choose "Find a team", **Then** they are taken to the existing team discovery/browse experience.
3. **Given** a teamless player on the "My team" home, **When** they choose "Create a team", **Then** they are taken to the existing team creation flow.
4. **Given** a player already on exactly one team, **When** they activate "My team", **Then** behavior is unchanged — they go straight to that team's space.
5. **Given** a player on more than one team, **When** they activate "My team", **Then** behavior is unchanged — they see the existing team chooser listing their teams.

---

### User Story 2 - A teamless player sees and acts on their pending invitations (Priority: P2)

A player who has been invited to one or more teams (targeted invitations addressed specifically to them) can see those pending invitations listed on the "My team" home and accept or decline each one inline, without needing to dig the original invitation link out of their email.

**Why this priority**: Invitations are the warmest path onto a team — someone already wants this player — but today the only way to act on a targeted invite is the emailed link. Surfacing pending invites in-app materially improves the newcomer's odds of joining, but the feature is still valuable without it (US1 stands alone), so it ranks second.

**Independent Test**: As a teamless player who has a pending targeted invitation, open the "My team" home and confirm the invitation appears with the team's name and the inviter; accept it and confirm you join the team; with a second pending invite, decline it and confirm it disappears and you do not join.

**Acceptance Scenarios**:

1. **Given** a teamless player with one or more pending, unexpired invitations addressed to them, **When** they open the "My team" home, **Then** each such invitation is listed showing at least the inviting team's name and the person who invited them.
2. **Given** a listed pending invitation, **When** the player accepts it, **Then** they become a member of that team and the invitation is removed from the list.
3. **Given** a listed pending invitation, **When** the player declines it, **Then** the invitation is removed from the list and they do not join the team.
4. **Given** a teamless player with no pending invitations, **When** they open the "My team" home, **Then** the page does not show an invitations list and still presents the find-a-team and create-a-team options.
5. **Given** an invitation that has expired or has already been revoked/consumed, **When** the player opens the "My team" home, **Then** that invitation is not shown as an actionable pending invite.
6. **Given** a player attempts to accept an invitation that became unusable since the page loaded (expired, revoked, or already accepted elsewhere), **When** they accept, **Then** they receive a clear, friendly message and the stale invitation is cleared from the list rather than silently failing.

---

### User Story 3 - Joining transitions the player out of the empty state (Priority: P3)

When a teamless player joins a team from the "My team" home — by accepting an invitation, or after creating a team — their team membership and navigation update so the "My team" destination now leads to their team, and they are no longer treated as teamless.

**Why this priority**: This closes the loop so the fix feels seamless, but the individual join/create actions already succeed without it (the player can navigate again manually), so it is the lowest priority of the three.

**Independent Test**: As a teamless player, accept a pending invitation from the "My team" home, then activate "My team" again and confirm it now routes to the joined team's space rather than back to the empty state.

**Acceptance Scenarios**:

1. **Given** a teamless player who accepts an invitation on the "My team" home, **When** the accept succeeds, **Then** they are navigated straight into the joined team's space and subsequent "My team" navigation resolves to a team rather than the empty state.
2. **Given** a teamless player who has just created a team, **When** they return to "My team", **Then** it resolves to their new team's space (single-team behavior), not the empty state.

---

### Edge Cases

- **No teams, no invites**: the page is a pure onboarding prompt (find a team + create a team) with no invitations section.
- **Expired or revoked invitations**: never presented as actionable; only currently-usable (pending and unexpired) invitations addressed to the player are listed.
- **Invitation for a team the player already belongs to**: accepting is treated as a no-op success (already a member) with a friendly message; it should not error out.
- **Stale list**: an invitation that becomes unusable between page load and action resolves to a friendly message and is removed from the list, not a raw error.
- **Many pending invitations**: the list is bounded/paginated rather than rendering an unbounded number of rows.
- **Reaching `/my-team` directly by URL while teamless**: shows the empty-state home, identical to arriving via the nav.
- **Reaching `/my-team` directly by URL while on one team**: consistent with today's behavior (the chooser degrades gracefully); the empty state is only for players with zero teams.
- **Player is suspended/banned**: no invitation actions succeed; server authorization remains the boundary (client rendering is not the gate).

## Requirements *(mandatory)*

### Functional Requirements

**Navigation & routing**

- **FR-001**: The "My team" navigation destination MUST route a player on **zero teams** to the "My team" home (empty state) rather than to the general Browse page.
- **FR-002**: The "My team" destination MUST appear active/current when the player is on the "My team" home, in both the desktop top bar and the mobile bottom bar.
- **FR-003**: Routing for players on **one team** (straight to that team's space) and **more than one team** (the existing chooser) MUST remain unchanged.

**Empty-state content & actions**

- **FR-004**: The "My team" home MUST clearly communicate to a teamless player that they are not yet on a team.
- **FR-005**: The "My team" home MUST provide an action that takes the player to the existing team discovery/browse experience ("Find a team").
- **FR-006**: The "My team" home MUST provide an action that takes the player to the existing team creation flow ("Create a team").
- **FR-007**: The find-a-team and create-a-team actions MUST reuse the existing browse and create experiences (no duplicate/parallel flows).

**Pending invitations**

- **FR-008**: The system MUST let a signed-in player retrieve the list of team invitations currently addressed to them that are usable (pending and unexpired).
- **FR-009**: The invitations list MUST be scoped server-side to the authenticated player; a player MUST NOT be able to see invitations addressed to anyone else.
- **FR-010**: Each listed invitation MUST include enough context to decide: at least the inviting team's name and the person who issued the invitation. It SHOULD also convey the team type and location where available.
- **FR-011**: The "My team" home MUST display the player's usable pending invitations (if any) and let them **accept** or **decline** each one inline. For v1, this list is presented **only** on the zero-team "My team" home; players already on one or more teams are not shown the list.
- **FR-012**: Accepting a listed invitation MUST make the player a member of that team, reusing the existing accept semantics (including the already-a-member and no-longer-usable outcomes), and MUST remove the invitation from the list.
- **FR-013**: Declining a listed invitation MUST remove it from the list and MUST NOT make the player a member.
- **FR-014**: When a player has no usable pending invitations, the "My team" home MUST omit the invitations list while still presenting the find-a-team and create-a-team options.
- **FR-015**: Attempting to act on an invitation that has become unusable (expired, revoked, or already consumed) MUST produce a clear, friendly message and remove the stale invitation from the list, never a raw error or stack trace.
- **FR-016**: The invitations list MUST be bounded (paginated or capped) rather than returning or rendering an unbounded number of invitations.

**State transitions**

- **FR-017**: After a player joins a team from the "My team" home (via accept), the player's cached team membership MUST refresh so that subsequent "My team" navigation resolves to a team rather than the empty state.
- **FR-018**: After accepting an invitation, the player MUST be navigated straight into the joined team's space (the single-team destination), consistent with the emailed-invite accept flow.

**Consistency & safety**

- **FR-019**: All authorization (who may list invitations, who may accept/decline which invitation, whether a suspended/banned account may act) MUST be enforced server-side; client rendering is never the security boundary.
- **FR-020**: The "My team" home MUST present accessible, responsive layouts and states (loading, empty, error) consistent with the rest of the application.

### Key Entities *(include if feature involves data)*

- **Team Invitation** (existing): an invitation to join a team. Relevant to this feature: whether it is **targeted at a specific player**, its **status** (pending/accepted/declined/revoked), its **expiry**, the **team** it grants access to, and the **inviter**. This feature adds the ability to read the set of usable, pending invitations targeted at the current player; it does not change how invitations are created.
- **Team Membership** (existing): the relationship that determines whether a player is "teamless" (zero memberships) and therefore which "My team" experience they receive.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A teamless player can reach an actionable "My team" home in a single interaction with the "My team" destination (no dead-end or wrong-destination redirect).
- **SC-002**: From the "My team" home, a teamless player can begin any of the three paths — find a team, act on an invitation, or create a team — without leaving the page to hunt for where to start.
- **SC-003**: A teamless player with a pending invitation can go from opening the "My team" home to being a team member in at most two interactions (open → accept).
- **SC-004**: 100% of invitations shown on the "My team" home are addressed to the viewing player and currently usable; no invitation addressed to another player is ever exposed.
- **SC-005**: Acting on a stale invitation never surfaces a raw error; the player always receives a friendly outcome and an up-to-date list.
- **SC-006**: Players already on one or more teams observe no change in their "My team" navigation behavior.

## Assumptions

- **Teamless = zero memberships.** "Not on a team yet" means the player has no team memberships; the empty state targets exactly this population.
- **Invitations surface on the teamless empty state only (v1).** Listing pending invitations is presented on the zero-team "My team" home. Surfacing the same list to players who are already on one or more teams (e.g. on the multi-team chooser) is out of scope for this feature and can be added later.
- **Only targeted, usable invitations are listed.** Shared/link invitations (not addressed to a specific player) and expired/revoked/consumed invitations are not shown as actionable pending invites.
- **Existing flows are reused, not rebuilt.** Team discovery, team creation, and the accept/decline semantics already exist; this feature aggregates and surfaces them plus adds one new "list my pending invitations" read capability. The invitation creation and email flows are unchanged.
- **The one new capability is a read.** The only genuinely new backend behavior is listing the invitations addressed to the current player; accept and decline reuse existing behavior/outcomes.
- **Most recent first, bounded.** Absent a stated preference, pending invitations are shown newest-first and bounded via the project's standard pagination.
- **Design system applies.** Visual treatment (headings, cards, empty/loading/error states, buttons) follows DESIGN.md; this spec does not prescribe visuals.
