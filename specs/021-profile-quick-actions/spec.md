# Feature Specification: Profile Quick-Actions (Message & Invite to a Team)

**Feature Branch**: `021-profile-quick-actions`

**Created**: 2026-07-21

**Status**: Draft

**Input**: User description: "On a person's profile we should display shorthand actions to start a message with them or to invite them to a team."

## Clarifications

### Session 2026-07-21

- Q: If a player opted out of player-search visibility, should the **Message** action still work from their public profile? → A: Yes — Message works for any player with a public profile regardless of their player-search opt-out; if the existing search excludes opted-out players, a minimal handle→identity resolution is added that exposes no account id publicly. **Update (post-020):** the player-search opt-out was removed entirely in feature 020, so this is now moot — every player is listed and reachable, and messaging is unconditionally universal with no opt-out special-casing.
- Q: When the viewer administers team(s) but none are eligible for the player (already a member/invited to all), should **Invite to a team** hide entirely or show disabled with a reason? → A: Show it disabled with a brief reason when the viewer administers ≥1 team but none are eligible; hide it entirely only when the viewer administers no team at all.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Message a player from their profile (Priority: P1)

A signed-in member is looking at another player's profile page and wants to reach
out. Without leaving the profile, they use a **Message** action that either opens
the conversation they already share with that player or starts a fresh direct
message with them, landing them in the chat ready to type.

**Why this priority**: This is the smaller, universally-applicable half of the
feature — any signed-in member can message any player (open direct-message reach
is an established product decision), so it delivers value to every user on every
profile and stands alone as a viable MVP.

**Independent Test**: Sign in, open another player's profile, click **Message**,
and confirm you arrive in a direct-message conversation with that player (the
existing one if you already have it, otherwise a newly created one). Fully
testable without the invite half of the feature.

**Acceptance Scenarios**:

1. **Given** I am signed in and viewing a player's profile with whom I have no
   prior conversation, **When** I choose **Message**, **Then** a direct message
   with that player is started and I am taken into that conversation.
2. **Given** I am signed in and already have a direct message with the player,
   **When** I choose **Message**, **Then** I am taken into the existing
   conversation rather than a duplicate being created.
3. **Given** I have blocked, or been blocked by, the player, **When** I choose
   **Message**, **Then** I am shown a clear, non-technical message explaining the
   action can't be completed, and no conversation is opened.
4. **Given** I am viewing my **own** profile, **When** the page renders, **Then**
   the **Message** action is not shown.
5. **Given** I am **not** signed in, **When** I view a public profile, **Then** the
   **Message** action is not shown.

---

### User Story 2 - Invite a player to a team I administer (Priority: P2)

A signed-in member who administers one or more teams is viewing a player's profile
and wants to recruit them. They use an **Invite to a team** action. If they
administer a single team, the invite targets that team directly. If they
administer several, they choose which team from a short list. Teams the player is
already a member of, or already has a pending invite to, are not offered. Sending
the invite triggers the team's existing targeted-invitation flow (which emails the
player), and the action reflects that the invite was sent.

**Why this priority**: Depends on the viewer being a team admin, so it reaches a
narrower audience than messaging and builds on more moving parts (team
membership, invite state). It delivers clear recruiting value but is secondary to
the always-available Message action.

**Independent Test**: Sign in as a member who administers at least one team, open
the profile of a player who is not on that team, choose **Invite to a team**,
select the team if prompted, and confirm the player receives a targeted team
invitation and the action shows a sent/confirmed state.

**Acceptance Scenarios**:

1. **Given** I administer exactly one team and the player is not on it and has no
   pending invite, **When** I choose **Invite to a team**, **Then** a targeted
   invitation to that team is created for the player and the action confirms it
   was sent.
2. **Given** I administer more than one eligible team, **When** I choose **Invite
   to a team**, **Then** I am prompted to pick which team, and only eligible teams
   (player not already a member and no pending invite) are offered.
