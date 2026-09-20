# Feature Specification: Chat Push Notifications

**Feature Branch**: `056-chat-push`

**Created**: 2026-09-20

**Status**: Draft

**Input**: GitHub issue #309 — "Chat reaches nobody who isn't on the site: no push, no email, no Alerts row". Builds on feature 054 (PWA shell, GH #307, merged) and feature 055 (push channel, GH #308, merged).

## The gap, concretely

Chat is the only feature in the product with real-time urgency and no reach-out path at all. A
player who is not on the site when a message arrives learns about it the next time they open the
app. Not a notification, not an email, nothing.

Feature 055 gave the platform a push channel, and nine kinds of notification now reach a player's
phone. A chat message — the one thing in the product that is actually time-critical — still does
not.

## Context: what this amends, and what it deliberately does not

- **Feature 019** decided *"Chat badge only — no Alerts rows"* (FR-051, FR-051a): chat is its own
  inbox and introduces no notification type, no Alerts entry and no preference category. **The
  part of that decision that mattered is upheld here, and one clause of it is amended.** Nothing
  in this feature writes a row to the Alerts inbox, no chat message ever appears there, and no new
  notification type is introduced — all of that stands. What changes is the last clause: chat gains
  a **preference category**, because a player must be able to say "not chat, on my phone" in the
  place where they already say it about everything else (FR-027). 019 gets an amendment callout in
  the same shape features 022, 046 and 048 each added.
- **Feature 055** anticipated this exact feature and shaped its architecture around it: the push
  dispatch seam sits *below* the notification store, precisely so chat could reach a device without
  writing a notification row. This feature is the first user of that decision.
- **Feature 048** settled what mute and hide mean: **mute is "don't bother me", hide is "archive"**,
  one job each. It also made mute the named stand-in for leaving a team chat, so players are
  already relying on it. A notification that ignores mute would break a promise the product has
  already made.
- **Feature 047** encrypts message bodies at rest, and its reasoning — that chat content deserves
  more protection than the rest of the product's data — bears directly on what a push payload may
  carry. The owner's decision to include a preview (see Clarifications) means message text now
  leaves the platform, which the privacy policy has to say (FR-029).

## Clarifications

### Session 2026-09-20

- Q: What may a chat push payload say, given that it passes through Google's, Apple's or Mozilla's
  push service and is rendered on a lock screen? → A: **Sender plus a preview of the message.** A
  direct message reads "Anna Meyer" / "Are we training tomorrow?"; a group, team or party message
  names the conversation and prefixes the sender. Name-only and fully generic payloads were both
  declined: a notification that does not say what was said does not save the trip into the app.
  **This is a deliberate widening of feature 055's rule** that a notification names its subject
  without reproducing it, and it carries two obligations that are requirements here, not
  footnotes — the privacy policy must describe it (FR-029), and nothing about it may be logged
  (FR-021). Hiding previews per device or per account was not asked for and is out of scope.
- Q: How does a player turn chat notifications off for good, rather than one conversation at a
  time? → A: **Chat gets its own entry in the notification preferences matrix**, with a Push
  toggle that behaves exactly like every other Push toggle — including "no stored choice means
  on". Chat is its own feature and belongs in the same list as the others rather than in a
  control of its own shape. Consequence: **019's FR-051a is amended** to permit this one new
  preference category. The half of FR-051a that matters is untouched and restated — chat still
  introduces no notification type and still writes nothing to the Alerts inbox. The row's In-app
  and E-mail cells are therefore not offered: chat has no Alerts row by design, and
  email-on-missed-message is out of scope for this feature.
- Q: How long must a message stay unread before it is worth a notification? → A: **30 seconds.**
  Closest to feeling instant, which is the point of singling chat out. The accepted cost is more
  notifications reaching someone who was about to open the conversation on another screen; two
  minutes and five minutes were declined as turning a message into a digest.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A message you missed reaches your phone (Priority: P1)

A player has turned on notifications for their phone. They are away from the app — phone in a
pocket, laptop closed. Someone writes to them: a direct message, or a post in their team's chat.
A short while later their phone shows a notification. Tapping it opens the app directly on that
conversation, where the message is waiting.

**Why this priority**: This is the whole feature. Without it, chat continues to reach nobody who
is not already looking at the site, which is the most-felt gap in the product.

