# Feature Specification: Join Requests Reach the People Who Decide Them

**Feature Branch**: `058-join-request-notifications`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "GH #360 — Join requests reach nobody. When a signed-in player asks to join a team (TeamJoinRequestService.RequestAsync, reached from the team page and the onboarding team step), no one is told: there is no NotificationType for a join request, admins only learn of it by opening the team page, the requester hears nothing on approve/decline, and Home's "Needs you" block (025) omits a request awaiting an admin's decision. Owner decision (in the issue): the request is announced as an alert to EVERY CURRENT TEAM ADMIN. Build: (1) admins are alerted on a new pending request (in-app row + email + push, each governed independently by the recipient's own Invites & roster preference per channel, 055's channel independence), once per request, linking to the team page where the queue lives; (2) the requester is told the outcome on approve (links to the team) and on decline (links to browse teams); (3) Home "Needs you" gains a join-request item for admins, one per pending request, resolved in place via the existing approve/decline endpoints; (4) every new notification kind has explicit push-composer arms (no generic fallback body) and localized email (en/de/es). Findings from reading the code that the spec must carry: (a) admins' notification rows SURVIVE the requester's account erasure, and 037 FR-023 forbids any surviving record from identifying an erased member — so the requester's name/handle must NOT be copied into the stored notification; the requester is identified at read time (the notification's actor), falling back to the neutral placeholder once banned/erased; (b) join requests have OPEN REACH (any signed-in player → any team's admins) and today have no rate limit, so once each request fans out email + push to every admin, a request→withdraw→request loop becomes a spam vector; (c) two admins deciding the same request at once can today both "win" (tracked read-then-save, no lock), which the new outcome notice would make visible (requester told both approved and declined) — a request must be decided exactly once; (d) withdrawal deletes the request row (009: no audit trail), so what happens to the admins' already-delivered alerts is an open decision (issue: removed vs left reading "withdrawn"); (e) Home "Needs you" titles for all five existing kinds are English sentences composed on the server (GH #141 defect class, untracked) — the new item must not add another; (f) the requester's decision notice should not need to identify which admin decided. Out of scope: event/party news, the marketplace application direction (same gap class, follow-up), extending chat blocks beyond chat."

## Context

A player who asks to join a team today is heard by nobody. The request is stored and waits in a
queue on the team page that only the team's admins can see — and nothing tells them it is there.
An admin who does not happen to open the team page never learns of it, and a request to a team
whose admins rarely visit sits until the player gives up. The player, having pressed *Request to
join*, is never told the answer either: an approval shows only because the team quietly appears
under *My team*, and a decline shows nowhere at all.

