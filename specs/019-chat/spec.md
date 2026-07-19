# Feature Specification: Chat

**Feature Branch**: `019-chat`

**Created**: 2026-07-16

**Status**: Draft

**Input**: User description: "Chat — in-app messaging between users and in groups. Chat is a new top-level destination in the main nav carrying a total unread badge. Four kinds of conversation: 1:1 direct messages, named groups a user creates manually, an auto-created chat per team mirroring the roster, and an auto-created chat per event party. Inbox rows with avatar, last line, time and unread badge; live typing; search across messages and people; a warm empty state. Conversations deliver live with typing indicators, sent/read state, a new-messages divider and a jump-to-latest pill. Pasted JuggerHub links unfurl into view-only cards that deep-link to the full page. A details panel per conversation holds members, shared items and controls. On desktop the layout opens into an inbox rail + conversation + details side panel."

## Clarifications

### Session 2026-07-16

- Q: Who can a player direct-message — anyone, or only players they share a context with? → A: **Anyone can DM anyone.** Any signed-in player may start a direct conversation with any other. Teammates are surfaced first in the picker as a convenience, but the people search reaches every player. Blocking (US5) is the recourse, which makes it load-bearing rather than a nicety; new-conversation creation is rate-limited so the open reach cannot be turned into a mass-DM spam tool.
- Q: Can a player delete or edit a message they sent? → A: **Delete own message only; no edit.** A sender may delete their own message; it leaves a quiet "message deleted" tombstone in place so the thread does not silently rewrite itself for someone who already read it. Editing is out of scope — amending text that others may already have replied to raises reconciliation questions that deletion does not.
- Q: Do chat messages also raise rows in the Alerts inbox (feature 010), or is the Chat nav badge the only surface? → A: **Chat badge only — no Alerts rows.** Chat is its own inbox with its own unread badge; Alerts remains for invites, news and trainings. Chat introduces no new `NotificationType` and no new `NotificationCategory`. Chat reuses feature 010's *realtime transport* only, not its notification store.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Two players hold a 1:1 conversation (Priority: P1)

A player opens Chat from the main nav and sees their conversations as a list. They tap the + button, pick one person, and land in an empty thread. They type a message and send it; it appears right-aligned with a time. The recipient sees the conversation appear in their own inbox with an unread badge, opens it, reads the message, and replies. Each player sees the other's messages left-aligned with the sender's time.

**Why this priority**: Sending a message to one other person and getting a reply is the entire reason the feature exists. Everything else — groups, auto-chats, unfurl, desktop — is an elaboration on this loop. It is independently valuable the moment two users exist.

**Independent Test**: Seed two users. User A opens Chat, starts a DM with user B, and sends a message. User B sees the conversation with an unread badge, opens it (badge clears), and replies. User A sees the reply. Delivers value with no groups, no realtime push, no search, and no unfurl.

**Acceptance Scenarios**:

1. **Given** a signed-in player with no conversations, **When** they open Chat, **Then** they see a warm empty state inviting them to start a chat, with no error and no spinner left hanging.
2. **Given** a player on the Chat inbox, **When** they tap +, choose exactly one person, and confirm, **Then** a direct conversation with that person is opened (created if it did not exist) and they can type into it.
2a. **Given** a player who shares no team, party or event with another player, **When** they start a direct conversation with them, **Then** it is allowed — reach is open — and their teammates are merely listed first in the picker as a convenience.
2b. **Given** a player starting or sending to direct conversations far faster than a person plausibly would, **When** they exceed the server's rate limit, **Then** further attempts are refused with a clear message until the limit resets.
3. **Given** a player in a direct conversation, **When** they send a non-empty message, **Then** the message is persisted, appears in their thread right-aligned with a time, and the thread moves to the top of both participants' inboxes.
4. **Given** a player who has received a message they have not read, **When** they view the Chat inbox, **Then** that conversation row shows an unread badge and the Chat nav destination shows a total unread count.
5. **Given** a player opening a conversation with unread messages, **When** the conversation is displayed, **Then** those messages are marked read, the row badge clears, and the nav total decreases accordingly.
6. **Given** a player who already has a direct conversation with someone, **When** they start a new chat with that same person, **Then** they are taken to the existing conversation rather than creating a duplicate.
7. **Given** a player attempting to open or send to a conversation they are not a member of, **When** the request is made, **Then** it is refused and no content is disclosed.
8. **Given** a message body that is empty or only whitespace, or that exceeds the maximum length, **When** the player tries to send it, **Then** sending is blocked with a clear, non-technical message.
9. **Given** a conversation with a long history, **When** the player opens it, **Then** a bounded page of the most recent messages loads and older messages are fetched only as the player scrolls back.
10. **Given** a player who sent a message, **When** they delete it, **Then** its content disappears for every member of the conversation and a neutral "message deleted" marker stands in its place.
11. **Given** a message sent by someone else, **When** a member views it, **Then** no delete control is offered for it, and a direct request to delete it is refused.
12. **Given** a deleted message that was the conversation's most recent, **When** a member views the inbox, **Then** the row preview no longer shows the deleted content.

