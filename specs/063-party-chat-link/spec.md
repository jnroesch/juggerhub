# Feature Specification: The Party Page Leads Into the Party Chat

**Feature Branch**: `063-party-chat-link`

**Created**: 2026-09-29

**Status**: Draft

**Input**: User description: "GH #382 — The party page has no way into the party chat. Crew members (party members whose status is In — admins and marketplace guests included) get a one-tap "Party chat" action on the party page (/parties/:id) that opens the party's auto-created chat, creating it first if nobody in the crew has opened Chat yet. Mirror feature 060 (team chat link, GH #362): a resolver that answers with the conversation only, the inbox's own creation step first, the chat's own access rule alone deciding, one generic not-found for everyone else. Button resolves on press (no unread count, no extra request on page load). Owner decisions (2026-09-29): placement in the viewer's own card at the top of the party page; team members not in the crew keep seeing the page as 016 designed it, without the button; disbanded parties have no page. Copy in en/de/es. DESIGN.md governs."

## Context

Every party has a chat of its own (feature 019): a conversation whose members are exactly the
party's crew, the people who said **I'm in**, marketplace guests included. It is not created
when the party forms. It comes into being the first time someone in the crew opens Chat, and
once it exists the only way to reach it is the Chat inbox.

The party page, the one place in the product that is about the party, has no way in. Feature
060 added exactly this route for teams (GH #362) and deliberately left parties for later
(060 spec, *Out of scope*).

**Who sees the party page, and why that stays as it is.** Forming a party asks the whole team
whether they are in (016 FR-006), and the party page is where a team member answers: the
party-request alert, its device notification and the event page's *View party* all open it.
So the page is shown to every member of the party's team, including those who have not
answered or have declined, and to the crew's marketplace guests. Only the crew is in the
party's chat. The owner considered restricting the page to the crew and decided to keep 016's
design (Clarifications); this feature only decides who sees the new action.

**One correction to the issue, found by reading the product**: the issue asks whether a
*disbanded* party's button should disappear or open the archived chat. Disbanding does not
leave a party behind in a closed state: it removes the party entirely, after preserving its
chat as a read-only record in each crew member's inbox. A disbanded party's page shows "not
found", so there is no page and no button, and the preserved chat stays reachable from the
inbox exactly as today.

**Out of scope, deliberately**: an unread count on the action (060's owner decision, carried
over); any change to who may see the party page or its roster; any change to what the party
chat is or who is in it; a link from the party **news** page or from the team page's party
card; and the page's other, existing actions, which keep their current behaviour and wording.

## Clarifications

### Session 2026-09-29

- Q: Where does **Party chat** sit on the party page? The page is one column, and its only
  admin card ("Party tools") is at the very bottom, after the roster and the news. → A: **In the
  viewer's own card at the top of the page.** A crew member who is not a party admin finds it
  beside **Leave party** in the card that tells them they are in the crew. A party admin finds it
  beside **Apply to event** (or **Withdraw**) in the card that shows the party's readiness, as a
  secondary action, so Apply stays the page's one coral action. On a phone it is visible without
  scrolling past the roster and the news.
- Q: Team members who are not in the crew (no answer yet, or declined) can see the party page
  but are not members of its chat. Hide the action, or explain why it is missing? The owner
  first asked why they can see the page at all; the answer is 016's design (the page is where the
  team-wide request is answered, and three links lead there). → A: **Keep 016's design; the
  action is simply not shown to them.** No hint line is added. Their card keeps asking **I'm in /
  Can't make it** as today.
- Q: What happens for a disbanded party? → A: **Nothing to decide.** Disbanding removes the party;
  its page shows "not found" and carries no action. The preserved chat stays in each former crew
  member's inbox (019).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A crew member opens the party chat from the party page (Priority: P1)

Lena is in Rheinfeuer's crew for the Hamburg Cup. She opens the party page to check who else is
in, and wants to ask whether anyone can take a passenger from Cologne. In the card at the top
that tells her she is in the crew, she presses **Party chat** and lands in the party's chat,
the same conversation her inbox lists for this party, ready to type.

If nobody in the crew has opened Chat since the party formed, the chat does not exist yet when
she presses. It is created then, the same way it would have been if she had opened Chat, and
she lands in it.

**Why this priority**: This is the issue. The crew, the people the party chat exists for, have
no route to it from the page about their party.

**Independent Test**: As a crew member, open the party page, press Party chat, and confirm the
open conversation is the party's own chat. Repeat for a party whose chat has never been opened,
and as a marketplace guest in the crew.

**Acceptance Scenarios**:

