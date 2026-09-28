# Feature Specification: The Team Page Leads Into the Team Chat

**Feature Branch**: `060-team-chat-link`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "GH #362 — The team page has no way into the team chat. Members (and admins) of a team get a one-tap "Team chat" action on the team page that opens the team's auto-created chat conversation (ConversationKind.Team), which may not exist yet (created lazily on first inbox open). A resolver endpoint GET chat/team/{teamId} returns the team conversation id for a member, ensuring it exactly as opening Chat would; 404 for non-members, never a second membership oracle. Copy in all three catalogues (en/de/es). DESIGN.md governs placement; one coral CTA per view. Optional: unread count on the button."

## Context

Every team has a chat of its own (feature 019): a conversation whose members are exactly the
team's current roster. It is not created when the team is: it comes into being the first time
one of the team's members opens Chat. Once it exists, the only way to reach it is the Chat
inbox, where the player has to find it by the team's name.

The team page, the one place in the product that is about the team, has no way in. The
opposite route does exist: since feature 027 the team page offers **Contact admins**, which
opens a private thread with the team's admins.

**Two corrections to the issue, found by reading the product:**

- **Contact admins is not only for outsiders.** The issue says it serves non-members. It is
  offered to every signed-in player who is not an admin of the team (027 FR-001/FR-002), plain
  members included. Today a plain member sees it at the top of the team page, and an admin
  sees no action there at all.
- **Some teams never get their chat.** A team's chat and its Contact-admins threads are both
  tied to the team. The check that decides whether a team already has its chat counts **any**
  conversation tied to the team, so a Contact-admins thread passes for the team chat. If
  someone contacts a team's admins before any member has opened Chat, the team's chat is never
  created, and the members' inbox shows no team chat for as long as that thread exists. A
  "Team chat" button that trusted the same check would be worse. For an admin it would open
  someone's private Contact-admins thread; for a plain member it would report that their own
  team has no chat. This feature fixes that defect, because the button depends on it.

**Out of scope, deliberately**: an unread count on the button (owner decision, below), a
link into a **party's** chat from the party page, any change to what the team chat is or who
is in it, and any change to how Contact admins behaves. For members only its **position**
changes.

## Clarifications

### Session 2026-09-28

- Q: Should the "Team chat" action show how many messages in the team chat are unread?
  → A: **No count.** The action is a plain button that looks the chat up and opens it when it
  is pressed. Loading the team page stays as it is, a team's chat is created only when a member
  actually wants it, and the navigation's chat badge remains where unread messages are counted.
- Q: Where does "Team chat" sit on the team page? → A: **For members, all of the page's
  actions move into the right-rail card; only non-members see actions at the top.** A member's
  actions (Team chat, Contact admins for plain members, Invites for admins, Manage) sit together
  in one card in the page's side column. The top of the page carries actions only for people
  who are not on the team: signed-out visitors, players who can ask to join, and players with a
  pending request.
  **Accepted consequence**: on a phone the side column stacks below the page's main column, so
  a member scrolls past the roster, events and news to reach these actions. The owner chose
  that when it was put to them.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A member opens the team chat from the team page (Priority: P1)

Mara is on the Rheinfeuer team page, reading this week's news post about Saturday's training.
She wants to ask the team who is bringing the spare pompfen. In the team's card of actions
she presses **Team chat** and lands in the Rheinfeuer chat, the same conversation her inbox
lists under the team's name, ready to type.

If nobody on the team has opened Chat yet, the chat does not exist when she presses. It is
created then, the same way it would have been if she had opened Chat, and she lands in it.

**Why this priority**: This is the issue. Members, the people the team chat exists for, have
no route to it from the page about their team.

**Independent Test**: As a team member, open the team page, press Team chat, and confirm the
open conversation is the team's own chat. Repeat for a team whose chat has never been opened,
and as an admin of the team.

**Acceptance Scenarios**:

1. **Given** a member of a team whose chat exists, **When** they press Team chat on the team
   page, **Then** the team's chat opens, and it is the same conversation the inbox lists
   under the team's name.
2. **Given** a member of a team whose chat has never been opened by anyone, **When** they
   press Team chat, **Then** the team's chat is created and opens, and no message,
   notification or alert is produced for anyone.
3. **Given** an admin of the team, **When** they press Team chat, **Then** the team's chat
   opens, never a Contact-admins thread addressed to the team.
4. **Given** a signed-out visitor, a signed-in non-member, or a player whose request to join
   is pending, **When** they view the team page, **Then** no Team chat action is shown.
5. **Given** a member presses Team chat, **When** the chat is still being looked up, **Then**
   the action shows that it is working and cannot be pressed a second time.

---

