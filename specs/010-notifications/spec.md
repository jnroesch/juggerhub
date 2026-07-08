# Feature Specification: In-App Notification System

**Feature Branch**: `010-notifications`

**Created**: 2026-07-08

**Status**: Draft

**Input**: User description: "In-app notification system. Authenticated users get an in-app notifications surface (the existing /alerts inbox + an unread badge on the bell) that shows team and event activity. Reusable, extensible notification engine. First release wires up: targeted team invite (inline accept/decline), team member role change, and new team news post. Delivery is real-time (push unread count and new notifications to connected clients) with a fallback for initial load and mark-read. Notifications support mark read/unread, are paginated, and are authorized strictly server-side per recipient. Complements transactional email without duplicating it."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See and read my notifications (Priority: P1)

As a signed-in player, I open the Alerts surface and see a reverse-chronological list of the
things that have happened for me — an invite to a team, a change to my role, a news post from my
team — each showing what happened, who/what it concerns, and how long ago. Unread items are
visually distinct. Opening the surface (or a single item) lets me mark items read, and the
unread count on the bell reflects reality.

**Why this priority**: This is the core value and the reusable spine every producer plugs into.
Without the inbox, unread state, and badge, no producer notification has anywhere to land. It is
the smallest slice that delivers standalone value — even with a single seeded notification the
user gets a working, honest inbox.

**Independent Test**: Seed one notification for a user; sign in; confirm it appears in the Alerts
list with correct text and relative time, the bell shows an unread count of 1, marking it read
removes it from the unread count, and another user never sees it.

**Acceptance Scenarios**:

1. **Given** I have 3 unread notifications, **When** I load any authenticated page, **Then** the bell shows an unread badge with the count (capped display for large counts).
2. **Given** I am on the Alerts surface, **When** notifications exist, **Then** they are listed newest-first, paginated, each with an icon, title, supporting line, and relative timestamp, unread ones visually marked.
3. **Given** I have unread notifications, **When** I mark one (or all) read, **Then** it is no longer counted as unread and the badge updates.
4. **Given** another user has notifications, **When** I request my notifications, **Then** I never receive theirs (server-enforced per-recipient scoping).
5. **Given** I have zero notifications, **When** I open Alerts, **Then** I see an honest empty state.

---

### User Story 2 - Act on a team invite from the notification (Priority: P1)

As a player who has been targeted-invited to a team, I receive an in-app notification that shows
the team and inviter. I can accept or decline the invite directly from the notification row
without leaving the Alerts surface. Accepting joins me to the team; declining dismisses the
invite. The notification reflects the resolved state afterward and is no longer actionable.

