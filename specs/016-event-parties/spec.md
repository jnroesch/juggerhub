# Feature Specification: Event Parties

**Feature Branch**: `016-event-parties`

**Created**: 2026-07-13

**Status**: Draft

**Input**: User description: "For team-participation events, a team does not enter directly; a team admin forms a *party* — a temporary subset of the team's players (up to a per-event roster cap, usually 8) formed for one event and disbanded after. The admin manages the party (invite/remove members, post news, co-admins). Creating a party posts a participation request to the whole team: members accept (join the party) or decline (visible to the admin; still able to join later while there is room). Applying the party to the event is a separate, deliberate step that lists the team's crew as its entry. This replaces the current direct team-join. Wireframes: wireframes/Party Wireframes (offline).html (screens 6a–6h)."

## Clarifications

### Session 2026-07-13

- Q: On teams-only events, should the party flow fully replace the current direct team-join, be additive, or also migrate existing entries? → A: **Full replacement** — the direct "Join with your team" is removed on teams-only events; the only way a team enters is by forming a party, filling it, and deliberately applying it. The applied party becomes the team's existing sign-up/waitlist entry. (No data migration; the feature ships before any real teams-only sign-ups exist in Dev/Prod.)
- Q: For the "LATER" party tools (party news, payment split, travel & carpool), what is built this iteration? → A: **Party news is built for real** (reusing the team-news pattern, scoped and private to the party, deleted on disband); **payment split and travel & carpool are reserved placeholder slots only** — clearly labelled "later", not functional.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Form a party from a teams-only event (Priority: P1)

A team admin browsing a **teams-only** event does not sign the whole team up. Where a
regular visitor would see a direct-join affordance, a team admin instead sees
**"Enter a party"**. Opening it presents a short create form, pre-bound to this event:
pick **which of their teams** enters (only teams the user administers; auto-selected when
they admin exactly one), see the event's **roster cap** (players per team — read-only,
set by the event), and write an optional **message to the team**. Submitting creates the
party (the creator becomes its party admin) but does **not** put the team on the event
yet — that is a separate, deliberate step (User Story 5). Exactly one party may exist per
team per event.

**Why this priority**: The party is the root object of the whole feature — the request,
roster, news, co-admins, applying, and disbanding all hang off it. Nothing else can be
demonstrated until a party can be formed, so this lands first. It is also the replacement
for the removed direct team-join, so teams-only events are otherwise unenterable without it.

**Independent Test**: As a user who administers a team, open a seeded teams-only event,
confirm the primary action is "Enter a party" (not a direct join), form a party choosing
the team and a message, and confirm the party is created with the creator as its admin,
the roster cap copied from the event, no team-on-event entry yet, and that a second party
for the same team+event is refused.

**Acceptance Scenarios**:

1. **Given** a signed-in user who administers a team, **When** they open a teams-only event page, **Then** the primary participation action is "Enter a party" and no direct "join with your team" action is offered.
2. **Given** a user who administers more than one team, **When** they open the form, **Then** they must pick which team enters; a user who administers exactly one team has it pre-selected.
3. **Given** the form, **When** the user submits, **Then** a party is created for that team and event, the creator is recorded as its party admin, the roster cap is taken from the event, and the team is **not** yet listed on the event.
4. **Given** a user who administers no team, **When** they view a teams-only event, **Then** they are not offered "Enter a party" (viewing remains open; forming is refused server-side).
5. **Given** a team that already has a party for this event, **When** anyone attempts to form another, **Then** it is refused (one party per team per event).
6. **Given** an individuals-only event, **When** anyone views it, **Then** the party flow is not offered and individual sign-up is unchanged.
7. **Given** a cancelled or already-ended event, **When** a team admin attempts to form a party, **Then** it is refused.

---

### User Story 2 - The participation request reaches the team (Priority: P1)

Forming a party immediately posts a **participation request** to **every member** of the
team. It surfaces two ways: a card **pinned to the top of the team space** that everyone
in the team sees, and a **notification plus a transactional email** nudging each member.
The card shows the event (type, name, dates), the current roster fill (e.g. 5/8), and the
admin's message, along with the members already in. The request stays pinned until the
party is applied/managed to completion or disbanded. The party admin's own copy of the
pinned card is a shortcut into managing the party.

