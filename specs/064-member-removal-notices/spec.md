# Feature Specification: Removing a Member Is Confirmed, and the People It Concerns Are Told

**Feature Branch**: `064-member-removal-notices`

**Created**: 2026-09-29

**Status**: Draft

**Input**: User description: "GH #385 — Removing a team member: confirm first, and tell the people it concerns. Today, on the team page, Remove in the roster's ⋯ menu removes a member at once with no confirmation, and nobody is told: the removed player gets no alert/email/push, the team's other admins get nothing, and a member leaving on their own is silent too — while the "Invites & roster changes" notification category promises "Team invites, people joining or leaving". The party page has the same one-tap Remove (and its Disband uses the browser's native confirm box). OWNER DECISIONS (2026-09-29): (1) Confirm before removing, on the team roster AND the party crew, with an in-page dialog whose destructive answer is a danger button and whose safe answer has initial focus; the party page gets that in-page dialog for Disband too (replacing the native confirm), so the page has one confirmation style. (2) The removed team player is told: Alerts row + push + email under their Invites & roster preferences; the notice names the team only — never the admin who removed them, no reason, no rejoin/browse pitch; it opens the team page. (3) The team's admins hear about departures — both a member leaving on their own and a member being removed: every current admin except the one who acted gets an Alerts row + push + email under their own Invites & roster preferences, naming the player who left or was removed. (4) A player removed from a PARTY crew is NOT told (confirmation only). The settings category text stays accurate ("people joining or leaving"). Out of scope: telling admins when someone joins by accepting an invitation, party-admin notices, a block on rejoining (a removed player can ask to join again right away)."

## Context

An admin who opens the ⋯ menu beside a teammate on the team page and presses **Remove** has
removed them. Nothing asks first, and a slip of the thumb on a phone cannot be undone from the
admin's side: the player has to ask to join again or be invited back. *Manage team* asks before a
player leaves and before a team is deleted, so the one irreversible action that affects *someone
else* is the one without a question.

Nobody is told either. The removed player finds out when the team disappears from *My team*. The
team's other admins are not told at all. A member who leaves on their own is just as silent. Yet the
notification settings describe the *Invites & roster changes* category as "Team invites, people
joining or leaving", and nothing is ever sent when someone leaves.

The party page has the same one-tap **Remove** for its crew. Its **Disband** does ask, but through the
browser's own confirmation box, which looks like nothing else in the product and which a browser
may suppress.

**Who needs to learn what:**

| Who | Needs to learn | Where it reaches them |
|-----|----------------|------------------------|
| The player an admin removed from a team | They are no longer a member of that team | Alerts inbox, email and their devices, each by their own settings |
| Every other admin of the team | A player left the team, or was removed from it | Alerts inbox, email and their devices, each by their own settings |
| A player removed from a party crew | Nothing is sent (owner decision) | — |

**What reading the product showed** — each one shapes a requirement below:

- **An admin's alert outlives the departed player's account.** An alert belongs to the person who
  received it, so an admin's alert that a player left survives that player later deleting their
  account. Feature 037 (its own FR-023) requires that nothing surviving an erasure lets anyone
  recover who the member was. The alert therefore must not keep its own copy of the player's name.
  It names them from their current profile each time it is shown, and names no one once they are
  gone or banned (FR-016). Feature 058 settled the same question for join requests.
- **A shared invite link can be used again and again.** A team's shared link stays usable after
  someone joins with it, and joining by it needs no admin. Once leaving a team reaches every admin's
  inbox, mailbox and phone, anyone holding the link could join and leave in a loop and message the
  admins as often as they like. The owner chose to bound joining by invitation (FR-024), the way
  058 bounded asking to join.
- **A player can be on a team more than once.** Someone who left can be invited back and leave
  again. Each stay on the team is its own, so each departure is told once (FR-018). A later
  departure is not swallowed as a repeat of an earlier one.
