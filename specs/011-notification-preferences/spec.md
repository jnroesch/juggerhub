# Feature Specification: Notification Preferences

**Feature Branch**: `011-notification-preferences`

**Created**: 2026-07-08

**Status**: Draft

**Input**: User description: "Notification preferences as its own feature. The design is present in the Notifications wireframe (a 'Notification settings' screen: a category × channel matrix on desktop, stacked cards on mobile, changes save automatically, security & sign-in always on). Cover in-app notifications as well as email. Scope confirmed with the owner: channels = In-app + Email (no Push); categories = only those with real producers today (invites & roster/role changes, team news); delivery is instant only (no daily/weekly digest batching); no pause-all or quiet hours."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Choose how I'm notified, per category and channel (Priority: P1)

As a signed-in player, I open **Notification settings** and see my notification categories, each with a
toggle per channel — **In-app** and **Email**. I turn a channel on or off for a category and it
saves automatically (no save button). The next time that category's event happens, I'm notified only
on the channels I left on. Security & sign-in messages are shown as always-on and can't be toggled.

**Why this priority**: This is the whole feature — a place to control delivery, and the enforcement
that makes those controls real. Without it, notifications are all-or-nothing. It's independently
valuable and testable on its own with the categories that exist today.

**Independent Test**: Sign in, open Notification settings, turn Email off for Team news, have an admin
post news, and confirm no team-news email is sent while the in-app notification still appears (and
vice-versa). Confirm the choice persists across reloads and that another user's settings are separate.

**Acceptance Scenarios**:

1. **Given** I have never changed my settings, **When** I open Notification settings, **Then** every togglable channel is on by default (opt-out model) and shown per category.
2. **Given** I am on Notification settings, **When** I toggle a channel for a category, **Then** it saves immediately (no save button) and a reload shows the same state.
3. **Given** I turned In-app off for a category, **When** an event in that category happens for me, **Then** no in-app notification is created for it (no unread bump), while other categories/channels are unaffected.
4. **Given** I turned Email off for a category, **When** an event in that category happens for me, **Then** no email for that category is sent, while the in-app notification (if on) still appears.
5. **Given** the Security & sign-in group, **When** I view settings, **Then** it is shown as always-on and cannot be toggled off.
6. **Given** another user changes their settings, **When** I view mine, **Then** I only ever see and change my own (server-enforced per user).

---

### User Story 2 - Email delivery for the categories that only had in-app (Priority: P1)

As a player who wants updates in my inbox, when I leave **Email** on for a category, I actually receive
an email for that category's events — including team role changes and team news, which previously only
appeared in-app. The email is a proper transactional message (base template header/footer) and links
back to the relevant place in the app.

**Why this priority**: The Email channel must be honest — a toggle that does nothing is worse than no
toggle. Two in-scope categories (role changes, team news) have no email path today, so this story
delivers that path so the Email column is real everywhere it appears.

**Independent Test**: With Email on for the relevant category, trigger a role change and a news post,
and confirm a well-formed email is sent to the recipient (captured by the local mail sink), links to
the team, and is suppressed when Email is off for that category.

**Acceptance Scenarios**:

1. **Given** Email is on for invites & roster, **When** my role is changed, **Then** I receive a role-change email linking to the team.
2. **Given** Email is on for team news, **When** an admin posts news, **Then** I receive a team-news email linking to the team's news.
3. **Given** Email is off for a category, **When** its event happens, **Then** no email for that category is sent.
4. **Given** any category email, **When** it is sent, **Then** it uses the shared base template (header/footer) and contains no secrets or other users' data.

---

### User Story 3 - Reach settings and use them on any device (Priority: P2)

As a player on desktop, I reach Notification settings from my account/settings area and see a compact
**category × channel matrix**. On mobile I see the same categories as **stacked cards** with per-channel
chips. Both surfaces present the same categories and channels and save the same way.

**Why this priority**: The controls must be reachable and usable on both form factors per the design,
but the value (US1/US2) doesn't depend on the exact layout, so it's secondary to having working,
enforced preferences.

**Acceptance Scenarios**:

1. **Given** I am on desktop, **When** I open Notification settings, **Then** I see a category × channel matrix with a toggle at each cell.
2. **Given** I am on mobile, **When** I open Notification settings, **Then** I see stacked category cards each exposing the same channel toggles.
3. **Given** either layout, **When** I toggle a channel, **Then** it auto-saves and both layouts reflect the same stored state.
4. **Given** a load or save failure, **When** it happens, **Then** I see an honest state (retry / "couldn't save") rather than silently losing my change.

---

### Edge Cases

- A user with no stored preferences is treated as all-channels-on (defaults) — no row needed to be notified.
- Turning off **all** channels for a category silences it entirely; that's allowed and honored (the record still exists in-app only if In-app is on — if both off, the user simply isn't notified for that category).
- Security & sign-in messages (email verification, password reset, login/security) are always sent regardless of preferences and never appear as togglable.
- A rapid sequence of toggles saves the latest state without losing an intermediate change (last-write-wins per cell is fine; no lost toggle).
- A preference lookup failure at delivery time must fail safe and must not break the originating action (invite issued, role changed, news posted) — default to the opt-out default (deliver) rather than dropping, and never throw into the producer.
- A category with no producer yet is not shown (no dead toggles); adding a future producer + category must not require changing existing users' stored preferences.
- Deleting a user removes their preferences with the account.

