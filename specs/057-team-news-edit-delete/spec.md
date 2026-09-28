# Feature Specification: Team News Posts Can Be Edited and Deleted

**Feature Branch**: `057-team-news-edit-delete`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Team news posts can be edited and deleted (GH #363). Today TeamNewsService has only GetFeedAsync and PostAsync; no edit, no delete on service or controller; TeamNewsDto carries no id; the composer is write-once. A typo, a wrong date in a "training moved to Thursday" post, or a post sent to the wrong team stays forever, visible to the whole roster and on Home's news module. Add edit and delete for team news posts (admin-gated, same guard as posting), show an "edited" marker on the team page and on Home's news module, never re-notify on edit, and on delete remove the TeamNews Alerts rows for that post (the dedupe key news:{postId}:{recipient} identifies them). Delete is a hard delete behind a confirmation. Party news and event news are out of scope (follow-ups). Fix the stale comment on TeamNewsPost.cs that says the composer is "a later iteration". Copy in all three catalogues; Gate 7 UI review for the edit affordance."

## Context

A team news post is permanent today. Feature 005 shipped the team's news feed read-only,
feature 010 added the composer so admins can post, and 010 explicitly deferred "a richer
news-management experience (edit/delete …)" to a later feature. This is that feature, for
**team** news only.

The consequence of permanence is concrete: a typo, a wrong date in *"Training moves to
Thursday"*, or an update posted to the wrong team stays in front of the whole roster for as
long as the team exists.

**Where a post goes once it is sent.** Every place a post reaches has to be accounted for,
because an edit or a delete behaves differently in each:

| Where | What it holds | Can this feature change it? |
|-------|---------------|-----------------------------|
| The team page's News card (members only) | Reads the post itself | Yes — shows the current text |
| Home's News module and its "See all" page | Reads the post itself, merged with event and party news | Yes — shows the current text |
| Each recipient's Alerts inbox | One row per member, holding a **copy of the post's opening text** (up to 140 characters) taken when it was sent; the row opens the team page, not the post | Yes — the rows are ours |
| Email, for members with *Team news → Email* on | The same opening text | **No** — already delivered |
| Push (feature 055) | The team's name and a fixed sentence — **no post text** | Nothing to correct; a shown notification cannot be withdrawn |

**Two corrections to the issue, found by reading the product:**

- Home does not show the team feed's item. It shows its **own** news item, shared by team,
  event and party news. The "edited" marker on Home is therefore a change to that shared
  item, and it is only ever set for team posts, because event and party posts cannot be
  edited yet.
- The Alerts link already survives a missing post — it points at the team page, never at
  the post. What an edit or delete leaves behind in the Alerts inbox is the **copied opening
  text**, not a broken link. That copy is why this feature touches Alerts rows at all.

**Out of scope, deliberately**: editing or deleting **party** news and **event** news (the
same shape of problem; follow-up issues carry the pattern decided here), an edit history,
rich text, recalling email already sent, and updating other people's open pages live.

## Clarifications

### Session 2026-09-28

- Q: May any admin edit and delete any admin's post, or only the author? → A: **Any admin,
  both.** The team's admins share the power over its news, as event co-admins share theirs
  (006). **Accepted consequences**: an edit by another admin appears under the original
  author's name, and the marker says only *edited*, not by whom; and an admin can edit or
  delete a post whose author has left, lost admin, been banned or erased their account.
  Feature 037's "retained verbatim" (FR-024) describes what an author's **erasure** does to
  their posts — it does not clear them — and is unchanged; it was never a promise that the
  team cannot change or remove a post later.
- Q: After an edit, what does an Alerts row already delivered for the post show? → A: **The
  corrected text.** The row's copy of the opening text is refreshed silently — not unread
  again, not moved, no new alert — so a member's inbox stops repeating the wrong detail. Alerts
  rows already follow the state of what they announced after delivery (an invite row turns
  *handled*), so this extends an existing behaviour rather than inventing one.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An admin corrects a post (Priority: P1)

Last night an admin posted *"Training moves to Thursday"*. It should have said Friday. They
open the post's menu on the team page, choose **Edit**, change one word and save. The post
now reads *Friday*, stays where it was in the feed, and is marked **edited** so a teammate who
read the first version can tell it changed. Nobody is notified again, and the edit form told
the admin so before they saved — if the change matters enough to announce, they post a new
update.

**Why this priority**: Correcting a detail is the most common reason to touch a post again,
and it is the issue's headline example. On its own it removes most of the harm permanence
causes today.

**Independent Test**: As an admin, post an update, edit its text, and confirm the team page
shows the new text in the same position with an "edited" marker; confirm no member received a
new alert, email or push and that no unread count changed.

**Acceptance Scenarios**:

1. **Given** any post on their team, **When** an admin changes its text and saves, **Then**
   the team page shows the new text in the post's original position, with its original
   author and posting date, marked edited — whether that admin wrote the post or not.
2. **Given** that edit, **When** any member checks their alerts, email or device, **Then**
   nothing new has arrived and their unread count is unchanged.
3. **Given** the edit form is open, **When** the admin cancels, **Then** the post is exactly
   as it was and is not marked edited.
4. **Given** the edit form is open, **When** the admin saves without changing the text,
   **Then** nothing changes and the post is not marked edited.
5. **Given** the edit form is open, **When** the admin clears the text and tries to save,
   **Then** the save is refused with a plain explanation and the post is unchanged.
6. **Given** the edit form is open, **When** the admin reads it, **Then** it tells them that
   saving will not notify the team again.

---

### User Story 2 - An admin removes a post (Priority: P1)

An admin posted an update meant for their other team. Editing cannot fix that. They open the
post's menu, choose **Delete**, and a confirmation tells them what will happen: the post
disappears from the team page and from everyone's alerts, and anyone who got it by email
keeps that copy. They confirm. The post is gone from the team page, from every member's Home
news, and from every recipient's Alerts inbox — and a member who had not read the alert sees
their unread count drop.

**Why this priority**: A post sent to the wrong team, or one that is wrong beyond repair, has
no remedy except removal, and editing cannot un-send it from people's inboxes.

**Independent Test**: As an admin, post an update, delete it through the confirmation, and
confirm it no longer appears on the team page, on Home or in any recipient's alerts, and that
an unread recipient's count dropped by one.

**Acceptance Scenarios**:

1. **Given** any post on their team, **When** an admin chooses Delete, **Then** a
   confirmation appears and nothing is removed until they confirm; dismissing it leaves the
   post in place.
2. **Given** the confirmation, **When** the admin reads it, **Then** it says the post will
   disappear for the whole team and that copies already sent by email stay with their
   recipients.
3. **Given** the admin confirms, **When** any member opens the team page, Home, or Home's
   "See all" news, **Then** the post is not there.
4. **Given** the admin confirms, **When** any member who received an alert for the post opens
   their Alerts inbox, **Then** that alert is gone — including for a member who has since
   left the team.
5. **Given** a member had not read the alert, **When** the post is deleted, **Then** their
   unread count drops by one.
6. **Given** the post was the team's only post, **When** it is deleted, **Then** the News card
   shows its usual empty state.

---

### User Story 3 - Every copy tells the same story (Priority: P2)

A member reads the team's news on Home rather than on the team page, and checks their Alerts
inbox more than either. When an admin corrects a post, Home shows the corrected text marked
**edited**, just like the team page, and the member's Alerts row for that post now opens with
the corrected text too — without lighting up as a new alert.

**Why this priority**: The correction in User Story 1 is only as good as the most-read place
the post appears. It follows US1 because it needs an edit to exist first, and because the
team page is where a correction is made and first checked.

**Independent Test**: Edit a post, then confirm Home's News module and "See all" page show the
new text marked edited, that event and party news items never show the marker, and that a
recipient's Alerts row shows the corrected opening text while keeping its read state and its
place.

**Acceptance Scenarios**:

1. **Given** an edited team post in a member's Home news, **When** they open Home, **Then**
   it shows the current text marked edited.
2. **Given** the same post, **When** they open Home's "See all" news, **Then** it is marked
   edited there too.
3. **Given** event or party news on Home, **When** a member views it, **Then** it never
   carries the edited marker.
4. **Given** a post that has never been edited — including every post that existed before
   this feature — **When** any surface shows it, **Then** it carries no marker.
5. **Given** a member's Alerts row for a post that has since been edited, **When** they open
   their Alerts inbox, **Then** the row shows the corrected opening text, stays in its place,
   and is exactly as read or unread as it was before the edit.
6. **Given** a member who has since left the team still holds an Alerts row for the post,
   **When** the post is edited, **Then** their row shows the corrected text as well.

---

### Edge Cases

- **The post is gone before the admin acts.** Another admin deleted it while this admin had
  the page open. Saving an edit or confirming a delete tells them plainly that the post no
  longer exists and removes it from their view; nothing else fails.
- **Two admins edit the same post at once.** The last save wins. There is no merge and no
  warning; each save is a complete replacement of the text.
- **Another admin edits my post.** It still shows my name, marked *edited* — the marker does
  not say by whom. The owner accepted this (Clarifications); co-admins act for the team.
- **The author can no longer act.** They left the team, were demoted, deleted their account
  (feature 037: the post survives, attributed to a neutral placeholder) or were banned. Any
  current admin can still edit or delete the post (FR-013); the demoted author cannot.
- **A post id from another team.** Presented under a different team's address, a post is
  treated as not found — even when the caller is an admin of both teams.
- **Not a member.** A non-member, and a team address that does not exist, get the same
  not-found answer as reading the feed does today. There is no way to learn that a post
  exists from outside the team.
- **A member, not an admin.** Sees no controls on any post, and a request made anyway is
  refused.
- **Text rules.** An edited text follows the posting rules: it cannot be empty or only
  spaces, and it is at most 1,000 characters. Surrounding spaces are trimmed.
- **Order and date.** An edit never moves a post and never changes its posting date. A post
  edited to carry news is not re-announced; the edit form says so (US1-6), and posting a new
  update is how to announce.
- **An Alerts inbox open during a delete.** A member with the inbox open still sees the row
  until they reload; opening it takes them to the team page as always, without an error.
- **The alert went only to the author.** It didn't: the author never receives an alert for
  their own post, so their inbox has nothing to change.
- **A member with in-app team news turned off.** Received no Alerts row, so a delete has
  nothing to remove for them.
- **Email already delivered** keeps the text it was sent with, after an edit or a delete.
  The delete confirmation says so (US2-2). Push notifications carry no post text.
- **Deleting the team** still removes all of its posts, exactly as today.

## Requirements *(mandatory)*

### Functional Requirements

**Editing**

- **FR-001**: A team admin MUST be able to change the text of a team news post (who, exactly:
  FR-013).
- **FR-002**: Edited text MUST follow the rules for a new post: after trimming surrounding
  whitespace it is non-empty and at most 1,000 characters, and it is stored trimmed. A text
  that breaks these rules is refused and the post is left unchanged.
- **FR-003**: Saving a text identical to the current one (after trimming) MUST change nothing
  and MUST NOT mark the post edited.
- **FR-004**: A saved change MUST record that the post was edited and when. The post keeps its
  author, its posting date and its position in the feed; only the most recent text is kept
  (no history).
- **FR-005**: Editing MUST NOT notify anyone: no new Alerts row, no email, no push, and no
  change to any member's unread count. The edit form MUST tell the admin this before they
  save.
- **FR-006**: When a post is edited, every Alerts row already delivered for it MUST show the
  corrected opening text — for every recipient, including members who have since left the
  team. A refreshed row MUST NOT become unread again, MUST NOT move in the inbox, and MUST NOT
  raise a realtime alert.

**Deleting**

- **FR-007**: A team admin MUST be able to delete a team news post (who, exactly: FR-013).
  Deletion is permanent: no undo, no bin, no retained copy.
- **FR-008**: The interface MUST ask for explicit confirmation before a delete. The
  confirmation MUST say that the post disappears for the whole team and that copies already
  sent by email stay with their recipients.
- **FR-009**: Deleting a post MUST remove every Alerts row that announced it, for every
  recipient — including members who have since left the team. Each affected recipient's
  unread count MUST drop accordingly: immediately for a member who is online (best effort),
  otherwise by their next visit.
- **FR-010**: Deleting MUST NOT notify anyone.

**Who may do what**

- **FR-011**: Every edit and delete MUST be decided by the server. A member who is not an
  admin MUST be refused. A non-member and a team that does not exist MUST receive the same
  not-found answer, exactly as for reading the feed.
- **FR-012**: A post MUST be reachable only through its own team. A post presented under
  another team's address MUST be treated as not found, whatever the caller's role in either
  team.
- **FR-013**: **Any current admin** of the team MUST be able to edit and to delete **any** of
  its posts, whoever wrote them — including a post whose author has left the team, lost admin,
  been banned or erased their account. Authorship grants nothing on its own: an author who is
  no longer an admin can neither edit nor delete. Posting stays admin-only and unchanged.
- **FR-014**: The interface MUST offer edit and delete on every post to the team's admins,
  and MUST show members who are not admins no controls at all. These controls are a
  convenience; FR-011 is the boundary.

**What readers see**

- **FR-015**: The team page MUST mark an edited post as edited, beside its author and date.
- **FR-016**: Home's News module and its "See all" page MUST mark an edited team post the same
  way. Event and party news items MUST never carry the marker.
- **FR-017**: A post that has never been edited — including every post that exists when this
  feature is released — MUST NOT carry the marker.
- **FR-018**: Each post MUST carry a stable identifier the interface can use to address it.
  The feed MUST stay ordered newest-first by posting time.

**When something goes wrong**

- **FR-019**: If the post no longer exists when an admin saves or confirms a delete, the admin
  MUST be told plainly and the post MUST disappear from their view.
- **FR-020**: A failed save MUST keep the admin's typed text in the form so they can try
  again. A failed delete leaves the post in place. Neither is retried automatically.

**Language**

- **FR-021**: Every new piece of interface text MUST exist in English, German and Spanish,
  following the house punctuation rules, and MUST fit at the narrowest supported width in
  German without truncation or horizontal scrolling.

### Key Entities *(include if feature involves data)*

- **Team news post** (existing): the author, the team, the text and the posting date. Gains a
  record of **when it was last edited** (absent for a post never edited), and exposes its
  identifier to the interface.
- **Alerts row for a team news post** (existing, one per recipient): identifies the post it
  announced and holds a copy of its opening text. Removed when the post is deleted; its copy
  of the opening text is refreshed when the post is edited (FR-006).
- **Home news item** (existing, shared by team, event and party news): gains whether the
  item was edited — only ever true for a team post.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An admin can correct a post from the team page, without leaving it, in under 30
  seconds.
- **SC-002**: An edit produces zero notifications of any kind and changes zero unread counts.
- **SC-003**: After a delete, the post's text appears nowhere in the product for any member —
  team page, Home, "See all" news and Alerts inbox — for 100% of recipients, including former
  members.
- **SC-004**: 100% of edit and delete attempts the rule does not allow are refused by the
  server, and a non-member cannot distinguish a real team from a non-existent one.
- **SC-005**: Every surface that shows an edited team post marks it as edited, and no surface
  ever marks a post that was not.
- **SC-006**: In German at 375px, the post controls, the edit form and the delete
  confirmation fit without horizontal scrolling, truncation or clipped text.

## Assumptions

- **Only the text is editable.** A post has no title, image or other field.
- **No edit history.** Only the latest text and the time of the last edit are kept; earlier
  versions are not recoverable by anyone.
- **Edits do not re-announce.** There is no "notify the team again" option; an admin who
  wants the team told posts a new update.
- **No live update of other people's pages.** Another member with the team page open sees an
  edit or a delete when they next load it, as with new posts today.
- **The team page keeps showing the most recent posts only**, as today; this feature adds no
  paging to it.
- **No audit trail.** Editing and deleting team news is an ordinary team action, like posting;
  it is not recorded as a moderation action. Platform-operator moderation is unchanged and
  out of scope.
- **Existing posts** are all treated as never edited; nothing is back-filled.
- **Email and push are not recalled.** Email already delivered keeps its text; push never
  carried any.
- **Party news and event news** keep today's behaviour. Follow-up issues carry the pattern this
  feature settles.