- **The party page's Remove is on two tabs.** On *In* it takes a player out of the crew. On
  *Declined* it clears a player's "can't make it" answer, which puts them back among those who
  have not answered. Both need the question, but they are different things to confirm (FR-007).
- **A departure is not the only way a membership ends.** Deleting a team, deleting an account and
  banning a player also end memberships. None of them is a player leaving or an admin removing
  someone, and none of them sends these notices (FR-019).

**Out of scope, deliberately**: telling admins when someone joins by accepting an invitation
(the other half of "joining", noted in the issue); notices for a player removed from a party crew
(owner decision); notices to party admins; any block on rejoining (a removed player can ask to join
again at once, as today); a reason field on removal; naming the removing admin anywhere; recalling an
email or device notification that has already been sent.

## Clarifications

### Session 2026-09-29

- Q: Should the team's admins hear when someone leaves or is removed? → A: **Yes, both.** Every
  current admin except the one who removed the player gets an Alerts row, a device notification and
  an email under their own *Invites & roster* settings, naming the player. The settings text stays
  as it is and becomes true for leaving.
- Q: Should a player removed from a party crew be told as well? → A: **No.** The party page asks
  before removing, and nobody is told.
- Q: What should the removal notice say beyond the team's name? → A: **The team's name only.** A
  neutral statement that they are no longer a member. It names no admin, gives no reason, and makes
  no suggestion to rejoin or look at other teams. The alert and the email open the team page.
- Q: How is Remove confirmed on the party page, which has no in-page dialog today? → A: **A new
  in-page dialog in the team page's style, used for Disband as well**, so the party page no longer
  uses the browser's confirmation box anywhere.
- Q: How is the admins' departure notice protected against a join-by-shared-link → leave loop? →
  A: **Limit joining by invitation.** A player may accept team invitations a bounded number of times
  per hour. Every departure still reaches the admins.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An admin confirms before removing a teammate (Priority: P1)

Mara runs Rheinfeuer and is tidying the roster on her phone. She opens the ⋯ menu beside Jonas and
presses **Remove**. Instead of Jonas disappearing, a dialog asks whether she wants to remove Jonas
from Rheinfeuer. It says he will lose access to the team's members-only pages and chat, and that he
will be told. **Keep Jonas** has the focus. She meant to open Jonas's neighbour, so she presses Keep
Jonas and nothing has changed. Later she does mean it: she presses **Remove from team** and Jonas is
gone from the roster.

**Why this priority**: This is the half of the issue that prevents harm. A removal by mistake cannot
be undone by the admin who made it.

**Independent Test**: As an admin, press Remove on a teammate, dismiss the dialog in each available
way (the safe answer, Escape), and confirm the roster is unchanged. Then confirm the removal and
check the teammate is gone.

**Acceptance Scenarios**:

1. **Given** an admin viewing their team's roster, **When** they press Remove in a teammate's menu,
   **Then** a confirmation dialog opens naming the teammate and the team, and nothing has been
   removed yet.
2. **Given** the confirmation dialog is open, **When** it appears, **Then** the safe answer has the
   focus and the destructive answer is styled as destructive.
3. **Given** the confirmation dialog is open, **When** the admin presses the safe answer or Escape,
   **Then** the dialog closes and the roster is unchanged.
4. **Given** the confirmation dialog is open, **When** the admin confirms, **Then** the teammate is
   removed and the roster shows the team as it now is.
5. **Given** the admin has confirmed, **When** the removal is still in progress, **Then** the
   destructive answer shows that it is working and cannot be pressed a second time.

---

### User Story 2 - The removed player is told (Priority: P1)

Jonas opens the app a day later and has an alert: he is no longer a member of Rheinfeuer. It does
not say who removed him or why, and it does not suggest anything. Pressing it opens Rheinfeuer's
page, which he now sees as someone outside the team. The same message reached his phone and his
inbox, in German, because that is his language.