Feature 009 recorded this as a placeholder ("notifications for request/approval … deferred with
feature 008"). Feature 010 then built the notification system, and the join-request half was never
added. This feature closes the loop.

**Who needs to learn what:**

| Who | Needs to learn | Where it reaches them |
|-----|----------------|------------------------|
| Every admin of the team, at the moment a player asks | Someone is waiting for an answer | Alerts inbox, email and their devices — each by their own settings; Home's *Needs you* for as long as the request waits |
| The player who asked | The answer: accepted or declined | Alerts inbox, email and their devices — each by their own settings |

**What reading the product showed** — each one shapes a requirement below:

- **An admin's alert outlives the requester's account.** An alert belongs to the person who
  received it, so an admin's alert about a request survives the requester later deleting their
  account — and feature 037 requires (its own FR-023, not this spec's) that nothing surviving an
  erasure lets anyone recover who the member was. The alert therefore must not keep its own copy of the requester's
  name: it names them from their current profile each time it is shown, and names no one once
  they are gone or banned (FR-005).
- **Anyone can ask any team.** Requesting to join needs no relationship with the team, and
  nothing limits how often a player may request. That is harmless today because a request reaches
  no one. Once every request lands in every admin's inbox, mailbox and phone, a player who
  requests and withdraws over and over could flood a team's admins (FR-022, FR-023).
- **Two admins can both answer the same request.** If two admins answer one request at nearly the
  same moment, both answers are applied today. Nobody notices, because nobody is told. Once the
  answer is sent to the player, they could be told both *accepted* and *declined* (FR-012).
- **A withdrawn request leaves no trace** — feature 009 deletes it — so what the admins' alerts
  say about it afterwards had to be decided; the issue left that open, and the owner decided the
  alerts go with the request (FR-022).
- **A request can outlive its reason.** A player whose request is pending can join the same team
  through an invitation. The request then stays in the admins' queue, asking them to decide on
  someone who is already a member — and, once this feature exists, would put that question on
  their Home and let a decline tell a member they were turned down (FR-020).
- **Home's *Needs you* speaks English to everyone.** Its five existing kinds are sentences built
  in English on the server, so a German dashboard reads "Hamburg Hammers invited you". The new
  item must not become a sixth, and the owner chose to fix the five here as well (FR-019a).

**Out of scope, deliberately**: a player's *own* pending request on their Home (it awaits someone
else, not them); a message from the requester to the admins; reminders for requests left
unanswered; answering a request from inside the Alerts inbox (alerts open the team page; Home and
the team page are where requests are answered); the marketplace's *application* direction, where
a party's admins are likewise not told about an application (same gap, follow-up issue); letting
a chat block also silence join requests (blocks govern direct conversations only — feature 019);
event and party news.

## Clarifications

### Session 2026-09-28

- Q: When a player withdraws a request, what happens to the alerts its team's admins already
  received? → A: **They are removed** — from every admin's inbox, lowering unread counts — in one
  all-or-nothing step with the withdrawal. Feature 009 already treats a withdrawn request as
  leaving no trace, so the alerts follow it; and a player who requests and withdraws repeatedly
  then leaves nothing behind in anyone's inbox. Email and device notifications already delivered
  are not recalled. (Rejected: keeping them marked as no longer waiting — they could not say
  *withdrawn* specifically, because a withdrawal and a deleted account look the same once the
  request is gone, and the rows would pile up.)
- Q: Do these notices also go out by email? → A: **Both directions.** Admins are emailed about a
  new request and the player is emailed the answer, each per their own *Invites & roster → Email*
  setting and in their own language. Email is the only channel that reaches an admin who never
  opens the site and has not turned on device notifications — the heart of the issue.
- Q: How are repeated requests bounded? → A: **A per-player cap: at most 10 requests per hour,
  across all teams**, decided by the server. The eleventh is refused with a plain "try again
  later". (Rejected: a per-team cooldown on re-announcing the same player — it targets the harm
  more narrowly, but would require keeping withdrawn requests, reversing 009; and no limit at all.)
- Q: *Needs you*'s five existing kinds are English sentences built on the server. Fix them here
  too? → A: **Yes — all six kinds are worded in the viewer's language in this feature.** The
  item's shape changes for the new kind anyway, so the card gets one consistent form instead of
  one translated item among five English ones.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The admins hear about a request (Priority: P1)

A team has three admins. A player new in town finds the team and presses *Request to join*.
Within moments each of the three admins has an alert — "Jonas Weber wants to join Hamburg
Hammers" — in their Alerts inbox, by email, and on the devices where they turned notifications
on, each exactly as their own settings say. Opening it takes them to the team page, where the
request waits to be answered.

**Why this priority**: This is the issue's headline. A request to a team whose admins do not check
the site today sits until the player gives up; being told is the whole fix.

**Independent Test**: As a player, request to join a team with two admins. Confirm each admin has
exactly one Alerts row naming the player and the team; that each admin with email on received one
email in their own language; that a device with notifications on received one notification; that
the requester and the team's other members received nothing; and that opening the alert lands on
the team page with the request in its queue.

**Acceptance Scenarios**:

1. **Given** a team with several admins, **When** a player sends a request, **Then** every current
   admin receives exactly one notice of it naming the player and the team — and nobody else does:
   not the player, not members who are not admins.
2. **Given** an admin who turned *Invites & roster → Email* off and left in-app and device
   notifications on, **When** a request arrives, **Then** they get the Alerts row and the device
   notification but no email — and every other combination of the three switches behaves the
   same way, each switch governing only its own channel.
3. **Given** a player whose request is already waiting, **When** they press *Request to join*
   again, **Then** no admin is notified a second time.
4. **Given** the alert, **When** an admin opens it, **Then** it takes them to the team page, where
   the request can be answered.
5. **Given** an admin who reads German and a player who reads English, **When** the request
   arrives, **Then** the admin's email and device notification are in German.
6. **Given** the player changes their display name after asking, **When** an admin looks at the
   alert, **Then** it shows the current name.
7. **Given** the player has since deleted their account, or been banned, **When** an admin looks
   at the alert, **Then** it names no one — it reads as coming from a former player — and it no
   longer reads as waiting.

---

### User Story 2 - The player hears the answer (Priority: P1)

