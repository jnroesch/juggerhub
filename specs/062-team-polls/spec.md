# Feature Specification: Team Polls — a Team Can Ask Its Members a Question

**Feature Branch**: `062-team-polls`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "GH #365 — Polls: a team cannot ask its members a question. 005 put polls out of scope (`specs/005-team-space/spec.md:9`, `:266`) and nothing since has built them: no poll entity, no message kind, nothing in the frontend. Beyond training times, what teams use their group chats for is questions with a fixed set of answers: 'Thursday instead of Tuesday this week?', 'Who's in for the tournament in May?', 'Which jersey colour?'. Today the only structured question JuggerHub can ask is a training RSVP; everything else is free text in the team chat and a hand count. Two candidate homes: in the team chat as a message kind (ChatMessageKind, 049 ChatAttachment precedent, 047 encryption; inbox preview, search and chat push would all need to know), or as an admin-only news post kind on the team page and in Home's news module. Either way a poll is a bounded set of options, one answer per member (or multi-select), an optional close date, visible tallies, and an author who can close it. Named vs anonymous answers is a product decision that cuts both ways. Open questions: placement; named vs anonymous; single vs multi-select, close date, editing options after the first vote; the 'who's in for X' case vs 016's party flow. Shape once decided: one Poll/PollOption/PollVote model, one migration, endpoints on the chosen surface, a notification to members when a poll opens, UI on the chosen surface, copy in all three catalogues, UI review checklist."

## Context

A team that wants to ask its members something — *Thursday instead of Tuesday this week? Which
jersey colour? Who's up for the beach tournament?* — has one way to do it today: write the
question into the team chat and count the replies by hand. Answers get lost between other
messages, people answer twice or in a different form, and nobody can see at a glance where the
team stands. The product exists to replace exactly this kind of patchwork, and the only structured
question it can ask is a training RSVP.

Feature 005 named polls and put them out of scope; nothing since has built them. This feature
gives a team's admins a way to put a question with fixed answers to the team, and gives every
member one place to answer it and see the result.

**Who does what:**

| Who | Can | Where |
|-----|-----|-------|
| Any current admin of the team | Start a poll; change it until the first answer arrives; move its close date; close it early; delete it; in a named poll, see who has not answered yet | The team page's Polls card |
| Every current member (admins included) | Answer an open poll, change or withdraw that answer until it closes, and see the results | The team page's Polls card; Home's *Needs you* while a poll is unanswered |
| Every current member except the poll's author | Be told that a poll opened | Alerts inbox, email and their devices — each by their own settings |
| Anyone who is not on the team | Nothing: they cannot see that the team has polls at all | — |

**What reading the product showed** — each point shapes a requirement below:

- **The team page is not a members-only page.** Any signed-in player can open it (feature 009
  made it public to signed-in players; 026 made the whole product sign-in-only). What is internal
  to the team — news, contact details, *What's happening* — is shown only to members. A poll's
  question and its answers are team-internal in the same way, so the Polls card is shown only to
  members, and the server refuses the team's polls to anyone else (FR-032, FR-034).
- **"Anonymous" has to hold everywhere, not just on the card.** The server has to know who
  answered, to allow one answer each and to let a member change theirs. For an anonymous poll
  it must never tell anyone which member chose what — not the team page, not Home, not the
  Alerts inbox, not an admin, not the poll's author (FR-016 – FR-019). This covers the answers
  as well as the screen that shows them.
- **Tallies alone can reveal an answer in a small team.** If three members answered and all
  three chose *yes*, everyone knows what each of them said, whatever the poll is called. This is
  inherent to any anonymous vote with visible counts. The spec states it as a known limit
  (FR-019) and does not work around it by hiding counts.
- **Two existing flows already ask "are you in?", and both do more than record an answer.** A
  training RSVP (018) belongs to one session. A party participation request (016) gives the member
  a seat in the team's entry to an event. A poll does neither: answering *yes* to "Who's in for the
  tournament in May?" commits nobody to anything. The owner decided a poll stays that kind of
  question and is not linked to a party (FR-043). The card copy must not suggest an answer reserves
  a place.
- **Team names are copied into alerts, and a rename rewrites them by the team's address**
  (feature 061). An alert that carries the team's address keeps up with a rename automatically;
  one that does not would keep the old name for ever (FR-026).