---

### User Story 2 - Messages arrive live, with typing and read state (Priority: P1)

Two players have a conversation open at the same time. When one starts typing, the other sees a typing indicator — both in the open thread and on the inbox row. When a message is sent, it lands in the other player's open thread without a refresh. If the reader has scrolled up, the new message drops in behind a "new messages" divider and a jump-to-latest pill appears. The sender sees their message move from sent to read once the other player has actually read it.

**Why this priority**: "Messages arrive instantly" is the difference between a chat and a message board. A player who has to refresh to see a reply will not use the feature for coordinating a carpool an hour before training. It layers directly onto US1 and is testable the moment two clients are connected.

**Independent Test**: Open the same conversation as two users in two sessions. Confirm typing in one shows an indicator in the other; a sent message appears in the other without a refresh; scrolled-up delivery shows a divider and jump pill; and the sender's message flips from sent to read when the other reads it.

**Acceptance Scenarios**:

1. **Given** two members of a conversation both connected, **When** one sends a message, **Then** it appears in the other's open thread without a manual refresh, and their inbox row updates its last line, time and unread badge.
2. **Given** a member with the conversation open and scrolled to the latest message, **When** a new message arrives, **Then** the thread stays pinned to the latest message and no divider or jump pill is shown.
3. **Given** a member scrolled up in the history, **When** a new message arrives, **Then** it is inserted behind a "new messages" divider, a jump-to-latest pill appears with a count, and the scroll position is not yanked.
4. **Given** a member who taps the jump-to-latest pill, **When** the jump completes, **Then** the thread is scrolled to the newest message, the pill disappears, and the messages are marked read.
5. **Given** a member typing into the composer, **When** they are actively typing, **Then** other members see a typing indicator in the thread and on the inbox row, identified by name in group conversations.
6. **Given** a member who stops typing or sends the message, **When** a short idle period passes, **Then** the typing indicator disappears on its own and never persists indefinitely.
7. **Given** a sent message in a direct conversation, **When** the other participant has not yet read it, **Then** the sender sees a sent state; **When** the other participant reads it, **Then** the sender sees a read state.
8. **Given** a member whose live connection drops, **When** they reopen or refresh the conversation, **Then** the full and correct history and unread state load over the normal (non-live) path — no message is only ever visible live.
9. **Given** a member who is not a participant of a conversation, **When** live events are broadcast for it, **Then** those events are never delivered to them.

---

### User Story 3 - A player creates and runs a named group (Priority: P2)

A player taps +, switches to Group, picks two or more people, names it ("Weekend crew"), and starts it. The thread labels each sender by name so a three-way conversation reads clearly. Any member can open the details panel to see who is in it, add more people, and leave quietly. Joins and leaves show as quiet system lines in the thread.

**Why this priority**: Groups are half of the stated goal ("chat with each other and in groups") and are the manual counterpart to the auto-created chats. They are not needed for the 1:1 loop to be useful, so they follow P1.

**Independent Test**: A player creates a named group with two others, sends a message, and confirms all three see it with sender labels. A member adds a fourth person (system line appears, new member sees the group). A member leaves (system line appears, group persists for the rest).

**Acceptance Scenarios**:

1. **Given** a player in the new-chat flow, **When** they select two or more people, **Then** a name field appears and the chat is created as a named group; **When** they select exactly one, **Then** no name field appears and it is created as a direct conversation.
2. **Given** a player creating a group, **When** they submit without a name, **Then** creation is blocked with a clear message asking for a name.
3. **Given** a group conversation with several participants, **When** a member views the thread, **Then** each message from someone else is labelled with its sender's name, and their own messages are not.
4. **Given** a member of a group opening its details, **When** the panel is shown, **Then** it lists all members with the viewer marked as "you", and offers Add people and Leave.
5. **Given** a member who adds a person to a group, **When** the add completes, **Then** the new member appears in the member list, a quiet system line records the join, and the new member sees the group in their inbox.
6. **Given** a member who leaves a group, **When** the leave completes, **Then** they no longer see the conversation, a quiet system line records the departure, and the group continues to exist for the remaining members.
7. **Given** a group whose creator has left, **When** the remaining members use it, **Then** it continues to work normally — no member holds an owner-only power that can strand the group.
8. **Given** a group with only one member remaining, **When** that member views it, **Then** the conversation remains usable and is not silently deleted.
9. **Given** a player attempting to add someone to a conversation that is not a manually-created group, **When** the request is made, **Then** it is refused.

