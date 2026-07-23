# Feature Specification: Contact the Admins

**Feature Branch**: `027-contact-admins`

**Created**: 2026-07-23

**Status**: Draft

**Input**: User description: "When viewing events or team pages I want to be able to contact the admins of this thing. I might have questions regarding the specific event or team. Ideally we use the chat feature for this. As a player I can 'chat' with an event or a team. Under the hood this acts as a group chat with the admins but ideally this is distinguishable in the chat window so that when I come back I know which group chats are my 'normal' groups and which belong to a team/event. As the normal user the name of the group should be used to clarify this."

## Clarifications

### Session 2026-07-23

- Q: When an event's date has passed but it hasn't been deleted, should its contact thread stay open? → A: Thread stays open after the event date passes; only deletion/cancellation archives it (one archival trigger, same as teams; supports post-event follow-ups).
- Q: When an admin replies in a contact thread, does the player see which admin, or a generic team/event sender? → A: Normal sender attribution — the player sees the replying admin's name, like any group chat (admins are already publicly listed on the team/event page).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ask a team's or event's admins a question (Priority: P1)

A signed-in player is looking at a team page or an event page and has a question — about
joining, about the schedule, about logistics. From that page they choose **Contact admins**,
type their question, and send it. The message reaches everyone who currently administers that
team or event, in a single shared conversation, and the player can see any reply and continue
the exchange from their chat inbox.

**Why this priority**: This is the entire point of the feature — giving a player a direct,
in-product line to the people responsible for a team or event, without needing anyone's
personal contact details or a public comment thread. Without it there is no feature.

**Independent Test**: From a team page (and separately from an event page), a signed-in
non-admin sends a message via **Contact admins**; verify every current admin of that team/event
receives it in a shared conversation and can reply, and the player sees the reply.

**Acceptance Scenarios**:

1. **Given** a signed-in player viewing a team page they do not administer, **When** they open
   **Contact admins**, type a question and send it, **Then** a conversation with that team's
   admin group is created and the message is delivered to every current team admin.
2. **Given** a signed-in player viewing an event page they do not administer, **When** they open
   **Contact admins**, type a question and send it, **Then** a conversation with that event's
   admin group is created and the message is delivered to every current event admin.
3. **Given** an admin has received a contact message, **When** the admin opens the conversation
   and replies, **Then** the requesting player sees the reply in their chat inbox.
4. **Given** a player has already contacted a team, **When** they choose **Contact admins** for
   that same team again, **Then** they are returned to the existing conversation with its full
   history rather than starting a new one.

---

### User Story 2 - Tell contact threads apart from normal chats (Priority: P1)

When the player returns to their chat inbox, they see a mix of their normal one-to-one and group
chats alongside any team/event contact threads. The contact threads are visually distinct: they
carry a distinguishing tag, and each is named after the team or event it concerns (e.g. the team
name or the event title) — so the player instantly knows "this is my question to the Rhein Ravens
admins," not a normal group. Admins, on their side, see the same thread named after the player who
asked, with the same distinguishing tag, so they can tell contact requests apart from their own
team/party chats.

**Why this priority**: The product owner called this out explicitly — a contact thread that looks
identical to a normal group is confusing and defeats the purpose. Distinguishability is a
first-class requirement, not polish, and it is testable independently of message delivery.

**Independent Test**: Create one normal group chat and one team-contact thread for the same
player; verify the inbox renders the contact thread with a distinguishing tag and the team's name
for the player, and with the player's name and the same tag for an admin.

**Acceptance Scenarios**:

1. **Given** a player with both a normal group chat and a team-contact thread, **When** they view
   their chat inbox, **Then** the contact thread shows a distinguishing tag and is labelled with
   the team's name, while the normal group shows no such tag.
2. **Given** an admin who is in a contact thread, **When** they view their chat inbox, **Then**
   the thread shows the same distinguishing tag and is labelled with the requesting player's name.
3. **Given** an event-contact thread, **When** either side views it, **Then** it is labelled with
   the event's title (player's side) / the requesting player's name (admin's side) and carries the
   distinguishing tag.

---

### User Story 3 - Membership follows the admin roster (Priority: P2)