- **The notification setting a poll would fall under is labelled "News posted to your teams".**
  A poll is not news, so a member deciding whether to hear about polls could not tell from the
  settings page where to look (FR-027).
- **Home's *Needs you* lists what is waiting for the viewer's response**, and an open poll the
  member has not answered is exactly that. 058 made every *Needs you* item speak the viewer's
  language, and a poll item must not bring back an English sentence built on the server (FR-029,
  FR-031).
- **The published Terms of Use list what outlasts an account deletion** — "Two things outlast
  that. Messages you wrote and your news posts stay where they are…". A poll whose author later
  deletes their account stays with the team too (FR-039), so that text would stop being complete.
  The terms and the privacy policy must stay true (FR-040).

**Out of scope, deliberately**: polls in any chat (team, group, party or direct); polls started by
members who are not admins; polls for parties, events or trainings; turning a poll into a party or
filling a party from its *yes* answers; answers written in by the voter ("Other: …"), ranked
choice and images in options; reminders before a poll closes, and any notice when it closes or
when someone answers; reopening a closed poll; results that update live on a page that is already
open; polls in Home's news module, the team's *What's happening* card or the public parts of the
team page; exporting results.

## Clarifications

### Session 2026-09-28

- Q: Where does a poll live? → A: **On the team page, in its own members-only Polls card**, not
  inside the News feed and not in chat. An open poll the member has not answered also appears in
  Home's *Needs you*, and opening a poll sends an Alerts notice. (Rejected: a chat message kind,
  which would mean encryption at rest, inbox preview, chat push and live tallies, and is the
  largest option; a news post kind, admin-only and mixed into the feed; both, which is really two
  features.)
- Q: Who may start a poll? → A: **Team admins only**, like news. A plain member who wants to
  ask the team asks an admin to post the poll. (Rejected: any member — closer to a group chat,
  but noisier, and every member could send a notice to the whole roster.)
- Q: Are answers named or anonymous? → A: **The admin who starts the poll chooses, per poll, and
  the choice is fixed once the poll exists.** "Who's in" needs names; "Which colour" is better
  without them. *Anonymous* means no one in the product sees who chose what — not the author, not
  the admins. (Rejected: always named; always anonymous.)
- Q: How does a "who's in for the tournament" poll relate to the party flow (016)? → A: **It is
  a plain poll with no link.** Answering creates no seat and no party. A party participation
  request stays the way a team commits to a teams-only event. (Rejected: a poll that can seed a
  party — more scope, and it needs the event to exist in JuggerHub already; a separate
  interest-check kind, which would be a different feature from this one.)
- Q: Should a named poll show who has not answered yet? → A: **Yes, to admins only, and only in
  named polls.** Admins chasing a "who's in" question see the current members still missing; plain
  members see only the count. An anonymous poll never shows who has or has not answered, to
  anyone, because whether someone voted can reveal too much in a small team. (Rejected: nobody
  sees it; every member sees it; admins see it in anonymous polls too.)
- Q: Can a member see the results before answering? → A: **The admin chooses per poll when
  starting it**: results *always visible*, or *hidden until you answer*. With hidden results, a
  member who has not answered sees the options and how many have answered, but no counts and no
  names. Their own answer, or the poll closing, reveals the result. The rule applies to everyone,
  the author and admins included. (Rejected: always visible; always hidden until answered.)
- Q: Which notification setting do poll notices fall under? → A: **The existing *Team news*
  setting**, with its description in all three languages widened to say it covers polls. Polls
  come from the same people through the same kind of team communication, and the settings matrix
  gets no extra row. (Rejected: a *Polls* row of its own; *Invites & roster changes*.)
- Q: Does a device notification show the poll's question? → A: **No.** It names the team and
  says a poll was started, the same way news does (055: name the subject, don't reproduce the
  content). The question is in the Alerts row, the email and on the team page. (Rejected:
  showing the question on the lock screen, which 056 allowed only for chat previews.)
- Q: How long does a closed poll stay? → A: **Until an admin deletes it.** It stays listed on the
  card after the open polls, paged, the way news, training responses and party answers are kept.
  No automatic removal and no new retention promise. (Rejected: removed one year, or 90 days,
  after closing.)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An admin asks, the team answers (Priority: P1)

