# Feature Specification: Team Details — Editable Name, Type and City, a Description, and Links

**Feature Branch**: `061-team-details`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "GH #359 + GH #321 in one feature — team details become editable and teams get a description and links. Team admins can change a team's name, type and home city after creation in Manage team (re-validated with the create rules: 2–50 chars; city required for a City team, none for a Mixteam; switching to Mixteam drops the city; the handle/slug stays immutable). Teams gain a plain-text description (≤1000 chars, line breaks kept, no formatting, URLs not auto-linked) and a generic labelled list of up to 5 external links (free-text label ≤30 chars + https-only URL), both edited in Manage team, both shown on the team page to every signed-in viewer. The team creation wizard gets an optional, skippable description step after the logo step (description only, not links). Owner decisions: generic labelled list (not fixed kinds); 1000 chars; team page + wizard (no browse-card excerpt); player profiles do NOT get links (follow-up); after a rename, already-delivered alerts show the NEW name (rewritten silently in the same save — 057 precedent); wizard step collects the description only. 050 already promised connected tournament placements show a renamed team's current name, but the code snapshots it — the rename must refresh them."

## Context

A team describes itself with a name, a type (City team or Mixteam), a home city, a
beginners-welcome flag and a logo. Only the flag and the logo can be changed after the team is
created. The name, the type and the city cannot be changed at all. Nobody decided that. The
settings form simply never grew past the flag. Only the team's **handle** (the address in
`/t/<handle>` and in invite links) was ever meant to be permanent. A team that renames itself,
a City team that moves, or a Mixteam that settles in one city today has to delete the team and
start again, and loses its members, chat, results and awards.

A team also has no way to say who it is. A player has had a short bio since the start. A team
has no description, and no link to the website, Instagram or Discord that every real team
already has. A team page that cannot point at those is a worse page than the team's own
Instagram bio. GH #359 asks for the edits and the links, and GH #321 for the description. They
land on the same settings page, so they are specified together and the page is laid out once.

**Two corrections to the issues, found by reading the product:**

- **Tournament results do not read the team's name live.** #359 says a rename "shows the new
  name while connected" in tournament results. That is what feature 050 promised ("a team is
  renamed later: its placements show its current name"). What the product actually does is
  copy the team's name onto the placement when the placement is connected. A rename would
  leave every ranking and every match the team played showing the old name. That never showed
  up because no rename existed. This feature keeps 050's promise.
- **Renaming is not safe everywhere else.** #359 lists chat, browse, Home, results and the
  roster as reading the name live. Chat, browse and the roster do. Nine kinds of alert do not:
  team invitations, role changes, team news, join requests and their answers, party requests,
  party news and marketplace invitations. Each keeps its own copy of the team's name, and so
  do Home's "your role changed" entries, which are built from those alerts. Following 057's
  choice for edited news, delivered alerts are brought up to date (owner decision, below).

**Out of scope, deliberately**: changing a team's handle; links on player profiles (owner
decision, follow-up issue); a description excerpt on the browse-teams list; links in the
creation wizard; finding teams by words in their description; telling members that the team
was renamed; platform-admin editing of a team's details; and any change to emails or push
notifications already sent. Those cannot be recalled.

## Clarifications

### Session 2026-09-28

- Q: What shape should a team's links take? → A: **A generic labelled list.** Up to 5 links,
  each a free-text label of up to 30 characters and a secure web address. This is not a fixed
  set of kinds (Website, Instagram, Discord …). A label can therefore say "Instagram" and
  point anywhere, so every link shows where it really goes beside its label (FR-017).
- Q: How long may a team's description be? → A: **1000 characters**, the same as a team news
  post. Plain text; line breaks are kept; no formatting; addresses inside it are not turned
  into links.
- Q: Where else, besides Manage team and the team page, does the description appear? → A:
  **In the creation wizard, as an optional step.** It does **not** appear on the browse-teams
  list.
- Q: Do player profiles get links too? → A: **No — teams only.** A follow-up issue is filed.
- Q: After a rename, what do alerts already delivered about the team show? → A: **The new
  name.** The rename brings every delivered alert that names the team up to date as part of
  the same save. It is silent: nothing becomes unread again and nothing moves (057's
  precedent for edited news). Emails and push notifications already sent keep the old name.