**Why this priority**: A party with no way for members to hear about it is inert; the
request is how a crew is actually gathered, so it is part of the core loop alongside
forming (US1) and answering (US3).

**Independent Test**: Form a party in a seeded team and confirm every team member sees a
pinned party-request card in the team space and receives an in-app notification and an
email; confirm a non-member of the team sees no such card and receives nothing.

**Acceptance Scenarios**:

1. **Given** a newly formed party, **When** any member of the team opens the team space, **Then** a party-request card is pinned at the top showing the event, the roster fill, the admin's message, and accept/decline actions.
2. **Given** a newly formed party, **When** it is created, **Then** every current team member receives an in-app notification and a transactional email about the request.
3. **Given** the party admin, **When** they view the pinned card, **Then** it doubles as an entry point into managing the party.
4. **Given** a user who is not a member of the team, **When** they view anything, **Then** they see no party-request card and receive no notification/email for it.
5. **Given** a member's notification preferences, **When** the request is sent, **Then** delivery respects the existing notification-preference rules (feature 011).

---

### User Story 3 - A team member answers the request (Priority: P1)

A regular team member sees the pinned card and makes one clear decision: **"I'm in"**
(joins the party, taking a roster spot if one is open) or **"Can't make it"** (declines).
A decline is a soft "not right now", **not** a door slam: it is visible to the party admin
so they know who has and hasn't answered, and the decliner keeps seeing the request and can
**still join later** while there is room. A member who is *in* can change their mind and
leave, freeing their spot. When the party is at its roster cap, the card shows **full** with
no "I'm in" action; it reopens on its own when a spot frees (User Story 6).

**Why this priority**: Accept/decline is the members' half of the core gathering loop;
without it a party can never fill. Together with US1 and US2 it is the minimum viable slice.

**Independent Test**: As a team member, open the pinned card and tap "I'm in" — confirm you
are added to the party and the fill count rises; tap "Can't make it" from another account —
confirm you are recorded as declined, the admin can see it, and you can still tap "I'm in"
later while a spot is open; from a member who is in, leave and confirm the spot is freed.

**Acceptance Scenarios**:

1. **Given** a team member on the pinned card with an open spot, **When** they tap "I'm in", **Then** they are added to the party's **in** roster and the fill count increases by one.
2. **Given** a team member, **When** they tap "Can't make it", **Then** they are recorded as **declined**, this is visible to the party admin, and the card still shows the request to them.
3. **Given** a member who has declined and a spot is open, **When** they change their mind and tap "I'm in", **Then** they move from declined to **in**.
4. **Given** a member who is **in**, **When** they leave the party, **Then** they are removed from the in roster (returning to *no response* / free to rejoin) and their spot is released.
5. **Given** the party is at its roster cap, **When** a member views the card, **Then** it shows **full** and offers no "I'm in" action (an option to be notified when a spot opens may be shown).
6. **Given** a user who is not a member of the team, **When** they attempt to accept the request via a direct call, **Then** it is refused server-side.
7. **Given** a party that has been disbanded, **When** a member attempts to accept or decline, **Then** the action is refused and the card is gone.

---

### User Story 4 - Manage the party roster and tools (Priority: P2)

The party admin has a management hub reachable from the pinned card or the party's own URL.
It shows the roster in three groups — **in**, **declined** (may rejoin if a spot is open),
and **no response yet** — with a per-member **Nudge** that re-sends the request notification
and email, and a **Remove** that takes a member off the party (members-only; removal never
touches team membership and attaches no badges or history). A **readiness** summary sits up
top (enough players to field a team; spots still open — optional; members not yet answered —
optional) with the single primary action **Apply to event** (User Story 5). A **party tools**
list offers: **post party news** (User Story 7), **invite co-admins** (User Story 8),
**payment split** and **travel & carpool** (reserved "later" placeholders, not functional
this iteration), and **disband** (User Story 9). Desktop shows a dense roster + readiness +
tools view; mobile shows the same three groups as tabs.