An admin of Hamburg Hammers wants to know whether training should move to Thursday this week. On
the team page they start a poll: the question, the options *Thursday works* / *Stay on Tuesday* /
*Either is fine*, one answer each, names shown. Members open the team page, see the question at
the top of the Polls card, tap their answer, and see straight away how the team stands — how many
chose each option, who chose it, and how many of the team have answered so far. A member who
changes their mind taps a different option; the tally follows.

**Why this priority**: This is the whole feature. Without asking and answering there is nothing
else; the notices, Home and closing all serve this loop.

**Independent Test**: As an admin, start a single-choice named poll with three options. As two
other members, answer it differently; as one of them, change the answer. Confirm each member sees
one answer counted per member, the names under each option, "2 of N answered", and their own
choice marked; confirm a non-member opening the team page sees no Polls card and cannot read the
poll through any address.

**Acceptance Scenarios**:

1. **Given** an admin on their team's page, **When** they start a poll with a question and two to
   ten options, **Then** the poll appears at the top of the team's Polls card for every member, open
   and unanswered, with the author's name and when it was asked.
2. **Given** an open single-choice poll, **When** a member picks an option, **Then** their answer is
   recorded, the tallies include it, and the card shows which option is theirs.
3. **Given** a member who has answered, **When** they pick a different option, **Then** their answer
   moves — it is counted once, under the new option, never twice.
4. **Given** an open multi-choice poll, **When** a member picks two options, **Then** both count, the
   count of members who answered goes up by one, and they can later deselect either.
5. **Given** a member who has answered an open poll, **When** they withdraw their answer, **Then** it
   no longer counts anywhere and the poll is unanswered for them again.
6. **Given** a named poll that some members have not answered, **When** an admin looks at it,
   **Then** they also see which current members have not answered yet — and a plain member looking
   at the same poll sees only how many have.
7. **Given** a plain member (not an admin), **When** they look at the Polls card, **Then** there is no
   way to start a poll — and the server refuses one if they try another way.
8. **Given** a signed-in player who is not on the team, **When** they open the team page, **Then**
   there is no Polls card, and asking for the team's polls directly tells them nothing about
   whether any exist.
9. **Given** a poll started with *results hidden until you answer*, **When** a member who has not
   answered — the author and admins included — looks at it, **Then** they see the options and how
   many have answered, but no counts and no names; **and when** they answer, the result appears.

---

### User Story 2 - Members hear that a poll is waiting (Priority: P1)

The admin starts the poll on Monday evening. Every other member gets a notice: an Alerts row
"Hamburg Hammers asks: Thursday instead of Tuesday this week?", an email with the same question,
and a notification on their phone saying Hamburg Hammers started a poll. Each arrives only where
their own settings say so. On Home, the poll sits under *Needs you*
until they answer it. Opening the notice or the Home item takes them to the poll on the team
page.

**Why this priority**: A poll nobody knows about gets no answers. The problem in the issue is
that people do not see the question at all, as much as that they have no structured way to
answer it.

**Independent Test**: As an admin of a team with three other members, start a poll. Confirm each of
the three has exactly one Alerts row naming the team and the question; that each with email on got
one email in their own language; that a device with notifications on got one notification; that
the author got nothing; that each of the three sees the poll under *Needs you* on Home, and that it
leaves *Needs you* once they answer it.

**Acceptance Scenarios**:

1. **Given** a team with several members, **When** an admin starts a poll, **Then** every current
   member except the author receives exactly one notice of it on each channel their own *Team
   news* setting allows. The Alerts row and the email name the team and the question; the device
   notification names only the team. Nobody outside the team receives anything.
2. **Given** a member who turned email off for team news, **When** a poll opens, **Then** they get the
   Alerts row and any device notification but no email; each switch governs only its own channel.
3. **Given** an open poll the member has not answered, **When** they open Home, **Then** it is listed
   under *Needs you*, in their language, and opening it takes them to that poll on the team page.
4. **Given** that Home item, **When** the member answers the poll, the poll closes, it is deleted, or
   the member leaves the team, **Then** the item is gone the next time Home loads.