- Q: Does the wizard's new step collect the links too? → A: **The description only.** Links
  are added in Manage team.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An admin changes the team's name, type or city (Priority: P1)

Jonas is an admin of "Rheinfuer", a typo he made when he created the team a year ago. The team
also moved from Köln to Düsseldorf in the spring. He opens **Manage team**, and at the top of
the page, under **Team details**, he corrects the name to "Rheinfeuer" and picks Düsseldorf as
the home city. He saves. The team page, the browse list, his own navigation, the team chat in
his inbox, and the alerts his teammates received over the past year all say "Rheinfeuer" now.
So do the placements from last summer's tournament. The team's address `/t/rheinfuer` stays
exactly as it was, so every shared link and invitation keeps working.

A season later the players are spread across three cities, and the team becomes a Mixteam.
Jonas switches the type, and the team no longer has a home city.

**Why this priority**: This is #359's main complaint. The alternative today is deleting the
team, which destroys its members, chat, results and awards.

**Independent Test**: As a team admin, change the name, then the city, then the type (City
team → Mixteam → City team) in Manage team. After each save, confirm the team page shows the
new values. After the rename, confirm that an alert delivered before it, and a tournament
placement connected before it, now show the new name. Confirm the team's address did not
change.

**Acceptance Scenarios**:

1. **Given** I am an admin of a team, **When** I change its name to another valid name and
   save, **Then** the team page, the browse list, my navigation and the team's chat show the new
   name, and the team's address is unchanged.
2. **Given** my team's members received alerts naming the team before the rename, **When** I
   rename it, **Then** those alerts show the new name, none of them becomes unread again, and
   their order does not change.
3. **Given** my team is connected to a placement in a tournament result, **When** I rename the
   team, **Then** the ranking and every match of that tournament show the new name for the team.
4. **Given** my team is a City team, **When** I choose a different home city and save, **Then**
   the team page shows the new city, and the team is found under the new city in browse.
5. **Given** my team is a City team, **When** I switch it to Mixteam and save, **Then** the team
   no longer has a home city, and the team page says Mixteam.
6. **Given** my team is a Mixteam, **When** I switch it to City team without choosing a city,
   **Then** the save is refused with a message that a City team needs a city, and nothing
   changes.
7. **Given** I enter a name shorter than 2 or longer than 50 characters, **When** I save,
   **Then** the save is refused with a message that says the allowed length, and nothing
   changes.
8. **Given** I am a member but not an admin, **When** I open Manage team, **Then** I see no way
   to edit the team's details, and the server refuses such an edit if one is sent anyway.

---

### User Story 2 - A team says who it is (Priority: P2)

Lea is looking for a team near Hamburg. On the page of "Elbwölfe" she reads: *"Founded in 2019
by students in Altona. We train twice a week, take tournaments seriously but never ourselves,
and beginners are welcome on Thursdays."* That is what she wanted to know before asking to
join.

An admin of Elbwölfe wrote that text in Manage team, in the same **Team details** section as
the name. The line breaks they typed are kept.

**Why this priority**: #321. It is the difference between a page that says what a team is
*called* and one that says what the team *is*. It is P2 because a team without a description
is still usable, while a team with a wrong name is not.

**Independent Test**: As an admin, write a description with a line break and save. Open the
team page as a signed-in player who is not on the team and confirm the text shows with its line
break and without any formatting applied. Clear it and confirm the section disappears.

**Acceptance Scenarios**:

1. **Given** I am an admin, **When** I write a description of up to 1000 characters and save,
   **Then** every signed-in player who opens the team page, member or not, sees it.
2. **Given** a description with line breaks, **When** it is shown, **Then** the line breaks
   are kept, and text that looks like formatting or like a web address shows exactly as typed.
   It is neither formatted nor turned into a link.
3. **Given** a description of more than 1000 characters, **When** I try to save, **Then** the
   save is refused with a message that says the limit, and nothing changes.
4. **Given** my team has a description, **When** I empty the field and save, **Then** the team
   has no description, and the team page shows no description section.

---

### User Story 3 - A team points at its website, Instagram and Discord (Priority: P2)