**Independent Test**: Enable notifications on a device, sign in as a second player, send a message,
leave the first account's session untouched, and confirm a notification arrives after the quiet
delay and opens the right conversation.

**Acceptance Scenarios**:

1. **Given** a player with a device enabled for notifications and no open session, **When** another
   member sends them a direct message and the quiet delay passes without the message being read,
   **Then** the player's device shows one notification headed by the sender's name and showing
   what they wrote.
2. **Given** that notification, **When** the player taps it, **Then** the app opens on the
   conversation the message was sent to.
2a. **Given** a message in a team conversation, **When** the notification is shown, **Then** it is
   headed by the team's chat name and its text names the sender before the message, so a member of
   two team chats can tell which one is talking.
2b. **Given** a message longer than the preview bound, **When** the notification is shown, **Then**
   it is cut short with a visible indication that it continues.
3. **Given** a player reading the conversation in a browser, **When** a message arrives and they
   read it before the quiet delay passes, **Then** no notification is sent to any of their devices.
4. **Given** a player with a device enabled, **When** they themselves send a message, **Then** their
   own devices receive nothing.
5. **Given** a team conversation where four messages arrive in quick succession, **When** the player
   is away throughout, **Then** their device shows **one** notification for that conversation, not
   four.
6. **Given** two different conversations each receive a message, **When** the player is away,
   **Then** their device shows one notification per conversation.
7. **Given** a player whose stored language is German, **When** a notification is sent to them,
   **Then** it is written in German regardless of the sender's language.

---

### User Story 2 - Muting a conversation keeps its promise (Priority: P1)

A player is in a team chat that talks all day. They muted it — the product's own answer to "I want
to stay on this team but I don't want this chat bothering me". Messages keep arriving and their
phone stays silent, exactly as it did before notifications existed.

**Why this priority**: Equal to US1 rather than below it. Mute is an existing promise, and feature
048 deliberately made it the stand-in for leaving a team chat. Shipping US1 without this turns a
working control into a broken one for every player already using it — a regression, not a missing
feature.

**Independent Test**: Mute a conversation, have someone post to it, wait past the quiet delay, and
confirm no device receives anything while an unmuted conversation still does.

**Acceptance Scenarios**:

1. **Given** a player who has muted a conversation, **When** a message arrives in it and goes
   unread past the quiet delay, **Then** none of their devices receive a notification.
2. **Given** a player who has muted one conversation but not another, **When** a message arrives in
   each, **Then** only the unmuted one produces a notification.
3. **Given** a player who unmutes a conversation, **When** the next message arrives, **Then**
   notifications resume for it.
4. **Given** a player who archives a conversation during the quiet delay, **When** the delay passes,
   **Then** no notification is sent for the message that arrived.
5. **Given** a player who left a group conversation, **When** the group keeps talking, **Then**
   they receive no notifications from it.
6. **Given** two players who have blocked one another, **When** one writes into their former direct
   conversation, **Then** the other receives no notification.

---

### User Story 3 - Turning chat notifications off altogether (Priority: P2)

A player wants notifications for invitations and training changes but not for chat, or wants
nothing from chat on their phone while keeping the chat badge in the app. They go to notification
settings, where Chat now sits in the same list as invites, team news, trainings and events, and
turn its Push toggle off. Nothing about chat inside the app changes.

**Why this priority**: Below the first two because the per-conversation control already exists and
covers the common case, and because US1 and US2 together are a coherent shippable product. But it
is the first thing a player will look for once notifications start arriving, and a notification a
player cannot switch off is a reason to switch all notifications off.

**Independent Test**: Turn chat notifications off in settings, have someone send a message, wait
past the quiet delay, and confirm nothing arrives while a non-chat notification still does.

**Acceptance Scenarios**:

1. **Given** a player who has turned chat notifications off, **When** any chat message arrives for
   them, **Then** none of their devices receive a notification.
2. **Given** that same player, **When** a team invitation or training change occurs, **Then** those
   notifications still reach their devices.
3. **Given** a player who has never touched the setting, **When** a chat message arrives, **Then**
   a notification is sent — the default is on, the same as every other category.
4. **Given** a player who turns chat notifications back on, **When** the next message arrives,
   **Then** notifications resume.
