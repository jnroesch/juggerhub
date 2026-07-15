# Feature Specification: Event Marketplace (Mercenaries)

**Feature Branch**: `017-event-marketplace`

**Created**: 2026-07-14

**Status**: Draft

**Input**: User description: "A marketplace on the event page. Individual users looking for a party — *mercenaries* in Jugger lingo — post themselves; parties short on players post their open spots. The party leader decides whether the party is visible there. Individual players can apply to join a party, and parties can invite individual players — both a two-way accept. Aside from the public marketplace, parties can invite other users directly. Wireframes: wireframes/Event Marketplace Wireframes (offline).html (screens 7a–7g)."

## Clarifications

### Session 2026-07-14

- Q: What makes a player ineligible to post as a mercenary or be invited to a party at a given event? → A: **Only if already *In* a party** — a player may hold at most one crew seat per event and is ineligible only once they are actually *In* a party for that event. Being a member of a team that has a party here (but not having joined it) does **not** disqualify them. This is the single "one event, one crew" invariant.
- Q: Where does a mercenary see the invites they've received and applications they've sent? → A: **On the event marketplace page, on their dashboard, and via notifications** — all three. The party admin's side lives on the party's recruiting page.
- Q: When a mercenary joins one party, what happens to their other pending market requests and their own listing for that event? → A: **Auto-cancel + take down** — joining one crew cancels all their other pending applications/invites for that event and removes their free-agent listing.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Post yourself as a mercenary (Priority: P1)

An individual with **no crew seat** at a **teams** event opens the event page and finds a **mercenary market** board right below "Who's taking part". Because they are not yet in any party here, they can **post themselves as a free agent**: their name and photo come from their profile, they pick the **positions** they play (from the Jugger pompfen/Läufer set) and write a **short pitch**. Once posted they appear on the board's free-agents side and parties can find them. They can **edit** or **take down** the listing at any time. A player who is **already in a party** for this event sees the market but the post action is unavailable, with the reason made plain.

**Why this priority**: The free-agent listing is one of the two halves the whole board is built from; without individuals able to post themselves, a party has no one to discover. It is the smallest slice that puts a real, visible thing on the board and is independently demonstrable.

**Independent Test**: As a signed-in user who is not in any party for a seeded teams event, open the event page, confirm the mercenary market renders below "Who's taking part", post a listing (positions + pitch), confirm it appears on the free-agents side; edit and take it down; from a second account that is already In a party for the event, confirm the post action is unavailable with an explanation and the board is still browsable.

**Acceptance Scenarios**:

1. **Given** a signed-in user not in any party for a teams event, **When** they open the event page, **Then** a "mercenary market" board is shown below "Who's taking part" with a free-agents side, a parties-recruiting side, and a position filter.
2. **Given** an eligible user, **When** they post a listing choosing positions and a pitch, **Then** the listing appears on the board's free-agents side showing their profile name, photo, chosen positions, and pitch.
3. **Given** a live listing, **When** the owner edits the positions/pitch or takes it down, **Then** the board reflects the change and a taken-down listing no longer appears.
4. **Given** a user who is already **In** a party for this event, **When** they view the market, **Then** posting is unavailable with a clear reason ("you're already in a crew here") and the board remains browsable.
5. **Given** an individuals-only event, **When** anyone views it, **Then** no mercenary market is shown (the market is offered on teams events only this iteration).
6. **Given** a cancelled or already-ended event, **When** anyone views it, **Then** posting/applying/inviting are unavailable (the board is read-only or hidden); existing listings are not actionable.

---

### User Story 2 - Put your party on the board (Priority: P1)

A **party admin** managing their party (feature 016) finds a new **Recruiting** block. It is **off by default** — the party fills from within the team until the admin opts in. Flipping **"looking for players"** on lists the party publicly on the event's board with **how many spots** to advertise, **which positions** are needed, and an optional **short blurb**. Only the party admin controls this. The listing can be flipped **off any time** — the party drops off the board, but applications already received stay in the recruiting inbox. The public listing **auto-closes at the party's roster cap** and reopens when a spot frees, mirroring the party request.

**Why this priority**: The recruiting listing is the other half the board is built from; together with US1 it makes the board two-sided and real. A party can't be discovered or applied to until it opts in, so this is core.