A day later one of the admins approves the request. The player gets a notice — "You're in:
Hamburg Hammers accepted your request" — in their Alerts inbox, by email and on their devices,
each as their own settings say; opening it takes them to their new team. Had the admin declined,
they would be told that Hamburg Hammers declined their request, and opening it would take them to
where they can look for other teams. The notice speaks for the team; it never says which admin
answered.

**Why this priority**: The other half of the loop. A player who asked deserves an answer, and a
decline is otherwise invisible for ever.

**Independent Test**: Request, then approve as an admin; confirm the player has exactly one notice
saying they were accepted, that it opens the team page, and that email and device notification
follow their settings. Repeat with a decline: exactly one notice saying declined, opening the team
browser. Confirm no admin's name appears in either.

**Acceptance Scenarios**:

1. **Given** a waiting request, **When** an admin approves it, **Then** the player is a member and
   receives exactly one notice saying the team accepted them, which opens the team's page.
2. **Given** a waiting request, **When** an admin declines it, **Then** the player is not a member
   and receives exactly one notice saying the team declined, which opens the team browser.
3. **Given** either notice, **When** the player reads it, **Then** it names the team and does not
   name or otherwise identify the admin who answered.
4. **Given** two admins answer the same request at nearly the same moment, **When** both answers
   arrive, **Then** exactly one takes effect, the player receives exactly one notice matching it,
   and the other admin is told the request was already answered.
5. **Given** a player who reads Spanish, **When** their request is answered, **Then** their email
   and device notification are in Spanish.
6. **Given** the player withdrew before anyone answered, **When** an admin then tries to answer,
   **Then** the admin is told the request is no longer waiting, nothing changes, and the player is
   told nothing.
7. **Given** a player about to send a request from the team page, or who has just sent one during
   onboarding, **When** they read the confirmation, **Then** it tells them they will be told the
   answer.

---

### User Story 3 - Withdrawn and repeated requests do not pile up on the admins (Priority: P1 — ships with User Story 1)

A player who asks to join and then withdraws does not leave the team's admins holding an alert
about a request which no longer exists: the alerts go with the request, and an admin who had not
read theirs sees their unread count drop. And no player can turn requesting-and-withdrawing into a
way of flooding a team's admins with alerts, emails and phone notifications: after ten requests in
the same hour, a further request is refused with a plain "try again later".

**Why this priority**: User Story 1 turns every request into a message to several people. Without
this story, the same button that asks to join becomes a way to message a team's admins as often as
one likes — an open reach with no bound, which is exactly what feature 019 refused to ship for
chat. It must ship with User Story 1, not after it.

**Independent Test**: Request, then withdraw; confirm every admin's alert about it is gone and an
unread admin's count dropped (FR-022). Then request and withdraw repeatedly as one player; confirm
the eleventh request within the same clock hour is refused with a plain message in the player's
language, and that no admin received more than ten announcements from that player in it (FR-023).

**Acceptance Scenarios**:

1. **Given** a request the admins were alerted about, **When** the player withdraws it, **Then**
   the alert is gone from every admin's inbox — including an admin who has since lost admin — and
   each admin who had not read it sees their unread count drop by one.
2. **Given** the withdrawal and the removal of the alerts, **When** either cannot be completed,
   **Then** neither happens: the request still waits and every alert is still there.
3. **Given** a player who has sent ten requests in the current hour, **When** they send another,
   **Then** it is refused, nothing is stored, no admin is notified, and the player is told plainly
   — in their language — to try again later.
4. **Given** a refused request, **When** the refusal is shown, **Then** nothing retries it
   automatically.
5. **Given** a player well inside the limit — for example asking three teams in one sitting during
   onboarding — **When** they send their requests, **Then** none is refused.

---

### User Story 4 - Waiting requests are on Home (Priority: P2)

An admin opens Home. Under *Needs you*, next to their team invitations and party requests, they see
"Jonas Weber wants to join · Hamburg Hammers" with *Approve* and *Decline* right there. They tap
*Approve*: the item disappears, the player is on the team and gets their notice. The co-admins no
longer see the item the next time they open Home, and the alerts they received now read as
answered.

**Why this priority**: *Needs you* is the product's list of everything awaiting the viewer, and a
request awaiting an admin's decision is exactly that. It is P2 because the alert (User Story 1)
already brings the admin to the team page, where the request can be answered.

