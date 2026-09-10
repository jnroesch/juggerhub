# Feature Specification: Hiding a Chat Is Reversible

**Feature Branch**: `048-chat-unhide`

**Created**: 2026-09-09

**Status**: Draft

**Input**: User description: "Hidden chats are archives, not exits. Hiding a conversation must be reversible, and a hidden conversation must come back on its own when it becomes active again. Owner decision (2026-09-09, GitHub #222): hide means ARCHIVE — tidy away until something happens — and mute means don't bother me; each control has exactly one job. Build issue #222's options 1 + 2: auto-un-hide when a new message arrives, and a Hide ↔ Show-in-inbox toggle in the conversation details panel mirroring mute/unmute. A 'Hidden chats' list or inbox filter (option 3) is out of scope. Feature 019 FR-026 is amended — mute, not hide, is the control offered in place of leave — and FR-029 gains its un-hide counterpart."

## Context & Amendment

This **amends feature 019 (chat)**, requirements **FR-026** and **FR-029**.

Feature 019 gave a player a way to **hide** a conversation from their inbox and never gave
them a way back. There is no un-hide control anywhere in the product. A hidden conversation
leaves the inbox, stops contributing to the navigation unread total (FR-018), and a new
message does not bring it back. It still opens by direct link — membership is untouched —
but the details panel there offers only *Hide*, again. A hidden direct message has one
accidental route home (the new-chat picker resolves an existing conversation by person);
a hidden **group, team, party or admin-contact** thread has none at all.

**The underlying problem is that "hide" was never defined.** Feature 019 asked it to be two
incompatible things: FR-026 offered it as the stand-in for *leave* in a roster-backed chat
(implying something deliberate and lasting), while the word itself promises an archive
(implying something temporary). Nobody chose, so nothing was built to reverse it.