---

### User Story 4 - Team and party chats appear by themselves (Priority: P2)

A player joins a team and finds the team's chat already waiting in their inbox, tagged TEAM, with the roster already in it. When someone new joins the team, they appear in the chat and a quiet system line notes it; when someone leaves the team, they lose the chat. The same happens for an event party, tagged PARTY. Nobody has to create or maintain these — they follow the roster. When a party disbands, its chat stays readable but no new messages can be posted.

**Why this priority**: Auto-created chats are the explicit ask ("automatically for teams and parties") and are what makes chat useful on day one without anyone organising it. They depend on the group machinery from US3.

**Independent Test**: Add a user to a team and confirm the team chat appears in their inbox tagged TEAM with the roster as members and a system line for the join. Remove them and confirm they lose access. Disband a party and confirm its chat is readable but closed to new messages.

**Acceptance Scenarios**:

1. **Given** a team, **When** a player joins its roster, **Then** the team's chat is present in that player's inbox tagged TEAM, with every current roster member as a participant, and a quiet system line records the join.
2. **Given** a member of a team chat, **When** they leave or are removed from the team roster, **Then** they lose access to that team's chat and it disappears from their inbox.
3. **Given** an event party (feature 016), **When** a player becomes a member of the party, **Then** the party's chat is present in their inbox tagged PARTY with the party roster as participants — including guests seated through the marketplace (feature 017).
4. **Given** a member of a team or party chat viewing its details, **When** the panel is shown, **Then** no Leave control is offered; mute and hide are offered instead.
5. **Given** a member of a team or party chat, **When** they attempt to leave it or to add/remove its participants directly, **Then** the attempt is refused — membership follows the roster only.
6. **Given** a party with a chat, **When** the party disbands, **Then** the chat becomes archived: existing members can still read the history, but no new message can be posted and no live events are emitted.
7. **Given** a team with an existing roster at the time this feature ships, **When** a roster member opens Chat, **Then** the team chat exists for them without any manual setup.
8. **Given** a team that is deleted, **When** the deletion completes, **Then** its chat is archived read-only for its members and never becomes writable again.

---

### User Story 5 - A player stays in control: mute, hide and block (Priority: P2)

A player who does not want a conversation buzzing can mute it — it stops driving the unread badge but stays in the inbox. A player who wants it gone can hide it. A player receiving unwanted direct messages can block the sender: the sender can no longer start or continue a direct conversation with them, and the thread is out of sight. Blocking never breaks the shared group and team chats they both legitimately belong to.

**Why this priority**: Any signed-in player can direct-message any other (FR-049), so blocking is not a nicety — it is the only thing standing between an open reach and unwanted contact. Open messaging between any two members of a community is exactly where harassment happens, and this is the recourse. It is not needed to prove the core loop works, but it MUST ship with the feature rather than after it.

**Independent Test**: Mute a conversation and confirm new messages do not raise the nav badge but the row still updates. Hide a conversation and confirm it leaves the inbox. Block a user and confirm they cannot send you a direct message, the thread is out of sight, and both users still function normally inside a group they share.

**Acceptance Scenarios**:

1. **Given** a member of any conversation, **When** they mute it, **Then** new messages in it no longer contribute to the Chat nav unread total, while the conversation row still updates its last line and time.
2. **Given** a member who has hidden a conversation, **When** they view the inbox, **Then** the conversation is not listed.
3. **Given** a player who blocks another player, **When** the blocked player attempts to send a direct message to them or to start a new direct conversation with them, **Then** the attempt is refused server-side and no message is delivered.
4. **Given** a player who has blocked someone, **When** they view their inbox, **Then** the direct conversation with that person is not shown to them.
5. **Given** two players where one has blocked the other, **When** both are members of the same group, team or party chat, **Then** both continue to participate in that conversation normally — the block applies to direct conversations only.
6. **Given** a player who has blocked someone, **When** they unblock them, **Then** direct messaging between them works again and the prior history is intact.
7. **Given** a blocked sender, **When** their send is refused, **Then** the refusal does not disclose the blocker's action beyond what is necessary to explain that the message cannot be delivered.