**Independent Test**: As a party admin of a seeded open party, open Manage party, confirm Recruiting is off by default; flip it on with spots/positions/blurb and confirm the party appears on the board's parties-recruiting side exactly as a free agent would see it; flip it off and confirm it disappears from the board while a previously-received application remains in the inbox; fill the party to its cap and confirm the listing auto-closes.

**Acceptance Scenarios**:

1. **Given** a party admin in Manage party, **When** they open the Recruiting block, **Then** it is **off by default** and the party is not on the board.
2. **Given** the admin flips recruiting on and sets spots, positions needed, and an optional blurb, **When** they save, **Then** the party appears on the board's parties-recruiting side showing the team, event, open-spot count, needed positions, and blurb.
3. **Given** recruiting is on, **When** a non-admin (party member or outsider) attempts to change it, **Then** it is refused server-side; only a party admin may toggle recruiting or edit its fields.
4. **Given** recruiting is on, **When** the admin flips it off, **Then** the party leaves the board but any applications already received remain in the recruiting inbox.
5. **Given** a recruiting party whose **In** count reaches the roster cap, **When** the board is viewed, **Then** the listing auto-closes (shows full / offers no "apply") and reopens automatically when a spot frees.
6. **Given** a disbanded party or a cancelled/ended event, **When** the board is viewed, **Then** the party's listing is gone and recruiting cannot be toggled on.

---

### User Story 3 - The two-way handshake: apply and invite (Priority: P1)

Entry works the same from either direction and **always needs the other side's yes**. A **free agent** taps **"Apply to join"** on a recruiting party, chooses **what they'd play**, and sends an **application** — a pending request the **party admin** must accept. A **party admin** taps **"Invite to party"** on a free agent, notes it would take one of their open spots, and sends an **invite** — a pending request the **mercenary** must accept. Nobody is added to a roster on one side's action alone. The initiator can **revoke/withdraw** a pending request before it is answered, and the recipient can **decline** (which simply drops it).

**Why this priority**: The handshake is the heart of the marketplace — the mechanism by which a listing or a recruiting party turns into a real connection. It is the payoff of US1 + US2 and the minimum that makes the board more than a directory.

**Independent Test**: As a free agent, apply to a seeded recruiting party and confirm a pending application is created and visible to the party admin; from a party admin, invite a seeded free agent and confirm a pending invite is created and visible to the mercenary; confirm the initiator can revoke a pending request and the recipient can decline; confirm neither action alone adds anyone to the party.

**Acceptance Scenarios**:

1. **Given** an eligible free agent viewing a recruiting party, **When** they apply choosing positions they'd play, **Then** a **pending application** (mercenary → party) is created and neither side's roster changes.
2. **Given** a party admin viewing a free agent, **When** they invite them, **Then** a **pending invite** (party → mercenary) is created, noted as taking one of the party's open spots, and no roster changes.
3. **Given** a pending request, **When** the **initiator** revokes/withdraws it before it is answered, **Then** it is cancelled and disappears from both inboxes.
4. **Given** a pending request, **When** the **recipient** declines it, **Then** it is recorded declined and drops from the actionable list (no roster change).
5. **Given** an existing pending request between a party and a user, **When** the same pair attempts a duplicate application/invite, **Then** it is refused (at most one active request per party+user).
6. **Given** a user who is **not eligible** (already In a party for this event), **When** an application or invite involving them is attempted, **Then** it is refused server-side.
7. **Given** a non-admin of the party, **When** they attempt to invite, accept, or revoke on the party's behalf, **Then** it is refused server-side.

---

### User Story 4 - Accept lands the mercenary in the party (Priority: P1)

A **yes from either side** lands the mercenary in the party as a **normal player**. Accepting requires a **free spot** and is enforced **atomically** (like the party join): the mercenary becomes a party member with status **In**, counting toward the party's **roster cap** (default 8) and holding a **pending-payment** placeholder exactly like a team member. The only visible trace is a quiet **"guest · via market"** tag. Because a player holds **one crew seat per event**, joining **auto-cancels all their other pending applications/invites** for this event and **takes down their free-agent listing**. The party's board listing **auto-closes** if this fills it.