**Owner decision (2026-09-09, GitHub #222)** — hide is an **archive**:

- **Hide** means *tidy this away until something happens*. It is reversible, and it reverses
  itself when the conversation becomes active again.
- **Mute** means *don't bother me*. It leaves the conversation in the inbox and stops it
  contributing to the unread total.

Each control now has exactly one job. The consequence for 019 **FR-026** is that its premise
is rejected: hide was justified there as a substitute for leaving a team or party chat, and
the owner's position is that **there is no reason to stay on a team but leave its chat** — a
player who wants out leaves the team, which removes them from the chat by the roster rule
(FR-025). **Mute** is the control offered in place of leave. Hide remains available on every
kind of conversation, as an archive, which is a different promise.

**Out of scope, deliberately**: a "Hidden chats" list, section or filter in the inbox
(issue #222 option 3). It stays a follow-up. The accepted consequence is recorded under
Assumptions.

## Clarifications

### Session 2026-09-09

- Q: What is "hide" for, given that FR-026 and the word itself disagree? → A: **Hide is an
  archive; mute is the leave substitute.** Staying on a team while abandoning its chat is
  not a real need — leaving the team leaves the chat. FR-026 is amended accordingly.
- Q: Should a "Hidden chats" entry point be built so hidden group and team chats are
  reachable? → A: **No, not in this feature.** Auto-return plus the details-panel toggle is
  the answer; a hidden-chats surface follows only if hidden chats are observed to accumulate.
- Q: Should a system line ("X joined", "X left", a chat becoming archived) also return a
  hidden conversation, or only a message written by a person? → A: **Only a message written
  by a person.** System lines count toward the unread total, so returning on them would let a
  busy team's roster churn repeatedly resurface a deliberately archived chat *carrying a
  badge* for "X joined" — noise rather than activity. "Something happened" means somebody
  wrote something.
- Q: When a player sends a message into a conversation they themselves hid, should it return
  to their own inbox? → A: **Yes.** Writing into a conversation is unambiguous use of it;
  leaving it hidden means posting a message that is invisible in your own inbox.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An archived conversation comes back on its own (Priority: P1)

A player tidies a quiet team chat out of their inbox. Weeks later a teammate posts about
Saturday's training. The conversation reappears in the player's inbox, in its normal place,
carrying an unread badge — without the player having done anything, and without them needing
to know they had hidden it.

**Why this priority**: This is the whole point of the archive definition, and it is the only
part that repairs the conversations that are unreachable today. Hidden group, team, party
and admin-contact threads have no route back through the interface at all; this gives every
one of them a route that needs no new surface.

**Independent Test**: Hide a conversation, have another member send a message to it, and
confirm the conversation is back in the inbox with an accurate unread badge — shipped alone,
this alone resolves the reported problem.

**Acceptance Scenarios**:

1. **Given** a player has hidden a team conversation, **When** another member sends a message
   to it, **Then** the conversation reappears in the player's inbox, ordered by that message,
   and the navigation unread total includes it.
2. **Given** a player has hidden a direct conversation and has **not** muted it, **When** the
   other person sends a message, **Then** the conversation reappears and the player is
   notified exactly as they would be for any unhidden conversation.
3. **Given** a player has hidden a conversation **and** muted it, **When** a message arrives,
   **Then** the conversation reappears in the inbox but still does not contribute to the
   navigation unread total — the mute is a separate choice and is not disturbed.
4. **Given** two players have both hidden the same group conversation and a third sends a
   message, **When** the message is delivered, **Then** it returns to the inbox of both, and
   of no one else who had not hidden it.
5. **Given** a player has hidden a conversation, **When** a message arrives while they have
   the application open, **Then** the conversation appears in their inbox without a manual
   refresh (FR-019).
6. **Given** a player has hidden a team conversation, **When** somebody joins or leaves that
   team and the conversation records it, **Then** the conversation stays hidden — a roster
   change is not somebody writing.
7. **Given** a player has hidden a conversation and opens it by its direct link, **When**
   they send a message into it themselves, **Then** it returns to their own inbox too.

---

### User Story 2 - Put a conversation back myself (Priority: P2)

A player who hid a conversation and now wants it back opens it and turns hiding off from the
details panel, the same way they would unmute. The control reads as a toggle and shows which
state the conversation is in.

**Why this priority**: It makes the choice explicit and reversible rather than something the
player must wait out, and it costs no new surface — the details panel already carries the
Hide button and already knows the conversation's hidden state. It ranks below User Story 1
because it presumes the player can reach the conversation, which for a hidden group or team
chat means having its direct link.

**Independent Test**: Open a hidden conversation by its link, use the details-panel control,
and confirm it returns to the inbox immediately without reloading the page.

**Acceptance Scenarios**:

1. **Given** a player is viewing a conversation they have hidden, **When** they open the
   details panel, **Then** the control reads as the action that would show it in the inbox
   again, not as "Hide".
2. **Given** a player is viewing a conversation they have **not** hidden, **When** they open
   the details panel, **Then** the control reads as "Hide" and behaves as it does today.
3. **Given** a player uses the control to un-hide a conversation, **When** the change is
   accepted, **Then** the conversation is present in their inbox straight away, without a
   page reload, and the player stays on the conversation they were reading.
4. **Given** a player uses the control to hide a conversation, **When** the change is
   accepted, **Then** they are returned to the inbox and the conversation is absent from it —
   unchanged from today's behaviour.
5. **Given** a player has the same account open in two places, **When** they un-hide a
   conversation in one, **Then** the other converges on the same state, as mute already does.

---

### Edge Cases

- **A message arrives in a conversation the player hid *and* muted.** The conversation
  returns to the inbox; the mute is untouched and continues to keep it out of the unread
  total. Hide and mute are independent (FR-028, FR-029).
- **A message arrives in a hidden *archived* conversation** (a disbanded party or deleted
  team, FR-027). Archived conversations are closed to new messages, so this cannot arise
  through a send; and the line recording the archival is a system line, which does not
  return a conversation (FR-011). A hidden archived conversation stays hidden until the
  player un-hides it.
- **A hidden conversation accumulates only system lines.** It stays hidden indefinitely, and
  its unread count keeps rising unseen — accepted, and the reason FR-011 was decided the way
  it was: those lines are exactly what the player archived the conversation to stop seeing.
  Opening it by link clears them as any read would.
- **A blocked direct conversation that is also hidden.** A block already excludes the
  conversation from the blocker's inbox (FR-031) and prevents the blocked player from
  sending (FR-031). The block rule wins: un-hiding does not return a blocked conversation
  to the inbox, and no message can arrive to trigger a return.
- **A player is removed from a team whose chat they had hidden.** They lose access by the
  roster rule (FR-025); the stored hidden state is irrelevant while they are not a member,
  and if they rejoin, the conversation's hidden state is whatever they last left it as.
- **Un-hiding a conversation the player never hid.** Accepted as a no-op rather than an
  error — the resulting state is the one requested.
- **A message arrives in a hidden conversation the player has never opened.** The
  conversation returns to the inbox with its unread count computed from the player's join
  point (FR-051), exactly as an unhidden conversation would.

## Requirements *(mandatory)*

### Functional Requirements

**Hide is reversible**

- **FR-001**: A player MUST be able to reverse hiding a conversation, restoring it to their
  inbox with its history and unread state intact.
- **FR-002**: The control that hides a conversation MUST present as a two-state toggle,
  showing the current state and the action that would change it, in the same manner as the
  existing mute control.
- **FR-003**: Un-hiding a conversation MUST take effect in the player's inbox immediately,
  without requiring a page reload, and MUST converge across that player's other sessions.
- **FR-004**: Un-hiding MUST leave the player where they are rather than navigating them
  away; hiding MUST continue to return them to the inbox.
- **FR-005**: Hiding and un-hiding MUST NOT change the conversation's muted state, and
  muting and unmuting MUST NOT change its hidden state.
- **FR-006**: Hiding and un-hiding MUST affect only the player who performs it, and MUST NOT
  be observable by any other member of the conversation.

**A conversation returns when it becomes active**

- **FR-007**: When a member sends a message to a conversation, the system MUST clear the
  hidden state for every member of that conversation who had hidden it — the sender included
  (FR-012) — so the conversation returns to their inbox.
- **FR-008**: A conversation returned by FR-007 MUST arrive in the inbox complete: ordered by
  the message that returned it, showing that message as its latest line, and carrying its
  correct unread count.
- **FR-009**: The navigation unread total MUST account for a conversation returned by FR-007
  at the moment it returns — a conversation MUST NOT reappear in the inbox while its unread
  messages are still excluded from the total (FR-018).
- **FR-010**: A conversation returned by FR-007 MUST reach an open session without a manual
  refresh, on the same terms as any other newly delivered message (FR-019, FR-023).
- **FR-011**: Only a message written by a member MUST return a hidden conversation. A
  system-generated line — a member joining or leaving, a conversation becoming archived —
  MUST NOT clear anyone's hidden state.
- **FR-012**: When a player sends a message into a conversation they themselves have hidden,
  that conversation MUST return to the sender's own inbox as well as to any recipient who
  had hidden it. A player MUST NOT be able to post a message into a conversation that is
  absent from their own inbox.

**Amendments to feature 019**

- **FR-013**: Feature 019 **FR-026** MUST be amended so that **mute** is the control offered
  in place of leave in a team or party chat. Hide MUST remain available on every kind of
  conversation but MUST NOT be described as a substitute for leaving.
- **FR-014**: Feature 019 **FR-029** MUST be amended to state that hiding is reversible, both
  by the player and automatically on new activity.
- **FR-015**: The wording presented to players MUST describe hide as tidying a conversation
  away rather than as leaving or removing it, and MUST be available in every language the
  product ships.

**Bounds**

- **FR-016**: This feature MUST NOT add a list, section or filter of hidden conversations to
  the inbox.
- **FR-017**: Who may open, read or write a conversation MUST be unchanged. Hidden state MUST
  continue to be a per-player inbox preference and MUST NOT act as an access control.

### Key Entities

- **Conversation membership state (per player, per conversation)**: already carries the
  player's muted and hidden preferences and their read position. This feature changes when
  the hidden preference is cleared and adds no new state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every conversation a player has hidden is recoverable through the interface —
  there is no conversation kind for which a hidden conversation is permanently unreachable.
- **SC-002**: A player who hid a conversation and receives a new message in it sees that
  conversation in their inbox on their next visit, with an accurate unread count, without
  performing any recovery action.
- **SC-003**: A player who wants a hidden conversation back can restore it in a single action
  from the conversation they are reading.
- **SC-004**: Hide and mute produce independently observable results: all four combinations of
  the two settings behave as their descriptions state.
- **SC-005**: No conversation appears in a player's inbox as a result of this feature that
  they could not already open by its direct link.
- **SC-006**: The inbox contents, ordering and unread total for a player who has hidden
  nothing are identical before and after this feature.
- **SC-007**: An archived conversation stays archived through roster activity alone: no
  sequence of members joining or leaving returns it to the inbox.

## Assumptions

- **The hidden state is stored per player already** and can be cleared as well as set — no new
  data is introduced and no stored data is migrated or discarded.
- **A hidden conversation remains reachable by its direct link**, as it is today; hiding has
  never restricted access. For a hidden group, team or party conversation this link is the
  only route to the details-panel toggle while a hidden-chats surface is out of scope. This is
  an accepted limitation of shipping User Story 2 without issue #222's option 3 — User Story 1
  is what covers the player who has no link to hand.
- **The inbox preview of a returned conversation does not distinguish it** from one that was
  never hidden. There is deliberately no "restored" marker.
- **Existing hidden conversations are not swept on release.** Players who hid something before
  this ships get it back the first time someone writes in it, or by using the new control.
- **Notification behaviour is inherited, not redefined.** A returning conversation notifies
  exactly as an unhidden one would; this feature does not add or suppress any notification.
