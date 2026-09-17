# Feature Specification: Team Creation Wizard

**Feature Branch**: `052-team-creation-wizard`

**Created**: 2026-09-17

**Status**: Draft

**Input**: User description: "Creating a team should follow the multi step wizard that we use during onboarding and Event creation. We should optionally allow to upload the logo and maybe move the description to that screen so it matches the user onboarding. We should then have an optional step to search for and invite users" (GitHub #320)

## Context

Every other "create" flow in the product asks one calm question per screen. Onboarding (004) walks
a new player through name, city, pompfen, team and photo with round-knob progress. Event creation
walks an organiser through type, when, where, who, fee and a review before Publish. Team creation
is the last one that has not been reshaped: name, team handle, type and city all sit on a single
screen, and the flow ends the moment the team is created.

Two things a brand-new team wants immediately are reachable only *after* that flow ends, from a
settings screen the creator has to go looking for:

| Want | Exists since | Reachable today from |
|---|---|---|
| A logo instead of a letter tile | feature 051 | team settings, after the fact |
| Members | targeted invites + user search | team settings, after the fact |

The moment someone has just named a team is the moment they know what it looks like and who they
want in it. This feature puts both of those moments inside the flow, as steps that can be skipped.

**Nothing new is being built on the server.** Slug availability, team creation, logo upload and
removal, targeted invites and the invitable-user search are all shipped capabilities. This feature
is a re-shaping of one screen into five, and the placement of two existing capabilities inside it.

## Clarifications

### Session 2026-09-17

- Q: The request says to "move the description" to the logo screen, the way onboarding pairs the
  photo and the bio. Which description? → A: **There is none, and one is not being added here.**
  A team has no description anywhere in the product — not on the entity, not in any contract, not
  in team settings, not on the team page. "Moving" it is therefore not available: it would mean
  introducing a new stored field and every surface that reads it, which is an additive feature in
  its own right. **Out of scope**, filed as GH #321.
- Q: Where in the wizard does the team actually get created? → A: **At the review step, before the
  logo and invite steps.** Both of those act on capabilities that are addressed by the team's
  handle and permitted only to its admins, so neither can run against a team that does not exist
  yet. Creating at review is what lets the invite step search real people and report their real
  relationship to the team, rather than collecting intentions to be replayed later.
- Q: What happens to someone who leaves after the team is created but before the optional steps? →
  A: **They have a complete, usable team.** The logo and invite steps are additions to a finished
  thing, not the remainder of an unfinished one. This is the deliberate difference from the event
  wizard, where leaving before Publish means nothing was created.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create a team, one question at a time (Priority: P1)

A player who wants to start a team opens the create screen and is asked for one thing at a time:
first what the team is called and what its address should be, then whether it is a city team (and
which city) or a Mixteam. A progress indicator shows how far along they are. Before anything is
created they see a review of every answer, and can go back to change any of them. Pressing the
create action creates the team with them as its first admin.

**Why this priority**: This is the feature. It replaces the single dense form with the flow the
rest of the product uses, and on its own — with both optional steps dropped — it is a complete,
shippable improvement that loses nothing the current screen does.

**Independent Test**: Walk the flow from the create screen to a created team without ever touching
the logo or invite steps, and confirm the resulting team is identical in every respect to one
created through today's form.

**Acceptance Scenarios**:

1. **Given** a signed-in player on the create screen, **When** they enter a name and an available
   team address and continue, **Then** they are asked for the team type, and the progress
   indicator advances.
2. **Given** a player on the type step, **When** they choose a city team, **Then** they are asked
   for a city and cannot continue until one is selected.
3. **Given** a player on the type step, **When** they choose a Mixteam, **Then** no city is asked
   for and they can continue immediately.
4. **Given** a player on any step after the first, **When** they go back, **Then** the previous
   step is shown with every answer they had entered still in place.
5. **Given** a player on the review step, **When** they look at it, **Then** it shows the name, the
   address, the type and (for a city team) the city, and offers a way back to change each.
6. **Given** a player on the review step, **When** they press the create action, **Then** a team is
   created with them as its first admin.
7. **Given** a team address that was available when checked but is taken by the time the team is
   created, **When** creation is refused, **Then** the player is returned to the step carrying the
   address, told why, and can pick another — their other answers are not lost.

---

### User Story 2 - Give the team a logo while creating it (Priority: P2)

Immediately after the team is created, the player is offered the chance to upload a logo. They can
pick an image and see it previewed as it will appear, or skip the step entirely. Skipping leaves
the team with the letter placeholder it would have had anyway.

**Why this priority**: A logo is the most visible thing about a team and the cheapest to supply at
the moment of creation. It is second rather than first because a team without one is not broken —
it simply looks like every team looked before feature 051.

**Independent Test**: Create a team, upload a logo on the step offered, and confirm the logo
appears on the team page — then create a second team, skip the step, and confirm that team shows
the letter placeholder and is otherwise identical.

**Acceptance Scenarios**:

1. **Given** a team has just been created, **When** the logo step is shown, **Then** it offers both
   an upload action and a way to skip.
2. **Given** a player on the logo step, **When** they choose an image, **Then** it is applied and
   shown as the team's logo, and they may choose a different one before moving on.
3. **Given** a player who has uploaded a logo, **When** they continue, **Then** the logo is the
   team's logo everywhere a team is pictured.
4. **Given** a player on the logo step, **When** they skip, **Then** the team keeps the letter
   placeholder and the flow continues.
5. **Given** an upload that is refused (wrong kind of file, too large, or a failure in transit),
   **When** the refusal is reported, **Then** the step says so, the team is unaffected, and the
   player may try another image or skip.

---

### User Story 3 - Invite people while creating the team (Priority: P3)

After the logo step the player is offered a search for people to invite. Typing a name or handle
lists players, each showing whether they can be invited, have already been invited, or are already
in the team. Inviting one sends them the same invitation team settings would send. Several people
can be invited without leaving the step, and the step can be skipped.

**Why this priority**: The most valuable of the three for a team that intends to have members, but
the one that depends on the other two being in place, and the one a solo creator will most often
skip. Last also means it can be cut without disturbing anything before it.

**Independent Test**: Create a team, invite two players from the step, and confirm both appear as
pending invitations on the team's invitations screen and received the same invitation they would
have received from there.

**Acceptance Scenarios**:

1. **Given** a player on the invite step, **When** they type a search term, **Then** matching
   players are listed with their relationship to the team shown.
2. **Given** a listed player who can be invited, **When** the creator invites them, **Then** the
   invitation is sent and that player is shown as invited without the list being re-searched.
3. **Given** a player who has just been invited, **When** the creator looks at the list, **Then**
   there is no way to invite them a second time.
4. **Given** a creator who has invited several people, **When** they finish, **Then** they land on
   the team's page and every invitation they sent is pending on the team.
5. **Given** a creator on the invite step, **When** they skip, **Then** they land on the team's
   page with no invitations sent.
6. **Given** an invitation that fails to send, **When** the failure is reported, **Then** the step
   says so and the invitations that did succeed are unaffected.

---

### Edge Cases

- **The team address is immutable once created.** After the create action succeeds there is no way
  back to the steps that set the name, address, type or city — those are settings changes now, not
  wizard answers. The flow must make the create action read as the commitment it is, and must not
  offer a back action from the optional steps that would suggest otherwise.
- **Leaving after creation, before the optional steps.** Closing the tab, pressing back, or
  navigating away at the logo or invite step leaves a complete team. Nothing is rolled back and
  nothing is left half-done; both skipped steps remain available from team settings.
- **Creation fails for a reason that is not the address** (network, server error). The player stays
  on the review step with every answer intact and can retry.
- **The creator finding themselves in the invite search.** The creator is already the team's only
  member, so they are reported as a member and cannot be invited.
- **A search that matches nobody.** The step says so plainly rather than showing an empty area.
- **Slow or failing availability checks on the address step.** The existing behaviour is preserved:
  progress is held while a check is in flight, and a check that fails says so rather than leaving a
  dead action.
- **A second team.** Nothing about the flow assumes this is the player's first team.

## Requirements *(mandatory)*

### Functional Requirements

#### The flow

- **FR-001**: Team creation MUST be presented as a sequence of steps, each asking for one group of
  related answers, in the same style as the onboarding and event-creation flows.
- **FR-002**: The flow MUST show progress through the steps.
- **FR-003**: The steps before creation MUST be: (1) the team's name and address, (2) the team's
  type and, for a city team, its city, (3) a review of those answers.
- **FR-004**: The player MUST be able to move back to any earlier pre-creation step, and every
  answer already given MUST still be there.
- **FR-005**: A step MUST NOT allow progress until the answers it asks for are valid — the name and
  address on step 1, a city on step 2 when the type is a city team.
- **FR-006**: The address MUST continue to be checked for availability as it is typed, and progress
  MUST continue to be held until a check has come back positive.
- **FR-007**: The review step MUST show every answer collected and offer a route back to change each
  of them.
- **FR-008**: The team MUST be created from the review step, and MUST be created with exactly the
  same information the single-screen form sends today.
- **FR-009**: If creation is refused because the address is no longer available, the player MUST be
  returned to the step carrying the address with the reason shown, retaining all other answers.
- **FR-010**: If creation fails for any other reason, the player MUST remain on the review step with
  all answers retained and MUST be able to retry.

#### After creation

- **FR-011**: Once the team exists, the flow MUST NOT offer a way back to the pre-creation steps.
- **FR-012**: The steps after creation MUST each be skippable, individually, without affecting the
  team or the other step.
- **FR-013**: Leaving the flow at any point after creation MUST leave a complete and usable team.
- **FR-014**: The flow MUST end on the new team's page.

#### The logo step

- **FR-015**: After creation the player MUST be offered the chance to upload a team logo.
- **FR-016**: A chosen image MUST be shown as the team's logo on the step, so the player sees the
  result before moving on, and MUST be replaceable by choosing another without leaving the step.
  *(Amended during planning: an earlier wording required a preview shown **before** the image was
  applied. Team settings — the surface that has offered logo upload since feature 051 — applies a
  chosen image immediately and shows the applied result. Two different interactions for the same
  act, on two screens, is worse than the one this replaces, so the wizard follows 051. The player
  still sees the image before committing to anything further, and replacing it costs one press.)*
- **FR-017**: An uploaded logo MUST become the team's logo on every surface that pictures a team,
  identically to a logo uploaded from team settings.
- **FR-018**: A refused or failed upload MUST be reported on the step, MUST leave the team
  unchanged, and MUST allow another attempt or a skip.
- **FR-019**: Skipping MUST leave the team with the letter placeholder.

#### The invite step

- **FR-020**: After the logo step the player MUST be offered a search for people to invite.
- **FR-021**: Search results MUST show, for each person, whether they can be invited, have already
  been invited, or are already a member.
- **FR-022**: Inviting a person MUST send the same invitation the team's invitations screen sends.
- **FR-023**: Several people MUST be invitable without leaving the step.
- **FR-024**: A person who has been invited MUST NOT be invitable again from the step.
- **FR-025**: A search matching nobody MUST say so.
- **FR-026**: A failed invitation MUST be reported without affecting invitations that succeeded.

#### Boundaries

- **FR-027**: This feature MUST NOT change what a team is: no information is collected that a team
  does not already hold, and the created team is indistinguishable from one created today.
- **FR-028**: This feature MUST NOT add, remove or change any server capability. Every action it
  takes is one the product already offers.
- **FR-029**: The logo and invite capabilities MUST remain available from team settings exactly as
  they are today; the wizard is an additional route to them, never a replacement.
- **FR-030**: A team description MUST NOT be introduced by this feature (see GH #321).

### Key Entities

No new entities. The feature reads and writes only what already exists:

- **Team** — created by the flow, exactly as the current form creates it.
- **Team logo** — optionally set by the flow, exactly as team settings sets it.
- **Team invitation** — optionally created by the flow, exactly as team settings creates it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A player can create a team through the flow without ever seeing more than one group
  of questions at a time.
- **SC-002**: A team created through the flow with both optional steps skipped is identical in
  every stored and displayed respect to a team created through the screen this replaces.
- **SC-003**: A player can go from opening the create screen to a created team in no more presses
  than the current screen requires, plus one per step boundary.
- **SC-004**: A player who uploads a logo during creation sees it on the team page they land on,
  with no further action.
- **SC-005**: A player who invites people during creation finds exactly those people pending on the
  team's invitations screen, with no further action.
- **SC-006**: Every failure the flow can encounter — an address taken, a refused image, an
  invitation that could not be sent — leaves the player on a step that explains what happened and
  offers a way forward. None of them ends the flow or loses an answer.
- **SC-007**: Abandoning the flow after creation and at any later point yields a team that behaves
  exactly as a team whose creator never opened the optional steps.
- **SC-008**: The flow reads as one of the product's wizards: a stranger shown the onboarding flow,
  the event flow and this one cannot tell which one was built last.

## Assumptions

- **The creator is the team's first admin**, as today. Nothing in the flow changes who may create a
  team or what creating one confers.
- **The optional steps are offered in the order logo, then invite.** The logo is the cheaper
  decision and the one that changes what the invite step shows, since an invitation is to a team
  that now has a face.
- **Both optional steps are offered to every creator**, not conditionally hidden. A skip is one
  press and an absent step cannot be discovered.
- **The address remains immutable after creation.** This is existing behaviour and the reason the
  pre-creation steps close behind the create action.
- **The existing single-screen form is replaced, not kept alongside.** Two routes to the same
  creation would be two things to keep in step.
- **Copy for the new steps follows the existing wizards' voice** and is supplied in every language
  the product ships, as any new copy must be.

## Out of Scope

- **A team description** — GH #321. Teams have no description field today; introducing one is
  additive work with its own stored field, its own editing surface and its own display surfaces,
  not a field this flow can relocate. This is the one part of the original request that is not
  being built, by owner decision.
- **Draft persistence across leaving the page.** The training and event wizards keep unfinished
  answers so that leaving and returning does not blank them (feature 045). This flow deliberately
  does not, in this feature: the exposure is two short steps rather than twenty-one answers, and
  adding a third kind of draft is its own change. Worth a follow-up once the flow exists.
- **Changing the invite capability itself** — the shared invite link, invitation expiry, pending
  invitation management and the emails all stay exactly as they are. The flow uses the targeted
  invite as it stands.
- **Changing the logo capability itself** — cropping, removal, size and format rules all stay as
  feature 051 defined them.
- **Anything reachable from team settings** stays reachable from team settings, unchanged.