5. **Given** the admin who started the poll, **When** they open Home, **Then** their own poll is not
   under their *Needs you* — they know it exists.
6. **Given** a poll that is edited before anyone answered (see Story 5), **When** a member looks at
   the Alerts row they already received, **Then** it shows the corrected question — without becoming
   unread again, moving, or arriving a second time.

---

### User Story 3 - An anonymous poll stays anonymous (Priority: P2)

The team is picking a new jersey colour and the admin does not want anyone to feel watched, so
they start the poll as *anonymous*. Every member sees, before answering, that nobody will see what
they chose. After answering, everyone sees how many picked each colour and how many have answered
— and no one, not the admin who asked and not any other admin, can find out which member chose
what.

**Why this priority**: Without anonymity a poll works for "who's in" but not for a sensitive
choice. It is a property of polls rather than a separate flow, so it comes after the core loop.
Getting it wrong would expose people who were promised privacy, which is why it has its own
story and test.

**Independent Test**: Start an anonymous poll. Have three members answer. As the author, as another
admin and as a member, inspect everything the product returns about the poll — the team page, Home,
the Alerts inbox, and every response the server sends — and confirm none of it connects a member to
an option, while each member can still see which option is their own.

**Acceptance Scenarios**:

1. **Given** the admin starting a poll, **When** they choose *anonymous*, **Then** the poll shows to
   every member, before they answer, that no one will see who chose what.
2. **Given** an anonymous poll with answers, **When** any member — including the author and every
   admin — looks at it, **Then** they see how many members answered, the per-option counts once
   its result is visible to them (FR-012a), no
   names attached to options, and no list of who has or has not answered.
3. **Given** an anonymous poll, **When** a member looks at their own answer, **Then** they see which
   option they chose — and only theirs.
4. **Given** an anonymous poll, **When** anything the server returns about it is inspected by any
   member or admin, **Then** nothing in it identifies which member chose which option.
5. **Given** a named or an anonymous poll, **When** an admin tries to switch it to the other kind,
   **Then** there is no way to, and the server refuses it — the promise a member answered under
   does not change afterwards.

---

### User Story 4 - A poll closes and its result stays (Priority: P2)

The admin set the jersey poll to close on Sunday at 20:00. At that moment it stops taking answers
on its own; members who open the team page afterwards see the final result, marked as closed.
Another poll has done its job early: most of the team has answered, so an admin closes it by hand.
A deadline that turns out to be too short can be moved while the poll is still open.

**Why this priority**: An optional close date and closing early are the difference between a
question that settles something and one that stays open for ever. The core loop works without
them, so they come second.

**Independent Test**: Start a poll closing a few minutes ahead; after that moment, confirm answers
are refused and the poll reads as closed with its result intact. Start another and close it early
as a different admin; confirm the same. On a third, move the close date later after answers exist;
confirm the answers are untouched.

**Acceptance Scenarios**:

1. **Given** a poll with a close date, **When** that moment passes, **Then** the poll is closed: no
   answer, change or withdrawal is accepted from then on, and the final result stays visible to
   members.
2. **Given** an open poll, **When** any current admin closes it early and confirms, **Then** it closes
   at once, the same way, and cannot be reopened.
3. **Given** a member with the poll open on screen, **When** they answer after it closed, **Then**
   the server refuses, and they are told the poll has closed and shown the final result.
4. **Given** an open poll with answers, **When** an admin moves its close date to a later moment or
   removes it, **Then** the poll stays open and every existing answer stays as it was.
5. **Given** a closed poll, **When** a member looks at the Polls card, **Then** it is listed after the
   open polls, marked closed, with the final counts and — for a named poll — the names.

---

### User Story 5 - Admins correct or remove a poll (Priority: P3)

An admin notices a typo in the options minutes after posting, before anyone answered, and fixes
it in place: nobody is notified again, and the notices already delivered show the corrected
question. Another poll was posted to the wrong team; an admin deletes it, and it disappears
everywhere — from the team page, from every member's Alerts inbox and from Home.

**Why this priority**: Mistakes happen, and without a correction route an admin's only fix is a
second poll and a second round of notices. Posting and answering stand on their own without it.