1. **Given** a crew member of a party whose chat exists, **When** they press Party chat on the
   party page, **Then** the party's chat opens, and it is the same conversation the inbox lists
   for the party.
2. **Given** a crew member of a party whose chat has never been opened by anyone, **When** they
   press Party chat, **Then** the party's chat is created and opens, and no message,
   notification or alert is produced for anyone.
3. **Given** a marketplace guest seated in the crew, **When** they press Party chat, **Then** the
   party's chat opens, as it does for a crew member from the team.
4. **Given** a crew member presses Party chat, **When** the chat is still being looked up,
   **Then** the action shows that it is working and cannot be pressed a second time.
5. **Given** two crew members of a party with no chat yet press Party chat at the same moment,
   **Then** both land in the same single party chat.

---

### User Story 2 - A party admin opens the party chat from the readiness card (Priority: P1)

Tom formed the party and is watching the readiness summary before applying to the event. He
wants to tell the crew that the bus leaves at seven. Beside **Apply to event** he presses
**Party chat** and lands in the party's chat. Apply is still the one coral action on the page;
Party chat sits beside it as a secondary action.

**Why this priority**: Party admins run the crew and are the most frequent writers in its chat.
The card that crew members see is not shown to admins, so without this story an admin would have
no route at all.

**Independent Test**: As a party admin (the creator, and separately an accepted co-admin), open
the party page before and after applying to the event, press Party chat, and confirm the party's
own chat opens.

**Acceptance Scenarios**:

1. **Given** a party admin of a party that has not applied, **When** they view the party page,
   **Then** Party chat appears beside Apply to event, and Apply remains the only coral action.
2. **Given** a party admin of a party that has applied, **When** they view the party page,
   **Then** Party chat appears beside Withdraw.
3. **Given** a party admin, **When** they press Party chat, **Then** the party's own chat opens.

---

### User Story 3 - Team members outside the crew see the page as before (Priority: P2)

Jonas is on Rheinfeuer but has not answered the Hamburg Cup request. He follows the alert to the
party page, which asks him **I'm in / Can't make it** exactly as today. There is no Party chat
action: he is not in the party's chat until he says he is in. Once he presses **I'm in**, the
page shows him the crew card, and Party chat is there.

**Why this priority**: The owner's decision about who sees the action. It adds no capability, but
it keeps the page from offering a chat the viewer cannot open.

**Independent Test**: View the party page as a team member with no answer, as one who declined,
and as one who is in; confirm Party chat appears only for the last. Then press I'm in as the first
and confirm the action appears.

**Acceptance Scenarios**:

1. **Given** a team member who has not answered the request, **When** they view the party page,
   **Then** no Party chat action is shown, and the page is otherwise unchanged.
2. **Given** a team member who declined, **When** they view the party page, **Then** no Party
   chat action is shown, and the page is otherwise unchanged.
3. **Given** a team member who has not answered, **When** they press I'm in, **Then** the page
   shows them as in the crew, with Party chat available.
4. **Given** a crew member, **When** they leave the party, **Then** the page no longer shows
   Party chat.

---

### Edge Cases

- **Left or removed from the crew since the page loaded**: pressing Party chat does not open the
  chat. The player is told they are no longer in the crew, and the page refreshes to show the
  party as they may now see it (the request card for a team member, or "not found" for someone
  who is no longer on the team).
- **The party was disbanded since the page loaded**: pressing Party chat behaves as above; the
  refreshed page shows "not found". The preserved chat stays in the inbox.
- **The party chat is hidden** (archived, feature 048) **or muted by this player**: Party chat
  still opens it. Pressing the action changes neither setting.
- **The lookup fails** (network error, server error): the player stays on the party page, sees a
  short message that the chat could not be opened, and can press again. They are never taken to a
  broken or empty chat page.
- **A player asks for a party's chat directly, without the page** (a team member outside the crew,
  someone not on the team, a signed-out caller, or a party that does not exist): the answer is the
  same "not found" in every case. It does not reveal whether the party exists, whether it has a
  chat, or who is in it.
- **Asking has side effects only for the party's own crew**: a request from anyone outside the
  crew creates nothing for the party they asked about.
- **The event has ended or was cancelled**: the party and its chat still exist, so a crew member
  still sees and can use Party chat.
- **Blocks**: a block between two players does not affect the party chat or this action. Blocks
  apply only to direct messages (019 FR-032).

## Requirements *(mandatory)*

### Functional Requirements

**Opening the party chat**

- **FR-001**: Every current member of a party's crew (every player who is **in**: party admins,
  crew members from the team, and marketplace guests) MUST see a **Party chat** action on the
  party page. Team members who have not answered or have declined MUST NOT see it.