**Why this priority**: This is the other half of the issue. Today a player finds out only when a
team quietly vanishes from *My team*.

**Independent Test**: Remove a player, then sign in as that player and check the Alerts inbox, the
email received and the device notification. Each names the team, none names an admin, and the alert
opens the team page. Repeat with each channel switched off in the player's settings.

**Acceptance Scenarios**:

1. **Given** an admin removes a player, **When** the removal completes, **Then** the player has one
   Alerts row naming the team, if their *Invites & roster* in-app setting is on.
2. **Given** an admin removes a player, **When** the removal completes, **Then** the player receives
   one email and one device notification naming the team, each only if that channel is on in their
   settings. Each channel is decided on its own.
3. **Given** the removal notice in any channel, **When** the player reads it, **Then** nothing in it
   names or identifies the admin who removed them.
4. **Given** the removal alert, **When** the player opens it, **Then** the team's page opens.
5. **Given** a player whose language is German or Spanish, **When** they receive the notice, **Then**
   the alert, email and device notification are in their language.
6. **Given** a member leaves the team on their own, **When** the leave completes, **Then** they
   receive no notice about their own departure.

---

### User Story 3 - The other admins learn about departures (Priority: P2)

Tom is Rheinfeuer's other admin. When Mara removes Jonas, Tom gets an alert that Jonas was removed
from Rheinfeuer. Mara gets nothing, because she did it. A week later Lena leaves the team on her own
from *Manage team*, and both Mara and Tom are told that Lena left Rheinfeuer.

**Why this priority**: The admins run the roster together and should not learn about changes to it by
accident. It is also what the settings text already promises.

**Independent Test**: With a team of three admins and some members, remove a member as one admin and
check that the other two are told and the acting admin is not. Then have a member leave and check
that all three admins are told.

**Acceptance Scenarios**:

1. **Given** a team with several admins, **When** one admin removes a player, **Then** every other
   current admin is told that the player was removed, and the admin who removed them is not.
2. **Given** a team, **When** a member leaves on their own, **Then** every current admin is told that
   the player left.
3. **Given** an admin leaves the team on their own, **When** the leave completes, **Then** every
   remaining admin is told, and the admin who left is not.
4. **Given** each admin's own *Invites & roster* settings, **When** a departure is announced, **Then**
   each admin receives the alert, the email and the device notification only on the channels they
   have on.
5. **Given** an admin's alert about a player who later deletes their account or is banned, **When**
   the admin views it, **Then** the alert no longer names that player.
6. **Given** a team whose only admin removes a player, **When** the removal completes, **Then** no
   admin is told (only the removed player is).

---

### User Story 4 - A party admin confirms before removing crew or disbanding (Priority: P2)

Tom manages Rheinfeuer's party for the Hamburg Cup. On the *In* tab he presses **Remove** beside a
crew member, and the same kind of dialog as on the team page asks whether to take them out of the
party. On the *Declined* tab, Remove asks whether to clear that player's answer. When the event is
over he presses **Disband**, and the confirmation is the same in-page dialog instead of a browser
box. Nobody is sent anything when a crew member is removed.

**Why this priority**: The same one-tap slip exists on the party page. It is lower than the team page
because a party is short-lived and a removed crew member can say "I'm in" again while the event is
open.

**Independent Test**: As a party admin, press Remove on the In tab and on the Declined tab, and press
Disband. Dismiss each dialog and confirm nothing changed. Confirm each and check the result. Confirm
the removed crew member received no alert, email or device notification.

**Acceptance Scenarios**:

1. **Given** a party admin on the *In* tab, **When** they press Remove beside a crew member, **Then**
   a dialog asks whether to take that player out of the party, and nothing has changed yet.
2. **Given** a party admin on the *Declined* tab, **When** they press Remove beside a player, **Then**
   a dialog asks whether to clear that player's answer, and says the player will count as not having
   answered.