**Why this priority**: Once a party exists and members can answer, the admin needs to shape
the crew (nudge stragglers, remove someone, read readiness) before applying — but a party is
already viewable and joinable without these controls, so it follows the core loop.

**Independent Test**: As a party admin of a seeded, partly-answered party, confirm the three
groups list the right members; nudge a no-response member and confirm the notification/email
is re-sent; remove an in member and confirm they leave the party but remain on the team;
confirm the readiness summary reflects the counts; confirm a non-admin cannot nudge or remove.

**Acceptance Scenarios**:

1. **Given** a party admin, **When** they open manage, **Then** members are grouped into **in**, **declined**, and **no response yet** with correct counts.
2. **Given** a member who has not answered, **When** the admin taps **Nudge**, **Then** that member is re-sent the request notification and email.
3. **Given** the admin removes an **in** member, **Then** that member leaves the party and their spot is released, **and** their team membership, badges, and history are unchanged.
4. **Given** the roster, **When** it is shown, **Then** a readiness summary indicates whether there are enough players to field a team, how many spots remain, and how many members have not answered.
5. **Given** the party tools, **When** shown, **Then** **payment split** and **travel & carpool** appear as clearly-labelled "later" placeholders and expose no functionality.
6. **Given** a non-admin (team member or outsider), **When** they attempt to nudge, remove, or otherwise manage, **Then** the action is refused server-side regardless of client state.

---

### User Story 5 - Apply the party to the event (Priority: P2)

Applying is a **deliberate, never-automatic** step the party admin takes when the crew is
ready. A pre-apply screen shows readiness (enough players to field a team; spots still open —
you can keep filling after; members still to reply — you can apply anyway) and, for a **paid**
event, notes the team will hold a **pending** spot until the organiser confirms the fee —
exactly like any team sign-up today. **Apply** lists the party as the **team's entry** on the
event and hands off to the event's existing sign-up / pending / waiting-list flow (feature
006): the team occupies one spot as a single participant, admitted or held pending or
waitlisted per the event's free/paid/full rules. After applying, the pinned card flips to
**applied**; the admin may keep editing the party (fill spots, remove) and may **withdraw**,
which removes the team's entry from the event but keeps the party in the team.

**Why this priority**: Applying is what actually enters the team into the event and is the
whole point of the two-phase flow — but it depends on a formed, filled party (US1–US4), so it
follows them.

**Independent Test**: As a party admin of a ready party on a seeded free teams-only event,
apply and confirm the team appears in the event's joined group as a single entry and the card
shows "applied"; on a paid event confirm the team lands in awaiting-approval (pending) with
payment instructions; withdraw and confirm the team leaves the event while the party remains.

**Acceptance Scenarios**:

1. **Given** a party admin on the pre-apply screen, **When** they apply to a **free** teams-only event with an open spot, **Then** the team is listed as a single **joined** entry on the event and the pinned card shows **applied**.
2. **Given** a **paid** teams-only event with an open spot, **When** the admin applies, **Then** the team is recorded **awaiting approval** holding a provisional spot, with the payment recipient/IBAN/deadline shown, per feature 006.
3. **Given** a teams-only event whose spots are full, **When** the admin applies, **Then** the team is placed on the event's **waiting list** in arrival order (the event's waiting list, not a party one).
4. **Given** an applied party, **When** the admin keeps editing it, **Then** they may still add/remove party members and may **withdraw**; withdrawing removes the team's event entry but leaves the party intact in the team.
5. **Given** applying, **When** it occurs, **Then** it is always an explicit admin action and never happens automatically on party creation or filling.
6. **Given** a team already entered on the event (an applied party), **When** the admin applies again, **Then** a duplicate team entry is refused (one team entry per event, per feature 006).
7. **Given** a non-admin of the party, **When** they attempt to apply or withdraw, **Then** it is refused server-side.
8. **Given** a cancelled or already-ended event, **When** the admin attempts to apply, **Then** it is refused.

---

### User Story 6 - Auto-close and reopen at the roster cap (Priority: P2)

The request manages its own open/closed state around the roster cap with **no admin action
and no party-level waiting list**. When the party reaches the cap it **auto-closes**: the
pinned card shows **full** and offers no "I'm in". If a member later drops below the cap, the
request **reopens by itself** and the **next member to tap "I'm in" gets the spot, first
come**. This keeps the flow low-pressure — the cap does the work.