Elbwölfe keep their training times on their website and chat day-to-day on Discord. In
**Team details** an admin adds three links: "Website" → `https://elbwoelfe.de`, "Instagram" →
`instagram.com/elbwoelfe`, and "Discord" → an invite link. On the team page, below the
description, each link shows its label and the site it leads to. A player taps "Instagram" and
lands on the team's Instagram in a new tab, with the team page still open behind it.

**Why this priority**: #359's second half. It is as valuable as the description and
independent of it.

**Independent Test**: As an admin, add up to five links, one of them typed without
`https://`. Confirm each appears on the team page with its label and its destination's site,
and opens in a new tab. Try an `http://` address, a `javascript:` address and a sixth link, and
confirm each is refused with a reason.

**Acceptance Scenarios**:

1. **Given** I am an admin, **When** I add a link with a label and a secure web address and
   save, **Then** the team page lists it with its label and the site it leads to.
2. **Given** I type an address without a scheme (for example `instagram.com/elbwoelfe`),
   **When** I save, **Then** it is stored and shown as a secure address
   (`https://instagram.com/elbwoelfe`).
3. **Given** I type an address that is not a secure web address (`http://…`, `javascript:…`,
   `mailto:…`, or one with no site in it), **When** I save, **Then** the save is refused with a
   message naming the offending link, and nothing changes.
4. **Given** my team already has five links, **When** I try to add a sixth, **Then** I cannot,
   and the server refuses more than five if they are sent anyway.
5. **Given** links in a certain order, **When** they are shown, **Then** they appear in the
   order the admin entered them.
6. **Given** a signed-in player follows a link, **When** it opens, **Then** it opens outside
   JuggerHub without giving the destination any hold on the JuggerHub page.
7. **Given** I remove every link and save, **When** the team page is shown, **Then** it shows
   no links.

---

### User Story 4 - A new team writes its description while being created (Priority: P3)

Mia creates "Nordlichter Kiel" with the creation wizard. After the logo step, a new optional
step asks her to **say a few words about the team**. She writes two sentences and continues to
the invite step. The team page shows her text from the start. Had she skipped the step, the
wizard would have gone on exactly as it does today.

**Why this priority**: #321 asks for it as an option. Without it, the description is still
reachable one page away in Manage team. So this is a convenience, not a capability.

**Independent Test**: Create a team, write a description on the new step, finish, and confirm
the team page shows it. Create another team, skip the step, and confirm the team has no
description and nothing else about the wizard changed.

**Acceptance Scenarios**:

1. **Given** I am creating a team and have passed the logo step, **When** the description step
   is shown, **Then** I can write a description, continue, or skip. Neither choice is required.
2. **Given** I write a description and continue, **When** I reach the team page, **Then** it
   shows my description.
3. **Given** I leave the field empty or skip the step, **When** I finish the wizard, **Then** the
   team has no description.
4. **Given** saving the description fails, **When** I am told so, **Then** I stay on the step
   with my text kept, and I can try again or skip. The team itself already exists and is not
   affected.

---

### Edge Cases

- **Renaming to the current name**, or saving with nothing changed, changes nothing. No alert
  or placement is touched.
- **Two admins save the details at the same time**: the later save wins as a whole. Nothing is
  merged, and one save never ends up with half of the other's values.
- **Saving the details never touches** the beginners-welcome flag, the logo, the roster or the
  team's address, and changing the flag or the logo never touches the details.
- **Alerts about a party or a marketplace invitation** name both the team and the event. A
  rename changes only the team's name in them.
- **Alerts for people who have since left the team** name it too, and are updated the same way.
  They are found by the team, not through today's roster.
- **Emails and push notifications already sent** keep the name they were sent with. They cannot
  be recalled, and this is stated rather than hidden.
- **A placement not connected to a team** is not touched by any rename. It shows the name the
  organiser entered.
- **A team deleted after a rename** leaves its placements showing its last name, as today.
- **A name made only of spaces** is not a name and is refused. Surrounding spaces are removed
  from the name, the description, and each link's label and address.
- **A description made only of spaces or line breaks** counts as no description.
- **A link label that is empty** is refused. A link needs a label.
- **The same address entered twice** is refused.
- **An address that hides its real destination**, such as a user name in front of the site
  (`https://instagram.com@example.net`), is refused. A site whose name uses characters that
  imitate another site's shows its real, encoded name beside the label, so what the reader sees
  is where the link goes.