**Independent Test**: Start a poll, edit its question and options before any answer, and confirm the
card and every recipient's Alerts row show the new text, with nothing re-sent. Answer it, and
confirm the question and options can no longer be changed. Delete it, and confirm nothing of it
remains for any member: no card entry, no Alerts row, no Home item.

**Acceptance Scenarios**:

1. **Given** an open poll nobody has answered, **When** any current admin changes its question,
   options or single/multi-choice setting and saves, **Then** members see the new version, and no
   notice is sent again.
2. **Given** a poll with at least one answer, **When** an admin tries to change its question,
   options or single/multi-choice setting, **Then** there is no way to, and the server refuses it,
   so nobody's answer comes to mean something they did not choose.
3. **Given** an edit and a first answer arriving at almost the same moment, **When** both are
   processed, **Then** either the edit lands first and the answer is to the edited poll, or the answer
   lands first and the edit is refused — never an answer to an option that no longer exists.
4. **Given** any poll, open or closed, **When** any current admin deletes it and confirms, **Then** the
   poll and all its answers are gone, its Alerts rows are removed from every recipient's inbox, and
   it leaves every member's *Needs you*.
5. **Given** the admin who started a poll and has since stopped being an admin, **When** they look at
   it, **Then** they can answer it like any member but no longer change, close or delete it — while
   every current admin can.

---

### Edge Cases

- **A member leaves, is removed, or is banned.** Their answers stop counting and stop being shown
  at once, in every poll of that team, and the number of members answered goes down with them. A
  member who comes back finds the answer they gave to a poll that is still open counted again, and
  can change it. A banned player who is reinstated is counted again the same way.
- **A member deletes their account.** Their answers are removed for good. A poll they started
  stays with the team, showing the placeholder used for a former player where the author's name
  was.
- **The team is renamed.** The poll notices already delivered show the new name (the owner's 061
  decision, applied here).
- **The team is deleted.** Its polls and every answer to them go with it.
- **A team with one member.** An admin alone on the team can start a poll and answer it. Nobody is
  notified, because nobody else is on the team.
- **Ten open polls already.** Starting an eleventh is refused with a plain message saying the team
  already has the most open polls it can have. Closing or deleting one frees a place.
- **Duplicate options.** Two options that differ only in letter case or surrounding spaces are
  refused as duplicates, so members never face two identical choices.
- **A close date in the past, or more than a year ahead.** Refused, with the reason.
- **Clearing every choice in a multi-choice poll.** This is the same as withdrawing the answer.
- **Answering only to peek, in a poll with hidden results.** A member can answer, see the result,
  and withdraw. After withdrawing the result is hidden from them again, but they have seen it.
  This is inherent to "hidden until you answer" and is accepted. Hiding results reduces
  following the crowd; it does not guard a secret.
- **A member opens the page just as the close date passes.** The server decides by the close
  moment. An answer arriving after it is refused, even if the screen still showed the poll as
  open.
- **Two admins close or delete the same poll at once.** The poll ends up closed, or deleted, once.
  The admin whose action found it already gone is told so plainly.
- **A long question or option with no spaces** (a pasted link, a long German compound). It wraps
  inside the card at 375px. It never widens the page and is never cut off.
- **A question or option that looks like a link or markup.** It shows exactly as typed. It is
  never turned into a link or formatted.
- **Every member answered the same way in an anonymous poll.** Everyone can then infer each
  member's answer. That follows from showing counts at all and is accepted (FR-019).
- **A notice about a poll that has since been deleted.** The notice is gone with it. A device
  notification already shown on a phone cannot be recalled.

## Requirements *(mandatory)*

### Functional Requirements

**Starting a poll**

- **FR-001**: A current admin of a team MUST be able to start a poll on that team. It has a
  question, two to ten options, a choice between *one answer* and *any number of answers*, a
  choice between *names shown* and *anonymous*, a choice between *results always visible* and
  *results hidden until you answer*, and an optional close date.
- **FR-002**: The question MUST be 1–200 characters and each option 1–80 characters, after
  surrounding spaces are trimmed. They are plain text, shown exactly as written, never formatted
  and never turned into links.
- **FR-003**: No two options of one poll may be the same when letter case and surrounding spaces
  are ignored. The options keep the order the admin gave them.