5. **Given** a player looking at the notification settings, **When** they find the Chat entry,
   **Then** it offers a Push toggle only, and it is evident that In-app and E-mail are not
   available for chat rather than merely switched off.
6. **Given** a player who has turned chat notifications off, **When** they open the app, **Then**
   the chat badge, the inbox and their unread messages are exactly as they were.

---

### Edge Cases

- **A player reading on their laptop while their phone is in their pocket.** The product cannot see
  that a window is open — browsers require every push to be visible, and a connection-presence check
  would depend on infrastructure that is not deployed (GH #219). The quiet delay is the mechanism:
  a message read on any device before the delay passes produces nothing. A player with the app open
  but the conversation closed has *not* read the message, and will be notified. That is deliberate.
- **A message deleted before the delay passes.** Nothing is sent. The notification would name a
  message that no longer exists.
- **A conversation archived (closed) between the send and the delay.** Nothing is sent.
- **A member with no device enabled.** Costs nothing: no delivery is attempted for them at all.
- **A device whose subscription has expired or been revoked.** Handled by the existing channel —
  the subscription is dropped and no error reaches the player.
- **Notification dispatch is unavailable or failing.** Sending and reading messages are entirely
  unaffected. A player who could send a message a minute ago can still send one.
- **A backlog after an outage.** Messages older than the maximum age are never notified about, so a
  restored service does not buzz every phone on the platform with yesterday's conversation.
- **Several servers running at once.** A message produces at most one notification per device,
  regardless of how many servers are running.
- **A group chat where the member is the only one left.** No recipients, nothing sent.
- **A message consisting only of attachments, with no text.** Still worth a notification; it says
  something was sent rather than showing an empty preview.
- **A message whose text cannot be read back.** The notification still goes out, naming the sender
  and the conversation without a preview — the same posture the app already takes when it shows a
  placeholder in place of an unreadable message rather than failing the conversation.
- **A sender whose profile is gone or hidden.** The notification uses the same neutral stand-in the
  conversation itself shows, never a blank name.
- **A very long message, or one that is a wall of text.** Cut to the preview bound with a visible
  indication that it continues.
- **An admin-contact conversation** (a member asking a team's or event's admins something) behaves
  like any other conversation: its members are notified under the same rules.

## Requirements *(mandatory)*

### Functional Requirements

#### Delivering the notification

- **FR-001**: The system MUST deliver a push notification to a member's enabled devices when a
  message written by another member in a conversation they belong to has remained unread for a
  quiet delay of **30 seconds**. The delay MUST be configurable, with 30 seconds as the safe
  built-in default.
- **FR-002**: The notification MUST NOT be sent as part of the request that sent the message.
  Sending a message MUST NOT wait for any notification work, and MUST NOT become measurably slower
  than it is today.
- **FR-003**: A failure anywhere in notification delivery MUST NOT affect sending, reading, or any
  other chat behaviour. A message is delivered to the conversation whether or not any notification
  leaves the building.
- **FR-004**: Chat notifications MUST NOT create an entry in the Alerts inbox, MUST NOT introduce a
  new notification type, and MUST NOT change what the Alerts inbox contains (feature 019, FR-051
  and FR-051a).
- **FR-005**: Opening a chat notification MUST take the player to the conversation the message was
  sent to.
- **FR-006**: The notification MUST be written in the recipient's own stored language, never the
  sender's and never the language of whatever caused it.
- **FR-007**: A member with no enabled device MUST cost nothing: no delivery MUST be attempted on
  their behalf.

#### Who does not get one

- **FR-008**: A member MUST NOT be notified about a message they sent themselves.
- **FR-009**: A member MUST NOT be notified about a message they have already read.
- **FR-010**: A member who has **muted** the conversation MUST NOT be notified about anything in
  it, for as long as it stays muted.
- **FR-011**: A member for whom the conversation is **hidden** at the moment eligibility is decided
  MUST NOT be notified. (Note: a member-written message already returns an archived conversation to
  the inbox — feature 048, FR-007 — so in practice this bites only when a member archives the
  conversation during the quiet delay, which is precisely when they have just said they do not want
  to hear about it.)
- **FR-012**: A member who has **left** a group conversation MUST NOT be notified about it.
- **FR-013**: Where two members have blocked one another, neither MUST be notified about the
  other's messages.
- **FR-014**: A member MUST NOT be notified about messages sent before they joined the conversation,
  applying the same rule the unread badge and the inbox already apply.
- **FR-015**: System lines — a member joined, left, or the conversation was archived — MUST NOT
  produce a notification, matching the existing rule that they neither bump the conversation's
  ordering nor return an archived conversation to the inbox.
- **FR-016**: A message that has been deleted before eligibility is decided MUST NOT produce a
  notification.
- **FR-017**: A message in an archived (closed) conversation MUST NOT produce a notification.

#### What it says

- **FR-018**: The notification MUST identify which conversation the message arrived in, so a player
  with several conversations can tell them apart without opening the app. For a direct message the
  conversation is the other person, so naming the sender satisfies this.
- **FR-019**: The notification MUST carry the sender's name and a preview of the message text. For
  a direct message the heading is the sender and the text is the message. For a group, team, party
  or admin-contact conversation the heading is the conversation's name and the text is the sender
  followed by the message, so a member of several team chats can tell which one is talking.
- **FR-020**: The preview MUST be bounded in length. A message longer than the bound is cut with a
  visible indication that it continues, and never sent whole.
- **FR-021**: The notification MUST NOT include anything the recipient is not entitled to see, and
  MUST NOT reveal anything about a conversation the recipient is not a member of.
- **FR-021a**: A message whose text cannot be read back MUST fall back to naming the sender and the
  conversation without a preview, and MUST NOT be skipped. (Feature 047 already renders an
  unreadable message as a placeholder in the app rather than failing the conversation; the same
  posture applies here.)
- **FR-021b**: A message carrying only attachments and no text MUST still produce a notification,
  saying that something was sent rather than showing an empty preview.
- **FR-021c**: Message content, sender names and conversation names MUST NOT appear in any log,
  error report, metric or diagnostic produced by this feature.

#### Not stacking up

- **FR-022**: Several messages arriving in the same conversation while a member is away MUST result
  in **one** notification on their device, not one per message. A later message replaces the
  earlier notification rather than adding to it.
- **FR-023**: Messages in *different* conversations MUST produce separate notifications, so a
  player can see that two people are waiting on them.
- **FR-024**: Each message MUST be considered for notification at most once. A message that has
  been evaluated MUST NOT be re-evaluated later, whether or not a notification was sent, and
  whether or not more than one server is running.
- **FR-025**: A message older than a bounded maximum age MUST NOT produce a notification, so an
  interruption in service is not followed by a flood of stale notifications.

#### Switching it off

- **FR-026**: A member MUST be able to stop chat notifications for a single conversation without
  leaving it, through the existing mute control (FR-010 is how that is honoured).
- **FR-027**: The notification preferences matrix MUST gain a **Chat** entry whose Push toggle
  governs every chat notification for that member, and which behaves exactly like the existing
  entries — including that a member who has never touched it is treated as on.
- **FR-028**: The Chat entry MUST NOT offer an In-app or an E-mail toggle, and the matrix MUST make
  clear that those are not choices rather than choices that were left off. Chat has no Alerts row
  by design (FR-004), and email for a missed message is not part of this feature.
- **FR-028a**: Turning the Chat entry off MUST NOT change any of the nine existing notification
  categories, and changing those categories MUST NOT change chat notifications.
- **FR-028b**: Turning the Chat entry off MUST NOT change anything a member sees inside chat: the
  unread badge, the inbox and the conversation behave exactly as before. It governs devices only.

#### Saying so in the privacy policy

- **FR-029**: The privacy policy MUST describe that the content of messages may be transmitted to
  the push service the member's own browser nominates, in the same generic, category-of-data terms
  the rest of the document uses. This is not optional and not deferrable: feature 055 recorded that
  a push service is chosen by the player's browser and operates under no contract with the
  platform, which the policy's processor section explicitly does not cover. All three locales are
  updated together, with German authoritative.

### Key Entities *(include if feature involves data)*

- **Chat notification eligibility**: not a stored thing but a decision, made per member per message
  at the moment the quiet delay expires, from state that already exists — read position, mute,
  hide, membership, join date, blocks.
- **Notification consideration record**: the platform's memory that a given message has already
  been considered, so it is never considered twice (FR-024). It records that the decision was made,
  not what was decided, and carries no message content.
- **Chat notification preference**: a new entry in the existing preferences matrix, stored exactly
  as the other entries are — a choice is recorded only when a member changes it, and its absence
  means on.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A player who is away from the app and has a device enabled is told about a message
  they have not read, within a minute of the quiet delay passing, without opening the app.
- **SC-002**: Sending a message takes no longer than it did before this feature — measured at the
  send request, the added time is indistinguishable from zero.
- **SC-003**: A player who is reading a conversation receives no notification for what they are
  reading, in 100% of cases where the message is read before the quiet delay passes.
- **SC-004**: A muted conversation produces zero notifications, in 100% of cases, for as long as it
  is muted.
- **SC-005**: Ten messages arriving in one conversation while a player is away produce exactly one
  visible notification on their device.
- **SC-006**: The Alerts inbox contains exactly the same kinds of entry after this feature as
  before it — no chat message ever appears there.
- **SC-007**: Every chat message produces at most one notification per enabled device, verified with
  more than one server running.
- **SC-008**: Chat and reading remain fully available while notification delivery is failing —
  every chat behaviour that worked before continues to work.
- **SC-009**: A player can silence chat notifications, for one conversation or for all of them,
  without leaving a team or a conversation, and doing so changes nothing they see inside the app.
- **SC-010**: A player can tell from the notification alone who wrote and what about, without
  opening the app, in every conversation kind the product has.
- **SC-011**: No message text, sender name or conversation name can be found in any log or
  diagnostic the platform produces, verified by searching them after exercising the feature.
- **SC-012**: The published privacy policy describes that message content may reach the push
  service the member's browser nominates, in all three languages, before the feature is enabled in
  any environment where real members can receive notifications.

## Assumptions

- **The push channel is in place.** Devices, permission, subscriptions, the on-device notification
  display and the outbound delivery path were all built by feature 055 and are reused unchanged.
  This feature adds no new outbound integration and no new kind of network call.
- **Presence is approximated by reading, not observed.** The product does not know whether a window
  is open. "Still unread after the quiet delay" is the stand-in, and it is deliberately preferred
  over a live connection check, which would depend on infrastructure that is not deployed anywhere
  (GH #219) and which would still not let a notification be suppressed — browsers require every
  push to be visible.
- **Notification dispatch is best-effort.** Chat's durable record is the conversation itself. A
  notification that is not delivered is a missed convenience, never lost data — every notification
  has an in-app equivalent that is still there when the player next opens the app.
- **Conversation naming follows the app.** Whatever a conversation is called in the inbox is what a
  notification calls it, so the two agree.
- **The preview bound is short.** FR-020's limit is around a hundred characters — enough for a
  question, well under what any lock screen shows, and small enough that a long message is not
  reproduced in full outside the platform.
- **The preview is not separately switchable.** A player who does not want message text on their
  lock screen turns chat notifications off, or mutes the conversation. A "hide previews" control
  is a plausible later ask and is recorded as out of scope, not rejected.
- **The Chat entry sits with the others.** It is an entry in the same preferences list, not a
  control of its own shape somewhere else on the page.
- **Maximum age has a sensible bound.** FR-025's limit is a small number of hours, in the same
  spirit as the existing rule that a push service should not hold an undelivered notification for
  long: a conversation from yesterday is not worth a buzz today.
- **Per-conversation levels are not offered.** "All messages / only mentions / nothing" is the
  obvious next ask, but chat has no mentions today, so the honest first version is on or off,
  riding the existing mute control.
- **No new device-facing contract.** The four fields a device already receives — heading, text,
  destination, collapse key — are enough; the on-device behaviour built by feature 054 is unchanged.

## Out of Scope

- Email notification for a missed message, and digests of any kind.
- Mentions, and per-conversation notification levels beyond on and off.
- Suppressing a notification because a live connection exists (see the presence assumption above,
  and GH #219).
- A managed list of devices — feature 055 settled that as "this device, on or off".
- Any change to what the Alerts inbox contains, or to the behaviour of the four existing
  notification categories and their nine producers.
- An In-app or E-mail toggle for chat. Neither has a producer, and building one is a separate
  decision — the In-app one would reverse 019's Alerts-row decision outright.
- A "hide previews" control, per device or per account. Chat notifications are on or off.
- Notification sounds, vibration patterns, or per-conversation notification appearance.
- Reply-from-the-notification and other notification actions.