- **A description containing an address** shows it as plain text. It is not a link: links
  belong in the links list, where their destination is shown.
- **Switching type back and forth in the form before saving** only saves the final state. A
  City team saved as a Mixteam loses its city, and switching back later needs a city chosen
  again.
- **A player whose view of the team page is already open** sees the old details until they load
  the page again. Nothing updates live.
- **The wizard's description step after the team is created**: the team exists from the review
  step on (feature 052). A failed or skipped description never undoes the team.

## Requirements *(mandatory)*

### Functional Requirements

**Editing the team's details (US1)**

- **FR-001**: A team admin MUST be able to change the team's name, type and home city after
  creation, in a **Team details** section of Manage team. It is the first section of that page.
- **FR-002**: The same rules as at creation MUST apply: a name of 2–50 characters after
  surrounding spaces are removed (names need not be unique); a City team MUST have a home
  city; a Mixteam MUST NOT have one.
- **FR-003**: Changing a City team into a Mixteam MUST remove its home city. Changing a Mixteam
  into a City team MUST require a city in the same save.
- **FR-004**: The team's handle (its address) MUST NOT be changeable by this or any other
  means.
- **FR-005**: Only a current admin of the team MUST be able to change its details. Anyone else
  MUST be refused by the server the way other team-admin actions refuse them: a member who is
  not an admin is told they are not allowed, and a player who is not on the team gets the same
  answer as for a team that does not exist. The Team details section MUST NOT be shown to
  members who are not admins.
- **FR-006**: A save MUST apply all of its changes or none of them. A refused save MUST change
  nothing and MUST say which rule was broken, in the reader's language.
- **FR-007**: Saving the details MUST NOT change the beginners-welcome flag, the logo, the
  roster or the handle. Changing those MUST NOT change the details.

**What a rename reaches (US1)**

- **FR-008**: After a rename, every place in the product that shows the team's name MUST show
  the new name. That includes the team page, the browse list, the player's own navigation and
  team list, the team's chat and its Contact-admins threads, Home, and the team's tournament
  placements. The acting admin MUST see it without reloading the page.
- **FR-009**: A rename MUST bring every delivered alert that names the team up to date, in the
  same save as the rename: all nine kinds listed in Context, for every recipient, including
  people who have since left the team. So MUST Home's "your role changed" entries built from
  those alerts. It MUST be silent: no alert becomes unread again, changes its position, or is
  delivered again, and no new alert, email or push notification is sent.
- **FR-010**: A rename MUST make every tournament placement connected to the team, and every
  match side that shows that placement, show the new name (keeping feature 050's rule that a
  connected placement shows its team's current name). A placement not connected to the team
  MUST NOT change.
- **FR-011**: If the name does not change, no alert and no placement MUST be touched.
- **FR-012**: A change of type or city MUST be reflected wherever the team's type or city is
  shown or searched on: the team page, the browse list, the city filter, and the near-me
  ordering. Nothing else about the team, its trainings or its events MUST change.

**Description (US2)**

- **FR-013**: A team MUST be able to have a description of up to 1000 characters, edited by an
  admin in the Team details section. An empty description (or one made only of spaces and line
  breaks) MUST mean the team has none.
- **FR-014**: The description MUST be shown on the team page to every signed-in viewer, members
  and non-members alike. It MUST be shown as plain text with its line breaks kept, with no
  formatting applied and no part of it turned into a link, and long words MUST wrap rather than
  widen the page. When a team has neither a description nor links, the team page MUST show no
  section for them.

**Links (US3)**

- **FR-015**: A team MUST be able to have up to 5 links, each a label of 1–30 characters and a
  web address, edited by an admin in the Team details section, kept in the order the admin
  entered them. The server MUST refuse more than 5.
- **FR-016**: An address MUST be a secure web address (`https`) naming a site, of at most 500
  characters. An address typed without a scheme MUST be treated as `https`. Any other scheme
  MUST be refused, including `http`, `javascript`, `data` and `mailto`. So MUST an address
  carrying a user name or password in front of the site, and an address the team already
  lists. The refusal MUST name which link is wrong and why.
- **FR-017**: On the team page each link MUST show its label and the name of the site it leads
  to, so a label cannot hide the destination. A site name in non-Latin or lookalike characters
  MUST be shown in its encoded form.
- **FR-018**: A followed link MUST open outside JuggerHub, in a new tab or window. The
  destination MUST get no access to the JuggerHub page and no information about which JuggerHub
  page the reader came from, and JuggerHub MUST NOT vouch for the link to search engines.
- **FR-019**: Links MUST be shown to the same viewers as the description (FR-014).

**Creation wizard (US4)**

- **FR-020**: The team creation wizard MUST offer an optional description step after the logo
  step and before the invite step. It collects the description only, with the same limit as
  FR-013.
- **FR-021**: The step MUST be skippable. Skipping it or leaving it empty MUST add no request
  and MUST leave the team without a description. The rest of the wizard MUST behave exactly as
  before.
- **FR-022**: A failed save on that step MUST keep the typed text, say that it failed, and let
  the creator try again or skip. It MUST NOT undo or alter the team that already exists. A retry
  is the creator's own press; the product never repeats the save by itself.

**Language and layout**

- **FR-023**: Every new label, hint, message and refusal MUST exist in English, German and
  Spanish. The client MUST show its own translated text for refusals rather than the server's
  English wording.
- **FR-024**: The Team details section, the team page's description and links, and the wizard
  step MUST follow DESIGN.md. In German at 375px wide, no text MUST be cut off or overflow the
  page, and link labels and site names MUST wrap rather than overflow.

### Key Entities

- **Team** (existing): gains a **description** (optional, plain text, up to 1000 characters).
  Its name, type and home city become editable. Its handle stays permanent.
- **Team link** (new): one of a team's up to 5 external links, with a **label** (1–30
  characters), an **address** (a secure web address, up to 500 characters) and a **position**
  giving its order. It belongs to exactly one team and goes when the team goes.