- **FR-004**: A close date, when given, MUST lie in the future and no more than one year ahead.
  It is entered and shown in the viewer's local time.
- **FR-005**: A team MUST NOT have more than 10 open polls at once. Starting another is refused
  with a plain message until one closes or is deleted.
- **FR-006**: Only current admins of the team may start a poll. The server decides this on every
  request, whatever the page shows.

**Answering**

- **FR-007**: Every current member of the team, admins and the poll's author included, MUST be
  able to answer an open poll: exactly one option for a one-answer poll, one or more for a
  multi-answer poll.
- **FR-008**: A member has at most one answer per poll. Answering again replaces their previous
  answer and never adds a second one.
- **FR-009**: A member MUST be able to change or withdraw their answer while the poll is open.
  Withdrawing leaves the poll unanswered for them.
- **FR-010**: An answer that refers to an option the poll does not have, gives more than one
  option to a one-answer poll, or arrives after the poll closed MUST be refused, with a reason
  the member can read in their own language.
- **FR-011**: Answering MUST NOT reserve a place, create a party, respond to a training, or
  commit the member to anything outside the poll.

**Seeing the result**

- **FR-012**: Every current member MUST be able to see every poll of their team, open or closed.
  For each one they see:
  - the question and the options in order;
  - how many of the team's current members have answered;
  - whether it is open or closed, and when it closes or closed;
  - whether answers are named or anonymous;
  - whether results are always visible or hidden until they answer;
  - who asked it, and when.

  Where FR-012a allows, they also see the result: the number of members who chose each option.
- **FR-012a**: Whether a member sees a poll's result depends on how the poll was started:
  - For a poll started as *results always visible*, every member sees the result at all times.
  - For a poll started as *results hidden until you answer*, a member sees it only while they
    have an answer in the poll, or once the poll is closed.
  - A member who withdraws their answer from an open poll with hidden results stops seeing the
    result again.

  The rule applies equally to the author and to admins. A member for whom the result is hidden
  MUST receive no per-option counts and no names from the server, whatever the page shows. The
  number of members who have answered stays visible to them.
- **FR-013**: A member MUST see which option or options are their own.
- **FR-014**: In a named poll, every member who can see its result (FR-012a) MUST see which
  members chose each option.
- **FR-014a**: In a named poll, current admins of the team MUST also see which current members
  have not answered yet. Plain members see only how many have answered (FR-012). The list follows
  the same membership rules as the answers (FR-015).
- **FR-015**: Only the answers of people who are currently members of the team, and neither
  banned nor deleted, count and are shown. Every count and every list of names follows the
  current membership.

**Anonymous polls**

- **FR-016**: Whether a poll is named or anonymous MUST be chosen when it is started and MUST
  NOT change afterwards, by anyone, for any reason.
- **FR-017**: Before answering, every member MUST be able to see whether the poll is named or
  anonymous, and whether its result is visible now or will be shown once they answer.
- **FR-018**: For an anonymous poll, nothing the product shows or returns to anyone — the poll's
  author, any admin, any member — may connect a member to the option they chose, or reveal
  whether a particular member has answered at all, apart from showing each member their own
  answer. This applies to the team page, Home, the Alerts inbox, email, device notifications and
  every server response. Home's *Needs you* lists a poll only for the viewer's own unanswered polls
  (FR-031), so it tells no one about anyone else.
- **FR-019**: The spec accepts that per-option counts can reveal answers when few members voted
  or everyone chose the same option. The card MUST NOT imply more privacy than that. The
  *anonymous* wording promises that no one sees *who chose what*, never that no one can work it
  out.

**Closing**

- **FR-020**: A poll with a close date MUST close by itself at that moment. From then on no
  answer, change or withdrawal is accepted, and the result stays as it was.
- **FR-021**: Any current admin MUST be able to close an open poll early, after confirming. Closing
  is final: a closed poll cannot be reopened.
- **FR-022**: While a poll is open, any current admin MUST be able to move its close date to
  another moment allowed by FR-004 or remove it. Existing answers stay untouched.

**Changing and deleting**

- **FR-023**: While nobody has answered a poll, any current admin MUST be able to change its
  question, its options, its one/many-answers setting and whether its result is visible before
  answering. Once any member has answered, none of these four can be changed. The server enforces
  this even when an edit and a first answer arrive together (User Story 5, scenario 3).