**Independent Test**: As the admin of two teams, each with a waiting request, confirm Home lists
both, each naming the player and the team; that approving or declining from Home has exactly the
effect of answering on the team page (membership, the player's notice); and that once answered the
item is gone for every admin. Then view *Needs you* in German holding one item of every kind and
confirm no English sentence remains.

**Acceptance Scenarios**:

1. **Given** an admin of a team with a waiting request, **When** they open Home, **Then** *Needs
   you* shows one item for that request, naming the player and the team, with *Approve* and
   *Decline*.
2. **Given** the viewer administers several teams, **When** they open Home, **Then** every waiting
   request across those teams appears, one item each, ordered with the rest of *Needs you* by when
   it arrived.
3. **Given** a member who is not an admin, or the player who asked, **When** they open Home,
   **Then** no join-request item appears for them.
4. **Given** an item, **When** the admin approves or declines it from Home, **Then** the effect is
   identical to answering on the team page, and the item disappears.
5. **Given** another admin already answered, or the player withdrew, **When** this admin presses
   *Approve* or *Decline* on the now-stale item, **Then** they are told it no longer needs them and
   the item disappears; nothing else changes.
6. **Given** an item, **When** the admin wants to see who is asking, **Then** they can open the
   player's profile from it.
7. **Given** a viewer reading German, **When** they see *Needs you*, **Then** every word of every
   item in it — the join request and each of the five existing kinds — is German, except the names
   themselves.
8. **Given** the five existing kinds (team invitations, party requests, party co-admin
   invitations, marketplace invitations and the player's own marketplace applications), **When**
   they are shown, **Then** they say what they said before, link where they linked before and
   offer the same actions — only their wording now follows the viewer's language.

---

### Edge Cases

- **An admin loses admin after being alerted.** They keep the alert, which still opens the team
  page, where they can no longer answer. Home stops showing them the request.
- **An admin is promoted after the request.** They receive no alert for it, but find it on Home
  and in the team's queue like every current admin.
- **A team with one admin.** That admin alone is notified.
- **The player joins the team another way while the request waits** — through an invitation link
  or a personal invitation. The request ends without an answer, exactly as if the player had
  withdrawn it: its alerts leave the admins' inboxes, no admin is asked to decide it any more, and
  nobody is notified (FR-020).
- **The player is banned while the request waits.** For as long as the ban lasts, the request does
  not wait anywhere, and admins' alerts about it name no one. If the ban is lifted, the request
  waits again (FR-021).
- **The player deletes their account.** The request goes with it (feature 037). Admins' alerts
  about it name no one and no longer read as waiting.
- **The team is deleted while a request waits.** The request goes with the team; admins' alerts no
  longer read as waiting, and opening one shows the team no longer exists, as any stale team link
  does today.
- **Two admins answer at once.** Exactly one answer stands (FR-012).
- **An admin answers a request the player just withdrew.** They are told it is no longer waiting;
  the player is told nothing.
- **An Alerts inbox open during a withdrawal.** An admin with the inbox open still sees the alert
  until they reload; opening it takes them to the team page as always, without an error.
- **Declined, then asks again.** A new request, announced again like any other (within FR-023's
  limit).
- **The limit and the other half of the loop.** Only *sending* a request counts towards the ten per
  hour. Withdrawing, and an admin answering, never count and are never refused by it.
- **An admin blocked the player in chat.** The player can still ask to join, and the admin is
  still alerted: a block governs direct conversations only (feature 019), and a join request goes
  to the team's queue, not to a person.
- **An admin with all three *Invites & roster* channels off** receives nothing; the request still
  waits in the queue and on their Home.
- **Email and device notifications already delivered are never recalled.** After a withdrawal or
  an answer, an email or a lock-screen notification may still say someone wants to join.
- **Requests already waiting when this feature is released** are not announced after the fact;
  they appear on the admins' Home and in the queue.
- **A requester with a very long name, and a long team name, in German at 375px** wrap without
  truncation or horizontal scrolling on Home, in the Alerts inbox and in the refusal message.

## Requirements *(mandatory)*

### Functional Requirements

**Telling the admins**

- **FR-001**: When a request to join a team is created, every person who is an admin of that team
  at that moment MUST be notified, and nobody else MUST be — not the player who asked, and not
  members who are not admins.
- **FR-002**: The notice MUST reach each admin on each channel — Alerts inbox, email (FR-024) and
  device — that their own *Invites & roster* setting has on, each channel decided independently:
  turning one channel off MUST never silence another, in either direction.
- **FR-003**: A request MUST be announced exactly once. Repeating a request that is still waiting
  MUST NOT notify anyone again, and two simultaneous identical requests from one player MUST be
  announced once.
- **FR-004**: The alert MUST name the player and the team, and MUST open the team page, where the
  request is answered.
- **FR-005**: The alert MUST show the player's *current* name each time it is shown, and MUST NOT
  keep a copy of the player's name or username of its own. Once the player is banned or has
  deleted their account, the alert MUST name no one, reading as coming from a former player.
- **FR-006**: Email and device notifications MUST be in each recipient's own language (English,
  German or Spanish; English when theirs is unknown), MUST name the team and the player, and MUST
  lead to the team page.
- **FR-007**: An admin's alert MUST stop reading as waiting once its request no longer awaits a
  decision — answered by any admin, the player banned or their account deleted, or the team gone —
  and MUST never offer to answer a request that is not waiting. (A withdrawn request, and one ended
  by the player joining another way, take their alerts with them: FR-022, FR-020.)

**Telling the player**

- **FR-008**: When an admin approves a request, the player MUST be told that the team accepted
  them, and the notice MUST open the team's page.
- **FR-009**: When an admin declines a request, the player MUST be told that the team declined it,
  and the notice MUST open the place to browse teams.
- **FR-010**: The answer MUST reach the player on each channel — Alerts inbox, email (FR-024) and
  device — that their own *Invites & roster* setting has on, each decided independently, in their
  own language.
- **FR-011**: The answer MUST name the team and MUST NOT identify which admin gave it.
- **FR-012**: A request MUST be answered at most once. When two answers to one request arrive
  together, exactly one MUST take effect; the admin whose answer did not MUST be told the request
  was already answered; and the player MUST receive exactly one notice, matching the answer that
  took effect.
- **FR-013**: A request that ends without an answer — withdrawn, ended by the player joining
  another way, or removed with the player's account or the team — MUST NOT produce a notice to the
  player.
- **FR-014**: Wherever the product tells a player about the request they are sending — the
  confirmation before it is sent on the team page, and the acknowledgement after it is sent during
  onboarding — it MUST tell them they will be told the answer.

**What "waiting" means**

- **FR-015**: A request is **waiting** while it has been neither answered nor ended and its player
  is not banned. The team page's queue, Home's *Needs you* and the state of the admins' alerts MUST
  all use this one meaning, so that no two of them ever disagree about whether a request waits.
- **FR-016**: Only a waiting request MUST be answerable. An attempt to answer any other request
  MUST be refused with the same "no longer waiting" outcome, whatever the reason it stopped
  waiting, and MUST change nothing.
- **FR-017**: Asking, withdrawing, approving and declining MUST keep today's rules about who may do
  each — only a signed-in non-member may ask; only the player may withdraw their own request; only
  a current admin may answer — all decided by the server.

**Home**

- **FR-018**: Home's *Needs you* MUST list, for a viewer who is currently an admin of a team, each
  waiting request to that team — one item per request, across every team they administer —
  ordered with the other *Needs you* items by when each arrived.
- **FR-019**: Each item MUST name the player and the team, MUST let the admin open the player's
  profile, and MUST offer *Approve* and *Decline* in place, with exactly the effect of answering on
  the team page. If the request stopped waiting in the meantime, the admin MUST be told it no
  longer needs them and the item MUST disappear.
- **FR-019a**: Every *Needs you* item — the join request and each of the five existing kinds (team
  invitation, party request, party co-admin invitation, marketplace invitation, the player's own
  marketplace application) — MUST be worded in the viewer's language. Apart from the names of
  people, teams and events, no part of any item may be text fixed in one language. The five
  existing kinds MUST otherwise keep what they say, where they link and which actions they offer.

**Requests that outlive their reason**

- **FR-020**: When a player with a waiting request joins that team by any other route, their
  request MUST end without an answer, exactly as if they had withdrawn it (FR-022): no admin is
  asked to decide it and nobody is notified.
- **FR-021**: While a player is banned, their request MUST NOT wait anywhere; if the ban is lifted,
  the request MUST wait again, unchanged.

**Withdrawal and repeated requests**

- **FR-022**: When a player withdraws a waiting request, every alert the team's admins received
  about it MUST be removed from their inboxes — including the inbox of an admin who has since lost
  admin — and each admin who had not read theirs MUST see their unread count drop: immediately if
  they are online (best effort), otherwise by their next visit. The withdrawal and the removal MUST
  happen together or not at all. Email and device notifications already delivered are not
  recalled.
- **FR-023**: The server MUST refuse a player's request to join once that player has sent **10
  requests in the current hour**, counted across all teams. The hour is a fixed window on the
  clock, the way chat's limits already count (feature 019), so a burst straddling the turn of an
  hour can reach at most twice the bound and no more. A refused request MUST store nothing and
  notify no one, MUST tell the player plainly in their language to try again later, and MUST NOT
  be retried automatically. Withdrawing a request and answering one are never limited by this
  bound.

**Email**

- **FR-024**: Both the admins' notice (FR-001) and the player's answer (FR-008, FR-009) MUST also
  be sent by email to each recipient whose *Invites & roster → Email* setting is on, in that
  recipient's language, carrying the site's usual header, footer and notification-settings link.
  An email that cannot be sent MUST NOT undo or block the request or the answer.

**Device notifications**

- **FR-025**: Every device notification this feature sends MUST name the team and say what
  happened in words of its own. None may fall back to the generic "You have a new notification".

**Language**

- **FR-026**: All new interface text MUST exist in English, German and Spanish, following the house
  punctuation rules, and MUST fit at the narrowest supported width in German without truncation or
  horizontal scrolling.

### Key Entities *(include if feature involves data)*

- **Join request** (existing): the player, the team, whether it waits or how it was answered, and
  who answered it and when. It gains nothing new it must store; what changes is that it is
  answered at most once (FR-012) and that its *waiting* meaning is shared by every surface
  (FR-015).
- **Admin alert about a request** (new kind of Alerts row; one per admin at the moment of the
  request): refers to the request and the team, and to the player without keeping their name
  (FR-005). What it reads as — waiting or not — follows the request (FR-007); it is removed with a
  request that is withdrawn or ended by the player joining another way (FR-022, FR-020).
- **Answer notice** (new kind of Alerts row; one per answered request): the team and whether the
  player was accepted or declined. It identifies no admin.
- **Home *Needs you* item** (existing, gains a join-request kind): worked out from the waiting
  requests each time Home is shown; it stores nothing. Every kind now carries the names it needs
  rather than a finished English sentence, so the words around them follow the viewer's language
  (FR-019a).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For 100% of new requests, every admin of the team at that moment receives exactly one
  notice on each channel they have on, and 0 other people receive one.
- **SC-002**: For 100% of answered requests, the player receives exactly one answer notice, and it
  matches the answer that took effect — including when two admins answer at the same moment.
- **SC-003**: No request is ever shown as waiting on one of the team page, Home or an admin's
  alert while another of them shows it as not waiting.
- **SC-004**: After a player deletes their account, no surviving alert anywhere shows their name or
  username.
- **SC-005**: An admin can get from an alert — or from Home — to having answered a request in under
  30 seconds.
- **SC-006**: No single player can cause any admin to be notified of more than 10 join requests
  from them within one clock hour, or more than 20 within any 60 minutes.
- **SC-006a**: After a withdrawal, 0 alerts about the withdrawn request remain in any inbox.
- **SC-007**: Every email and device notification this feature sends is in its recipient's
  language for English, German and Spanish.
- **SC-008**: In German at 375px, the *Needs you* card with one item of every kind, both new kinds
  of Alerts row and the "try again later" message fit without horizontal scrolling, truncation or
  clipped text.
- **SC-009**: Viewed in German or Spanish, *Needs you* contains no English words other than names.

## Assumptions

- **Admins at the moment of the request** are the ones notified; admins added later find the
  request on Home and in the team's queue.
- **Alerts are link-only.** Answering happens on Home or on the team page; the Alerts inbox offers
  no *Approve*/*Decline* of its own.
- **The *Invites & roster* category covers both new kinds.** Its description already reads "Team
  invites, people joining or leaving", so no new setting and no change to the settings page are
  needed; the defaults stay as they are for that category.
- **No reminders.** A request left unanswered is not announced again.
- **Nothing is recalled.** Email already delivered and device notifications already shown keep what
  they said.
- **No back-fill.** Requests already waiting at release are not announced after the fact.
- **Blocks stay chat-only** (feature 019).
- **No message from the player.** A request carries no text today and gains none.
- **The ten-per-hour bound is the same for every player and every team**, like chat's limits
  (feature 019); there is no per-team or per-player setting for it.
- **The privacy policy** already describes what a device notification can contain — including whom
  it concerns — in a way that covers these notices; planning re-verifies this against the German
  text, which is authoritative.
- **All existing accounts are test data**, so data left in an inconsistent state by the defects
  above (for example a request still waiting from someone who already joined) needs no repair.