**Why this priority**: This is the behaviour that lets a party fill correctly and fairly
without admin babysitting; it refines US3's accept flow, so it rides alongside management.

**Independent Test**: Fill a seeded party to its cap and confirm the card shows **full** with
no "I'm in"; have an in member leave and confirm the card reopens automatically showing one
spot open; from two other members, confirm the first to tap "I'm in" takes the spot and the
second sees **full** again.

**Acceptance Scenarios**:

1. **Given** a party one short of its cap, **When** a member joins to reach the cap, **Then** the request auto-closes and the card shows **full** with no join action and no waiting list.
2. **Given** a full party, **When** an in member leaves, **Then** the request reopens automatically showing one spot open.
3. **Given** a reopened spot and two members tapping "I'm in" near-simultaneously, **When** the requests are processed, **Then** at most one takes the spot and the other is shown **full**; the cap is enforced atomically server-side.
4. **Given** a full party, **When** a member views the card, **Then** no waiting-list join is offered (auto-reopen replaces it); an optional "notify me when a spot opens" may be shown.

---

### User Story 7 - Post party news (Priority: P3)

The party admin can post **news** updates scoped to the party. It behaves exactly like team
news (same compose-and-feed pattern) but is **private to the party's crew** and is
**deleted when the party disbands**. Posts appear newest-first with author, role, relative
time, and body; the feed is paginated with a friendly empty state. Posting a party news update
**notifies the crew** — every member who is *in* the party receives an in-app notification and
an email (respecting notification preferences), exactly as a team news post notifies a team.

**Why this priority**: Party news is valuable for run-of-show coordination (meet times,
kit) but the party is fully usable without it, so it follows the core and management slices.

**Independent Test**: As a party admin, post a party news update and confirm it appears
newest-first for party members only; confirm a team member who is not in the party cannot
read it and a non-admin party member has no compose affordance; disband and confirm the news
is gone.

**Acceptance Scenarios**:

1. **Given** a party admin, **When** they post a news update with a body, **Then** it appears at the top of the party news feed with author, role, and relative time.
2. **Given** a posted party news update, **When** it is published, **Then** every **in** crew member (except the author) receives an in-app notification and an email, subject to their notification preferences.
3. **Given** a party member, **When** they open party news, **Then** they can read the feed but a non-admin has no compose affordance and a direct post attempt is refused server-side.
4. **Given** a team member who is not *in* the party, **When** they attempt to read party news, **Then** it is refused (news is private to the crew).
5. **Given** a party with no news, **When** the feed is shown, **Then** a friendly empty state appears; the feed is paginated newest-first.
6. **Given** a disbanded party, **When** anyone looks for its news, **Then** none remains (it was deleted with the party).

---

### User Story 8 - Invite party co-admins (Priority: P3)

A party admin can invite other **team members** to co-administer the party, mirroring the
event co-admin model (feature 006): by a shareable invite link and/or by searching team
members and inviting them directly. Accepting grants full party-admin powers (manage roster,
nudge/remove, post news, invite further co-admins, apply/withdraw, disband). Co-admins are
restricted to members of the party's team. The party always retains at least one admin.

**Why this priority**: Shared administration helps larger crews but a single admin can run a
party alone, so it follows the core slices — reusing the events invitation machinery.

**Independent Test**: As a party admin, copy the co-admin invite link and confirm its form
and expiry; search a team member by name and invite them; confirm both show as pending, that
accepting grants party-admin powers, that a non-team-member cannot be invited, and that the
last admin cannot step down.

**Acceptance Scenarios**:

1. **Given** a party admin, **When** they copy the co-admin invite link, **Then** a shareable link is presented that, when accepted by a team member, grants party-admin powers, and can be revoked/regenerated.
2. **Given** a party admin, **When** they search a team member and invite them, **Then** a targeted invitation is created, delivered, and shown pending until accepted or revoked.
3. **Given** an invitation targeting a user who is not a member of the party's team, **When** it is attempted or accepted, **Then** it is refused (co-admins are team-scoped).
4. **Given** a user who accepts a co-admin invitation, **When** they open the party, **Then** they have full party-admin powers.
5. **Given** the last remaining party admin, **When** a step-down/removal is attempted, **Then** it is blocked so the party always retains at least one admin.
6. **Given** a non-admin, **When** they attempt to invite or revoke a co-admin, **Then** it is refused server-side.