- **FR-024**: Any current admin MUST be able to delete any poll of their team, open or closed,
  after confirming. Deleting removes the poll, every answer to it, its Alerts rows from every
  recipient's inbox (with unread counts lowered to match), and its *Needs you* entries. This
  happens in one all-or-nothing step: a poll is never gone while its notices remain, and never
  still there after its notices are gone.
- **FR-025**: "Any current admin" means whoever holds the admin role on the team at the moment of
  the action. An author who is no longer an admin keeps only a member's abilities.

**Notices**

- **FR-026**: When a poll is started, every current member except its author MUST receive exactly
  one notice of it on each channel their settings allow (FR-027). Every notice opens that poll on
  the team page. The Alerts row and the email name the team and the question; a device notice
  names only the team (FR-028). The notice MUST carry the team's address so that a later rename
  of the team reaches it.
- **FR-027**: A poll notice falls under the member's existing *Team news* setting, channel by
  channel: in-app, email and device, each deciding only for itself. The setting's description in
  every language MUST say that it covers polls as well as news. No new setting is added.
- **FR-028**: A notice sent to a device MUST name the team and say that a poll was started, but
  MUST NOT contain the question or any option. Device notices for news work the same way, which
  keeps the question off lock screens.
- **FR-029**: Poll notices, the email and the *Needs you* item MUST be worded in the recipient's
  language (English, German or Spanish). No new text is assembled in one language on the server
  and shown to everyone.
- **FR-030**: Changing a poll before its first answer (FR-023) MUST update the question shown in
  its Alerts rows. The rows are not marked unread again, not moved and not sent again. Closing a
  poll, moving its close date and answering it send nothing to anyone.

**Home**

- **FR-031**: Home's *Needs you* MUST list, for each team the viewer belongs to, every open poll
  they have not answered and did not start. The item names the team and the question and opens
  that poll on the team page. It MUST disappear once the poll is answered, closed or deleted, or
  the viewer leaves the team.

**Access**

- **FR-032**: Only current members of a team may see or answer its polls. Anyone else, whether
  signed in or not, MUST be refused without learning whether the team has any polls, how many, or
  what they ask.
- **FR-033**: Every rule in this specification — who may start, answer, change, close or delete
  a poll, what an anonymous poll reveals, and when a poll is closed — MUST be enforced by the
  server on every request, whatever the page shows.

**The team page**

- **FR-034**: The team page MUST show members a Polls card listing open polls first (newest
  first) and closed polls after them (most recently closed first). Only a limited number of
  closed polls is shown at first, with the rest available on request. A closed poll stays until
  an admin deletes it; nothing removes it automatically.
- **FR-035**: For members who are not admins, an empty Polls card shows a plain empty state. For
  admins it also offers to start a poll.
- **FR-036**: Answering MUST take no more than one press per option chosen, plus at most one
  press to confirm. A member MUST never need to leave the team page to answer.
- **FR-037**: Every text on the card — labels, states, errors, confirmations, the anonymous
  notice — MUST exist in English, German and Spanish. Errors are chosen by what the server
  refused, never by displaying the server's own wording.
- **FR-038**: Starting, answering, changing, closing and deleting MUST NOT be retried
  automatically after a failure. A member retries by pressing again.

**Account deletion and the published texts**

- **FR-039**: Deleting an account MUST remove every poll answer the account gave. A poll the
  account started stays with the team, with the author shown as the placeholder for a former
  player.
- **FR-040**: The published Terms of Use and privacy policy MUST remain true once polls exist,
  in all three languages with German authoritative. This applies in particular to the sentence
  listing what outlasts an account deletion. Any wording changed MUST describe categories of
  data, not individual features.

**Limits of this feature**

- **FR-041**: Polls exist only for teams. There are no polls in chat, parties, events or
  trainings.
- **FR-042**: Polls MUST NOT appear in Home's news module, in the team's *What's happening*
  card, or in any part of the team page shown to non-members.
- **FR-043**: A poll is not connected to the party flow (016). Its options are plain text and
  refer to nothing in the product.

### Key Entities *(include if feature involves data)*