**Why this priority**: Turning an accepted handshake into party membership is the whole point — it is what actually gets a player onto a crew and closes the loop opened by US1–US3.

**Independent Test**: As a party admin, accept a seeded pending application and confirm the mercenary appears in the party's **In** roster with a guest tag, the In count rises, their listing is gone, and any other pending requests of theirs for this event are cancelled; from a mercenary account, accept a seeded invite and confirm the same; fill a party to its cap and confirm a further accept is refused (no free spot) and the board listing shows full.

**Acceptance Scenarios**:

1. **Given** a pending application and an open spot, **When** the party admin accepts, **Then** the mercenary becomes a party member (In) tagged "guest · via market", the In count increments, and they hold a pending-payment placeholder like any member.
2. **Given** a pending invite and an open spot, **When** the mercenary accepts, **Then** the same membership result occurs.
3. **Given** a mercenary who joins a party, **When** the join completes, **Then** all their other **pending** applications/invites for this event are cancelled and their free-agent listing is taken down.
4. **Given** a party at its roster cap, **When** an accept is attempted, **Then** it is refused (no free spot) and the board listing shows full; the cap is enforced atomically so concurrent accepts can never exceed it.
5. **Given** an accepted guest, **When** the party is later applied to the event (feature 016), **Then** the guest is part of the team's single crew entry and does **not** create a separate individual event entry.
6. **Given** a decline (not an accept), **When** it is recorded, **Then** no membership is created and no spot is taken.

---

### User Story 5 - One shared inbox, both sides, and reach (Priority: P2)

Every application and invite lands in **one shared inbox**, shaped the same from both sides. The **party admin** gets a recruiting view (dense on desktop): **applications to accept** (accept / decline) and **invites they've sent** (awaiting-reply / declined, with revoke), plus the current fill (e.g. 6 / 8). The **mercenary** gets their **market** view: **invites to answer** (accept / decline) and **their own applications** with status (pending / declined). The mercenary's view is reachable **on the event marketplace page** and **from their dashboard** (a market module), and each **incoming invite** also arrives as an **in-app notification and a transactional email** (respecting notification preferences, feature 011).

**Why this priority**: The core loop (US1–US4) works with minimal surfacing, but a usable market needs both sides to see and manage their pending requests in one place and to be reachable off the event page. It rides just behind the core loop.

**Independent Test**: Seed a party with pending applications and sent invites; as the party admin confirm both groups render with correct statuses and counts and that revoke/accept/decline work. As a mercenary with a pending invite and pending applications, confirm the market view renders on the event page and on the dashboard, that a notification and email were sent for the incoming invite, and that a non-owner cannot read someone else's inbox.

**Acceptance Scenarios**:

1. **Given** a party admin on the recruiting page, **When** it loads, **Then** it shows applications to accept and invites sent (with awaiting/declined status and revoke) and the party's current fill.
2. **Given** a mercenary, **When** they open their market view on the event page or the dashboard module, **Then** they see invites to answer and their own applications with status.
3. **Given** an incoming invite to a mercenary, **When** it is created, **Then** the mercenary receives an in-app notification and a transactional email, subject to their notification preferences.
4. **Given** any inbox surface, **When** it lists requests, **Then** the lists are paginated/bounded with friendly empty states.
5. **Given** a user, **When** they attempt to read another user's market inbox or another party's recruiting inbox, **Then** it is refused server-side.

---

### User Story 6 - Invite anyone directly (Priority: P2)

Beyond the public board, a **party admin** can pull in **any eligible user** by hand — **search by name or @handle**, exactly like inviting event co-admins (feature 006). The target **need not have a listing**. It is still the same **two-way handshake**: the invite lands as a **notification and in the target's market inbox**, and nothing happens until they **accept**. Only players who are **not already in a party** for this event can be invited; a pending direct invite can be **revoked** before it is answered.

