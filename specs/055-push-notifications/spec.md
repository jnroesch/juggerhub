# Feature Specification: Push Notifications

**Feature Branch**: `055-push-notifications`

**Created**: 2026-09-19

**Status**: Draft

**Input**: GitHub issue #308 — "Notifications have no push channel: NotificationChannel stops at In-app + Email". Builds on feature 054 (PWA shell, GH #307, merged). Chat push is GH #309 and is out of scope here.

## Context: what this amends

- **Feature 011** built the notification preference matrix with two channels and recorded push as a
  deferral, not a rejection: *"Web push … is out of scope — no push infrastructure exists; it can be
  added later behind the same model."* The wireframe already drew the third column. This feature
  adds it, and the deferral note in the channel's own description stops being true.
- **Feature 054** shipped the manifest and the background worker and deliberately shipped no words
  about installing, recording that *"the install affordance, its copy, and its UI review belong to
  #308, where the player has a reason to install"*. That debt is paid here: the reason is
  notifications, and the affordance lives beside the switch that turns them on.
- **Feature 019** decided chat writes no notification rows. Nothing here changes that, and nothing
  here sends push for a chat message. Chat push is #309.

## The gap, concretely

An in-app notification is a row and a badge. If the player's tab is not open, they find out the
next time they visit. Email reaches off-site but is the wrong instrument for "your training
tomorrow was cancelled": it is slow, it is easy to miss, and for a volunteer community app it is
the only reach the product currently has.

The browser has been able to receive a push since 054 merged. Nothing sends one, nothing asks the
player whether they want one, and there is nowhere to say which kinds they want.

## Clarifications

### Session 2026-09-19

- Q: Preferences are per account, a push subscription is per device. How does the Push column
  work? → A: **A device section plus a normal per-category column.** Settings gains a
  "notifications on this device" section holding the enable control and the current device's
  state; the matrix gains a Push column that follows the same rule as the other two channels (no
  stored choice means on). Nothing can be delivered without a subscription, and the device section
  is what makes that state legible, so the column reads as *which categories would reach my
  devices* rather than as a claim that a device is enabled. Inverting the default for push alone,
  and dropping the column in favour of push mirroring the in-app choices, were both declined.
- Q: What does a push actually say on the lock screen, given it passes through Google, Apple or
  Mozilla? → A: **Name the subject.** "Hamburg Hammers invited you to join", "Thursday's training
  was cancelled". The nine existing producers carry invitations, news and schedule changes, not
  private correspondence, and a notification that does not say what happened does not save anyone
  the trip into the app. Category-only and fully generic payloads were declined. Whether a chat
  message's text may appear is a separate decision belonging to #309.
- Q: Does this feature ship a list of the devices receiving notifications, with remove? → A:
  **No — this device, on or off.** Settings speaks only about the browser the player is currently
  in. Other devices are turned off from those devices, signing out always removes the device the
  player signed out of, and deleting the account removes all of them. A managed device list is a
  later decision if players ask for one.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Turning on notifications for this device (Priority: P1)

A player opens notification settings and sees that this device is not receiving notifications, and
a control to change that. They press it, their browser asks whether to allow notifications, they
allow, and the page now says this device is on. On an iPhone, where notifications only reach an app
that has been added to the Home Screen, they are told that first, in plain words, with the steps.

**Why this priority**: Nothing else in this feature can be observed until a device is subscribed.
It is also where the install debt from 054 is paid.

**Independent Test**: In a supporting browser, enable notifications from settings and confirm the
page reflects the new state and survives a reload. On an iPhone in Safari, confirm the page
explains Add to Home Screen instead of offering a control that cannot work.

**Acceptance Scenarios**:

1. **Given** a signed-in player in a browser that supports notifications and has never been asked,
   **When** they press the enable control, **Then** the browser's own permission request appears,
   and on allowing it the page states that this device is now receiving notifications.
2. **Given** a player who has just enabled notifications, **When** they reload the page or return
   later, **Then** the page still states that this device is on, without asking again.
3. **Given** a player who declines the browser's permission request, **When** the request closes,
   **Then** the page says notifications are not allowed for this site and explains that the choice
   is the browser's to change, and the product does not ask again on its own.
4. **Given** a player who previously blocked notifications for the site, **When** they open
   settings, **Then** they are told the browser is blocking them and how to undo that, and no
   control pretends it can be turned on from here.
5. **Given** a player in Safari on an iPhone who has not installed the app, **When** they open
   notification settings, **Then** they are told that notifications require adding JuggerHub to the
   Home Screen first, with the steps, instead of an enable control.