---

### User Story 9 - Disband the party (Priority: P3)

Parties are temporary; there is no auto-cleanup. From a guarded danger zone the party admin
can **disband** it — an irreversible manual action (lighter than deleting a team). Disbanding
removes the party and its news (and any later payment/travel data) for everyone, removes the
pinned request from the team space, and — **if the party had been applied** — **withdraws the
team from the event**. The team, its roster, its members, and all badges/history are
**untouched** either way. Reactivation is not offered; a new party would be formed instead.

**Why this priority**: Wind-down matters but is infrequent and not part of the core loop, so
it is the final slice.

**Independent Test**: As a party admin, disband a seeded applied party and confirm the party
and its news are gone, the pinned request disappears from the team space, the team's entry is
withdrawn from the event, and the team/roster/members/badges are unchanged; confirm a
non-admin cannot disband and that disband requires an explicit confirmation.

**Acceptance Scenarios**:

1. **Given** a party admin in the danger zone, **When** they confirm disband, **Then** the party and its news are removed and the pinned request disappears from the team space.
2. **Given** an **applied** party, **When** it is disbanded, **Then** the team's entry is withdrawn from the event (freeing the spot, no auto-promotion), consistent with a normal withdrawal.
3. **Given** disband, **When** it completes, **Then** the team, its membership roster, and all badges/history are unchanged and no badge is granted or removed.
4. **Given** a disbanded party, **When** an admin looks to reactivate it, **Then** none is offered — a fresh party must be formed.
5. **Given** a non-admin, **When** they attempt to disband, **Then** it is refused server-side; disband requires an explicit confirmation in the client.

---

### Edge Cases

- **One party per team per event**: a team may have at most one party for a given event; a second creation is refused. A team may have parties for many different events concurrently.
- **Roster cap bounds**: the per-event roster cap is a positive integer, default 8, never below 5; the event creator may set a higher value at event creation. A cap below 5 or non-positive is refused server-side.
- **Cap vs. event participation limit**: the roster cap governs **players within one team's party**; the event's participation **limit** (feature 006) governs **how many teams** hold a spot. They are independent.
- **Decline is reversible**: a declined member keeps seeing the request and may rejoin while a spot is open; a decline never permanently removes them from consideration.
- **Full → auto-close, drop → auto-reopen**: reaching the cap closes joining with no admin action and no waiting list; dropping below reopens it first-come. Concurrent last-spot joins are resolved atomically so the cap is never exceeded.
- **Removal semantics**: removing a member from a party (or a member leaving) never removes them from the team and attaches no badges/history; it only frees a party spot.
- **Apply is manual and idempotent per team**: applying is never automatic; a team may appear on an event at most once (feature 006 duplicate guard), so re-applying an already-entered team is refused.
- **Withdraw vs. disband**: withdrawing removes the team's event entry but keeps the party; disbanding removes the party and, if applied, also withdraws the event entry.
- **Paid events**: an applied party on a paid event holds a pending (awaiting-approval) spot until the organiser confirms the out-of-band fee, exactly like any team sign-up; the party is not a payment mechanism this iteration.
- **Members-who-answered vs. applied crew**: the party may be applied while members are still in *no response* or *declined*; readiness flags these as optional, not blocking.
- **Team membership changes**: if a member leaves the team, they leave any party for that team; the party admin must be a current team member (co-admins are team-scoped).
- **Cancelled/ended event**: forming, applying, and (re)joining are refused on a cancelled or already-ended event; existing party content remains viewable until disbanded.
- **Last-admin guard**: a party always retains at least one admin; the last party admin cannot step down or be removed.
- **Private party news**: party news is visible only to party members; team members not in the party and outsiders are refused, and it is deleted on disband.
- **Individuals-only events**: parties are not offered; individual sign-up (feature 006) is unchanged.
- **Empty states**: a brand-new party with no members answered, no news, and no co-admins shows friendly empty states, not errors.
- **Non-admin direct API access**: forming (non-team-admin), managing, nudging, removing, posting news, inviting co-admins, applying, withdrawing, and disbanding via direct API by an unauthorized actor are all refused regardless of client state.