3. **Given** I administer one or more teams but the player is already a member of
   (or has a pending invite to) every team I administer, **When** the page
   renders, **Then** the **Invite to a team** action is shown in a disabled state
   with a brief explanation (no eligible team) and cannot send an invite.
4. **Given** I do **not** administer any team, **When** I view a player's profile,
   **Then** the **Invite to a team** action is not shown.
5. **Given** I am viewing my **own** profile, or I am not signed in, **When** the
   page renders, **Then** the **Invite to a team** action is not shown.

---

### Edge Cases

- **Own profile / anonymous viewer**: neither action appears on the viewer's own
  profile, nor for anonymous (signed-out) visitors to a public profile.
- **Player not reachable for messaging**: if the player cannot be resolved for a
  direct message (e.g. account no longer exists, or a block is in force), the
  Message action fails gracefully with a friendly message and opens no
  conversation.
- **Rate limiting**: if messaging or inviting is refused because the viewer has hit
  a rate limit, the action surfaces a friendly "try again shortly" style message
  rather than a raw error.
- **Invite already exists / already a member**: the invite action must not create a
  duplicate invitation or invite an existing member; such teams are filtered out of
  the picker.
- **Concurrent state change**: if the player joins a team (or an invite is created)
  between the page loading and the viewer acting, the server remains the authority
  — a now-ineligible invite is rejected and the viewer sees a clear message.
- **Universal message reach** *(the player-search opt-out was removed in feature 020)*:
  there is no longer any per-player search opt-out, so every player with a public
  profile is both listed and messageable. Messaging needs no special handling for
  formerly-hidden players — the identity-resolution search returns all players.
- **Loading / in-flight**: while an action is resolving, it is disabled and shows a
  pending state so it can't be double-submitted.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The profile page MUST display a **Message** shorthand action and an
  **Invite to a team** shorthand action, subject to the visibility rules below.
- **FR-002**: Both actions MUST be shown ONLY to a signed-in viewer, and MUST NOT
  be shown on the viewer's own profile or to anonymous (signed-out) visitors.
- **FR-003**: The **Message** action MUST, for a target player, open the direct
  conversation the viewer already shares with that player if one exists, and
  otherwise start a new direct message with that player, then place the viewer in
  that conversation.
- **FR-003a**: The **Message** action MUST be available for ANY player who has a
  public profile (universal direct-message reach). It MUST resolve the target's
  identity via existing authenticated means keyed on the public handle, and MUST NOT
  expose the player's account identifier on any public (unauthenticated) response.
  *(The former player-search opt-out this requirement originally accommodated was
  removed in feature 020, so no opt-out special-casing is needed — the resolution
  search returns every player.)*
- **FR-004**: The **Message** action MUST handle refusal cases (blocked in either
  direction, target not resolvable, rate-limited) by showing a clear, non-technical
  message and opening no conversation.
- **FR-005**: The **Invite to a team** action MUST be shown whenever the viewer
  administers at least one team (of any eligibility), and MUST be hidden only when
  the viewer administers no team at all (as well as on the viewer's own profile and
  for anonymous visitors). When the action is shown but NO administered team is
  eligible (the player is already a member of, or has a pending invite to, every
  team the viewer administers), it MUST be presented in a disabled state with a
  brief explanation and MUST NOT send an invite.
- **FR-006**: When the viewer administers exactly one eligible team, the invite
  action MUST target that team without an extra selection step; when the viewer
  administers more than one eligible team, the action MUST present a picker listing
  only eligible teams.
- **FR-007**: Sending an invite MUST create a targeted team invitation for the
  player via the team's existing invitation mechanism, and the action MUST reflect
  a sent/confirmed state afterward.
- **FR-008**: The system MUST NOT create a duplicate invitation for a player who is
  already a member of, or already has a pending invitation to, the selected team;
  such teams MUST be excluded from selection.