**Why this priority**: This is the motivating producer (GitHub issue #14) and the only one with
inline actions, so it exercises the full round trip: producer → notification → action → state
change → notification update. It is independently testable and valuable on its own.

**Independent Test**: Admin targeted-invites a user; that user signs in and, from the Alerts row,
taps Accept; confirm they become a team member and the notification is no longer actionable.
Repeat with Decline and confirm the invite is declined.

**Acceptance Scenarios**:

1. **Given** an admin sends me a targeted team invite, **When** I open Alerts, **Then** I see an invite notification naming the team and inviter with Accept and Decline actions.
2. **Given** an invite notification, **When** I tap Accept, **Then** I join the team and the notification shows a resolved state with the actions removed.
3. **Given** an invite notification, **When** I tap Decline, **Then** the invite is declined and the notification shows a resolved state with the actions removed.
4. **Given** the underlying invite already expired or was revoked, **When** I tap Accept or Decline, **Then** I get a clear "no longer available" outcome and the notification resolves without error.
5. **Given** I acted on the invite elsewhere (e.g. via the invite link/screen), **When** I return to Alerts, **Then** the notification reflects the resolved state (no duplicate join, no stale action).

---

### User Story 3 - Stay informed of team role changes and news (Priority: P2)

As a team member, when an admin changes my role (promotes or demotes me) or posts team news, I
receive an in-app notification so I learn about it without waiting for an email or visiting the
team page. Tapping the notification takes me to the relevant place (the team space / news).

As a team admin, I can post a news update to my team from the team space; posting it announces the
update to the whole roster (via notifications) and adds it to the team's news feed.

**Why this priority**: Proves the engine's extensibility beyond invites with two additional
producers that have no inline actions (link-only), covering the common "informational"
notification shape. Valuable but secondary to having an inbox and acting on invites. The team-news
producer requires a news-posting action, which does not yet exist (only reading the feed is
implemented), so this story also delivers the minimal admin "post team news" capability that fires
the notification. Posting is admin-only because it fans out to every team member.

**Acceptance Scenarios**:

1. **Given** I am a team member, **When** an admin changes my role, **Then** I receive a notification stating my new role on that team, linking to the team space.
2. **Given** I am a team admin, **When** I post team news, **Then** the post is added to the team news feed and every other team member receives a notification linking to the team's news.
3. **Given** I am not a team admin, **When** I attempt to post team news, **Then** the action is refused server-side.
4. **Given** an admin performs the action, **When** the actor is myself (e.g. I post my own team's news, or I change my own role by stepping down), **Then** I do not receive a notification for my own action.
5. **Given** a team is deleted, **When** I view Alerts, **Then** notifications for that team degrade gracefully (no broken links, no errors).

---

### User Story 4 - Real-time delivery (Priority: P2)

As a signed-in player with the app open, when a new notification is created for me it appears in
my Alerts surface and increments my bell badge without a manual refresh. If real-time delivery is
unavailable, the surface still loads correct data on navigation.

**Why this priority**: Real-time is the chosen delivery mechanism and materially improves
responsiveness, but the system must remain correct and usable through the non-real-time load path
alone, so it layers on top of P1.

**Acceptance Scenarios**:

1. **Given** I have the app open, **When** a new notification is created for me, **Then** it appears and the bell badge increments without a page reload.
2. **Given** I mark notifications read in one open tab, **When** I have another open tab/device, **Then** the unread count converges (eventually consistent).
3. **Given** the real-time channel is unavailable, **When** I navigate to Alerts, **Then** I still see my correct, current notifications and unread count.
4. **Given** I am not signed in, **When** the real-time channel is attempted, **Then** the connection is rejected (no notifications delivered to anonymous clients).

---

### Edge Cases

- Very large unread counts display capped (e.g. "9+" / "99+") rather than an unbounded number.
- A producer action targeting many recipients (e.g. team news to a large roster) fans out one notification per recipient without blocking the originating action.
- The actor of an action is never notified about their own action.
- A recipient who is offline when a notification is created still sees it on next load and via the badge.
- An invite notification whose underlying invite is resolved/expired/revoked out-of-band is never double-actionable; inline actions reconcile to the true invite state.
- Duplicate suppression: a producer firing twice for the same logical event should not spam identical unread notifications (idempotency where a natural key exists).
- Deleting the source (team, invite, news post) must not break the notification list rendering.
- Pagination never returns unbounded results; deep history is bounded and retrievable page by page.

## Requirements *(mandatory)*

### Functional Requirements

**Surface & unread state**

- **FR-001**: System MUST provide an authenticated in-app notifications surface (the Alerts inbox) reachable from the app shell on both mobile and desktop.
- **FR-002**: System MUST display an unread-count badge on the notifications bell across the authenticated app, reflecting the signed-in user's unread notifications, with a capped display for large counts.
- **FR-003**: System MUST list a user's notifications newest-first with pagination and never return unbounded collections.
- **FR-004**: Each notification MUST convey a type-appropriate icon, a title, a supporting line, a relative timestamp, a read/unread state, and (when applicable) a navigation target and/or inline actions.
- **FR-005**: Users MUST be able to mark an individual notification read and mark all read; unread count MUST update accordingly.
- **FR-006**: System MUST show an honest empty state when a user has no notifications.

**Authorization & privacy**

- **FR-007**: System MUST authorize every notification read/mutation strictly server-side, scoped to the authenticated recipient; a user MUST NOT be able to read or mutate another user's notifications.
- **FR-008**: System MUST reject real-time notification connections from unauthenticated clients and MUST only deliver a user's own notifications over the real-time channel.
- **FR-009**: System MUST NOT expose internal errors, stack traces, or another user's data through the notifications surface or channel.

**Engine & producers**

- **FR-010**: System MUST provide a reusable, extensible notification creation capability that new sources can call without bespoke per-source storage, distinguishing notification types via a stable type discriminator and a small structured payload.
- **FR-011**: System MUST create a notification for the target user when a targeted team invite is issued; that notification MUST support inline Accept and Decline actions.
- **FR-012**: Accepting/declining from the notification MUST perform the same authoritative invite action as the existing invite flow (idempotent, reconciled to true invite state) and MUST resolve the notification so it is no longer actionable.
- **FR-013**: System MUST create a notification for a member whose team role is changed by an admin, stating the new role and linking to the team.
- **FR-014**: System MUST provide an admin-only capability to post a team news update from the team space; posting MUST persist the news to the existing team news feed and MUST create a notification for every other team member linking to the team news. The posting actor MUST NOT be notified of their own post.
- **FR-015**: System MUST NOT notify the actor about their own action.
- **FR-016**: Producer fan-out MUST NOT block or fail the originating action; a notification-delivery failure MUST NOT roll back the underlying action (invite issued, role changed, news posted).
- **FR-017**: Where a natural idempotency key exists for a logical event, the system SHOULD avoid creating duplicate unread notifications for the same event and recipient.

**Real-time delivery**

- **FR-018**: System MUST push newly created notifications and unread-count changes to the affected user's connected clients in real time.
- **FR-019**: System MUST remain fully correct without the real-time channel: initial load, pagination, unread count, and mark-read all work over the request/response path.

**Integration with email**

- **FR-020**: In-app notifications MUST complement, not replace, existing transactional email; the two MUST NOT be coupled such that one failing breaks the other, and the in-app system MUST NOT duplicate transactional email sending.

### Key Entities *(include if feature involves data)*

- **Notification**: A single item addressed to one recipient user. Attributes: recipient, type discriminator (e.g. team-invite, team-role-change, team-news), a structured payload sufficient to render title/supporting line/target and to drive any inline action (e.g. related team, related invite, actor, resulting role), read/unread state and read time, and creation time. Derives from the shared base entity (UUIDv7 id, created/modified timestamps). Owned strictly by its recipient.
- **Notification Type**: A stable enumeration of notification kinds that determines icon, rendering, navigation target, and whether inline actions apply. Extensible — new kinds are added without schema changes to existing kinds.
- **Recipient (User)**: The existing account. A user has many notifications; each notification belongs to exactly one user.
- **Related sources (existing)**: Team invite, team membership/role, team news post — referenced by id within the notification payload so the surface can render and (for invites) act, and so deletion of a source degrades gracefully.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A newly created notification for an online recipient appears on their Alerts surface and updates the bell badge within a few seconds without a manual refresh.
- **SC-002**: 100% of notification reads and mutations are scoped to the recipient; no request can retrieve or change another user's notifications (verified by authorization tests).
- **SC-003**: A recipient can accept or decline a team invite from the notification and reach the correct end state (joined or declined) in a single interaction, with the notification resolving so it cannot be double-acted.
- **SC-004**: The notifications list is always paginated and bounded; no endpoint returns an unbounded collection regardless of history size.
- **SC-005**: Issuing an invite, changing a role, or posting news never fails or is delayed by notification delivery; the originating action succeeds even if notification creation fails.
- **SC-006**: A user with no notifications sees an honest empty state; a user with unread notifications sees an accurate, capped badge count.
- **SC-007**: The Alerts surface and its states (list, unread, empty, loading, error, inline invite actions) follow DESIGN.md and are usable on mobile and desktop.

## Assumptions

- The Alerts surface and the notifications bell already exist as placeholders (feature 008); this feature adds data, unread badge, and real-time behavior to them rather than introducing new navigation.
- The existing team invite accept/decline logic is reused as the authoritative action for inline invite actions; the notification surface does not reimplement invite semantics.
- Recipients of team news and role-change notifications are current team members at the time of the action; historical membership changes do not retroactively generate notifications.
- Notifications are per-user and are not shared, broadcast to teams as a unit, or grouped across users.
- No user-facing per-type notification preferences/muting in this release (all supported producers notify eligible recipients); preferences are a future extension.
- No separate notification retention/expiry policy in this release beyond bounded pagination; deep history remains but is only retrieved page by page.
- Real-time delivery targets the signed-in web client; email remains the durable channel for users who are not active in-app.
- Requires the existing authentication/session, team membership/role, and team invite capabilities.
- Team-news *posting* does not yet exist (only reading the feed is implemented). This feature adds
  the minimal admin-only posting action needed to trigger the team-news notification producer; a
  richer news-management experience (edit/delete, rich text) is out of scope and can follow in a
  team-space spec. This is intentional cross-feature drift into 005-team-space, recorded here.