## Requirements *(mandatory)*

### Functional Requirements

**Preference model & surface**

- **FR-001**: System MUST let an authenticated user view and change their notification preferences as a per-category × per-channel setting, for the channels **In-app** and **Email**.
- **FR-002**: The categories exposed MUST be exactly those with real producers today: **Invites & roster changes** (targeted team invite + team role change) and **Team news**. The model MUST be extensible to add categories/channels later without migrating existing users' settings.
- **FR-003**: The default for every togglable channel MUST be **on** (opt-out): a user who has never changed a setting is notified on all channels.
- **FR-004**: Changes MUST save automatically (no explicit save action) and persist per user.
- **FR-005**: A **Security & sign-in** group MUST be presented as always-on and MUST NOT be togglable; its messages are always delivered.
- **FR-006**: Settings MUST be reachable from the app's account/settings area and usable on mobile and desktop, presenting the same categories and channels.

**Enforcement**

- **FR-007**: Before creating an in-app notification, a producer MUST honor the recipient's **In-app** preference for that category — suppressing the in-app notification (and its unread count) when off.
- **FR-008**: Before sending a category email, a producer MUST honor the recipient's **Email** preference for that category — suppressing the email when off.
- **FR-009**: Preference enforcement MUST fail safe: a lookup error defaults to the opt-out default (deliver) and MUST NOT throw into or roll back the originating action.
- **FR-010**: Security/transactional sign-in email (verification, password reset, login/security) MUST be exempt from preferences and always sent.

**Email delivery for in-scope categories**

- **FR-011**: System MUST send an email for a **team role change** and for a **team news post** when the recipient's Email preference for that category is on (these categories had no email path before this feature).
- **FR-012**: Category emails MUST use the shared base email templates (header/footer) and link back to the relevant place in the app; they MUST NOT leak secrets or other users' data, and MUST NOT duplicate the existing invite email.
- **FR-013**: Sending a category email MUST NOT block or fail the originating action; a send failure is logged, not fatal.

**Authorization & privacy**

- **FR-014**: All preference reads and writes MUST be authorized server-side and scoped to the authenticated user; a user MUST NOT read or change another user's preferences.
- **FR-015**: The preferences surface and API MUST NOT expose internal errors, stack traces, or other users' data.

### Key Entities *(include if feature involves data)*

- **Notification Preference**: A per-user setting expressing, for a given notification category and channel, whether delivery is enabled. Sparse — only stored when it differs from the default (or stored per changed cell); absence means "default (on)". Owned strictly by its user; removed with the account.
- **Notification Category (reference)**: A stable grouping of producer events surfaced as one row in settings (Invites & roster changes; Team news). Maps one or more producer notification types to a single user-facing toggle set. Extensible.
- **Channel (reference)**: The delivery medium a preference governs — **In-app** or **Email** in this release (Push is out of scope).
- **User (existing)**: The account; has zero or more preference entries.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can turn any in-scope category's In-app or Email delivery on/off and the change persists and is reflected on reload, with no explicit save step.
- **SC-002**: With a channel off for a category, 0 deliveries occur on that channel for that category's next event; with it on, delivery occurs (verified per channel per category).
- **SC-003**: 100% of preference reads/writes are scoped to the authenticated user; no request can read or change another user's preferences.
- **SC-004**: Security & sign-in messages are delivered in 100% of cases regardless of preferences and are never presented as togglable.
- **SC-005**: A preference lookup failure never fails the originating action (invite/role change/news post still succeeds) and never drops a security message.
- **SC-006**: Role-change and team-news emails are well-formed (base template), link to the team, and are sent only when the recipient's Email preference for that category is on.
- **SC-007**: The settings surface follows DESIGN.md and works on mobile (stacked cards) and desktop (matrix), including honest load/save-error states.

## Assumptions

- Builds directly on feature 010 (in-app notification engine + the invite/role-change/team-news producers). This feature adds the preference model, its enforcement in those producers, and the missing email paths.
- **Channels are In-app + Email only.** Web push (the wireframe's third "Push" column) is out of scope — no push infrastructure exists; it can be added later behind the same model.
- **Instant delivery only.** The wireframe's news digest (Instant / Daily / Weekly) is not built; there is no daily/weekly batching in this release. All enabled notifications are delivered as they occur. Digest cadence is a future extension.
- **No pause-all / quiet hours.** These wireframe elements are out of scope for this release.
- Category scope is limited to producers that exist today; mentions, RSVP reminders, waitlist openings, and official fixtures are not shown because they have no producers yet.
- Opt-out model: everything on by default; a missing preference means "on".
- Security/transactional auth email (feature 002) remains always-on and is never governed by these preferences.
- The invite already sends an email; under preferences it is governed by the Invites & roster **Email** toggle (still complemented by the in-app notification governed by the In-app toggle).
