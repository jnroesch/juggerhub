# Feature Specification: Event and Party News Posts Can Be Edited and Deleted

**Feature Branch**: `059-event-party-news-edit-delete`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Event news and party news posts can be edited and deleted (GH #367 + GH #368, in one feature). Follow-up to 057 (team news edit/delete). Owner decisions already taken: (1) any current event admin may edit and delete any event news post, and any current party admin may edit and delete any party news post, whoever wrote it; (2) party news Alerts rows carry NO copy of the post, so an edit leaves them untouched — #368's premise that they hold a copy is wrong; on delete the post's Alerts rows are REMOVED for every recipient, former crew included, together with the post, and unread badges refreshed afterwards; (3) the edit/delete UI (per-post menu, inline editor, delete confirmation) becomes ONE shared component used by team, event and party news, and the team page moves onto it. Event news creates no Alerts rows and sends no email; party news emails an excerpt (out of reach once sent — the delete confirmation says so). Edited marker on the event page, the party pages and Home news. Edit is silent. Hard delete behind confirmation. Event news max 2000 characters, party news 1000."

## Context

Feature 057 made **team** news editable and deletable (GH #363) and named the other two kinds
of news as follow-ups: **event** news (GH #367) and **party** news (GH #368). Both are still
permanent today. A wrong start time in an event update, or a wrong meeting point in a party
update (*"Meet 07:00 at the Aral…"*), stays in front of everyone who can see it for as long
as the event or party exists. This feature closes both, with the rules 057 settled.

**Where a post goes once it is sent.** An edit or a delete behaves differently in each place,
so every place is accounted for:

| Kind | Where | What it holds | Can this feature change it? |
|------|-------|---------------|-----------------------------|
| Event news | The event page's News card (every signed-in player) | Reads the post itself | Yes — shows the current text |
| Event news | Home's News module and its "See all" page (players connected to the event) | Reads the post itself | Yes — shows the current text |
| Event news | Alerts, email, push | **Nothing** — posting event news notifies nobody | Nothing to change |
| Party news | The party page's news section and the party's news page (the crew only) | Reads the post itself | Yes — shows the current text |
| Party news | Home's News module and its "See all" page (the crew) | Reads the post itself | Yes — shows the current text |
| Party news | Each recipient's Alerts inbox | One row per crew member naming the team and the event (*"Party update · Team @ Event"*) and opening the party's news page. **No copy of the post's text** | Delete: yes, the rows are ours. Edit: nothing to correct |
| Party news | Email, for crew with *Team news → Email* on | The post's opening text | **No** — already delivered |
| Party news | Push | The event's name and a fixed sentence — **no post text** | Nothing to correct; a shown notification cannot be withdrawn |

**One correction to GH #368, found by reading the product**: the issue says each party-news
Alerts row holds a copy of the post, like team news. It does not. A party-news row names the
team and the event, and nothing the admin wrote. So an edit leaves the rows exactly as they
are: there is no stale text in them to correct. The issue's question "refresh the excerpt on
edit?" therefore does not arise. What remains is the delete (Clarifications).

**One consistency change beyond the two issues (owner decision)**: the controls that edit and
delete a post — its menu, the in-place editor and the delete confirmation — become one shared
piece used by every kind of news, and the team page moves onto it. For team news this changes
nothing a member can see or do. It exists so the four news surfaces cannot drift apart.

**Out of scope, deliberately**: notifying anyone about event news (event news stays
un-notified, as today), an edit history, rich text, recalling email already sent, updating
other people's open pages live, and any change to who may *post* news.

## Clarifications

### Session 2026-09-28

- Q: Who may edit and delete event news and party news posts? → A: **Any current admin, any
  post** — 057's rule carried over. Any current event admin may edit and delete any post of
  that event, and any current party admin any post of that party, whoever wrote it. Event
  co-admins already share their powers (006) and party admins theirs (016). **Accepted
  consequences**, as for team news: another admin's edit appears under the original author's
  name, the marker says only *edited*, and an admin can edit or delete a post whose author has
  left, lost admin, been banned or erased their account.
- Q: Party-news Alerts rows carry no text, so an edit leaves them as they are. When a party
  post is deleted, what happens to the alerts it produced? → A: **They are removed** — for
  every recipient, including crew who have since left, and each recipient's unread count
  drops. No alert is left announcing an update that no longer exists, which matters most for a
  post sent to the wrong party.
- Q: The edit and delete controls now exist on four surfaces. How are they built? → A: **One
  shared piece, and the team page moves onto it.** Team news keeps every behaviour 057 gave
  it; the rest of the product gains the same controls.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An event admin corrects or removes an event update (Priority: P1)

The organisers of a tournament posted *"Check-in opens at 08:00"*. It opens at 09:00. One of
the co-admins — not the one who wrote it — opens the post's menu on the event page, chooses
**Edit**, changes the time and saves. The update now reads 09:00, stays where it was, and is
marked **edited**. Later an admin notices an update that was meant for a different tournament
entirely. They choose **Delete**, confirm, and it is gone from the event page and from every
connected player's Home.

**Why this priority**: Event news is read by every player who looks at the event, including
teams deciding whether to travel. A wrong time or place there has the widest reach of any
news in the product, and today nobody can fix it.

**Independent Test**: As an event admin, post an event update, edit it, and confirm the event
page shows the new text in the same position with an "edited" marker; then delete it through
the confirmation and confirm it is gone from the event page and from Home.

**Acceptance Scenarios**:

1. **Given** any post on an event they administer, **When** an admin changes its text and
   saves, **Then** the event page shows the new text in the post's original position, with its
   original author and posting date, marked edited — whether that admin wrote it or not.
2. **Given** the edit form is open, **When** the admin cancels, or saves the text unchanged,
   **Then** the post is exactly as it was and is not marked edited.
3. **Given** the edit form is open, **When** the admin clears the text and tries to save,
   **Then** the save is refused with a plain explanation and the post is unchanged.
4. **Given** the edit form is open, **When** the admin reads it, **Then** it tells them that
   saving notifies nobody.
5. **Given** any post on their event, **When** an admin chooses Delete, **Then** a confirmation
   appears saying the post will disappear from the event page for everyone, and nothing is
   removed until they confirm; dismissing it leaves the post in place.
6. **Given** the admin confirms, **When** any player opens the event page, Home or Home's "See
   all" news, **Then** the post is not there.
7. **Given** a signed-in player who is not an admin of the event, **When** they view the event
   page, **Then** they see no edit or delete controls on any post.

---

### User Story 2 - A party admin corrects or removes a party update (Priority: P1)

A party admin posted *"Meet 07:00 at the Aral on the A7"*. It is the other Aral. They open
the post's menu — on the party page or on the party's news page, whichever they are looking
at — and correct it. The crew sees the corrected text marked **edited**; nobody's phone
buzzes again, and their Alerts inbox is unchanged. Another time, an admin posts an update to
the wrong party. They delete it: it disappears from both party pages and from the crew's Home,
and the "Party update" alert it produced disappears from every crew member's Alerts inbox —
including a member who has since left the party. A crew member who had not read that alert
sees their unread count drop.

**Why this priority**: Party news is where a crew is told where and when to meet on the day.
A wrong detail there sends people to the wrong place, and a post sent to the wrong party
reaches people it was never meant for.

**Independent Test**: As a party admin, post an update, edit it and confirm both party pages
show the new text marked edited while no member received anything new; then delete it through
the confirmation and confirm it is gone from both party pages, from Home, and from every
recipient's Alerts inbox, and that an unread recipient's count dropped by one.

**Acceptance Scenarios**:

1. **Given** any post in a party they administer, **When** a party admin changes its text and
   saves — on the party page or on the party's news page — **Then** both pages show the new
   text in the post's original position, with its original author and posting date, marked
   edited.
2. **Given** that edit, **When** any crew member checks their alerts, email or device, **Then**
   nothing new has arrived, their unread count is unchanged, and the alert they already had
   for the post is exactly as it was.
3. **Given** the edit form is open, **When** the admin cancels, saves the text unchanged, or
   clears it and tries to save, **Then** the post is unchanged and not marked edited (the empty
   save is refused with a plain explanation).
4. **Given** the edit form is open, **When** the admin reads it, **Then** it tells them that
   saving will not notify the crew again.
5. **Given** any post in their party, **When** an admin chooses Delete, **Then** a confirmation
   appears saying the post disappears for the whole crew together with its alerts, and that
   copies already sent by email stay with their recipients; nothing is removed until they
   confirm.
6. **Given** the admin confirms, **When** any crew member opens either party page, Home or
   Home's "See all" news, **Then** the post is not there.
7. **Given** the admin confirms, **When** anyone who received an alert for the post opens their
   Alerts inbox, **Then** that alert is gone — including for someone who has since left the
   party — and a recipient who had not read it sees their unread count drop by one.
8. **Given** a crew member who is not a party admin (including a guest from the market),
   **When** they view either party page, **Then** they see no edit or delete controls.

---

### User Story 3 - Every copy tells the same story (Priority: P2)

A player follows several events and a party from Home rather than from each page. When an
admin corrects an event or party update, Home's News module shows the corrected text marked
**edited**, just like the page the post lives on — and so does the "See all" news page.

**Why this priority**: A correction is only as good as the most-read place the post appears.
It follows US1 and US2 because it needs an edit to exist first.

**Independent Test**: Edit an event post and a party post, then confirm Home's News module and
"See all" page show both with the new text marked edited, while posts that were never edited
carry no marker.

**Acceptance Scenarios**:

1. **Given** an edited event post in a player's Home news, **When** they open Home or its "See
   all" news, **Then** it shows the current text marked edited.
2. **Given** an edited party post in a crew member's Home news, **When** they open Home or its
   "See all" news, **Then** it shows the current text marked edited.
3. **Given** a post that has never been edited — including every event and party post that
   existed before this feature — **When** any surface shows it, **Then** it carries no marker.
4. **Given** an edited team post, **When** any surface shows it, **Then** it is marked exactly
   as it was before this feature.

---

### Edge Cases

- **The post is gone before the admin acts.** Another admin deleted it while this admin had the
  page open. Saving an edit or confirming a delete tells them plainly that the post no longer
  exists and removes it from their view; nothing else fails.
- **Two admins edit the same post at once.** The last save wins. No merge, no warning.
- **Another admin edits my post.** It still shows my name, marked *edited*, not by whom (owner
  accepted, Clarifications).
- **The author can no longer act.** They stopped being an admin, left the party or the team,
  deleted their account (the post survives under a neutral placeholder) or were banned. Any
  current admin can still edit or delete the post; the former admin cannot.
- **A post id from elsewhere.** An event post presented under another event, or a party post
  under another party, is treated as not found — even for someone who administers both.
- **Not allowed to see the party's news.** A party's news is private to its crew. Someone who
  is not in the crew — including a member of the party's team who is not in it — and a party
  that does not exist get the same not-found answer as reading the party's news does today.
- **Allowed to see, not allowed to act.** Any signed-in player can read an event's news; a
  crew member can read the party's. Neither sees controls unless they are an admin of that
  event or party, and a request made anyway is refused.
- **A cancelled or ended event.** Its admins can still edit and delete its news, and a party's
  admins can still edit and delete the party's news after the event. The page stays readable,
  so a wrong post on it stays harmful. Posting new event news on a cancelled event stays
  unavailable, as today.
- **Text rules.** An edited text follows the posting rules of its kind: not empty or only
  spaces, at most 2,000 characters for event news and 1,000 for party news. Surrounding spaces
  are trimmed.
- **Order and date.** An edit never moves a post and never changes its posting date. It is not
  re-announced; posting a new update is how to announce.
- **An Alerts inbox open during a party-post delete.** It still shows the row until reloaded;
  opening it takes the member to the party's news page as always, without an error.
- **The author never got an alert.** Posting never alerts the author, so there is nothing of
  theirs to remove.
- **A crew member with in-app alerts for this category turned off** received no row, so a
  delete has nothing to remove for them.
- **Email already delivered** keeps the text it was sent with, after an edit or a delete. The
  party-post delete confirmation says so. Event news is never emailed.
- **The party is disbanded or the event deleted** — their posts go with them, exactly as
  today.

## Requirements *(mandatory)*

### Functional Requirements

**Editing**

- **FR-001**: An event admin MUST be able to change the text of an event news post, and a party
  admin the text of a party news post (who, exactly: FR-012).
- **FR-002**: Edited text MUST follow the posting rules of its kind: after trimming surrounding
  whitespace it is non-empty and at most **2,000** characters for event news and **1,000** for
  party news, and it is stored trimmed. A text that breaks these rules is refused and the post
  is left unchanged.
- **FR-003**: Saving a text identical to the current one (after trimming) MUST change nothing
  and MUST NOT mark the post edited.
- **FR-004**: A saved change MUST record that the post was edited and when. The post keeps its
  author, its posting date and its position; only the most recent text is kept (no history).
- **FR-005**: Editing MUST NOT notify anyone: no new Alerts row, no email, no push, and no change
  to any unread count. The edit form MUST tell the admin this before they save.
- **FR-006**: Editing a party news post MUST leave every Alerts row already delivered for it
  unchanged: not unread again, not moved, no realtime alert. (The rows quote none of the post's
  text, so they hold nothing to correct.)

**Deleting**

- **FR-007**: An event admin MUST be able to delete an event news post, and a party admin a
  party news post (who, exactly: FR-012). Deletion is permanent: no undo, no bin, no retained
  copy.
- **FR-008**: The interface MUST ask for explicit confirmation before a delete. The confirmation
  MUST say who the post disappears for (everyone who can see the event; the whole crew,
  together with its alerts). For party news it MUST also say that copies already sent by email
  stay with their recipients.
- **FR-009**: Deleting a party news post MUST remove every Alerts row that announced it, for
  every recipient — including people who have since left the party — and the post and its rows
  MUST go together or not at all. Each affected recipient's unread count MUST drop accordingly:
  immediately for someone who is online (best effort), otherwise by their next visit.
- **FR-010**: Deleting MUST NOT notify anyone.

**Who may do what**

- **FR-011**: Every edit and delete MUST be decided by the server.
  - Event news: an event that does not exist MUST receive the same not-found answer as reading
    its news; a signed-in player who is not an admin of the event MUST be refused.
  - Party news: a party that does not exist and a caller who is not in its crew MUST receive the
    same not-found answer as reading its news; a crew member who is not a party admin MUST be
    refused.
- **FR-012**: **Any current admin** of the event MUST be able to edit and delete **any** of its
  news posts, and **any current admin** of the party any of the party's posts, whoever wrote
  them — including a post whose author has left, lost admin, been banned or erased their
  account. Authorship grants nothing on its own. Posting rules stay unchanged.
- **FR-013**: A post MUST be reachable only through its own event or party. A post presented
  under another event's or party's address MUST be treated as not found, whatever the caller's
  role in either.
- **FR-014**: The interface MUST offer edit and delete on every post to that event's or party's
  admins wherever the post is listed for them — the event page, the party page and the party's
  news page — and MUST show everyone else no controls at all. These controls are a
  convenience; FR-011 is the boundary.
- **FR-015**: Neither the event's state (cancelled, ended) nor the party's MUST restrict editing
  or deleting existing news.

**What readers see**

- **FR-016**: The event page, the party page and the party's news page MUST mark an edited post
  as edited, beside its author and date.
- **FR-017**: Home's News module and its "See all" page MUST mark edited event and party posts
  the same way they already mark edited team posts.
- **FR-018**: A post that has never been edited — including every post that exists when this
  feature is released — MUST NOT carry the marker.
- **FR-019**: Each feed MUST stay ordered newest-first by posting time.

**When something goes wrong**

- **FR-020**: If the post no longer exists when an admin saves or confirms a delete, the admin
  MUST be told plainly and the post MUST disappear from their view.
- **FR-021**: A failed save MUST keep the admin's typed text in the form so they can try again. A
  failed delete leaves the post in place. Neither is retried automatically.

**One behaviour everywhere**

- **FR-022**: The post menu, the edit form and the delete confirmation MUST look and behave the
  same on every news surface — the team page, the event page, the party page and the party's
  news page — differing only in the words that describe who is affected.
- **FR-023**: Team news editing and deleting MUST keep every behaviour feature 057 specified.

**Language**

- **FR-024**: Every new piece of interface text MUST exist in English, German and Spanish,
  following the house punctuation rules, and MUST fit at the narrowest supported width in German
  without truncation or horizontal scrolling.

### Key Entities *(include if feature involves data)*

- **Event news post** (existing): the author, the event, the text and the posting date. Gains a
  record of **when it was last edited** (absent for a post never edited).
- **Party news post** (existing): the author, the party, the text and the posting date. Gains
  the same record of when it was last edited.
- **Alerts row for a party news post** (existing, one per recipient): identifies the post it
  announced; holds the team's and event's names but none of the post's text. Removed when the
  post is deleted; untouched by an edit.
- **Home news item** (existing, shared by team, event and party news): already carries whether
  the item was edited; from now on that is true for edited event and party posts too.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An admin can correct an event or party post from the page it is listed on, without
  leaving that page, in under 30 seconds.
- **SC-002**: An edit produces zero notifications of any kind and changes zero unread counts.
- **SC-003**: After a delete, the post's text appears nowhere in the product — event page, party
  pages, Home and "See all" news — and, for a party post, no alert announcing it remains for any
  recipient, former crew included.
- **SC-004**: 100% of edit and delete attempts the rules do not allow are refused by the server,
  and someone outside a party's crew cannot distinguish a real party from a non-existent one.
- **SC-005**: Every surface that shows an edited event or party post marks it as edited, and no
  surface ever marks a post that was not.
- **SC-006**: In German at 375px, the post controls, the edit form and the delete confirmation fit
  without horizontal scrolling, truncation or clipped text on the event page, the party page and
  the party's news page.
- **SC-007**: Every acceptance scenario feature 057 specified for team news still holds.

## Assumptions

- **Only the text is editable.** A post has no title, image or other field.
- **No edit history.** Only the latest text and the time of the last edit are kept.
- **Edits do not re-announce.** There is no "notify again" option.
- **No live update of other people's pages.** Another player with the page open sees an edit or a
  delete when they next load it, as with new posts today.
- **No audit trail.** Editing and deleting news is an ordinary organiser action, like posting; it
  is not recorded as a moderation action.
- **Existing posts** are all treated as never edited; nothing is back-filled.
- **Email and push are not recalled.** Party news email keeps its text; push never carried any;
  event news sends neither.
- **Posting is unchanged**, including that the event page hides its composer on a cancelled
  event and who may post each kind of news.