---

### User Story 6 - A player finds a message or a person (Priority: P3)

A player taps the search bar on the inbox and types. They get two things: messages from inside their own conversations, and people they could start a new chat with. Tapping a message result jumps to that conversation; tapping a person starts (or opens) a direct chat with them.

**Why this priority**: Search makes a busy inbox navigable and is the fastest path to "start a chat with a specific person", but a player with five conversations does not need it. It depends on there being conversations to search.

**Independent Test**: Seed a player with several conversations containing known text. Search a term and confirm the matching messages appear grouped under the player's own conversations, and that a name search surfaces people with an action to chat. Confirm a term that exists only in a conversation the player is not in returns nothing.

**Acceptance Scenarios**:

1. **Given** a player on the inbox, **When** they enter a search term, **Then** results are grouped into messages from their conversations and people they can chat with.
2. **Given** a search term that appears in a conversation the searching player is not a member of, **When** they search, **Then** that message is not returned under any circumstances.
3. **Given** a message result, **When** the player selects it, **Then** they are taken to that message in its conversation.
4. **Given** a person result, **When** the player selects the chat action, **Then** the existing direct conversation with that person opens, or a new one is started.
5. **Given** a search that matches nothing, **When** results are shown, **Then** a plain empty state is shown rather than an error.
6. **Given** any search, **When** results are returned, **Then** they are bounded and paginated rather than unbounded.
7. **Given** a player who has blocked someone, **When** they search for people, **Then** the blocked person is not offered as a chat target.

---

### User Story 7 - A pasted JuggerHub link becomes a card (Priority: P3)

A player pastes a JuggerHub link to a training, event, player profile or team into the composer. Instead of a bare URL, the message shows a card: the training's name and when it is, or the player's name and team, with a way through to the full page. There is no attach step and no separate menu — pasting is the whole interaction. Any other URL just sends as text.

**Why this priority**: Unfurl is what makes chat feel woven into the rest of JuggerHub rather than bolted on, but every message still delivers correctly without it. It depends on the message rendering from US1.

**Independent Test**: Paste a link to a known training into a conversation and confirm the message renders a card with that training's name and time and a working link to the training page. Paste an external URL and confirm it sends as plain text. Paste a link to a team-only training and confirm a viewer who cannot see that training does not get its details.

**Acceptance Scenarios**:

1. **Given** a player who pastes a JuggerHub link to a training, event, player profile or team, **When** the message is sent, **Then** it renders as a card identifying that item with a link through to its full page.
2. **Given** a card in a conversation, **When** the viewer selects it, **Then** they are taken to that item's own page — the card itself offers no RSVP, join or other action.
3. **Given** a player who pastes a URL that is not a recognised JuggerHub item link, **When** the message is sent, **Then** it is delivered as plain text and no card is shown.
4. **Given** a message whose card refers to an item the *viewer* is not allowed to see, **When** that viewer opens the conversation, **Then** they see the plain link rather than any of the item's details.
5. **Given** a message whose linked item has since been deleted, **When** a viewer opens the conversation, **Then** the message degrades to the plain link without an error.
6. **Given** a message containing a link, **When** it is rendered, **Then** any text supplied by the sender is displayed as text and never interpreted as markup.

---

### User Story 8 - Chat opens up on a desktop screen (Priority: P3)

A player on a wide screen sees the inbox become a left rail with every conversation one click away, the open conversation filling the space beside it, and the details panel sliding in on the right instead of replacing the screen. Same bubbles, same typing, same receipts — just more room.

**Why this priority**: A layout affordance for a better desktop experience, explicitly carrying no new mechanics. The feature is fully usable on mobile without it.

**Independent Test**: Open Chat at a desktop width and confirm the rail, conversation and details panel are visible together, that selecting a conversation in the rail swaps the conversation pane without a full page change, and that at mobile width the same screens behave as separate pushed views.

**Acceptance Scenarios**:

1. **Given** a player at a wide viewport, **When** they open Chat, **Then** the conversation list is a persistent left rail and the selected conversation fills the area beside it.
2. **Given** a player at a wide viewport with a conversation open, **When** they open its details, **Then** the details appear as a side panel alongside the conversation rather than replacing it.
3. **Given** a player at a mobile viewport, **When** they open a conversation and then its details, **Then** each is a full screen they can navigate back from.
4. **Given** a conversation open on desktop, **When** the player selects a different conversation in the rail, **Then** the conversation pane swaps and the address reflects the open conversation so it can be linked to and reloaded.
5. **Given** any viewport, **When** live delivery, typing, receipts and unread behaviour are exercised, **Then** they behave identically to the mobile layout.