## Requirements *(mandatory)*

### Functional Requirements

#### Roster cap on the event

- **FR-001**: A **teams-only** event MUST carry a **roster cap** (players per team) captured at event creation as part of the "who can join" step; it MUST default to **8**, MUST be at least **5**, and the creator MAY set a higher value. A cap below 5 or non-positive MUST be refused server-side. Individuals-only events MUST NOT carry a roster cap.

#### Replacing direct team-join

- **FR-002**: On a **teams-only** event, the system MUST NOT offer a direct team sign-up; the only way a team enters MUST be by forming a party, filling it, and applying it. Individuals-only sign-up MUST remain unchanged.

#### Forming a party

- **FR-003**: A user who **administers** a team MUST be able to form a **party** for a teams-only event, choosing which of their administered teams enters (auto-selected when they administer exactly one) and optionally a **message to the team**. A user who administers no team MUST be refused (viewing remains open).
- **FR-004**: Forming a party MUST record the creator as the party's first **party admin**, MUST copy the event's roster cap onto the party, and MUST NOT enter the team on the event (applying is separate).
- **FR-005**: The system MUST enforce **one party per team per event**; a second party for the same team and event MUST be refused. Forming a party on a **cancelled or already-ended** event MUST be refused.

#### The participation request

- **FR-006**: Creating a party MUST post a **participation request** to every current member of the team, surfaced as a card **pinned to the top of the team space** for all team members and as an **in-app notification plus a transactional email** to each member (respecting notification preferences, feature 011). Non-members MUST neither see the card nor receive notifications.
- **FR-007**: The pinned card MUST show the event (type, name, dates), the current roster fill (in / cap), the admin's message, and accept/decline actions; for the party admin it MUST also act as an entry point to managing the party.

#### Answering the request

- **FR-008**: A team member MUST be able to **accept** ("I'm in") when a spot is open, which adds them to the party's **in** roster and increments the fill, and to **decline** ("can't make it"), which records them as **declined**.
- **FR-009**: A **decline** MUST be non-final: it MUST be visible to the party admin, the request MUST remain visible to the decliner, and they MUST be able to **rejoin** while a spot is open. A member who is **in** MUST be able to **leave**, freeing their spot.
- **FR-010**: Only a **member of the party's team** MUST be able to accept/decline; any accept/decline by a non-member, or on a disbanded party, MUST be refused server-side.

#### Roster cap behaviour

- **FR-011**: When the party's **in** count reaches the roster cap, joining MUST **auto-close** (the card shows full, no "I'm in") with **no admin action and no party-level waiting list**. When the in count later drops below the cap, joining MUST **reopen automatically**, first-come.
- **FR-012**: The cap MUST be enforced **atomically** server-side so concurrent joins for the last spot cannot exceed it; the surplus join MUST be refused (shown full), not waitlisted.

#### Managing the party

- **FR-013**: A party admin MUST see the roster in three groups — **in**, **declined**, and **no response yet** — with correct counts and a **readiness** summary (enough to field a team; spots open; members unanswered), and MUST have exactly one primary action to **apply to the event**.
- **FR-014**: A party admin MUST be able to **Nudge** a member (re-send the request notification and email) and to **Remove** any party member. Removal MUST free the party spot and MUST NOT change the member's **team** membership, badges, or history.
- **FR-015**: The management surface MUST list party tools including **post news**, **invite co-admins**, **disband**, and clearly-labelled non-functional **payment split** and **travel & carpool** placeholders. It MUST be responsive (dense desktop view; tabbed roster groups on mobile) per DESIGN.md.

#### Applying to the event