- **FR-009**: Determining the target player's identity for these actions MUST NOT
  require exposing the player's raw account identifier on the public profile data —
  identity MUST be resolved through existing authenticated means keyed on the
  player's public handle, preserving the public profile's existing privacy
  guarantee (no account id, no email, no account status).
- **FR-010**: All authorization for messaging and inviting MUST remain enforced by
  the server; the profile's visibility rules are a UX convenience only and MUST NOT
  be the security boundary (a viewer who is not actually a team admin, or who is
  blocked, is refused by the server regardless of what the profile showed).
- **FR-011**: Both actions MUST present clear loading, disabled, empty (no eligible
  team), success, and error states consistent with the app's design system.
- **FR-012**: The actions MUST be reachable and operable on both mobile and desktop
  layouts of the profile page.

### Key Entities *(include if feature involves data)*

- **Player (profile subject)**: the person whose profile is being viewed;
  identified publicly by an immutable handle. The target of both actions.
- **Viewer**: the signed-in member looking at the profile; carries their own
  identity and their set of team memberships/roles, which determine whether the
  actions appear.
- **Direct conversation**: a one-to-one message thread between the viewer and the
  player; may already exist (open it) or be created on demand.
- **Team**: a group the viewer may administer; the target of an invitation. Its
  relationship to the player (member / pending invite / invitable) governs
  eligibility.
- **Targeted team invitation**: an emailed invitation created for the player to a
  specific team via the existing team-invitation flow; admin-only.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From another player's profile, a signed-in viewer can reach a ready
  direct-message conversation with that player in a single action (one click/tap),
  with no intermediate search step.
- **SC-002**: 100% of the time, choosing **Message** for a player the viewer
  already messages opens the existing conversation rather than creating a second
  one.
- **SC-003**: The **Invite to a team** action never offers a team for which the
  player is already a member or already has a pending invite (0 invalid invites
  offered).
- **SC-004**: Neither action ever appears on the viewer's own profile or for
  anonymous visitors (0 occurrences in testing across mobile and desktop).
- **SC-005**: The public profile continues to expose no account identifier or
  contact detail — verifiable by inspecting the public profile response, which is
  unchanged by this feature.
- **SC-006**: Every refusal path (blocked, rate-limited, no eligible team, target
  unresolvable) results in a clear, non-technical message and no partial action.

## Assumptions

- **Reused surfaces**: The feature is delivered on the existing profile page(s);
  no new profile view or route is introduced. The only "other person" profile
  today is the public profile page, so the actions are added there and gated to
  signed-in, non-self viewers.
- **Reused flows, no new public data**: Messaging reuses the existing direct-message
  start/open behavior and its open-reach product decision; inviting reuses the
  existing admin-only targeted-invitation flow. Identity is resolved from the
  player's public handle via existing authenticated capabilities — the public
  profile data contract is intentionally left unchanged (no account id added).
- **Admin scope**: "Invite to a team" is admin-only by decision; ordinary members
  cannot invite. The viewer's administered teams are derived from their own
  membership data.
- **No backend contract change needed**: The implementation reuses existing
  endpoints and adds no API, schema, or permission changes. Identity is resolved
  from the public handle via the existing authenticated chat people-search
  (messaging) and team user-search (inviting), both of which return all players.
  The open risk in earlier drafts — whether search excluded search-opted-out
  players — is void: feature 020 removed the player-search opt-out entirely, so
  every player is resolvable. Messaging is frontend-only. Any resolution path still
  MUST NOT expose the player's account identifier on a public response, preserving
  the public profile's privacy guarantee.
- **Design system**: Button styling, states, and the team-picker presentation
  follow DESIGN.md; this feature introduces no new visual style.
- **Out of scope**: Adding an account identifier to the public profile; non-admin
  team invites; changing direct-message reach; group-message creation from a
  profile; any messaging/inviting from anonymous context.