---

### Edge Cases

- **Direct conversation with yourself**: Starting a chat with only yourself selected is not offered and is refused if attempted.
- **A cold DM from a complete stranger**: Allowed by design (FR-049) — reach is open. The recipient's recourse is block (US5); the sender's reach is bounded by rate limiting (FR-049a), not by a relationship check.
- **Mass-DM attempt**: A player scripting direct conversations against many players hits the server-side rate limit; the limit cannot be bypassed by driving the API directly rather than the interface.
- **Deleting the only message in a conversation**: The conversation remains, showing the tombstone; it is not resurrected as empty nor silently removed.
- **Deleting a message someone is replying to**: The tombstone stays in place so the surrounding thread still reads coherently; the reply is untouched.
- **Deleting a message that carried a card**: The card goes with it — a deleted message surfaces no linked-item detail (FR-050c).
- **Deleting a message already counted as unread**: The recipient's unread count stays correct and never goes negative.
- **A participant's account is deleted or banned**: Their past messages remain readable in shared conversations with a neutral placeholder identity; no new messages arrive from them; a direct conversation with them can no longer be started. (Ban/soft-delete semantics follow feature 013.)
- **Blocked user already in a shared group**: The block applies only to direct conversations; both remain full participants of the shared group, team or party chat.
- **Simultaneous sends / clock skew**: Two members sending at the same instant produces a stable, identical ordering for every viewer; ordering never depends on a client-supplied timestamp.
- **The same person is added to a group twice**: The second add is a no-op rather than a duplicate participant row or a second system line.
- **Duplicate direct conversation race**: Two clients starting a direct conversation with the same person at the same moment result in exactly one conversation, not two.
- **Unread on a conversation with no messages**: A newly created conversation with no messages shows no badge and does not inflate the nav total.
- **Unread total is large**: The nav badge caps its display (consistent with the existing Alerts badge) rather than growing unbounded.
- **Reading on one device**: Reading a conversation on one device clears its unread state on the player's other open sessions too.
- **Typing indicator when the typist leaves or disconnects**: The indicator expires on its own; it never sticks after the typist has gone.
- **Live connection is down**: Every value the live channel delivers is also reachable on load, so a player with no live connection still sees correct history, unread counts and read state.
- **Very long message or a wall of text**: A maximum length is enforced server-side; the thread renders long content without breaking the layout.
- **Message that is only a link**: Renders as its card (or plain link) with no empty text bubble.
- **Team chat for a team with one member**: Exists and is usable by that single member.
- **Player is on many teams and parties**: The inbox stays paginated; auto-created chats do not bypass the inbox's bounds.
- **Rejoining a team**: A player who leaves and rejoins a team regains access to the team chat and its history.
- **Archived (disbanded party / deleted team) chat**: Read-only for its members — no send, no typing, no add; it still appears in the inbox marked as such.

## Requirements *(mandatory)*

### Functional Requirements

**Navigation & inbox**

- **FR-001**: Chat MUST be a top-level destination in the app's primary navigation, present in both the desktop and mobile navigation surfaces, carrying a badge with the player's total unread conversation count.
- **FR-002**: The inbox MUST list the player's conversations, most recently active first, each row showing an identifying avatar, the conversation's name, a preview of the last message, the time of the last message, and an unread badge when unread.
- **FR-003**: The inbox MUST visually distinguish the four kinds of conversation, tagging team chats TEAM and party chats PARTY.
- **FR-004**: The inbox MUST show a typing indicator on a conversation's row while another member of it is typing.
- **FR-005**: The inbox MUST show a warm, actionable empty state when the player has no conversations, offering a way to start one.
- **FR-006**: The inbox and every message list MUST be paginated or explicitly bounded; no surface may return an unbounded collection.

**Conversations & messages**