The set of admins reachable through a contact thread always reflects who administers the team or
event **right now**. If an admin is added, they immediately see the thread and its history from
their join onward; if an admin steps down or is removed, they immediately lose access. The
requesting player remains the one fixed non-admin participant.

**Why this priority**: It is a correctness and security property — a former admin must not keep
reading contact messages, and a newly-added admin must be reachable — but the P1 stories deliver
value even while the roster is static, so this is P2.

**Independent Test**: With an open contact thread, add a new admin and confirm they gain access;
remove an admin and confirm they lose access on their next request, without any manual sync step.

**Acceptance Scenarios**:

1. **Given** an open team-contact thread, **When** a new user is granted team admin, **Then** that
   user can see and reply in the thread.
2. **Given** an open event-contact thread with two admins, **When** one admin is removed, **Then**
   that former admin can no longer see the thread or its messages.
3. **Given** a contact thread, **When** membership is inspected, **Then** the requesting player is
   always present and is never treated as an admin of the team/event.

---

### Edge Cases

- **Viewer is already an admin of that team/event**: the **Contact admins** action is not offered
  — they are the admin group and cannot meaningfully contact themselves.
- **No message ever sent**: opening **Contact admins** and leaving without sending does not create
  a persisted thread or place anything in the admins' inboxes (thread is created on first send).
- **Underlying team is deleted / event is cancelled or deleted**: the thread is archived — history
  stays readable to its existing members, but no new messages can be posted (mirrors chat's
  one-way archival for deleted teams/parties).
- **Rapid re-sends / spam**: starting a brand-new contact thread is subject to the same
  new-conversation send rate limit as any other new chat, so admins cannot be mass-messaged.
- **Player is banned/suspended**: handled by the platform-wide access rules already governing chat;
  a blocked or banned player cannot use chat at all.
- **Blocking**: blocking is a one-to-one direct-message concept and does not apply to contact
  threads; there is no per-admin block within a contact thread.
- **Concurrent first sends**: if the same player sends two first messages to the same team/event at
  once (e.g. two tabs), exactly one thread results — the pair (player, team/event) is unique.
- **Last admin**: teams and events always retain at least one admin (last-admin guard), so a
  contact thread's admin side is never empty while the team/event exists.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST offer a **Contact admins** action on team pages and on event pages to
  any signed-in player who is **not** an admin of that team/event.
- **FR-002**: The system MUST NOT offer the **Contact admins** action to a player who is already an
  admin of the team/event in question.
- **FR-003**: When a player sends their first contact message for a team/event, the system MUST
  create a single conversation between that player and the team's/event's admin group and deliver
  the message to every current admin.
- **FR-004**: The system MUST persist **at most one** contact conversation per (player, team) pair
  and per (player, event) pair; a subsequent **Contact admins** action for the same target MUST
  return the existing conversation, preserving its history.
- **FR-005**: The system MUST create the contact conversation only upon the first sent message —
  merely opening the compose view MUST NOT create a thread or notify admins.
- **FR-006**: The admin-side membership of a contact conversation MUST be derived live from the
  team's/event's current admin roster on every access decision, so that granting or revoking admin
  changes reachability immediately with no separate synchronization step.
- **FR-007**: The requesting player MUST always be a participant of their contact conversation and
  MUST never be treated as an admin of the team/event by virtue of it.
- **FR-008**: The system MUST render contact conversations as visually distinguishable from normal
  direct/group chats in the chat inbox, using a dedicated tag alongside the existing team/party
  tags.
- **FR-009**: For the requesting player, the system MUST label the contact conversation with the
  team's name or the event's title.
- **FR-010**: For an admin, the system MUST label the contact conversation with the requesting
  player's display name **and** the team/event it concerns (e.g. "Ada K. · Rheinfeuer"), so an admin
  of several teams/events can tell inquiries apart at a glance.
- **FR-011**: All participants MUST be able to send and read messages in an active contact
  conversation, with delivery to other participants in real time, reusing the platform's existing
  chat messaging, read-state, and real-time behavior.
- **FR-012**: The system MUST enforce conversation access on the server for every read, send, and
  real-time delivery; a non-member request MUST be indistinguishable from a request for a
  non-existent conversation (no confirmation that the conversation exists).