### User Story 2 - A team's chat exists even when someone contacted its admins first (Priority: P1)

Rheinfeuer was created on Monday. On Tuesday Jonas, who is thinking about joining, pressed
Contact admins and asked when they train. No member had opened Chat yet. Today every member's
inbox shows no Rheinfeuer chat, and it never will. After this feature, the next member to open
Chat, or to press Team chat, gets the team's chat, and Jonas's thread with the admins stays
exactly as it was.

**Why this priority**: Story 1 cannot be correct without it. For an admin of such a team,
the new button would otherwise open Jonas's private thread; for a plain member it would report
that their team has no chat.

**Independent Test**: Have a non-member contact a team's admins before any member has opened
Chat, then open Chat as a member and confirm the team's chat is listed, and that Team chat on
the team page opens it rather than the Contact-admins thread.

**Acceptance Scenarios**:

1. **Given** a team whose only conversation is a Contact-admins thread, **When** a member
   opens Chat, **Then** the team's chat is created and listed, and the Contact-admins thread
   is unchanged.
2. **Given** the same team, **When** a member presses Team chat on the team page, **Then** the
   team's chat is created and opens.
3. **Given** the same team, **When** an admin presses Team chat, **Then** the team's chat
   opens, not the Contact-admins thread they are also a member of.
4. **Given** a team already affected by this defect before the change ships, **When** any
   member next opens Chat or presses Team chat, **Then** the team's chat is created, with no
   manual step and no data migration.

---

### User Story 3 - A member's actions sit together in one card (Priority: P2)

On the team page an admin sees one card in the side column with everything they can do:
Team chat, Invites and Manage. A plain member sees Team chat, Contact admins and Manage in
the same card. The top of the page, beside the team's name, carries no actions for either.
Someone who is not on the team still sees Contact admins and Request to join at the top,
exactly as today.

**Why this priority**: The owner's placement decision. It does not create a new capability,
so it ranks below the two stories above, but it decides where Story 1's button lives.

**Independent Test**: View the team page as an admin, a plain member, a non-member, a player
with a pending request and a signed-out visitor, and confirm where each action appears.

**Acceptance Scenarios**:

1. **Given** a plain member, **When** they view the team page, **Then** Team chat, Contact
   admins and Manage appear together in the side-column card, and the top of the page shows
   no action.
2. **Given** an admin, **When** they view the team page, **Then** Team chat, Invites and
   Manage appear together in the side-column card, and the top of the page shows no action.
3. **Given** a signed-in non-member, **When** they view the team page, **Then** Contact admins
   and Request to join appear at the top as today, and the side-column card is not shown.
4. **Given** a plain member presses Contact admins in the card, **When** it opens, **Then** it
   behaves exactly as it did from the top of the page.

---

### Edge Cases

- **Removed from the team since the page loaded**: pressing Team chat does not open the chat.
  The player is told they are no longer on the team, and the page refreshes to show it as a
  non-member sees it.
- **The team chat is hidden** (archived, feature 048) **or muted by this player**: Team chat
  still opens it. Pressing the button changes neither flag. Hiding stays the player's own
  choice, and the way back into the inbox remains the chat's details panel.
- **The lookup fails** (network error, server error): the player stays on the team page, sees
  a short message in the card that the chat could not be opened, and can press again. They are
  never taken to a broken or empty chat page.
- **Two members press at the same moment on a team with no chat yet**: both land in the same,
  single team chat. Never two.
- **A player asks for a team's chat directly, without the page** (a non-member, a signed-out
  caller, or a team that does not exist): the answer is the same "not found" in every case.
  It does not reveal whether the team exists, whether it has a chat, or who is in it.
- **Asking has side effects only for the team's own members**: a non-member's request creates
  nothing for the team they asked about.
- **The team is deleted**: its page no longer exists, so there is no button. Its archived
  chat stays readable in members' inboxes, as today (019 FR-027).
- **Blocks**: a block between two players does not affect the team chat or this action.
  Blocks apply only to direct messages (019 FR-032).

## Requirements *(mandatory)*

### Functional Requirements

**Opening the team chat**

- **FR-001**: Every current member of a team, admins included, MUST see a **Team chat** action
  on the team page. Signed-out visitors, non-members and players with a pending join request
  MUST NOT see it.
- **FR-002**: Pressing Team chat MUST open **the team's own chat**, the conversation whose
  members are the team's roster and which the inbox lists under the team's name. It MUST
  never open a Contact-admins thread, another team's chat or any other conversation.
- **FR-003**: If the team's chat does not exist yet, pressing Team chat MUST create it the
  same way opening Chat does. The only visible difference MUST be that the chat now exists
  for the team's members, as it would after anyone had opened Chat. No message, alert,
  notification, email or push may result.