- **FR-007**: A player MUST be able to start a direct conversation by selecting exactly one person, and a named group by selecting two or more.
- **FR-008**: Starting a direct conversation with someone the player already has one with MUST open the existing conversation; the system MUST guarantee at most one direct conversation per pair of players.
- **FR-009**: A group MUST require a non-empty name at creation.
- **FR-010**: A member of a conversation MUST be able to send a text message to it; the system MUST reject empty, whitespace-only, and over-length messages with a clear, non-technical message.
- **FR-011**: Messages MUST display in a stable chronological order that is identical for every viewer and MUST NOT be ordered by any client-supplied time.
- **FR-012**: A message MUST show its time; a message from another member in a group, team or party conversation MUST show its sender's name; the viewer's own messages MUST be visually distinguished from others'.
- **FR-013**: The system MUST record quiet system lines in the thread for membership changes (joined, left, removed), distinct in appearance from member messages and attributable to no sender.
- **FR-014**: Message content MUST be stored and rendered as plain text and MUST never be interpreted as markup by any client.
- **FR-014a**: A member MUST be offered a delete control on their own messages only, and MUST NOT be offered one on anyone else's.

**Read state & unread**

- **FR-015**: The system MUST track, per member per conversation, how far they have read, and MUST derive the unread count from it.
- **FR-016**: Opening a conversation MUST mark its messages read for that member, clear its row badge, and reduce the navigation total accordingly, converging across that player's other sessions.
- **FR-017**: In a direct conversation, the sender MUST see whether their message has been read by the other participant, distinguishing at least a sent state from a read state.
- **FR-018**: The navigation unread total MUST exclude muted **and hidden** conversations, and MUST cap its displayed value rather than growing unbounded.

**Live delivery**

- **FR-019**: A message sent to a conversation MUST appear to its other connected members without a manual refresh, and MUST update their inbox row.
- **FR-020**: The system MUST convey that a member is typing to the other members of that conversation, and this indication MUST expire automatically without any action from the typist.
- **FR-021**: When a message arrives while the reader is scrolled away from the latest, it MUST be inserted behind a "new messages" divider with a jump-to-latest affordance, and MUST NOT move the reader's scroll position.
- **FR-022**: Live events for a conversation MUST only ever be delivered to that conversation's current members.
- **FR-023**: Live delivery MUST be an enhancement, not the source of truth: every message, unread count and read state delivered live MUST also be obtainable on a normal load, so a player without a live connection sees correct state.

**Auto-created team & party chats**

- **FR-024**: The system MUST maintain exactly one chat per team and one per event party, created without any user action, including for teams and parties that already exist when the feature ships.
- **FR-025**: Membership of a team or party chat MUST mirror the underlying roster: joining the roster grants access, leaving or being removed from it revokes access.
- **FR-026**: A member MUST NOT be able to leave a team or party chat independently of the roster, nor add or remove its participants directly; mute and hide MUST be offered in place of leave.
- **FR-027**: When a party disbands or a team is deleted, its chat MUST become archived — readable by its members, closed to new messages and to live events, and marked as such.

**Control & safety**

- **FR-028**: A player MUST be able to mute any conversation, which stops it contributing to the navigation unread total while leaving it in the inbox.
- **FR-029**: A player MUST be able to hide a conversation from their inbox.
- **FR-030**: A player MUST be able to block another player, and to unblock them; unblocking MUST restore direct messaging with the prior history intact.
- **FR-031**: A block MUST prevent the blocked player from starting or continuing a direct conversation with the blocker, enforced server-side on every send and start, and MUST hide that direct conversation from the blocker's inbox.
- **FR-032**: A block MUST NOT affect either player's participation in any group, team or party conversation they both belong to.
- **FR-033**: A blocked player MUST NOT be offered as a chat target in the blocker's people search.

**Search**

- **FR-034**: A player MUST be able to search from the inbox and receive results grouped into messages and people.
- **FR-035**: Message search MUST only ever return messages from conversations the searching player is currently a member of.
- **FR-036**: Selecting a message result MUST take the player to that message in its conversation; selecting a person result MUST open or start a direct conversation with them.

**Link unfurl**

- **FR-037**: A message containing a link to a JuggerHub training, event, player profile or team MUST render as a card identifying that item with a link through to its full page.
- **FR-038**: An unfurled card MUST be view-only — it MUST NOT offer RSVP, join or any other action inside the conversation.
- **FR-039**: A link that is not a recognised JuggerHub item link MUST be delivered as plain text with no card.
- **FR-040**: A card's contents MUST be resolved against the *viewer's* own permission to see the linked item, not the sender's; a viewer who may not see the item MUST see only the plain link.
- **FR-041**: A card whose linked item no longer exists MUST degrade to the plain link without an error.
- **FR-042**: Unfurl MUST resolve only JuggerHub's own items from the link's shape and MUST NOT fetch arbitrary external URLs.

**Details panel & layout**