3. **Given** a party admin, **When** they press Disband, **Then** an in-page dialog asks whether to
   disband the party, and the browser's own confirmation box is not used.
4. **Given** any of these dialogs, **When** it opens, **Then** the safe answer has the focus, and the
   safe answer or Escape closes it with nothing changed.
5. **Given** a party admin confirms a crew member's removal, **When** it completes, **Then** the
   removed player receives no alert, email or device notification.

---

### User Story 5 - Joining by invitation is bounded (Priority: P3)

Somebody has Rheinfeuer's shared invite link. They join, leave, join and leave again, trying to fill
the admins' inboxes. After ten joins by invitation within the hour, the next one is refused with a
message asking them to try again later. Each departure up to then was told to the admins once.

**Why this priority**: It is a safeguard that only matters when someone abuses a link. It is what
makes the admins' departure notices safe to send.

**Independent Test**: As one player, accept team invitations repeatedly within one hour and check
that the eleventh is refused with a "try again later" message in the player's language, and that
nothing is retried automatically.

**Acceptance Scenarios**:

1. **Given** a player who has accepted ten team invitations in the current hour, **When** they accept
   another, **Then** it is refused, they are not added to the team, and they see a message in their
   language asking them to try again later.
2. **Given** a player accepting an invitation from any place the product offers it (the invite page,
   onboarding, *My team*, the Alerts inbox, Home), **When** the limit refuses it, **Then** that place
   shows the "try again later" message and does not retry by itself.
3. **Given** a player who accepts one or two invitations in an hour, **When** they accept, **Then**
   nothing about accepting has changed for them.

---

### Edge Cases

- **The member already left, or another admin removed them, while the dialog was open**: confirming
  does not remove anyone or send anything a second time. The admin is told the player is no longer
  on the team, and the roster refreshes.
- **Two admins remove the same player at the same moment**: the player is removed once, told once,
  and the other admins are told once.
- **The acting admin lost their admin role while the dialog was open**: the removal is refused, the
  admin sees a short message in their language, and the page refreshes to show what they may now do.
- **The removal fails for another reason** (network error, server error): the dialog says so in the
  admin's language, nothing has changed, and they can try again or keep the member.
- **Removing another admin**: allowed as today, since a team always keeps at least one admin (the
  acting admin). The removed admin gets the removal notice. The remaining admins except the acting
  one get the departure notice.
- **The last admin tries to leave**: refused as today, so nothing is sent.
- **The team is renamed after a notice was delivered**: the alert shows the team's new name, like
  every other alert that names a team (feature 061). Emails and device notifications already sent
  keep the old name.
- **The team is deleted after a notice was delivered**: the alert stays. Opening it shows that the
  team cannot be found, as it does for other alerts about a deleted team.
- **A player who left rejoins and leaves again**: each departure is told once. The second is not
  treated as a repeat of the first.
- **A recipient has every channel off for Invites & roster**: they receive nothing, and nothing else
  changes.
- **A notice cannot be delivered** (email provider down, a device unreachable): the removal or leave
  still succeeds, and no other channel is affected.
- **Memberships ending another way** (the team is deleted, a player deletes their account, a player
  is banned): none of these notices is sent.
- **A party player removed from the crew is also a team member**: nothing is sent. Their team
  membership is not affected, as today.
- **The removed player opens the team page from the alert**: they see it as any signed-in non-member
  does, including the option to ask to join again.
- **The invitation limit is reached while accepting an addressed invitation**: the same "try again
  later" refusal applies. Accepting an invitation is one action whether it came by a shared link or
  was addressed to the player.

## Requirements *(mandatory)*

### Functional Requirements

**Confirming a removal from a team**

- **FR-001**: Choosing **Remove** for a teammate on the team page MUST open a confirmation dialog
  before anything is removed. The dialog MUST name the teammate and the team, say that they will lose
  access to the team's members-only pages and chat, and say that they will be told.