- **Alert** (existing): the nine team-related kinds each hold a copy of the team's name, which
  a rename now brings up to date (FR-009).
- **Tournament placement** (existing, feature 050): shows its connected team's name, which a
  rename now brings up to date (FR-010).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A team admin can change the team's name, type or city in one save, from one page,
  with no need to delete and recreate the team. Members, chat, results and awards are all kept,
  and the team's address stays the same.
- **SC-002**: After a rename, every in-product surface listed in FR-008 and FR-009 shows the new
  name: 100% of them, checked one by one. The old name survives only in emails and push
  notifications that were already sent.
- **SC-003**: After a rename, 100% of the placements connected to the team show the new name,
  and 0 placements of other teams change.
- **SC-004**: A signed-in player can read a team's description and reach any of its links from
  the team page in one tap per link, with each link's destination site visible before tapping.
- **SC-005**: 100% of submitted addresses that are not secure web addresses, or that disguise
  their destination, are refused with a reason, and none is stored.
- **SC-006**: Opening the team page takes no more requests than before this feature. The
  description and links arrive with the page's existing data.
- **SC-007**: Creating a team without using the description step takes exactly the same presses
  and requests as before this feature, apart from passing one more optional step.
- **SC-008**: In German at 375px, the Team details section, the team page's description and
  links, and the wizard's description step show every label in full, with no horizontal
  scrolling.

## Assumptions

- The team page is shown only to signed-in players (feature 026). "Every viewer" in this
  specification therefore means every signed-in player. The anonymous invitation preview is
  unchanged and shows neither the description nor the links.
- The limits reuse the product's existing ones: the team-name limit of 2–50 characters from
  creation, 1000 characters as for a team news post, and 500 characters per address as for an
  event or training's online link. Accepting an address typed without a scheme follows the same
  precedent. Unlike that precedent, `http` is refused, because #359 asks for secure addresses
  only.
- Links are reordered by removing and re-adding them. With at most five links, no separate
  reordering control is needed.
- A contact email address is not a link kind (`mailto` is refused). Players reach a team's
  admins through Contact admins (feature 027).
- The rename's update of delivered alerts finds them by the team they name, not by the current
  roster. So former members' alerts are included (057's lesson).
- The privacy policy already covers what members write about their teams ("whatever you put on
  the site … teams …"), and the terms already cover team descriptions. No legal text changes.
- Platform admins keep their existing tools. Editing another team's details from the admin area
  is not part of this feature.
- Nothing is announced when a team's details change: no alert, no "What's happening" entry, no
  email.