6. **Given** a player using a browser that does not support notifications at all, **When** they
   open settings, **Then** the device section says so plainly and the rest of the page works
   exactly as before.
7. **Given** a player who enables notifications on a second device, **When** they do so, **Then**
   both devices are subscribed and the first is unaffected.

---

### User Story 2 - Being told something happened while the app is closed (Priority: P1)

A player has notifications on. Their team posts news, someone invites them to a team, a training is
cancelled. A notification appears on their device saying what happened, even though no JuggerHub
tab is open. Tapping it opens the product at the thing it is about.

**Why this priority**: This is the reach the product does not have. It is the whole point.

**Independent Test**: With a subscribed device and no tab open, trigger each of the nine producing
events and confirm a notification arrives naming the subject, in the player's language, and that
opening it lands on the relevant page.

**Acceptance Scenarios**:

1. **Given** a player with notifications on for a category, **When** an event of that category
   concerns them and no tab is open, **Then** a notification appears on that device naming what
   happened and which team, event or training it concerns.
2. **Given** a notification has appeared, **When** the player opens it, **Then** the product opens
   at the page that notification is about, signing in first if the session has expired.
3. **Given** a player whose language is German, **When** a notification is sent to them, **Then**
   its text is in German, regardless of the language of whoever caused the event.
4. **Given** a player with several subscribed devices, **When** one event concerns them, **Then**
   each of their devices receives it once, and no device receives it twice.
5. **Given** the same event is processed more than once, **When** notifications are sent, **Then**
   the player is not notified twice for it.
6. **Given** a player who is actively using the product in another window, **When** an event
   concerns them, **Then** a notification still appears on their subscribed devices; suppressing it
   based on where the player is looking is not attempted in this feature.
7. **Given** an event that concerns nobody with push enabled, **When** it happens, **Then** no
   outbound delivery is attempted at all.

---

### User Story 3 - Choosing what reaches your devices (Priority: P2)

A player opens notification settings and sees a third column beside In-app and Email. They turn
Team news off for push but leave it on in-app, because they want it in the product and not on their
phone. Their choice applies to every device they have enabled.

**Why this priority**: Without it push is all-or-nothing per device, which is the fastest route to
a player turning the whole thing off.

**Independent Test**: Turn a category's push off, trigger an event of that category, and confirm no
notification arrives while the in-app entry still does. Confirm the choice holds on another device.

**Acceptance Scenarios**:

1. **Given** a player who has never touched the Push column, **When** they open settings, **Then**
   every category shows push as on, and the device section states whether any device is actually
   receiving them.
2. **Given** a player who turns a category's push off, **When** an event of that category concerns
   them, **Then** no notification reaches any of their devices, while the in-app entry and the
   email behave exactly as their own settings say.
3. **Given** a player who turns a category's **in-app** notifications off but leaves push on,
   **When** an event of that category concerns them, **Then** a notification still reaches their
   devices. The three channels are independent, and switching one off never silences another.
4. **Given** a player who changes a push preference on one device, **When** they open settings on
   another, **Then** the same choice is shown, because the choice belongs to the account.
5. **Given** the always-on security group, **When** a player looks for a push toggle for it,
   **Then** it is shown as always-on exactly as it is today, with no push column entry that implies
   otherwise.

---

### User Story 4 - Turning it off, and devices that stop working (Priority: P2)

A player turns notifications off for the device they are on, and it stops. A player signs out on a
borrowed phone, and that phone stops receiving their notifications. A device that has been wiped or
has not been used in a long time stops being sent to, without anyone doing anything.

**Why this priority**: A notification channel that cannot be switched off, or that keeps delivering
to a phone someone no longer has, is worse than no channel. The sign-out case is the one with a
real consequence for a person.

**Independent Test**: Turn off from settings and confirm delivery stops. Sign out and confirm the
same. Confirm a subscription the push service reports as gone is no longer used.

**Acceptance Scenarios**:

1. **Given** a player with notifications on, **When** they turn this device off in settings,
   **Then** that device receives nothing further, and their other devices are unaffected.
2. **Given** a player signs out, **When** the sign-out completes, **Then** the device they signed
   out of stops receiving their notifications, without any further action from them.
3. **Given** a device whose subscription the push service reports as no longer valid, **When**
   delivery to it is attempted, **Then** it is not retried and the device is dropped, so nothing
   accumulates that can never be delivered to.
4. **Given** a device that has not been reachable for a long time, **When** enough time passes,
   **Then** it stops being stored at all, without an administrator doing anything.
5. **Given** a player deletes their account, **When** the deletion completes, **Then** every device
   of theirs is removed along with the rest of their data, and no further notification can be sent
   to any of them.