- **FR-043**: Every conversation MUST offer a details view listing its members with the viewer marked as "you", the items shared in it, and the controls appropriate to its kind (add people and leave for manual groups; mute, hide and block where applicable).
- **FR-044**: Any member of a manually-created group MUST be able to add people to it and to leave it; leaving MUST NOT delete the group for the others.
- **FR-045**: At a wide viewport the inbox MUST become a persistent rail beside the open conversation, with details as a side panel; at a narrow viewport these MUST be separate navigable screens. Behaviour other than layout MUST be identical.
- **FR-046**: An open conversation MUST be addressable, so it can be linked to, reloaded and shared as a location within the app.

**Access control**

- **FR-047**: Every read of a conversation, its messages, its members and its details, and every send, MUST be authorised server-side against the requester's current membership; client-side checks are for presentation only.
- **FR-048**: A request for a conversation the requester is not a member of MUST be refused without disclosing whether it exists or any of its content.

**Reach & rate limiting**

- **FR-049**: Any signed-in player MUST be able to start a direct conversation with any other signed-in player; there is no shared-team, shared-party or mutual-consent precondition. The new-chat picker MUST surface the player's teammates first as a convenience, while people search MUST be able to reach any player.
- **FR-049a**: Because reach is open, starting new direct conversations and sending messages MUST be rate-limited server-side, so the open reach cannot be used to mass-message the community. Limits apply per sender and MUST NOT be enforceable only in the client.
- **FR-049b**: A player blocked by someone MUST NOT be able to circumvent the block by starting a fresh direct conversation with them (see FR-031); blocking is the primary recourse against unwanted contact and MUST hold on every path.

**Withdrawing a message**

- **FR-050**: A sender MUST be able to delete their own message. The message's content MUST be removed for every viewer and replaced in place by a neutral "message deleted" marker, preserving the thread's continuity for anyone who already read around it.
- **FR-050a**: Deleting a message MUST be available only to its own sender; no member may delete another member's message, and there is no moderator delete in this feature.
- **FR-050b**: Editing a sent message is out of scope. A message's text MUST be immutable once sent — the only correction available is delete-and-resend.
- **FR-050c**: A deleted message MUST stop contributing content to the inbox preview, to the unfurled card it carried, and to message search results.

**Surfacing outside Chat**

- **FR-051**: Chat MUST be its own inbox: an unread message MUST be surfaced by the Chat destination's badge and the conversation's own row, and MUST NOT raise a row in the Alerts inbox (feature 010).
- **FR-051a**: Chat MUST NOT introduce a new notification type or notification-preference category; the existing Alerts spine and its preference matrix (features 010/011) MUST be left unchanged by this feature.

### Key Entities *(include if feature involves data)*

- **Conversation**: A thread of messages with a set of participants. Attributes: kind (direct / group / team chat / party chat), name (groups only — direct and auto chats derive their display name), the team or party it mirrors (auto kinds only), lifecycle state (active / archived), last-activity time for inbox ordering.
- **Conversation Participant**: One player's membership of one conversation. Attributes: the conversation, the player, when they joined, how far they have read (drives unread and read receipts), muted flag, hidden flag. For team and party chats these rows follow the roster rather than being managed by hand.
- **Message**: One entry in a conversation. Attributes: the conversation, the sender (absent for a system line), kind (member message / system line), plain-text body, creation time, the JuggerHub item it links to when it unfurls, and whether it has been deleted by its sender (a deleted message keeps its place and its ordering but surrenders its content and its card).
- **Link Card**: The view-only representation of a JuggerHub item referenced by a message — the item's kind (training / event / player / team) and identity, resolved for display against the *viewer's* permissions. Not user-authored content.
- **Block**: One player's block of another. Attributes: the blocker, the blocked player, when it was created. Governs direct conversations only.
- **Team** (existing): Owns a mirrored team chat; its roster defines that chat's participants.
- **Party** (existing, feature 016): Owns a mirrored party chat; its roster — including marketplace guests (feature 017) — defines that chat's participants; disbanding archives the chat.
- **User** (existing): Participates in conversations; deletion/ban (feature 013) neutralises their identity in shared history.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A player can go from opening Chat to having sent a first message to a chosen person in under 30 seconds, with no setup step.
- **SC-002**: With two players in the same conversation, a sent message is visible to the other without any manual refresh, and a typing indicator appears and then clears by itself — 100% of the time across repeated exchanges.
- **SC-003**: A player joining a team finds that team's chat already in their inbox with the full roster present, having taken no action to create or join it.
- **SC-004**: A player who leaves a team, or is removed from it, loses access to that team's chat in 100% of cases, verified by a direct request for it being refused.
- **SC-005**: A blocked player cannot deliver a direct message to the blocker under any path — including a direct request that bypasses the interface — while both continue to use a shared group chat normally.
- **SC-006**: Message search returns results only from the searching player's own conversations; a term present only in a conversation they are not in returns zero results, verified by a direct request.
- **SC-007**: A pasted link to a training renders a card carrying that training's name and time, and a viewer without permission to see that training gets only the plain link — verified for the same message from two different viewers.
- **SC-008**: A player's unread total is correct after reading on a second device, with no refresh of the first.
- **SC-009**: The same conversation exercised at mobile and desktop widths produces identical message, unread, typing and receipt behaviour; only the layout differs.
- **SC-010**: No inbox, message list, member list or search surface returns an unbounded collection; every one is paginated or explicitly bounded.
- **SC-011**: A player with no live connection, on a normal load, sees the same history, unread counts and read state as a connected player.
- **SC-012**: A player can direct-message any other player with no shared team, party or event, in the same number of steps as messaging a teammate — while a scripted attempt to open many direct conversations in quick succession is refused by the server before it can reach the community.
- **SC-013**: A sender can delete their own message and every other member sees its content gone and a tombstone in its place, with no other message's content or ordering disturbed; a request to delete someone else's message is refused.
- **SC-014**: An unread chat message raises the Chat badge and its own inbox row and produces no row in the Alerts inbox — verified by an unread message leaving the Alerts unread count unchanged.