- **FR-016**: A party admin MUST be able to **apply** the party to the event as a **deliberate, manual** action that lists the team as the event's entry and hands off to the event's existing sign-up / pending / waiting-list flow (feature 006): admitted to **joined** for a free event with an open spot, **awaiting approval** for a paid event, or the **event waiting list** when full. Applying MUST NEVER happen automatically.
- **FR-017**: After applying, the party MUST remain editable (add/remove members) and the admin MUST be able to **withdraw**, which removes the team's event entry (releasing the spot, no auto-promotion) while keeping the party in the team. A team MUST appear on an event at most once (feature 006 duplicate guard); re-applying an already-entered team MUST be refused.
- **FR-018**: Applying MUST be refused on a **cancelled or already-ended** event and for any actor who is not a **party admin** (and who does not administer the team, consistent with feature 006).

#### Party news

- **FR-019**: A party admin MUST be able to post **party news** (with a body); posts MUST appear newest-first with author, role, and relative time, and MUST be **private to the party's crew** (team members not *in* the party and outsiders are refused). Non-admin crew members MUST see the feed read-only with no compose affordance; a direct post attempt MUST be refused server-side.
- **FR-020**: Posting a party news update MUST **notify the crew** — every *in* member except the author receives an in-app notification and an email, subject to their notification preferences (reusing the existing notification/email infrastructure, mirroring team news).
- **FR-021**: Party news MUST be **deleted when the party disbands**. The feed MUST be paginated/bounded with a friendly empty state.

#### Co-admins

- **FR-022**: A party admin MUST be able to invite **co-admins** — restricted to **members of the party's team** — by a shareable link and by searching team members; a targeted invite MUST be delivered and shown pending until accepted or revoked. Accepting MUST grant full party-admin powers.
- **FR-023**: The party MUST always retain **at least one admin**; any step-down/removal that would leave zero admins MUST be blocked server-side. Inviting or accepting a co-admin who is not a member of the party's team MUST be refused.

#### Disband

- **FR-024**: A party admin MUST be able to **disband** the party from a guarded danger zone with explicit confirmation; disband MUST be **irreversible**. Disbanding MUST remove the party and its news (and any later payment/travel data), remove the pinned request from the team space, and — if the party had been **applied** — **withdraw the team's entry** from the event.
- **FR-025**: Disbanding MUST leave the **team, its membership, and all badges/history untouched** and MUST NOT grant or remove any badge. A non-admin disband attempt MUST be refused server-side.

#### Cross-cutting

- **FR-026**: Every authorization decision in this feature MUST be enforced **server-side** (form/manage/nudge/remove/apply/withdraw/news/co-admin/disband); client-side gating exists only for UX and is never the security boundary.
- **FR-027**: Every list surface (roster groups, party news, co-admin invitations, member search) MUST be **paginated/bounded** and never returned as an unbounded collection.
- **FR-028**: All screens MUST be **responsive** and legible on phone and desktop per DESIGN.md, including empty, loading, and error states, and MUST NOT leak raw exceptions or sensitive data to the client.

### Key Entities *(include if feature involves data)*

