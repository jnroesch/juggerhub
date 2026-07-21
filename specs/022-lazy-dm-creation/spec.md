# Feature Specification: Lazy Direct-Message Creation

**Feature Branch**: `022-lazy-dm-creation`

**Created**: 2026-07-21

**Status**: Draft

**Input**: User description: "When I start a chat but don't send any message, then the chat should disappear from my chats again, otherwise I could pollute the list with empty chats."

## Context & Amendment

This **amends feature 019 (chat)**. Today, starting a direct message eagerly creates
the conversation the moment you open it, and that empty thread immediately appears in
your inbox with a blank preview. Opening a chat with someone — especially via the
one-click **Message** action added in feature 021 — and then leaving without typing
leaves an empty conversation behind, cluttering the inbox.

**Owner decision**: switch direct messages to **lazy creation** — a direct
conversation comes into existence only when the first message is sent. Opening a chat
with someone you don't yet message is a transient *compose* state that persists
nothing. Group chats and team/party chats are unaffected.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Opening a chat and leaving leaves nothing behind (Priority: P1)

A signed-in member opens a direct chat with another player (from that player's
profile, or from the "new message" flow), reads the compose view, then navigates away
without sending anything. Nothing is created: their inbox is exactly as it was, and
the other player is never notified or shown a thread.

**Why this priority**: This is the whole point — an unsent chat must not pollute the
inbox. It is the observable outcome the change exists to deliver.

**Independent Test**: Note the inbox contents, open a compose chat with a player you
have never messaged, leave without sending, return to the inbox — it is unchanged, and
the other player's inbox is unchanged too.

**Acceptance Scenarios**:

1. **Given** I have no existing conversation with a player, **When** I open a chat with
   them and leave without sending, **Then** no conversation exists, my inbox shows no
   new entry, and my unread count is unchanged.
2. **Given** I opened and abandoned a compose chat with that player, **When** the other
   player opens their inbox, **Then** they see no thread and received no notification.
3. **Given** I abandoned a compose chat with a player, **When** I open a chat with the
   same player again, **Then** I get a fresh compose state (still nothing persisted),
   with no duplicate or leftover thread.

---

### User Story 2 - Sending the first message creates and delivers the thread (Priority: P1)

From the compose state, the member types and sends a first message. Now the direct
conversation is created and behaves exactly as a direct conversation does today — it
appears in both people's inboxes, carries the message, and is reachable again later.

**Why this priority**: The counterpart to US1 — lazy creation is only correct if the
first send reliably materialises a normal, persistent conversation.

**Independent Test**: Open a compose chat with a never-messaged player, send a message,
and confirm the conversation now appears in both inboxes with that message as the
latest, and can be reopened.

**Acceptance Scenarios**:

1. **Given** I am composing to a player I have no conversation with, **When** I send the
   first message, **Then** a direct conversation is created, my message is in it, and it
   appears at the top of my inbox.
2. **Given** I sent that first message, **When** the other player opens their inbox,
   **Then** the conversation is present with my message and their normal unread
   indication.
3. **Given** a first message is being sent from two of my devices at the same moment,
   **When** both are processed, **Then** exactly one direct conversation exists (no
   duplicate), and both messages land in it.

---

### User Story 3 - Existing conversations open directly (Priority: P2)

Opening a chat with a player the member **already** has a direct conversation with goes
straight to that existing thread — never a compose state, never a second conversation.

**Why this priority**: A regression guard — lazy creation must not disturb the common
case of continuing an existing conversation.

**Independent Test**: With an existing DM, use the profile Message action (or new-message
flow) for that same player and confirm you land in the existing thread with its history,
and no new conversation is created.

**Acceptance Scenarios**:

1. **Given** I already have a direct conversation with a player, **When** I open a chat
   with them, **Then** I land in the existing conversation with its history, and no new
   conversation is created.

---

### Edge Cases

- **Blocked at send time**: if a block is in force (either direction) when the first
  message is sent, the send is refused with a friendly message and no conversation is
  created — a block cannot be circumvented by composing then sending.
- **Target becomes unavailable between compose and send** (account deleted/banned): the
  send fails gracefully and nothing is created.