6. **Given** a push service is briefly unreachable or fails, **When** notifications are sent,
   **Then** the action that caused them still succeeds, and no player-visible error results from a
   notification failing to leave the building.

---

### Edge Cases

- **Permission is the browser's, not ours.** A player who has blocked notifications for the site
  cannot be re-asked by the product; only their browser settings can undo it. The product must say
  so rather than offering a control that silently does nothing.
- **Enabled in the browser, then blocked later.** A subscription can exist while the permission has
  since been revoked. Delivery quietly stops being possible; the device must eventually be dropped
  the same way any dead subscription is.
- **iPhone, installed, older than iOS 16.4.** The app installs and runs but can never receive a
  notification. The device section must not claim otherwise.
- **The same person on two browsers of one machine.** Each browser is its own device with its own
  subscription; turning one off leaves the other on.
- **A shared device.** Signing out removes the subscription (US4). Two people using one browser
  therefore never inherit each other's notifications.
- **In-app off, push on.** No in-app entry exists for the event, so the notification cannot point
  at an inbox row; it points at the thing itself. This must work, because the channels are
  independent (US3 scenario 3).
- **A notification whose subject has since changed.** The text was composed when it was sent; the
  page it opens shows current truth. A cancelled training's notification opens a cancelled
  training. No attempt is made to recall or rewrite a notification already delivered.
- **The push service throttles us.** A push provider asking us to slow down is not the same as our
  own rate limiter rejecting a caller, and must not be treated as one.
- **Language changes after sending.** A notification already delivered keeps the language it was
  composed in.
- **Nothing to send to.** A player with push preferences on but no subscribed device costs nothing:
  no outbound call is made.

## Requirements *(mandatory)*

### Functional Requirements

#### Enabling a device

- **FR-001**: A signed-in player MUST be able to turn notifications on for the browser they are
  currently using, from notification settings, and the control MUST act on the player's own press
  rather than on page load.
- **FR-002**: Notification settings MUST show the current state of the device the player is on:
  receiving, not enabled, blocked by the browser, unsupported by the browser, or requiring
  installation first. Each state MUST be distinguishable, and each MUST say what the player can do
  about it.
- **FR-003**: On a platform where notifications require the product to be installed to the home
  screen, the settings page MUST say so and give the steps, in place of a control that cannot
  succeed. This satisfies the install affordance deferred from feature 054.
- **FR-004**: The product MUST NOT request notification permission anywhere other than this
  control, MUST NOT request it on page load, and MUST NOT ask again on its own after a refusal.
- **FR-005**: Enabling MUST be durable: a device enabled once continues to receive notifications
  across reloads, restarts and new sessions of the same signed-in player, until it is turned off,
  signed out of, or becomes undeliverable.
- **FR-006**: Each enabled device MUST be recorded against the player who enabled it, so that a
  device belongs to exactly one account at a time.

#### What is delivered

- **FR-007**: When an event of one of the nine existing producing kinds concerns a player, the
  product MUST deliver a notification to each of that player's enabled devices, subject to the
  player's push preference for that event's category.
- **FR-008**: A delivered notification MUST name its subject — what happened and which team, event
  or training it concerns — and MUST be in the recipient's own language.
- **FR-009**: A delivered notification MUST carry only what is needed to say what happened and to
  open the right page. Anything further MUST be fetched by the product after the player opens it,
  not carried in the delivery.
- **FR-010**: Opening a delivered notification MUST bring the player to the page the notification
  is about; if their session has expired they MUST be asked to sign in and then land there.
- **FR-011**: One event MUST reach each of a player's devices exactly once, and MUST NOT reach the
  same device twice, including when the originating action is processed more than once.
- **FR-012**: Delivery MUST NOT depend on where the player is currently looking; no attempt is made
  in this feature to suppress a notification because a window is open elsewhere.
- **FR-013**: Sending MUST NOT be able to fail the action that caused it. A push service that is
  slow, unreachable or failing MUST leave the team news posted, the invitation sent and the
  training cancelled, and MUST surface nothing to the player who performed the action.

#### Choosing categories

- **FR-014**: The notification preference matrix MUST gain push as a third channel alongside in-app
  and email, per existing category, following the same rule as the other channels: a category the
  player has never set is on.
- **FR-015**: The three channels MUST be independent. Turning a category off for one channel MUST
  NOT change or suppress delivery on either other channel, in either direction.
- **FR-016**: Push preferences MUST belong to the account and apply to every device the player has
  enabled; there MUST be no per-device category choice.
- **FR-017**: The always-on group MUST remain always-on and MUST NOT gain a push toggle that
  suggests it can be turned off.
- **FR-018**: The settings page MUST make clear whether any device is actually receiving
  notifications, so that a category switched on is never read as a promise the player's devices can
  keep.