- **FR-002**: Pressing Party chat MUST open **the party's own chat**, the conversation whose
  members are the party's crew and which the inbox lists for the party. It MUST never open
  another party's chat or any other conversation.
- **FR-003**: If the party's chat does not exist yet, pressing Party chat MUST create it the same
  way opening Chat does. The only visible difference MUST be that the chat now exists for the
  crew, as it would after anyone in the crew had opened Chat. No message, alert, notification,
  email or device notification may result.
- **FR-004**: Whether a player may open the party chat MUST be decided by the server, by the
  **same rule that decides who may read the chat**. There must be no second, separately written
  answer to "is this player in the crew?" for this feature.
- **FR-005**: A player who is not in the party's crew MUST receive the same "not found" answer as
  for a party that does not exist. The answer MUST NOT distinguish these cases.
- **FR-006**: The answer to a crew member MUST identify the conversation and nothing more: no
  messages, member list, name or unread count.
- **FR-007**: A request from a player who is not in the crew MUST NOT create a chat for that
  party.
- **FR-008**: The action MUST NOT show an unread count. Loading the party page MUST NOT make any
  request it does not make today. The chat is looked up only when Party chat is pressed.
- **FR-009**: While the chat is being looked up, the action MUST show that it is working and MUST
  NOT accept a second press.
- **FR-010**: If the lookup fails, the player MUST stay on the party page and see a short message,
  in their language, that the chat could not be opened. The action MUST be usable again. The
  message MUST be chosen by the kind of failure, never taken from the server's wording.
- **FR-011**: If the lookup answers "not found" (the player has left or been removed from the
  crew, or the party was disbanded, since the page loaded), the player MUST be told they are no
  longer in the crew, and the page MUST refresh to show the party as they may now see it.
- **FR-012**: Opening the party chat from the party page MUST NOT change the player's own hide or
  mute setting for that chat.
- **FR-013**: There MUST remain at most one chat per party, including when two crew members cause
  it to be created at the same moment.

**Placement**

- **FR-014**: For a crew member who is not a party admin, Party chat MUST sit in the card that
  tells them they are in the crew, beside **Leave party**.
- **FR-015**: For a party admin, Party chat MUST sit in the card that shows the party's readiness,
  beside **Apply to event** before the party has applied and beside **Withdraw** after.
- **FR-016**: Party chat MUST NOT be the page's coral primary action. It is a secondary action
  (DESIGN.md: one coral CTA per view); Apply to event stays the admin's one coral action.
- **FR-017**: For every viewer, everything else on the party page (who may see it, the roster, the
  news, the request card, the admin's tools) MUST stay exactly as it is today.
- **FR-018**: Every new piece of interface text MUST exist in English, German and Spanish.

### Key Entities

- **Party chat**: an existing conversation, one per party, whose members are the party's current
  crew, marketplace guests included (feature 019). Unchanged by this feature.
- **Crew**: the party's members who are **in**. Party admins are always in the crew.

No new data is stored.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From the party page, a crew member (party admin, team crew member or marketplace
  guest) reaches the party's chat with **one press** of Party chat, whether or not the chat has
  been opened before.
- **SC-002**: In **zero** cases does Party chat open a conversation other than the party's own
  chat.
- **SC-003**: A team member outside the crew, a player not on the team, a signed-out caller and a
  request about a non-existent party receive **identical** "not found" answers, and none of those
  requests creates anything.
- **SC-004**: The party page makes **the same number of requests** while loading as before this
  feature, for every viewer.
- **SC-005**: A team member who has not answered or has declined sees **no** Party chat action,
  and otherwise the same page as before this feature.
- **SC-006**: In German at 375px, the card holding Party chat shows each of its actions in full,
  with no truncated label and no horizontal overflow, for both the crew card and the admin's
  readiness card. The same holds on desktop.

## Assumptions

- The party chat's membership and lifecycle are unchanged (feature 019). This feature adds a route
  to it.
- Creating a party's chat when a crew member presses the action is acceptable, because opening
  Chat already does exactly that for every party the player is in.
- Opening a hidden (archived) party chat from the party page is intended. It is the player's own
  conversation, and feature 048 made hiding reversible from the chat's details.
- The party page does not update live. A player removed from the crew while the page is open
  learns it when they press the action (FR-011), as with the page's other actions.
- Party admins are always in the crew: becoming an admin puts a player in, and stepping out of the
  crew gives up the admin role (016). So "crew" and "may open the chat" coincide for every viewer
  the page can show the action to.
