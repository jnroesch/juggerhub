# Feature Specification: Events

> **⚠️ Superseded in part by [feature 026](../026-authenticated-only-access/spec.md) (2026-07-22):**
> the "anonymous / public event reads" invariant is reversed — event detail, participants, news,
> and contacts are now **authenticated-only**. Anonymous access is no longer supported for any
> event data. The rest of this spec (creation, sign-up, admin) stands.

**Feature Branch**: `006-events`

**Created**: 2026-07-03

**Status**: Draft

**Input**: User description: "Events page. Authenticated users can create new events (tournaments, workshops, or any custom type). Every event has a start and end date/time, a name, and a description. Events are offline (requiring a full address including country) or virtual (requiring only a link to a tool like Zoom). Each event allows sign-ups from either teams only or individuals only, with a participation limit; once full, further sign-ups go to a waiting list, and promotion from the waiting list is a manual admin action. Some events charge an entrance fee (paid out-of-band; no in-app payment). Each event has a news section admins post to, a free-form contacts list admins manage, and a participant list (joined + waiting). Admins can remove participants, promote from the waiting list, approve paid sign-ups, invite co-admins, and cancel the event. The event page shows joined teams/players and lists contact information. Creation is a guided stepped wizard. Reference wireframes at wireframes/Events Wireframes.html."

## Clarifications

### Session 2026-07-04

- Q: Is a browsable/searchable events index page in scope this iteration, or are events reached only by direct link? → A: **Direct-link only** — no events index/browse/discovery page this iteration; events are reached via their own URL, exactly like team pages in feature 005. A browsable/searchable events list is a later slice.
- Q: For a teams-only event, who may enter or withdraw a team? → A: **Team admins only** — only a user who administers the team may enter it into an event or withdraw it, mirroring feature 005's team-management authority. A regular member cannot commit or withdraw the crew.
- Q: Should cancelling an event and inviting co-admins be available to all admins, or the creator only? → A: **All admins share powers** — the creator and accepted co-admins have identical powers (edit, news, participants, contacts, invite co-admins, cancel), subject to the last-admin guard. The creator is simply the first admin (mirrors the team model).
- Q: How are cancellation notices (and future participant notifications) delivered this iteration? → A: **Email** — transactional email via the existing Mailpit/Resend infrastructure, consistent with feature 005's targeted invites. A general in-app notification system remains a separate GitHub issue (#14).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create an event with the guided wizard (Priority: P1)

Any signed-in user can create an event through a guided, stepped wizard — one decision per screen with round-knob progress, in the same calm style as onboarding. The steps are: (1) **type & name** — pick a type (tournament, workshop, or a custom/other type with its own label) and give the event a name and description; (2) **when** — a start and an end date/time, multi-day allowed; (3) **where** — either **in person** (a full address including country) or **virtual** (a link to a tool such as Zoom); (4) **who can join** — either **teams only** or **individuals only**, plus a participation limit (how many teams/people can hold a spot); (5) **fee** — free (this screen is otherwise skipped) or a paid event with a recipient name, an IBAN, and an optional payment deadline. A final review screen summarises everything, then **Publish** creates the event. Whoever creates the event becomes its first admin and lands on the new event's page.

**Why this priority**: The event is the root object of the whole feature — the page, sign-ups, participants, news, contacts, and administration all hang off it. Nothing else can be demonstrated until an event can be created, so this must land first.

**Independent Test**: Sign in, open "create event", walk the wizard for an in-person paid tournament (teams only, limit 16) and again for a virtual free workshop (individuals only), and confirm each event is created with the entered details, the creator is recorded as its sole admin, and the creator lands on the event page; confirm invalid inputs at each step (blank name, end before start, missing country for in-person, missing link for virtual, non-positive limit) are rejected server-side.

**Acceptance Scenarios**:

1. **Given** a signed-in user in the wizard, **When** they complete all steps with valid input for an in-person teams-only paid event and publish, **Then** the event is created with that type, name, description, dates, address (including country), teams-only mode, limit, and fee details, and the creator becomes its first (and only) admin.
2. **Given** the "where" step, **When** the user chooses **virtual**, **Then** the address fields are hidden and not required, a link is required instead, and the event is stored as virtual with that link.
3. **Given** the "where" step, **When** the user chooses **in person**, **Then** the link field is hidden and a full address including **country** is required; submitting without a country (or other required address parts) is refused with a clear message.
4. **Given** the "who can join" step, **When** the user selects teams-only or individuals-only and a participation limit, **Then** the mode and a positive integer limit are stored; a missing/zero/negative limit is refused server-side.
5. **Given** the "when" step, **When** the end date/time is before the start, **Then** creation is refused with a clear message and no event is created; a multi-day span (end on a later day than start) is accepted.
6. **Given** the "fee" step left as **free**, **When** the event is published, **Then** it is a free event and no payment details are stored; **When** a fee is set, **Then** a recipient name and IBAN are required and an optional payment deadline may be given.
7. **Given** an unauthenticated visitor, **When** they attempt to create an event, **Then** the action is refused and they are directed to sign in.
8. **Given** a completed wizard, **When** the user reviews the summary and publishes, **Then** the event exists and is reachable at its own page; abandoning the wizard before publish creates no event.

---

### User Story 2 - View the public event page (Priority: P1)

Anyone — including a logged-out visitor — can open an event's page and see everything they need to decide whether to take part: the event name and type, its description, when it runs, where it happens (address for in-person, or the join link for virtual), the entrance fee if any (shown as information only), and the latest news. The page also shows **who's taking part** in three groups — **joined** (in), **awaiting approval**, and the **waiting list** — and a **contacts** list so a visitor can reach the organisers with questions. Whether spots are open or full, the same three groups are shown; when the event is full the primary action becomes "join the waiting list", and a cancelled event is clearly marked as cancelled by the organiser.

**Why this priority**: The event page is the headline surface — once an event exists and can be viewed by anyone, the feature delivers value to organisers and prospective participants alike. Reading the page (spots, groups, contacts) is also a precondition for signing up and for every management action.

**Independent Test**: For a seeded event, open the page as a logged-out visitor and confirm it shows the name, type, description, dates, location (address or link), fee (if any), latest news, the three participant groups with their members, and the contacts list; confirm a full event shows a "waiting list" call to action and a cancelled event shows the cancelled state.

**Acceptance Scenarios**:

1. **Given** a published in-person event, **When** anyone opens its page, **Then** they see its name, type, description, start/end, full address, fee (if any, marked information-only), latest news, and the contacts list.
2. **Given** a virtual event, **When** the page is shown, **Then** the join link is presented in place of an address.
3. **Given** an event with participants, **When** the page is shown, **Then** the joined, awaiting-approval, and waiting-list groups each list their members (teams or players, per the event's mode), and the counts reflect the participation limit.
4. **Given** an event whose occupied spots have reached its limit, **When** a prospective participant views it, **Then** the page indicates it is full and the primary action offers the **waiting list** rather than a direct join.
5. **Given** a cancelled event, **When** anyone opens its page, **Then** it is clearly marked "cancelled by the organiser", no join/waitlist actions are offered, and the existing content (details, news, contacts) remains visible.
6. **Given** any event page, **When** it is rendered, **Then** it is responsive and legible on phone and desktop per DESIGN.md, with friendly empty states for news, participants, and contacts when there are none.

---

### User Story 3 - Sign up for an event (join or waiting list) (Priority: P1)

A signed-in user can take part in an event that accepts their kind of participant. For an **individuals-only** event they join as themselves; for a **teams-only** event they enter a team they administer, which takes one spot as a single crew. If the event is **free** and has an open spot, they are admitted straight to **joined**. If the event is **paid** and has an open spot, their sign-up is recorded as **awaiting approval** (a provisional hold on a spot) until an admin confirms payment; the page shows them how to pay (recipient, IBAN, deadline). If the event is **full**, they instead join the **waiting list** and are not charged unless a spot opens for them. A participant (or the team's admin) can withdraw, freeing their spot or waitlist place.

**Why this priority**: Sign-up is the core loop that makes an event more than a listing; together with create (US1) and view (US2) it is the minimum viable product.

**Independent Test**: As a signed-in user, join a free individuals-only event with open spots and confirm you land in **joined**; sign up for a paid event with open spots and confirm you land in **awaiting approval** with payment instructions; when the event is full, confirm sign-up puts you on the **waiting list**; withdraw and confirm your place is released.

**Acceptance Scenarios**:

1. **Given** a free individuals-only event with an open spot, **When** a signed-in user joins, **Then** they are added directly to the **joined** group and the open-spot count decreases.
2. **Given** a paid individuals-only event with an open spot, **When** a signed-in user signs up, **Then** they are recorded as **awaiting approval** holding a provisional spot, and the page shows the payment recipient, IBAN, and any deadline.
3. **Given** a teams-only event, **When** a user who administers a team enters that team, **Then** the team occupies one spot as a single participant (subject to the free/paid rules above); a user with no team they administer cannot enter a team.
4. **Given** an event whose spots are full (joined plus awaiting-approval equal the limit), **When** a user signs up, **Then** they are placed on the **waiting list** in arrival order and are not asked to pay yet.
5. **Given** a participant in joined, awaiting-approval, or the waiting list, **When** they (or, for a team, an admin of that team) withdraw, **Then** they are removed and any spot they held is released; withdrawal never auto-promotes anyone from the waiting list.
6. **Given** an individuals-only event, **When** a user tries to enter a team (or a teams-only event, **When** a user tries to join as an individual), **Then** the mismatched sign-up is refused server-side.
7. **Given** a user (or team) already taking part in an event, **When** they attempt to sign up again, **Then** a duplicate sign-up is prevented.
8. **Given** a cancelled or already-ended event, **When** a user attempts to sign up or join the waiting list, **Then** the action is refused.

---

### User Story 4 - Administer participants and the waiting list (Priority: P2)

An admin manages the event from the very same event page (there is no separate admin layout) — a manage affordance opens the admin toolkit. In **Manage participants & waitlist** the admin sees the joined, awaiting-approval, and waiting-list groups and can: **approve** an awaiting-approval sign-up (confirming payment was received out-of-band, which admits them to joined), **promote** someone from the waiting list into an open spot (a deliberate manual action — never automatic), and **remove** anyone from the joined list or the waiting list. Approvals and promotions are always explicit admin decisions.

**Why this priority**: Without administration a paid event can never admit anyone and a full event's waiting list is inert, so this is the first management slice after the core loop — but the event is still viewable and joinable (for free events with open spots) without it.

**Independent Test**: As an admin of a seeded paid, full event, approve an awaiting-approval participant and confirm they move to joined; promote a waiting-list entry into a freed spot and confirm the move; remove a joined participant and a waiting-list entry and confirm each leaves; confirm a non-admin cannot perform any of these.

**Acceptance Scenarios**:

1. **Given** an admin viewing an awaiting-approval sign-up on a paid event, **When** they approve it, **Then** the participant moves to **joined** (payment recorded as confirmed) and continues to occupy their spot.
2. **Given** an admin and a waiting-list entry with at least one open spot, **When** the admin promotes that entry, **Then** it leaves the waiting list and takes a spot — moving to **joined** for a free event or to **awaiting approval** (pending payment) for a paid event.
3. **Given** an admin, **When** they remove a participant from the joined or awaiting-approval group, or an entry from the waiting list, **Then** that participant/entry is removed and any spot it held is released, with no automatic promotion of anyone else.
4. **Given** promotion when no spot is open, **When** the admin attempts it, **Then** it is refused with a clear reason (promotion cannot exceed the limit).
5. **Given** a non-admin (participant or visitor), **When** they attempt to approve, promote, or remove anyone, **Then** the action is refused server-side regardless of client state.
6. **Given** any promotion or approval, **When** it occurs, **Then** it happens only as an explicit admin action; nothing auto-fills a spot when a participant leaves.

---

### User Story 5 - Post news updates (Priority: P2)

An admin can post updates to the event's **news** section from the event page. Posts appear newest-first on the page for everyone to read (author, when, body). News keeps participants informed of schedule changes, logistics, and announcements.

**Why this priority**: News is high-value for run-of-show communication but the event is fully usable without it, so it follows participant administration.

**Independent Test**: As an admin, post an update and confirm it appears at the top of the event's news for all viewers; confirm a non-admin sees the feed but has no compose affordance and cannot post via a direct attempt.

**Acceptance Scenarios**:

1. **Given** an admin on the event page, **When** they post a news update with a body, **Then** it appears at the top of the news section with the author and a relative time, visible to everyone who views the page.
2. **Given** a non-admin viewer, **When** they view the page, **Then** they see the news feed read-only with no compose affordance, and a direct post attempt is refused server-side.
3. **Given** an event with no news, **When** the page is shown, **Then** a friendly empty state appears instead of an error.
4. **Given** many news posts, **When** the feed is shown, **Then** it is paginated/bounded and ordered newest-first.

---

### User Story 6 - Manage event contacts (Priority: P2)

An admin can maintain a free-form **contacts** list for the event so participants know who to reach for what. Each contact has a name, a role (free text such as location host, caterer, or moderator), and a phone number and/or an email (at least one). An admin can add any number of contacts and update or remove them; all contacts show on the public event page.

**Why this priority**: Contacts materially help participants but are not required to run the sign-up loop, so they sit alongside the other management slices.

**Independent Test**: As an admin, add a contact with a role and an email, update its phone, and remove it; confirm each change is reflected on the public page; confirm a non-admin cannot add/update/remove contacts.

**Acceptance Scenarios**:

1. **Given** an admin, **When** they add a contact with a name, a role, and at least one of phone/email, **Then** it is stored and shown on the event page under contacts.
2. **Given** an admin adding a contact with neither phone nor email, **When** they submit, **Then** it is refused with a clear message (at least one contact method required).
3. **Given** an admin, **When** they update or remove an existing contact, **Then** the change is reflected on the page immediately.
4. **Given** a non-admin, **When** they attempt to add, update, or remove a contact, **Then** the action is refused server-side.
5. **Given** an event with no contacts, **When** the page is shown, **Then** a friendly empty state appears in the contacts area.

---

### User Story 7 - Invite co-admins to help administer (Priority: P2)

An admin can invite other users to co-administer the event, either by copying an invite link to share, or by searching existing users by name and inviting them directly. Co-admins can edit the event, post news, and manage participants and contacts. The invite screen lists pending admin invitations and lets an admin revoke one before it is accepted.

**Why this priority**: Shared administration is important for larger events but a single organiser can run an event alone, so it follows the core management slices.

**Independent Test**: As an admin, copy the admin-invite link and confirm its form and expiry; search a user by name and invite them; confirm both appear as pending, that accepting grants co-admin powers, and that a non-admin cannot invite or revoke.

**Acceptance Scenarios**:

1. **Given** an admin on the invite screen, **When** they copy the admin-invite link, **Then** a shareable link is presented; anyone accepting it (once signed in) becomes a co-admin, and the link can be revoked/regenerated.
2. **Given** an admin, **When** they search a user by name and invite them, **Then** a targeted admin invitation is created, delivered to that user, and shown as pending until accepted or revoked.
3. **Given** a user who accepts an admin invitation, **When** they open the event, **Then** they can edit the event, post news, and manage participants and contacts.
4. **Given** a pending admin invitation, **When** an admin revokes it, **Then** it can no longer be accepted.
5. **Given** a non-admin, **When** they attempt to invite or revoke a co-admin, **Then** the action is refused server-side.
6. **Given** the last remaining admin, **When** any removal/step-down is attempted, **Then** it is blocked so the event always retains at least one admin.

---

### User Story 8 - Cancel an event (Priority: P3)

From a clearly marked danger zone on the event page, an admin can cancel the event. Cancellation cannot be undone: the page stays up, marked "cancelled by the organiser"; no new sign-ups or waiting-list joins are accepted; and everyone currently joined or waiting is notified. To run the event again, the organiser would create a new event.

**Why this priority**: Destructive lifecycle management matters but is infrequent and not part of the core loop, so it is the final slice.

**Independent Test**: As an admin, cancel a seeded event with joined and waiting participants and confirm the page shows the cancelled state, all sign-up/waitlist actions are disabled, participants are notified, and the action cannot be reversed; confirm a non-admin cannot cancel.

**Acceptance Scenarios**:

1. **Given** an admin in the event's danger zone, **When** they confirm cancellation, **Then** the event is marked cancelled by the organiser and the page reflects this while retaining its details, news, and contacts.
2. **Given** a cancelled event, **When** anyone attempts to sign up, join the waiting list, be approved, or be promoted, **Then** the action is refused.
3. **Given** an event with joined and waiting participants, **When** it is cancelled, **Then** everyone joined or waiting is notified of the cancellation.
4. **Given** a cancelled event, **When** an admin looks for a way to reactivate it, **Then** none is offered — reactivation requires creating a new event.
5. **Given** a non-admin, **When** they attempt to cancel, **Then** it is refused server-side.

---

### Edge Cases

- **Occupied spots definition**: A spot is occupied by **joined** and **awaiting-approval** participants; **waiting-list** entries never count toward the limit. The event is "full" when occupied spots equal the limit.
- **No auto-promotion**: When a joined or awaiting-approval participant leaves (withdraws, is removed, or a paid hold is dropped), the freed spot stays open until an admin manually promotes someone; nothing auto-fills.
- **Concurrent last-spot sign-ups**: Two users signing up for the final spot at once — at most one takes the spot; the other is placed on the waiting list. Capacity is enforced atomically server-side.
- **Promotion beyond capacity**: An admin promoting more entries than there are open spots is refused for the ones that would exceed the limit.
- **Paid hold with a deadline**: A payment deadline is informational for participants and admins; approval remains a manual admin action. (Whether unpaid holds auto-expire at the deadline is out of scope — see Assumptions.)
- **Teams-only entry authority**: Only a user who administers a team may enter that team; entering a team the user does not administer is refused. A team may appear at most once per event.
- **Individual/team in multiple events**: A user or team may take part in many different events concurrently; the duplicate guard is per-event, not global.
- **Mode/limit changes after sign-ups**: Changing participant mode (teams↔individuals) once anyone has signed up is disallowed; the limit may be raised freely but cannot be lowered below the current occupied-spot count. (Editability rules — see Assumptions.)
- **Cancelled/past events**: Sign-up, waitlist join, approval, and promotion are all refused on a cancelled or already-ended event; the page remains viewable.
- **Contact with no method**: A contact must carry at least one of phone or email; one with neither is refused.
- **Last-admin guard**: The event must always retain at least one admin; the last admin cannot be removed or step down (mirrors the team model).
- **Non-admin direct API access**: Approving, promoting, removing, posting news, managing contacts, inviting co-admins, editing, and cancelling via direct API by a non-admin are refused regardless of client state (never trust the client).
- **Empty states**: A brand-new event with no participants, news, or contacts shows friendly empty states, not errors.
- **Withdrawn/removed then re-signup**: A participant who left may sign up again if the event still accepts sign-ups; they re-enter under the current spot rules (joined/pending/waitlist as applicable).

## Requirements *(mandatory)*

### Functional Requirements

#### Event creation & identity

- **FR-001**: Any authenticated user MUST be able to create an event via a guided, multi-step wizard; unauthenticated users MUST be refused and directed to sign in.
- **FR-002**: An event MUST capture a **type** (tournament, workshop, or a custom/other type with a free-text label), a **name**, and a **description**; name and (for a custom type) the custom label are validated server-side against published length limits.
- **FR-003**: An event MUST capture a **start** and an **end** date/time; the end MUST be on or after the start, and multi-day spans MUST be allowed. Invalid ranges are refused server-side.
- **FR-004**: An event MUST be either **in person** or **virtual**. In-person events MUST require a full address including **country**; virtual events MUST require a **link** and MUST NOT require an address. The unused location fields MUST be hidden and not stored.
- **FR-005**: An event MUST have a participation **mode** of either **teams only** or **individuals only**, chosen at creation, and a positive-integer participation **limit**. A missing/zero/negative limit MUST be refused.
- **FR-006**: An event MUST be either **free** or **paid**. A paid event MUST store a payment **recipient name** and **IBAN** and MAY store an optional **payment deadline**; a free event MUST store no payment details. Fee information is **display-only** — the system MUST NOT process any in-app payment.
- **FR-007**: On creation, the creating user MUST be recorded as the event's first admin, and MUST be taken to the created event's page. Abandoning the wizard before the final publish MUST create no event.

#### Event page & visibility

- **FR-008**: The event page MUST be viewable by anyone, including unauthenticated visitors, and MUST present the event's name, type, description, start/end, location (address for in-person or link for virtual), fee (if any, marked information-only), latest news, the three participant groups (joined, awaiting approval, waiting list), and the contacts list.
- **FR-009**: The page MUST indicate whether spots are **open** or **full** based on occupied spots (joined + awaiting-approval) versus the limit, and MUST offer the **waiting list** as the primary action when full.
- **FR-010**: A **cancelled** event MUST be clearly marked as cancelled by the organiser, MUST retain its details/news/contacts for viewing, and MUST offer no sign-up, waitlist, approval, or promotion actions.
- **FR-011**: Every list surface on the page (participant groups, news, contacts, user search) MUST be paginated/bounded and MUST show friendly empty, loading, and error states; raw exceptions and sensitive data MUST NOT reach the client.

#### Sign-up, spots & waiting list

- **FR-012**: A signed-in user MUST be able to sign up for an event that matches its mode — as **themselves** for an individuals-only event, or by entering a **team they administer** for a teams-only event. A mismatched sign-up (individual to teams-only, or team to individuals-only) MUST be refused server-side.
- **FR-013**: For a team entry, only a user who **administers** that team MUST be able to enter it, and a team MUST appear at most once per event.
- **FR-014**: For a **free** event with an open spot, a sign-up MUST be admitted directly to **joined**. For a **paid** event with an open spot, a sign-up MUST be recorded as **awaiting approval**, holding a provisional spot, with the payment recipient/IBAN/deadline shown to the participant.
- **FR-015**: **Occupied spots** MUST be counted as joined plus awaiting-approval participants; waiting-list entries MUST NOT count. When occupied spots equal the limit, a new sign-up MUST be placed on the **waiting list** in arrival order and MUST NOT be asked to pay.
- **FR-016**: Capacity MUST be enforced atomically server-side so that concurrent sign-ups for the last spot cannot exceed the limit; the surplus sign-up MUST fall through to the waiting list.
- **FR-017**: A participant MUST be able to **withdraw** from any group (joined, awaiting approval, or waiting list); for a team entry, an admin of that team MUST be able to withdraw it. Withdrawal MUST release any held spot and MUST NOT auto-promote anyone.
- **FR-018**: The system MUST prevent a **duplicate** sign-up by the same user or team for the same event, and MUST refuse any sign-up or waitlist join on a **cancelled** or already-ended event.

#### Participant administration

- **FR-019**: An admin MUST be able to **approve** an awaiting-approval sign-up on a paid event, recording payment as confirmed and moving the participant to **joined** while keeping their spot.
- **FR-020**: An admin MUST be able to **promote** a waiting-list entry into an open spot — moving it to **joined** for a free event or to **awaiting approval** for a paid event — only when a spot is open; promotion that would exceed the limit MUST be refused. Promotion MUST always be an explicit manual admin action and MUST NEVER occur automatically.
- **FR-021**: An admin MUST be able to **remove** any participant from the joined, awaiting-approval, or waiting-list groups; removal MUST release any held spot without auto-promoting anyone.
- **FR-022**: All participant-administration actions (approve, promote, remove) MUST be authorized server-side and refused for non-admins regardless of client state.

#### News

- **FR-023**: An admin MUST be able to post a **news** update (with a body) to the event; posts MUST appear newest-first and be visible to everyone viewing the page.
- **FR-024**: Non-admins MUST see the news feed read-only with no compose affordance, and a direct post attempt MUST be refused server-side. The feed MUST be paginated/bounded with a friendly empty state.

#### Contacts

- **FR-025**: An admin MUST be able to add any number of **contacts**, each with a name, a free-text role, and at least one of a phone number or an email; a contact with neither method MUST be refused.
- **FR-026**: An admin MUST be able to **update** and **remove** contacts; changes MUST be reflected on the public event page. All contacts MUST be shown on the event page; contact management MUST be admin-only and authorized server-side.

#### Administration & co-admins

- **FR-027**: An event MUST support one or more **admins**; the creator is the first admin. Admins MUST be able to **edit** the event's details, post news, and manage participants and contacts.
- **FR-028**: An admin MUST be able to invite **co-admins** by a shareable invite link and by searching existing users by name; a targeted invite MUST be delivered to that user and shown as pending until accepted or revoked. Accepting grants co-admin powers (edit, news, participant and contact management).
- **FR-029**: The event MUST always retain **at least one admin**; any admin removal or step-down that would leave zero admins MUST be blocked server-side, enforced atomically (mirrors the team last-admin guard).
- **FR-030**: Editing MUST NOT break existing sign-ups: the participant **mode** MUST NOT be changed once any sign-up exists, and the **limit** MUST NOT be lowered below the current occupied-spot count (it MAY be raised). Other details (name, description, dates, location, fee, type) MAY be edited by an admin.

#### Cancellation

- **FR-031**: An admin MUST be able to **cancel** the event from a clearly marked danger zone with an explicit confirmation; cancellation MUST be irreversible (no reactivation path).
- **FR-032**: A cancelled event MUST reject all new sign-ups, waitlist joins, approvals, and promotions, while remaining viewable, and MUST **notify by email** everyone currently joined or on the waiting list of the cancellation (reusing the existing transactional-email infrastructure). For **individual** sign-ups the notified recipient is the user; for **team** sign-ups (a team has no single address) the notified recipients are that team's **admins**.

#### Cross-cutting

- **FR-033**: Every authorization decision in this feature MUST be enforced server-side; client-side gating exists only for UX and is never the security boundary.
- **FR-034**: Every list surface (participant groups, news, contacts, co-admin invitations, user search) MUST be paginated/bounded and never returned as an unbounded collection.
- **FR-035**: All screens MUST be responsive and legible on phone and desktop per DESIGN.md, including empty, loading, and error states, and MUST NOT leak raw exceptions or sensitive data to the client.

### Key Entities *(include if feature involves data)*

- **Event**: The root object. Attributes: type (tournament / workshop / other + custom label), name, description, start and end date/time, location kind (in-person or virtual) with either a full address (including country) or a link, participant mode (teams-only or individuals-only), participation limit, fee details (free, or recipient name + IBAN + optional deadline), and status (published or cancelled). Has many participants, news posts, contacts, admins, and admin invitations.
- **Event Admin**: A link between an event and a user granting administrative powers (edit, news, participant and contact management, invite co-admins, cancel). The creator is the first admin; an event always has ≥ 1 admin.
- **Event Admin Invitation**: A pending or resolved invitation to co-administer, as a shared **link** or a **targeted** invite bound to one user. Attributes: kind, opaque token, target user (targeted only), issuing admin, issued time, expiry, and status (pending / accepted / revoked / expired). A targeted invite is delivered to its user.
- **Event Participant** (a live **sign-up / registration**, distinct from the historical activity/attendance record that backs profile & team activity): A team's or a user's participation in an event. Attributes: the subject (a user for individuals-only, a team for teams-only), status (joined / awaiting approval / waitlisted), arrival order (for waiting-list ordering), payment-confirmed flag (paid events), and joined time. A user/team appears at most once per event.
- **Event News Post**: An event update shown newest-first on the page. Attributes: author (an admin), body, and time.
- **Event Contact**: A free-form point of contact shown on the event page. Attributes: name, free-text role, and at least one of phone or email.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A signed-in user can create an event end-to-end through the guided wizard in under 3 minutes and lands on its page as its sole admin.
- **SC-002**: 100% of admin-only actions (approve, promote, remove, post news, manage contacts, invite/revoke co-admin, edit, cancel) are refused for non-admins when attempted directly, independent of the client UI.
- **SC-003**: A prospective participant can, from the public page alone, determine the event's type, timing, location, fee, remaining spots, who is taking part, and how to contact organisers — without signing in.
- **SC-004**: Sign-up routes correctly in 100% of cases: free-with-open-spot → joined, paid-with-open-spot → awaiting approval, full → waiting list; and occupied spots never exceed the limit under concurrent sign-ups.
- **SC-005**: No spot is ever filled automatically — every admission from awaiting-approval or the waiting list is an explicit admin approval or promotion, verifiable in 100% of transitions.
- **SC-006**: An admin can approve a paid sign-up, promote from the waiting list into an open spot, and remove any participant, with the three groups and counts reflecting each change immediately.
- **SC-007**: Cancelling an event makes it read-only for sign-ups (100% of sign-up/waitlist/approve/promote attempts refused), keeps the page viewable marked cancelled, and notifies every joined and waiting participant.
- **SC-008**: The event's participant groups, news, and contacts render correct, bounded data with friendly empty states and are usable on phone and desktop including loading and error states.
- **SC-009**: An event always has at least one admin; the last admin can never be removed or step down (100% of such attempts blocked).

## Assumptions

The following reasonable defaults were chosen where the description left a detail
open. The four genuine product decisions were locked via `/speckit-clarify` (see
`## Clarifications`, Session 2026-07-04) and are marked *(resolved in clarification)*
below.

- **Events reached by direct link** *(resolved in clarification)* — an event's page is viewable by anyone, including logged-out visitors (it is the surface people use to decide whether to take part); creating, signing up, and all administration require authentication. There is **no events index / discovery / search page this iteration** — events are reached via their own URL, exactly like team pages in feature 005; a browsable/searchable events list is a later slice.
- **Team entry authority** *(resolved in clarification)* — for a teams-only event, a team is entered by a user who **administers** that team (reusing feature 005 roles); the team occupies one spot as a single participant, and only an admin of that team may enter or withdraw it. A regular team member cannot commit or withdraw the crew.
- **Co-admin power parity** *(resolved in clarification)* — all admins (creator and accepted co-admins) share the same powers, including inviting further co-admins and cancelling, subject to the last-admin guard; the creator is simply the first admin, mirroring the team model the codebase already implements.
- **Notification delivery** *(resolved in clarification)* — cancellation notices (and any future participant notifications) are delivered by **transactional email**, reusing the existing Mailpit/Resend infrastructure from feature 002, consistent with how feature 005 handled targeted invites. A general in-app notification system remains the separately-tracked GitHub issue (#14).
- **Paid holds do not auto-expire** — an awaiting-approval (unpaid) participant keeps their provisional spot until an admin approves or removes them; the payment deadline is informational and does not automatically drop the hold this iteration.
- **Waiting-list order** — the waiting list preserves arrival order for display and as a suggested promotion order, but promotion is fully manual and an admin may promote any entry (not strictly FIFO).
- **Fee is out-of-band** — payment happens entirely outside the app by bank transfer; "approve" means an admin confirms funds were received. No payment provider, invoicing, or refund handling is in scope. Any amount/currency of the fee is captured as free-form fee information alongside recipient/IBAN.
- **Editability** — an admin may edit event details after publish; the participant **mode** is locked once sign-ups exist and the **limit** cannot drop below current occupied spots. Finer edit rules (e.g. editing address after people committed) are UX guardrails finalized in planning.
- **Address shape** — an in-person address captures the parts needed to locate a venue (e.g. venue/name, street, postal code, city, country), with **country required**; exact fields finalized in planning. Virtual events store a single link (URL).
- **Admin-invite lifetime** — event admin invitations reuse the invitation model/machinery from feature 005 (opaque token, expiry, revoke), including a shareable link and targeted email invites, adapted for admin (not membership) grants.
- **Authentication/session reuse** — the existing auth, session, and account model from features 002–004 is reused unchanged; sign-up and administration require a signed-in account, and sign-in/registration redirect back to the event afterward.
- **Data model reuse** — this feature builds on the existing minimal Events model already used for profile/team activity (features 003/005); the richer event described here extends that model rather than introducing a parallel one. Exact reconciliation is a planning task.
- **Out of scope this iteration**: in-app payment/refunds and payment-provider integration; automatic waiting-list promotion or auto-expiry of unpaid holds; a browsable events index / discovery / search page (a later slice — events are reached by direct link this iteration); calendar export / reminders; per-event chat or comments; recurring events and templates; ticketing/QR check-in; and any general in-app notification system (a separate GitHub issue, #14) — notifications use email this iteration.