- **FR-013**: The system MUST apply the platform's existing new-conversation send rate limit when a
  player starts a new contact conversation.
- **FR-014**: When the underlying team is deleted or the underlying event is cancelled/deleted, the
  system MUST archive the associated contact conversation(s): existing members retain read access to
  the history, and no further messages can be posted.
- **FR-015**: Archiving a contact conversation MUST preserve the membership needed to read its
  history even after the underlying team/event roster no longer exists (the membership at archive
  time is retained so former participants keep read access).
- **FR-016**: Blocking MUST NOT apply to contact conversations; there is no per-participant block
  within a contact thread.
- **FR-017**: A participant MUST be able to mute and/or hide a contact conversation from their own
  inbox, but MUST NOT be able to "leave" it (membership is fixed for the player and derived for
  admins).
- **FR-018**: Unread contact messages MUST surface through the existing chat unread indicators and
  MUST NOT create separate notification/alert entries.
- **FR-019**: A newly-added admin MUST see the contact conversation and the history from their join
  onward; they MUST NOT be shown messages that predate their admin grant.
- **FR-020**: Messages in a contact conversation MUST be attributed to their individual sender
  (normal group-chat sender attribution); an admin's reply MUST be shown to the requesting player
  under that admin's own display name, not anonymized behind a generic team/event sender.

### Key Entities *(include if feature involves data)*

- **Contact conversation**: A chat conversation of a new kind representing a player's line to a
  team's or event's admins. Holds a reference to the target team **or** event, and to the fixed
  requesting player. Its admin-side membership is not stored but derived from the target's admin
  roster. Uniquely identified by (player, target) so it is reused rather than duplicated. Carries
  the standard conversation lifecycle state (active vs. archived).
- **Team admin roster**: The set of a team's members holding the admin role — the live source of
  the admin-side membership for a team-contact conversation.
- **Event admin roster**: The set of a event's admins — the live source of the admin-side
  membership for an event-contact conversation.
- **Contact message**: An ordinary chat message posted within a contact conversation; reuses the
  existing message, ordering, read-marker, and system-line concepts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From a team or event page, a signed-in non-admin can send their first question to the
  admins in under 30 seconds and three interactions or fewer (open action → type → send).
- **SC-002**: 100% of a team's/event's current admins receive a newly sent contact message, and
  0% of former admins retain access after losing their admin role.
- **SC-003**: In the chat inbox, a user can correctly distinguish contact threads from normal
  direct/group chats at a glance, via the tag and the team/event- or player-based name, with no
  ambiguity in usability testing.
- **SC-004**: Reopening **Contact admins** for a team/event a player has already contacted returns
  the same conversation 100% of the time (no duplicate threads are ever created for a pair).
- **SC-005**: After a team is deleted or an event is cancelled, its contact conversations remain
  readable to prior members and reject all new messages, with no orphaned or unreadable threads.

## Assumptions

- **Builds on feature 019 (Chat).** The messaging pipeline, inbox, read-state, real-time transport,
  archival-snapshot behavior, "404 not 403" non-member handling, and tag rendering already exist and
  are reused; this feature adds a new conversation kind and the two entry points.
- **Thread-on-first-send** follows feature 022's lazy-creation precedent for direct messages:
  opening the compose view is transient; the thread is persisted only when the first message is sent.
- **Admin definitions are the existing ones**: a team admin is a team member with the admin role; an
  event admin is an event admin record. Both models already guarantee at least one admin exists.
- **A past-but-not-deleted event keeps its contact thread open** — only deletion/cancellation of the
  event (or deletion of the team) archives the thread; a date passing does not, since players may
  have follow-up questions. (Candidate for `/speckit-clarify` if the owner wants time-based closure.)
- **Blocking is intentionally out of scope** for contact threads, consistent with chat treating
  blocks as a direct-message-only mechanism.
- **No new notification categories** are introduced; contact messages ride the existing chat unread
  badge, consistent with chat raising no alert rows.
- **UI follows DESIGN.md** for the entry-point button, the inbox tag, and any empty/loading/error
  states; the distinguishing tag is a new variant alongside the existing TEAM/PARTY tags.