#### Turning off and staying clean

- **FR-019**: A player MUST be able to turn notifications off for the device they are on, and that
  device MUST stop receiving them, leaving their other devices untouched.
- **FR-020**: Signing out MUST stop the device signed out of from receiving that player's
  notifications, without requiring the player to remember to do anything.
- **FR-021**: A device the push service reports as no longer valid MUST be dropped and MUST NOT be
  retried.
- **FR-022**: Devices that have gone unreachable MUST stop being stored after a bounded period,
  automatically, without an administrator acting.
- **FR-023**: Deleting an account MUST remove every device belonging to it, so that no notification
  can be sent to a device of a deleted account.

#### Disclosure

- **FR-024**: The privacy policy MUST disclose, in all three languages with German authoritative,
  that enabling notifications stores a delivery address for that device and that delivering a
  notification hands its text to the push service operated by the maker of the player's browser.
  The wording MUST be durable and category-based, naming what kind of data and which kind of
  recipient rather than a snapshot of today's providers.
- **FR-025**: The product MUST NOT send a notification's text anywhere before the player has
  enabled notifications on at least one device.

#### Boundaries

- **FR-026**: This feature MUST NOT send push for chat messages and MUST NOT change any chat
  behaviour. Chat push is #309.
- **FR-027**: This feature MUST NOT add a managed list of the player's devices, quiet hours, a
  per-notification snooze, or any grouping or summarising of notifications.
- **FR-028**: This feature MUST NOT change what the in-app notification list or the email channel
  deliver today, in content, timing or preference behaviour.

### Key Entities

- **Enabled device**: one browser on one machine that a player has turned notifications on for.
  Belongs to exactly one player, carries the delivery address the browser issued and the keys
  needed to deliver to it, a note of when it was added and when it was last reachable, and a label
  the player would recognise. Removed when the player turns it off, signs out of it, when the push
  service reports it as gone, when it has been unreachable too long, and when the account is
  deleted.
- **Push preference cell**: the existing per-category, per-channel preference, extended with push
  as a third channel. Sparse as today: a cell the player never set means on.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A player on a supported browser can go from never having been asked to a device that
  receives notifications in one press plus the browser's own permission request, with no
  intermediate page.
- **SC-002**: With no JuggerHub tab open, each of the nine producing event kinds results in a
  notification on an enabled device, naming its subject, in 100% of trials.
- **SC-003**: A notification is delivered in the recipient's own language in 100% of trials,
  including when the person who caused the event uses a different language.
- **SC-004**: One event produces exactly one notification per enabled device, and zero when the
  category's push preference is off.
- **SC-005**: Turning a category's in-app channel off while push is on still delivers push, and the
  reverse also holds, in 100% of trials.
- **SC-006**: Turning this device off, and signing out, each stop delivery to that device on the
  next event, while other devices continue to receive.
- **SC-007**: An action whose notifications cannot be delivered — push service unreachable,
  failing, or slow — still completes, with nothing shown to the acting player and no increase in
  the time they wait.
- **SC-008**: A player with no enabled device causes no outbound delivery attempt for any event.
- **SC-009**: Deleting an account leaves no device able to receive a notification for it.
- **SC-010**: The in-app list and the email channel behave identically to before this feature in
  every observable respect.
- **SC-011**: The settings page states the current device's state correctly in every one of its
  states, verified on Android, iPhone and desktop.

## Assumptions

- **This feature depends on 054 and nothing else open.** The manifest and the background worker are
  merged; the worker deliberately has no notification handling yet, and gains it here.
- **The nine existing producing kinds only.** No new kind of notification is introduced. Chat is
  #309, and it depends on the shape this feature gives the delivery path.
- **A delivery address is personal data and the push service is a new recipient.** Which is why
  FR-024 exists and why planning must confirm the exact policy wording rather than assume the
  existing text still covers it, as it had to in 054.
- **Browsers decide permission, and they do not give it back.** A blocked site cannot re-ask. The
  product's job is to explain, never to work around it.
- **iOS delivers only to an installed app, 16.4 or later.** Older iPhones can use the product
  fully; they cannot receive notifications, and are told so rather than left to wonder.
- **Devices are not named by the player.** A recognisable label is derived from the browser; naming
  and managing devices would be the device list that was explicitly declined.
- **No new notification surface in the product.** The in-app list, its badge and its rows are
  untouched; this feature adds a delivery channel, not a place to read things.
- **Category copy stays where it already lives.** The matrix's category labels are composed by the
  product server-side today and the column headings come from the interface catalogues; this
  feature follows whatever each already does rather than moving either.