- **FR-004**: Whether a player may open the team chat MUST be decided by the server, by the
  **same rule that decides who may read the chat**. There must be no second, separately
  written answer to "is this player on the team?" for this feature.
- **FR-005**: A player who is not a current member of the team MUST receive the same "not
  found" answer as for a team that does not exist or has no chat. The answer MUST NOT
  distinguish these cases.
- **FR-006**: The answer to a member MUST identify the conversation and nothing more: no
  messages, member list, name or unread count.
- **FR-007**: A request from a player who is not on the team MUST NOT create a chat for that
  team.
- **FR-008**: The action MUST NOT show an unread count (owner decision). Loading the team page
  MUST NOT make any request it does not make today. The chat is looked up only when Team chat
  is pressed.
- **FR-009**: While the chat is being looked up, the action MUST show that it is working and
  MUST NOT accept a second press.
- **FR-010**: If the lookup fails, the player MUST stay on the team page and see a short
  message, in their language, that the chat could not be opened. The action MUST be usable
  again. The message MUST be chosen by the kind of failure, never taken from the server's
  wording.
- **FR-011**: If the lookup answers "not found" (the player has left or been removed since the
  page loaded), the player MUST be told they are no longer on the team, and the page MUST
  refresh to show it as they may now see it.
- **FR-012**: Opening the team chat from the team page MUST NOT change the player's own hide
  or mute setting for that chat.

**The defect (Story 2)**

- **FR-013**: Whether a team has its chat MUST be decided by the **team chat alone**.
  Contact-admins threads addressed to the team MUST NOT count as the team's chat, either when
  the product decides whether to create the team's chat or when it looks the chat up.
- **FR-014**: Teams already affected MUST get their chat the next time a member opens Chat or
  presses Team chat, without a data migration or manual step. Existing Contact-admins threads
  MUST be left exactly as they are.
- **FR-015**: There MUST remain at most one team chat per team, including when two members
  cause it to be created at the same moment.

**Placement (Story 3)**

- **FR-016**: For a member (plain member or admin), every action the team page offers them MUST
  sit together in one card in the page's side column: **Team chat**; **Contact admins** (plain
  members only, as today); **Invites** (admins only, as today); **Manage** (every member, as
  today). The top of the page MUST carry no action for a member.
- **FR-017**: For everyone who is not a member (signed-out visitors, signed-in non-members,
  players with a pending request), the top of the page MUST keep its actions exactly as today:
  Sign in to join; Contact admins with Request to join; or Requested with Cancel request.
- **FR-018**: Contact admins MUST behave the same wherever it is shown: same destination, and
  an existing thread with the admins is reopened rather than started again (027 FR-004).
- **FR-019**: None of the card's actions may be the page's coral primary action. They are
  secondary actions (DESIGN.md: one coral CTA per view).
- **FR-020**: Every new piece of interface text MUST exist in English, German and Spanish.

### Key Entities

- **Team chat**: an existing conversation, one per team, whose members are the team's current
  roster (feature 019). Unchanged by this feature, apart from being found reliably (FR-013).
- **Contact-admins thread**: an existing conversation between one player and a team's current
  admins (feature 027). Unchanged. What changes is that it can no longer pass for the team's
  chat.

No new data is stored.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From the team page, a member reaches their team's chat with **one press** of
  Team chat, in every case: an existing chat, a never-opened chat, and a team that already has
  Contact-admins threads.
- **SC-002**: In **zero** cases does Team chat open a conversation other than the team's own
  chat. This includes an admin of a team with Contact-admins threads.
- **SC-003**: A non-member, a signed-out caller and a request about a non-existent team receive
  **identical** "not found" answers, and none of those requests creates anything.
- **SC-004**: A member's team page makes **the same number of requests** while loading as
  before this feature.
- **SC-005**: Every team affected by the defect has its chat after its next member opens Chat,
  with **no** manual intervention.
- **SC-006**: In German at 375px, the card with a member's actions shows every action in full,
  with no truncated label and no horizontal overflow. The same holds on desktop.

## Assumptions

- The team chat's membership and lifecycle are unchanged (feature 019). This feature adds a
  route to it and fixes how it is found.
- Creating a team's chat when a member presses the button is acceptable, because opening Chat
  already does exactly that for every team the member is on.
- Opening a hidden (archived) team chat from the team page is intended. It is the player's own
  conversation, and feature 048 made hiding reversible from the chat's details.
- The team page does not update live. A member removed while the page is open learns it when
  they press the button (FR-011), as with the page's other member actions.
- The side column's card keeps its existing heading. Only what it holds and who sees it change.