## Assumptions

- **Realtime spine reuse**: Live delivery reuses the existing per-user realtime *transport* established for notifications (feature 010) rather than introducing a second mechanism. It stays best-effort and layered over durable storage, consistent with how notifications already behave. Chat reuses the transport only — it does not write to the notification store (FR-051a).
- **Open reach, block as the recourse**: Reach is deliberately open (any player may DM any player). The safety model is therefore reactive-plus-bounded: block (US5) is the recourse and is enforced server-side on every path, and rate limiting (FR-049a) bounds how fast the open reach can be exercised. This was chosen over a shared-context restriction by the product owner, accepting that an unwanted first message can land before block is available.
- **Read state is a marker, not a receipt per message**: Read state is tracked as a per-participant position in the conversation rather than a row per message per member, which is what makes unread counts and receipts affordable at roster scale. Read receipts are surfaced for direct conversations; group "seen by" detail beyond that is out of scope.
- **Typing is ephemeral**: Typing indications are transient signals with a short expiry and are never persisted or reconstructed on load.
- **Auto-chat backfill**: Team and party chats for rosters that already exist when the feature ships are created without user action, so no team has to be re-created to get a chat.
- **Guests count as party chat members**: A mercenary seated through the event marketplace (feature 017) is a party roster member and therefore a party chat participant, consistent with how the party roster already treats them.
- **Group size**: A manually-created group is bounded by a sensible maximum member count rather than being unlimited; the exact bound is set in planning.
- **Anyone in a group can add**: Manual groups have no admin role — any member can add people and any member can leave. This matches the wireframe, which offers Add and Leave to the viewer with no role distinction.
- **Message retention**: Messages are retained indefinitely and there is no scheduled purge; retention policy is not part of this feature.
- **Out of scope**: photo, file and voice-note sharing (the details panel's "shared items" means unfurled link cards only); inline RSVP or any other action from inside a card; reporting a conversation or a user to admins (blocking is the recourse this feature ships); editing a sent message (delete-and-resend instead, FR-050b); moderator/admin deletion of a member's message; message reactions, replies/threading, forwarding and pinning; push notifications and email-on-missed-message; chat rows in the Alerts inbox (FR-051); admin visibility into private conversations; group read receipts / "seen by" detail.
- **Design system**: Chat follows DESIGN.md. Where the wireframe conflicts with it, DESIGN.md wins: the wireframe's blue own-message bubbles are rendered in the coral brand primary, since blue is reserved for the "info" status token and DESIGN.md forbids introducing colors ad hoc. Reported rather than silently resolved, per the constitution.
- **Wireframe navigation is illustrative**: The wireframe draws a Home / Teams / Events / Chat / You navigation, which is not the app's actual navigation (Home / Browse / My team / Alerts, feature 008). Chat is added as a new destination to the real navigation; the wireframe's other tabs are not adopted.
- **Existing team/party news is unaffected**: Team news and party news posts (features 005/016) are a separate broadcast surface and are not replaced or merged by chat.