- **Poll**: A question an admin put to one team. It records the team, the author, the question,
  whether it allows one answer or several, whether answers are named or anonymous (fixed at
  creation), whether the result is always visible or hidden until the member answers, an
  optional close date, and when it was closed early, if it was. When the author's
  account is deleted, the poll stays and names no one.
- **Poll option**: One of a poll's two to ten fixed answers, with its text and its position in
  the order the admin gave. Options can be replaced only while the poll has no answers.
- **Poll answer**: One member's choice in one poll: the member and the option or options they
  chose, one per member per poll. It counts only while that person is a current member of the
  team and neither banned nor deleted. It is removed when their account is deleted, and it goes
  with the poll when the poll is deleted.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An admin on a phone can start a three-option poll in under one minute from opening
  the team page.
- **SC-002**: A member can answer an open poll from the team page in at most two presses for a
  one-answer poll, and reach it from its notice or Home item in one more.
- **SC-003**: For an anonymous poll, inspecting everything the product returns to the author, a
  second admin and a member finds zero places that connect a member to an option, other than
  each member's own answer.
- **SC-003a**: For a poll with hidden results, a member who has not answered receives zero
  per-option counts and zero names from anything the product returns, until they answer or the
  poll closes. The same holds when that member is the author or an admin.
- **SC-004**: When a poll opens, every current member except the author gets exactly one notice
  per channel their settings allow, and people outside the team get none. The test is a team
  with members who have mixed settings.
- **SC-005**: After a poll closes, by its date or early, it accepts zero answers, and its counts
  change only when people leave or rejoin the team.
- **SC-006**: At every moment, every count on a poll equals the number of current members'
  answers behind it. No member is ever counted twice under one option.
- **SC-007**: After a poll is deleted, no trace of it is left for any member: no card entry, no
  Alerts row and no *Needs you* item.
- **SC-008**: In German at 375px width, the Polls card shows a poll with ten options of the
  maximum length, the anonymous notice and the admin actions with no horizontal scrolling and no
  text cut off.
- **SC-009**: For a signed-in player who is not on the team, the team page loads and looks
  exactly as it did before this feature, with no trace of polls.

## Assumptions

- **The owner decided nine points**, recorded under Clarifications:
  - placement;
  - who asks;
  - anonymity;
  - the relation to parties;
  - the list of who has not answered;
  - when results are visible;
  - the notification setting;
  - the device notice;
  - how long closed polls stay.

  The following are defaults this spec chose, which planning or a later clarification may revisit:
  - one-answer or multi-answer is chosen per poll;
  - the close date is optional;
  - the question and options lock at the first answer, while the close date can still move;
  - a member can change or withdraw their answer while the poll is open;
  - any current admin can close early, and closing is final;
  - the author does not see their own poll in *Needs you*;
  - the limits are 2–10 options, 200/80 characters, 10 open polls per team and a close date at
    most one year ahead.
- **Answers are team content like training responses, not chat.** They are kept the same way the
  team's news and training responses are, and are not encrypted the way chat messages are (047).
  They are shown only to members of the team.
- **No limit on how often polls are started beyond FR-005.** Only the team's own admins can
  start one, and a team can have at most ten open at once. There is no per-hour cap like 058's for
  join requests, which have open reach: anyone can send one to any team. An admin who spams their
  own team is a matter for the team, not for a rate limit.
- **Results are current when the page loads.** A page that is already open does not update as
  others answer. Reloading, or the member's own action, shows the current state. This matches
  news, join requests and the rest of the team page today.
- **Only current admins manage polls.** An author who stops being an admin keeps only a member's
  abilities (feature 057 treats news posts the same way).
- **Membership changes are read, not written.** Leaving, being removed, being banned or being
  reinstated changes whose answers count through current membership. No answer is rewritten
  when someone leaves, and none is lost for a member who comes back.
- **Existing features this relies on are unchanged in their own behaviour:**
  - team membership and roles (005, 006);
  - the Alerts inbox, notification preferences, email and device notifications (010, 011, 055);
  - Home's *Needs you* and its per-language items (025, 058);
  - the rename rewrite of delivered alerts (061);
  - the removal of a deleted item's alerts (057);
  - account deletion (037).