- **FR-002**: The dialog's destructive answer MUST be styled as destructive. Its safe answer MUST have
  the focus when the dialog opens. The safe answer and Escape MUST close the dialog with nothing
  changed. While the dialog is open, keyboard focus MUST stay inside it. On narrow screens it MUST
  appear as a sheet from the bottom of the screen, like the page's existing confirmation.
- **FR-003**: Once the admin confirms, the destructive answer MUST show that it is working and MUST
  NOT accept a second press until the removal has finished.
- **FR-004**: If the teammate is no longer on the team when the admin confirms, the dialog MUST close,
  the admin MUST see a short neutral note that the player is no longer on the team, and the roster
  MUST refresh. Nothing may be sent as a result of that attempt.
- **FR-005**: If the removal is refused because the acting player is no longer an admin, or fails for
  any other reason, the admin MUST see a short message in their language. The message MUST be chosen
  by the kind of failure, never taken from the server's wording. After a refusal the page MUST
  refresh; after a failure the admin MUST be able to try again or keep the member.

**Confirming on the party page**

- **FR-006**: Choosing **Remove** for a player on the party page MUST open a confirmation dialog of the
  same style and behaviour as FR-002 and FR-003 before anything changes.
- **FR-007**: For a player on the *In* tab, the dialog MUST ask whether to take them out of the party.
  For a player on the *Declined* tab, it MUST ask whether to clear their answer and say they will
  count as not having answered.
- **FR-008**: **Disband** MUST ask through the same in-page dialog, with the same meaning as today's
  question (the party is disbanded and it cannot be undone). The party page MUST NOT use the
  browser's own confirmation box anywhere.
- **FR-009**: Removing a player from a party crew MUST NOT send any alert, email or device
  notification to anyone.

**Telling the removed player**

- **FR-010**: When an admin removes a player from a team, the removed player MUST be told through an
  Alerts row, an email and a device notification. Each channel MUST follow only the player's own
  *Invites & roster* setting for that channel. Turning one channel off MUST NOT change another.
- **FR-011**: The removal notice MUST name the team and say that the player is no longer a member. It
  MUST NOT name or identify the admin who removed them, in any channel. It MUST NOT give a reason,
  suggest asking to rejoin, or point to other teams.
- **FR-012**: The removal alert and the email MUST lead to the team's page.
- **FR-013**: A player who leaves a team on their own MUST NOT receive a notice about their own
  departure.

**Telling the admins**

- **FR-014**: When a member leaves a team on their own, or is removed from it, every current admin of
  the team MUST be told, except the admin who removed them and the departing player. Each admin is told
  through an Alerts row, an email and a device notification, and each channel follows only that
  admin's own *Invites & roster* setting.
- **FR-015**: The admins' notice MUST name the team and the player, and say whether the player left
  or was removed. It MUST NOT name the admin who removed them.
- **FR-016**: The stored alert MUST NOT keep its own copy of the departing player's name. Each time the
  alert is shown, the player MUST be named from their current profile. Once the player is banned or
  their account is deleted, the alert MUST name no one, showing the product's neutral placeholder
  instead. A device notification names the player as they were when it was sent.
- **FR-017**: The admins' notice MUST open the team's page.

**Rules for both notices**

- **FR-018**: Each departure MUST produce at most one notice per recipient per channel, even if the
  removal is attempted twice or by two admins at once. A player who rejoins and departs again MUST
  produce new notices for the new departure.
- **FR-019**: These notices MUST be sent only when a player leaves on their own or an admin removes
  them. A membership ending any other way (the team is deleted, an account is deleted, a player is
  banned) MUST NOT produce them.
- **FR-020**: Notices MUST be sent only after the departure has been saved. A notice that cannot be
  delivered MUST NOT fail, undo or delay the departure, and MUST NOT stop another recipient or channel
  from being told.