**Why this priority**: Direct invites let a party reach beyond the board (e.g. a known player who hasn't posted), but the board handshake (US3–US4) already delivers the core value, so this follows.

**Independent Test**: As a party admin, search for a user by name/@handle who has no listing, invite them, and confirm a pending invite is created, delivered as a notification and in their market inbox, and appears in the party's "sent invites" as awaiting reply; confirm an ineligible user (already in a party here) cannot be invited and that the invite can be revoked before it is answered.

**Acceptance Scenarios**:

1. **Given** a party admin, **When** they search users by name/@handle and invite one who has no listing, **Then** a pending invite is created identical in kind to a board invite (same accept/decline handshake).
2. **Given** a direct invite, **When** it is created, **Then** it is delivered to the target as an in-app notification and appears in their market inbox, and shows in the party's sent-invites list as awaiting reply.
3. **Given** a target who is already **In** a party for this event, **When** a direct invite is attempted, **Then** it is refused server-side (ineligible).
4. **Given** a pending direct invite, **When** the admin revokes it before it is answered, **Then** it is cancelled and removed from both sides.
5. **Given** a non-admin, **When** they attempt a direct invite on the party's behalf, **Then** it is refused server-side.

---

### User Story 7 - The mercenary is a normal party member (Priority: P3)

Once in, a mercenary is **just another player**. They appear in the party's **In** roster alongside team members, carrying only a quiet **"guest · via market"** tag and their agreed position(s); they see the party hub (roster, news, travel) like any member. Their presence is subject to the same lifecycle as any party member: a party admin may **remove** them (freeing the spot, no team/badge side-effects since they were never a team member), they may **leave**, and if the **party disbands**, the **event entry is withdrawn**, or the **event is cancelled/ends**, the guest membership and any related listings/requests are cleaned up. A guest is never a party admin/co-admin (co-admins stay team-scoped).

**Why this priority**: Correct membership semantics and cleanup matter, but they refine the already-working accept flow (US4) and are exercised less often, so they land last.

**Independent Test**: As a party admin of a party containing a guest, confirm the guest shows in the In roster with a guest tag and counts toward the fill; remove the guest and confirm the spot frees with no team-membership or badge change; from the guest's account confirm they can view the party hub; disband the party and confirm the guest membership and all related market listings/requests are removed.

**Acceptance Scenarios**:

1. **Given** a party with a guest, **When** the roster is listed, **Then** the guest appears in the **In** group with a "guest · via market" tag and counts toward the fill exactly like a team member.
2. **Given** a guest, **When** they open the party, **Then** they can view the party hub (roster, news) as a member; they are never granted party-admin powers and cannot be made a co-admin.
3. **Given** a party admin removes a guest (or the guest leaves), **When** it completes, **Then** the spot frees, the board listing may reopen, and no team membership, badge, or history is created or changed (the guest was never on the team).
4. **Given** a party is disbanded or its event entry withdrawn or the event cancelled/ended, **When** it completes, **Then** guest memberships and the party's market listings and pending requests are cleaned up consistently.
5. **Given** a paid event, **When** a guest is in the crew, **Then** their pending-payment placeholder is shown but no in-app payment occurs (the payment split remains a feature-016 placeholder this iteration).

---

### Edge Cases

- **One event, one crew (eligibility)**: a user is eligible to post a listing, apply, or be invited iff they are **not currently In any party** for that event. Merely being a member of a team that has a party here does not disqualify them.
- **Join cancels the rest**: accepting into a party auto-cancels all the joiner's other pending applications/invites for that event and removes their free-agent listing; a subsequent accept of a now-cancelled request is refused.
- **At most one active request per pair**: a party and a user have at most one active (pending) request between them at a time; duplicates are refused. A request that was declined/revoked may later be superseded by a fresh one while the user stays eligible.
- **Free spot required to accept**: accepting an application/invite requires an open spot; at the cap it is refused (not queued), enforced atomically so concurrent accepts never exceed the cap. The board listing auto-closes at the cap and reopens first-come when a spot frees.
- **Recruiting is opt-in and admin-only**: recruiting is off by default and only a party admin may toggle it or edit spots/positions/blurb; flipping it off drops the party from the board but retains already-received applications.
- **Direct invite needs no listing**: a direct invite may target any eligible user regardless of whether they have a listing; it is the same handshake and can be revoked before it is answered.
- **Guest is not a team member**: an accepted mercenary is a party member without a team membership; removing/leaving never touches team data or badges, and the "no response" roster group (team members with no party row, feature 016) is unaffected by guests.
- **Guest counts toward the party cap, not the event limit**: a guest occupies a party roster spot (players within one crew); the event's participation limit (how many teams hold a spot, feature 006) is unaffected — the team is still one entry.
- **Take-down and revoke are always available pre-resolution**: a listing can be taken down any time; a pending request can be revoked by its initiator or declined by its recipient any time before it resolves.
- **Cancelled/ended event**: posting, applying, inviting, and accepting are refused on a cancelled or already-ended event; existing listings/requests become inert.
- **Individuals-only events**: the marketplace is not offered (teams events only this iteration); individual sign-up (feature 006) is unchanged.
- **Empty states**: an event with no listings and no recruiting parties shows a friendly empty board; a mercenary with no invites/applications and a party with no applications/invites show friendly empty inboxes.
- **Non-admin / non-owner direct API access**: posting/editing/taking-down someone else's listing, toggling another party's recruiting, accepting/revoking on another party's behalf, or reading another user's inbox via direct API are all refused server-side regardless of client state.

## Requirements *(mandatory)*

### Functional Requirements

#### The marketplace board

- **FR-001**: A **teams** event MUST present a **mercenary market** board on the event page, positioned below the "Who's taking part" participation view, with two sides — **free agents** (individuals available) and **parties recruiting** (crews with open spots) — switchable by a control, and a **position filter** over the Jugger pompfen/Läufer set. Individuals-only events MUST NOT present the board.
- **FR-002**: The board MUST reflect the viewer's context: a party admin browsing the free-agents side MUST see an **invite** affordance per free agent; a mercenary browsing the parties side MUST see an **apply** affordance per recruiting party. Affordances that the viewer is not entitled to MUST be hidden client-side and refused server-side.
- **FR-003**: Both sides of the board and every inbox/search list MUST be **paginated/bounded** (never an unbounded collection) and MUST render friendly **empty**, **loading**, and **error** states.

#### Free-agent listings (mercenaries)

- **FR-004**: A signed-in user who is **eligible** (not currently In any party for the event) MUST be able to **post a free-agent listing** for a teams event: their profile name and photo are used automatically; they choose one or more **positions** (pompfen/Läufer) and write a short **pitch**. At most **one live listing per user per event**.
- **FR-005**: A listing owner MUST be able to **edit** (positions/pitch) and **take down** their listing at any time; a taken-down listing MUST no longer appear on the board and MUST NOT be actionable.
- **FR-006**: A user who is **not eligible** (already In a party for the event) MUST be refused posting server-side; the client MUST surface the reason and keep the board browsable.
- **FR-007**: A listing MUST be **automatically taken down** when its owner joins a party for that event (see FR-015).

#### Party recruiting

- **FR-008**: A **party admin** MUST be able to toggle a party's **recruiting** state (list it publicly on the board). Recruiting MUST default to **off**. When on, the admin sets **spots to advertise**, **positions needed**, and an optional **blurb**. Only a party admin MUST be able to toggle recruiting or edit its fields; all others MUST be refused server-side.
- **FR-009**: A recruiting party MUST appear on the board's parties side showing team, event, open-spot count, needed positions, and blurb. Flipping recruiting **off** MUST remove the party from the board while **retaining** applications already received.
- **FR-010**: A recruiting listing MUST **auto-close at the party's roster cap** (offer no apply) and **reopen automatically** when the In count drops below the cap, mirroring the feature-016 party request. Recruiting MUST NOT be available on a disbanded party or a cancelled/ended event.

#### The two-way handshake

- **FR-011**: A free agent MUST be able to **apply** to a recruiting party (choosing positions they'd play), creating a **pending application** (mercenary → party). A party admin MUST be able to **invite** a free agent or any eligible user, creating a **pending invite** (party → mercenary). Neither MUST change any roster until accepted.
- **FR-012**: The **recipient** of a pending request MUST be able to **accept** or **decline** it; the **initiator** MUST be able to **revoke/withdraw** it before it is answered. There MUST be **at most one active (pending) request per party+user pair**; a duplicate MUST be refused.
- **FR-013**: Applications, invites, accepts, declines, and revokes MUST all be **authorized server-side**: only the party's admins act on the party's behalf; only the targeted user answers/withdraws their own request; ineligible targets/applicants are refused regardless of client state.

#### Accepting into the party

- **FR-014**: Accepting an application (by a party admin) or an invite (by the mercenary) MUST require a **free spot** and MUST be enforced **atomically** (pessimistic, like the feature-016 join) so concurrent accepts can never exceed the roster cap; a surplus accept MUST be refused (not queued).
- **FR-015**: On a successful accept, the mercenary MUST become a **party member with status In**, distinguished as a **guest (via market)**, counting toward the roster cap like any In member and holding a **pending-payment placeholder**. The join MUST **auto-cancel all the joiner's other pending applications/invites for that event** and **take down their free-agent listing**.
- **FR-016**: A guest MUST **not** create a separate individual event entry; when the party is applied (feature 016) the guest is part of the team's single crew entry. The event participation **limit** (feature 006) MUST be unaffected by guests.

#### Reach: inbox, dashboard, notifications

- **FR-017**: The **party admin** MUST have a **recruiting inbox** listing **applications to accept** (accept/decline) and **invites sent** (awaiting/declined, revoke) with the party's current fill. The **mercenary** MUST have a **market inbox** listing **invites to answer** (accept/decline) and **their own applications** with status.
- **FR-018**: The mercenary's market view MUST be reachable **on the event marketplace page** and **from the user's dashboard** (a market module summarizing their pending invites/applications).
- **FR-019**: Each **incoming invite** MUST generate an **in-app notification and a transactional email** to the target, reusing the existing notification/email infrastructure (features 010/011) and respecting the recipient's notification preferences.

#### Direct invites

- **FR-020**: A party admin MUST be able to **invite any eligible user directly** by **searching name/@handle** (reusing the feature-006 co-admin search), with **no listing required**. A direct invite MUST be the **same handshake** (pending, accept/decline, revoke) and deliver via notification + market inbox.

#### Guest membership & lifecycle

- **FR-021**: A guest MUST appear in the party's **In** roster group with a **"guest · via market"** tag and their agreed position(s), counting toward the fill; the feature-016 "no response" group (team members with no party row) MUST remain unaffected by guests.
- **FR-022**: A guest MUST be able to view the party hub as a member but MUST NEVER be a party admin/co-admin (co-admins stay team-scoped). Removing a guest (admin) or a guest leaving MUST free the spot and MUST NOT create or change any team membership, badge, or history.
- **FR-023**: When a party **disbands**, its **event entry is withdrawn**, or the **event is cancelled/ends**, the guest memberships, the party's recruiting listing, and all related pending market requests MUST be cleaned up consistently (no orphaned board entries or actionable stale requests).

#### Cross-cutting

- **FR-024**: Every authorization decision in this feature (post/edit/take-down listing, toggle recruiting, apply, invite, accept, decline, revoke, direct-invite, read inbox) MUST be enforced **server-side**; client gating exists only for UX and is never the security boundary.
- **FR-025**: All screens MUST be **responsive** and legible on phone and desktop per DESIGN.md, MUST NOT leak raw exceptions or sensitive data to the client, and MUST reuse the established DESIGN.md tokens/components (position chips, cards, segmented control, inbox rows).

### Key Entities *(include if data involved)*

- **Mercenary Listing**: A free-agent's public post for one **event** by one **user**. Attributes: the event, the user, the **positions** they play (pompfen/Läufer set), a short **pitch**, and a live/taken-down state. At most one live listing per (user, event); taken down manually or automatically when the user joins a party for that event.
- **Party Recruitment**: A party's public recruiting state (extends the feature-016 **Party**). Attributes: recruiting **on/off** (default off), **spots to advertise**, **positions needed**, and an optional **blurb**. Governs whether and how the party appears on the board; auto-closes at the roster cap.
- **Market Request**: The two-way handshake between a **party** and a **user**. Attributes: the party, the user, a **direction** (**application** = user→party, or **invite** = party→user), the **positions** offered/requested, a **status** (pending / accepted / declined / revoked), and time. At most one active (pending) request per (party, user). Direct invites are invites with no listing prerequisite.
- **Party Member (extended)**: The feature-016 party membership gains a **guest / via-market** distinction so an accepted mercenary — a party member who is **not** a team member — is shown with a guest tag and counted in the roster while never being eligible for party-admin/co-admin.
- **Notification (extended)**: A new notification type for a **market invite** (and, where useful, an application accepted), mapped to the existing **invites & roster** preference category (feature 011).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a teams event, an individual with no crew seat can find the board and post themselves in one short form, and their listing is visible to parties in 100% of cases; individuals already In a party are refused posting with a clear reason.
- **SC-002**: A party admin can opt the party onto the board and off again; recruiting is off by default in 100% of new parties, only a party admin can change it, and flipping off retains 100% of already-received applications.
- **SC-003**: No player is ever added to a party by one side alone — 100% of marketplace memberships originate from a two-way accept (an application accepted by the party, or an invite accepted by the mercenary).
- **SC-004**: The roster cap is honoured in 100% of accepts — a party never exceeds its cap under concurrent accepts, the board listing auto-closes at the cap, and reopens when a spot frees.
- **SC-005**: "One event, one crew" holds in 100% of cases — a player is In at most one party per event, and joining one cancels their other pending requests and removes their listing.
- **SC-006**: 100% of party-admin-only and owner-only actions (toggle recruiting, invite, accept application, revoke, read the party inbox; edit/take-down a listing, answer/withdraw one's own request, read one's own inbox) are refused for unauthorized actors when attempted directly, independent of the client UI.
- **SC-007**: An invited player is reachable in 100% of cases — every incoming invite produces an in-app notification and an email (subject to preferences) and appears in the mercenary's market inbox on the event page and their dashboard.
- **SC-008**: An accepted mercenary is indistinguishable from a normal party member except for a guest tag: they count toward the cap, appear in the In roster, can view the party hub, and their removal/leaving never changes any team membership or badge in 100% of cases.

## Assumptions

The genuine product decisions were locked with the requester (see `## Clarifications`, Session 2026-07-14). Other reasonable defaults chosen where the description or wireframe left a detail open:

- **Teams events only** — the marketplace is offered on teams events (where parties exist); the marketplace on individuals-only events is a later slice (out of scope), matching the wireframe "try next". Individual sign-up (feature 006) is unchanged.
- **Positions reuse the pompfen catalog** — listing positions, positions-needed, and the position filter reuse the existing **Pompfe** set (Stab/Langpompfe/Schild/Q-Tip/Kette/Doppel-Kurz + Läufer, feature 003); no new position vocabulary is introduced.
- **Recruiting extends the party** — the recruiting on/off + spots/positions/blurb are properties of the feature-016 party managed from Manage party, available while the party is open or applied (not disbanded) on a non-cancelled/non-ended event. "Spots to advertise" is informational; actual availability is roster cap minus In, and the listing auto-closes at the cap.
- **Payment stays a placeholder** — a guest's "pending payment" mirrors the feature-016 deferred payment split; no in-app payment or per-share accounting is built this iteration.
- **Requests have no TTL** — a pending market request stays until accepted, declined, revoked, auto-cancelled (on join), or cleaned up when the party disbands / the event ends. (Co-admin invitations keep their own feature-016 TTL; market requests are separate.)
- **Direct-invite search reuses co-admin search** — searching users by name/@handle for a direct invite reuses the feature-006 event co-admin search behavior and the same "targeted invite" delivery, restricted to eligible users (not already In a party here).
- **Notifications/email reuse** — the incoming-invite notification and email reuse features 010/011 and the transactional-email base-template pattern (Mailpit local / Resend Dev/Prod); no new delivery channel is introduced. Applications to a party surface in the party's recruiting inbox (and may notify admins) rather than by email digest (admin email digests are out of scope).
- **Guest identity** — a guest is modeled as a feature-016 party member who is not a team member, marked via-market; the party roster, capacity, and hub reuse feature-016 machinery, extended to include and correctly display guests.
- **Authentication/session reuse** — the existing auth/session/account model (features 002–004) is reused unchanged; every marketplace action requires a signed-in account.
- **Out of scope this iteration**: the marketplace on individuals-only events; the functional payment split with a mercenary's share (placeholder only); a dedicated mercenary public-profile detail page; admin email digests for applications/invites; any browsable cross-event marketplace index beyond the per-user dashboard module.