- **Concurrent first sends**: two devices (or the two participants) sending a first
  message near-simultaneously resolve to exactly one conversation, not two.
- **No side effects in draft**: while composing (pre-send), nothing that presumes a
  conversation is emitted — no typing indicator to the other person, no read receipts,
  no unread changes.
- **Pre-existing empty conversations**: direct conversations that were created empty
  *before* this change are not addressed here (see Assumptions) — a separate cleanup can
  follow if desired.
- **Repeated opens**: opening and abandoning compose any number of times never
  accumulates rows or inbox entries.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Opening a direct chat with a player the viewer has **no** existing
  conversation with MUST NOT create any persisted conversation, nor make anything appear
  in either person's inbox, unread count, or notifications.
- **FR-002**: A direct conversation MUST be created only upon the viewer sending the
  **first message**; upon that send it MUST appear in both participants' inboxes and
  carry the message, exactly as direct conversations do today.
- **FR-003**: If a direct conversation already exists with the target player, opening a
  chat with them MUST go directly to that existing conversation — no compose state and no
  new conversation.
- **FR-004**: Abandoning a compose (draft) direct chat without sending MUST leave no
  trace: no conversation, no inbox entry, no unread change, no notification to the other
  player.
- **FR-005**: Block and reach rules MUST be enforced at send time — a first-message send
  that would create a direct conversation between a blocked pair MUST be refused and MUST
  NOT create a conversation.
- **FR-006**: The system MUST preserve the one-direct-conversation-per-pair guarantee on
  the create-on-send path: concurrent first sends (including from two of the sender's own
  devices, or from both participants at once) MUST resolve to exactly one conversation.
- **FR-007**: Group, team, and party conversations MUST be unaffected — their
  creation/materialisation behavior is unchanged by this feature.
- **FR-008**: The profile **Message** action (feature 021) and the chat "new message"
  flow MUST route into the compose state when no conversation exists, and to the existing
  conversation when one does — neither may create an empty conversation.
- **FR-009**: No empty (message-less) direct conversation may ever be visible in any
  inbox as a result of normal use after this change.

### Key Entities *(include if feature involves data)*

- **Direct conversation**: a one-to-one message thread. After this change it is brought
  into existence by the first message, not by opening the chat. Its one-per-pair
  uniqueness is unchanged.
- **Compose (draft) state**: a transient intent to message a specific player before any
  message is sent. It is not persisted and has no inbox presence; it resolves either to a
  newly created conversation (on send) or to nothing (on abandon).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Opening a chat with a never-messaged player and leaving without sending
  results in **0** new conversations and **0** new inbox entries for both people.
- **SC-002**: Sending a first message results in exactly **1** conversation, visible to
  both participants with the message present.
- **SC-003**: **100%** of concurrent first-send races resolve to a single conversation —
  no duplicate direct conversation is ever created for a pair.
- **SC-004**: Opening a chat with a player the viewer already messages lands in the
  existing thread in **100%** of cases, creating no new conversation.
- **SC-005**: After this change, **no** empty message-less direct conversation appears in
  any inbox through normal use (0 occurrences).
- **SC-006**: Group, team, and party chat behavior is unchanged (existing behavior and
  coverage continue to pass).

## Assumptions

- **DMs only**: The change is limited to direct messages. Group chats (deliberately
  named, members added) and team/party auto-chats (roster-derived, materialised on first
  inbox view) keep their current creation behavior.
- **Reach unchanged**: Who may be messaged is unchanged (open reach, feature 019). This
  feature changes only *when* the conversation is created, and reaffirms block
  enforcement at the moment of creation (send).
- **Pre-existing empty DMs are out of scope**: Any empty direct conversations created
  before this change are not migrated or cleaned by this feature. If their removal is
  desired, it is a separate follow-up.
- **Compose is addressed by the target player**: The compose state identifies the target
  player; exactly how it is addressed and presented is an implementation concern for
  planning, provided it persists nothing until send.
- **Amends 019**: This is a deliberate amendment to feature 019; the associated 019
  documents should note the create-on-send behavior so the source-of-truth stays
  accurate.