- **FR-021**: Leaving and removing MUST otherwise behave exactly as today: who may remove whom, the
  rule that a team keeps at least one admin, and the answers a player or admin sees.
- **FR-022**: Both notices MUST belong to the existing *Invites & roster* category. No new category or
  setting is added, and the category's description is unchanged.
- **FR-023**: Every alert this feature adds MUST show the team's current name if the team is renamed
  later, as other alerts that name a team do (feature 061).

**Bounding joins by invitation**

- **FR-024**: A player MUST be able to accept team invitations, by shared link or addressed to them, at
  most **10 times per clock hour** across all teams. The window is fixed, so a burst across the turn of
  an hour can reach twenty and no more. An acceptance beyond the limit MUST be refused without adding
  the player to the team.
- **FR-025**: Every place a player can accept a team invitation (the invite page, onboarding, *My
  team*, the Alerts inbox and Home) MUST answer the refusal with a message in the player's language
  asking them to try again later. The refusal MUST NOT be retried automatically, and the message MUST
  be chosen by the kind of failure, never taken from the server's wording.

**Language**

- **FR-026**: Every new piece of interface text, email and device notification MUST exist in English,
  German and Spanish. Emails and device notifications MUST be in the recipient's saved language.

### Key Entities

- **Removal notice**: a new kind of alert for the player an admin removed. It carries the team (its
  address and name) and nothing identifying a person.
- **Departure notice**: a new kind of alert for the team's admins. It carries the team (its address
  and name) and whether the player left or was removed. The player is recorded as the person the alert
  is about, so their name is read from their current profile when shown, never stored in the alert.
- **Team membership**: existing. Each stay on a team is its own membership, which is what lets a second
  departure be told as a new one.

No other data is stored. The invitation limit keeps only short-lived counters, like the join-request
limit (feature 058).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On the team page and the party page, **zero** removals and **zero** disbands happen
  without an explicit confirmation in an in-page dialog. Dismissing any of these dialogs leaves
  **everything** unchanged.
- **SC-002**: A removed player receives **exactly one** notice on each channel they have on, and **none**
  on channels they have off. In **zero** cases does the stored alert, the email or the device
  notification name or identify the removing admin.
- **SC-003**: For every departure, each current admin other than the acting admin receives **exactly
  one** notice on each channel they have on. The acting admin and the departing player receive
  **none**.
- **SC-004**: After a departed player's account is deleted or they are banned, **zero** admin alerts
  about their departure show their name.
- **SC-005**: A player removed from a party crew receives **zero** notices.
- **SC-006**: A player accepting team invitations is added to a team at most **10** times in one clock
  hour. The eleventh attempt is refused with a message in their language, and is not retried
  automatically.
- **SC-007**: A notice that fails to send never fails a departure: in **100%** of cases where delivery
  fails, the departure is still saved.
- **SC-008**: In German at 375px, every new dialog shows its title, text and both answers in full, with
  no truncated label and no horizontal overflow. The same holds on desktop.

## Assumptions

- The team page's existing join confirmation (feature 009) is the style to follow for the new dialogs.
  Feature 057's delete dialog is the model for the focus behaviour: the safe answer is focused and
  focus stays inside. Where the two differ, the stricter behaviour is used.
- Naming the removing admin to the other admins was not asked for. The admins' notice names the player
  only, as the removal notice to the player names no admin. Each notice is about one person.
- The limit of 10 acceptances per hour matches the join-request limit (feature 058). Nobody accepts ten
  team invitations in an hour in normal use. The limit covers addressed invitations too, because
  accepting is one action whichever kind of invitation it is.
- Party admins still have no notices about crew changes. Adding them would be a separate feature.
- Pages do not update live. An admin whose teammate left while the page was open learns it when they
  act (FR-004) or from the new alert.
- Sent emails and device notifications cannot be recalled. This is the same as every other notice.