- **Party**: A temporary subset of one **team** formed for one **event**. Attributes: the team, the event, roster cap (copied from the event), optional message to the team, status (**open** — being filled / **applied** — listed on the event / **disbanded**), and a link to the team's event entry (the feature 006 sign-up) when applied. Has one or more party admins, party members, news posts, and co-admin invitations. At most one non-disbanded party per team+event.
- **Party Member**: A team member's relationship to a party. Attributes: the party, the user, status (**in** / **declined** / **no response**), role (member or admin), and arrival order (for first-come reopen). Removing/leaving frees a spot and does not touch team membership.
- **Party News Post**: An update private to the party. Attributes: author (a party admin), body, and time. Newest-first; deleted when the party disbands.
- **Party Co-admin Invitation**: A pending or resolved invitation to co-administer, as a shareable **link** or a **targeted** invite bound to one **team member** (mirrors the event admin invitation). Attributes: kind, opaque token, target user (targeted only), issuing admin, issued time, expiry, and status (pending / accepted / revoked / expired).
- **Event (extended)**: Gains a **roster cap** (players per team) for teams-only events, captured at creation (default 8, minimum 5). All other feature 006 event attributes are unchanged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a teams-only event, a team is entered **only** via the party flow — a direct team-join is never available, and 100% of team entries on the event originate from an applied party.
- **SC-002**: A team admin can form a party and gather a crew: after forming, 100% of the team's members receive both the pinned request and a notification/email, and members can accept or decline in one tap.
- **SC-003**: 100% of party-admin-only actions (manage, nudge, remove, apply, withdraw, post news, invite/revoke co-admin, disband) are refused for non-admins when attempted directly, independent of the client UI; party news is never readable by a non-party team member or outsider.
- **SC-004**: The roster cap is honoured in 100% of cases — the party never exceeds its cap under concurrent joins, auto-closes at the cap, and reopens first-come when a spot frees, with no party-level waiting list.
- **SC-005**: Applying is always explicit — no team is ever entered on an event without a deliberate admin apply — and an applied party lands in the correct event group (joined / awaiting approval / event waiting list) per the event's free/paid/full state in 100% of transitions.
- **SC-006**: Disbanding a party removes the party and its news and the team-space request, withdraws any applied event entry, and leaves the team, its roster, and all badges/history unchanged in 100% of disbands.
- **SC-007**: A decline is reversible — a declined member can rejoin while a spot is open in 100% of cases — and removing/leaving a party never changes the member's team membership or badges.
- **SC-008**: Every party surface (roster groups, request card, news, co-admin invitations) renders correct, bounded data with friendly empty states and is usable on phone and desktop including loading and error states.

## Assumptions

The following reasonable defaults were chosen where the description or wireframe left a
detail open. The two genuine product decisions were locked with the requester (see
`## Clarifications`, Session 2026-07-13) and are marked *(resolved in clarification)* below.

- **Full replacement of direct team-join** *(resolved in clarification)* — on teams-only events the direct "join with your team" is removed; a team enters only via a formed, filled, and applied party. No data migration is performed (the feature ships before real teams-only sign-ups exist in Dev/Prod); should any exist, they are treated as out-of-scope one-offs to be re-created as parties.
- **Party tools scope** *(resolved in clarification)* — **party news is built** (reusing the feature 005 team-news compose/feed pattern, scoped and private to the party, deleted on disband). **Payment split** and **travel & carpool** are reserved, clearly-labelled non-functional placeholders this iteration; their data model and UI are a later slice.
- **Roster cap lives on the event** — the per-team roster cap (default 8, min 5) is an event-creation field on teams-only events, extending feature 006's "who can join" step; it is copied onto each party at formation so later cap edits do not retroactively resize existing parties (edit rules finalized in planning).
- **Reuse of the events entry machinery** — applying a party creates/updates the existing feature 006 team sign-up (`EventSignup` with a team subject) and reuses its pending/payment/waiting-list/withdraw behaviour unchanged; the party is the *pre-entry gathering*, the event sign-up is the *entry*.
- **No party-level waiting list** — the auto-close/auto-reopen behaviour replaces any party waiting list; only the **event** has a waiting list (for teams competing for event spots), reached after a party is applied.
- **Notifications and email reuse** — the participation request and Nudge reuse the existing notification system (features 010/011) and transactional email (Mailpit/Resend) with the existing base template pattern; no new delivery channel is introduced.
- **Co-admins mirror events, team-scoped** — party co-admin invitations reuse the feature 006 event-admin invitation machinery (link + targeted, opaque token, expiry, revoke, last-admin guard) but are restricted to members of the party's team.
- **Team roles reuse** — "team admin" (who may form a party and pick the team) reuses the feature 005 team roles; "party admin" is a party-scoped role independent of team-admin status once granted (a party co-admin need not be a team admin, only a team member).
- **Authentication/session reuse** — the existing auth/session/account model (features 002–004) is reused unchanged; forming, answering, managing, and applying require a signed-in account.
- **Party URL** — a party has its own direct URL under its team (e.g. a team-scoped party path); there is **no browsable parties index** this iteration (consistent with events being direct-link only).
- **Out of scope this iteration**: payment split and travel/carpool functionality (placeholders only); any party-level waiting list; parties on individuals-only events; cross-team parties; naming/renaming parties; a browsable parties index; auto-disband/cleanup (disband is manual); and reactivation of a disbanded party.
