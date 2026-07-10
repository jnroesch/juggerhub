# Feature Specification: Platform Admin Area

**Feature Branch**: `013-admin-area`

**Created**: 2026-07-10

**Status**: Draft

**Input**: User description: "Platform admin area: introduce a first-class platform system-administrator role replacing the temporary config-driven admin email allowlist (AdminOptions/PlatformAdminHandler, GitHub issue #21), and build the full gated admin area per wireframes/Admin Wireframes (offline).html. Scope: (1) Real platform-admin authorization — bootstrap of the first admin without chicken-and-egg, server-side policy enforcement, migration of feature 012's PlatformAdmin policy call sites without behavior change. One admin role, no tiers. (2) Gated admin entry — a lock-marked Admin nav item on desktop and an account-menu row on mobile, rendered only for admins. (3) Admin overview at /admin. (4) User management at /admin/users. (5) Player detail at /admin/users/{handle} with account actions: suspend, send password reset link, ban — every action logged. (6) Badges & achievements on the player detail with an Assign picker, integrating feature 012's existing grant/revoke capabilities."

**Clarifications (2026-07-10, session with feature owner)**:

1. **Suspend** blocks sign-in only. All the player's data stays present and publicly visible; nothing is hidden or paused beyond access. Reversible by any admin.
2. **Ban** removes the player from the platform: the account is soft-deleted (deactivated and invisible everywhere — profile gone from public surfaces, removed from rosters/lists, cannot sign in) with data retained internally so an admin can undo a mistaken ban. The account's email cannot re-register while it remains denylisted.
3. **Admin designation is purely configuration-driven** this pass: the set of platform administrators mirrors environment configuration (synced at startup — additions grant, removals revoke). There is no in-app grant/revoke admin UI or endpoint.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A real platform-administrator role (Priority: P1)

JuggerHub has designated platform administrators whose admin status is a first-class, per-account designation in the platform's own user data — materialized from environment configuration at startup rather than checked against raw configuration on every request. The first administrator comes into existence automatically from that configuration (no manual database surgery, no chicken-and-egg), and every admin-only operation is refused server-side for anyone who is not an administrator. All admin capabilities that feature 012 shipped behind the temporary email-allowlist gate keep working identically for admins, and the per-request allowlist check is retired.

**Why this priority**: This is the privilege boundary everything else stands on (GitHub issue #21). Without it, every admin surface in this feature would sit on a mechanism that was explicitly declared temporary. It is independently valuable even with no new UI: it closes the security debt.

**Independent Test**: Via the API alone — a non-admin account calling any admin endpoint is refused; a configured admin account is permitted; restarting with unchanged configuration changes nothing; removing an identity from configuration revokes their admin designation at the next startup; with nothing configured, nobody is authorized (fails closed).

**Acceptance Scenarios**:

1. **Given** a deployment with an admin identity configured, **When** the system starts, **Then** that account is a platform administrator without any manual intervention.
2. **Given** an authenticated account that is not a platform administrator, **When** it calls any admin-only operation, **Then** the operation is refused by the server regardless of anything the client claims.
3. **Given** an identity removed from the admin configuration, **When** the system next starts, **Then** that account is no longer a platform administrator (configuration is the source of truth — it mirrors, not just seeds).
4. **Given** a platform administrator, **When** they use the badge/achievement admin capabilities from feature 012, **Then** behavior is unchanged from before this feature.
5. **Given** an empty admin configuration and no designated administrators, **When** anyone calls an admin-only operation, **Then** it is refused (fail closed), and the system logs a clear operational warning that no administrators exist.
6. **Given** a configured admin identity that has not registered yet, **When** the system starts, **Then** nothing breaks — the account is designated once it exists (at a later startup), and the skip is logged.

---

### User Story 2 - The gated door and the admin landing (Priority: P2)

As a platform administrator, I see one extra, lock-marked **Admin** entry — in the top navigation on desktop, and as a row in my account menu on mobile. Nobody else ever sees it. It opens a dedicated admin area with its own shield header, its own navigation (Overview, Users), and a clear "Back to app" exit. The landing page is an overview: four counts that matter (players, teams, events in the last 30 days, suspended accounts), a "new players this week" list, a "recently granted" badges/achievements list, and a player search that jumps straight into user management.

**Why this priority**: The entry point and landing make the admin area exist as a place. The overview turns "I'm an admin" into "I know what needs attention" in one glance and routes into the two real jobs (finding players, seeing grants).

**Independent Test**: Sign in as an admin → the Admin entry is present, opens `/admin`, shows four accurate counts and both lists. Sign in as a regular player → no Admin entry is rendered anywhere, and navigating to `/admin` directly bounces them out (while the server refuses the underlying data regardless).

**Acceptance Scenarios**:

1. **Given** a signed-in platform administrator on desktop, **When** they look at the main navigation, **Then** an Admin item, visually set apart and lock-marked, appears after the normal links.
2. **Given** a signed-in platform administrator on mobile, **When** they open their account menu, **Then** an "Admin panel" row appears (no fifth tab in the bottom navigation).
3. **Given** a signed-in regular player, **When** they use the app on any device, **Then** no admin entry is rendered anywhere, and directly visiting an admin URL redirects them away.
4. **Given** an admin on the overview, **When** the page loads, **Then** they see: total players, total teams, events in the last 30 days, and currently suspended accounts — all correct against the live data.
5. **Given** an admin on the overview, **When** they view the two lists, **Then** "New players this week" shows recently registered players (each opening that player's admin detail) and "Recently granted" shows the latest badge/achievement grants with what, to whom, by whom, and when.
6. **Given** an admin on the overview, **When** they type into the player search, **Then** they land in user management with that search applied.

---

### User Story 3 - Find and open any player (Priority: P2)

As a platform administrator, I can find any player in seconds: search by name, @handle, or team; filter by account status (All / Active / Suspended / Banned); and page through a table showing player, teams, status, admin marker, and badge count. Opening a row lands on that player's admin detail. On mobile the table folds into tappable cards. Built for hundreds of players, so search leads. Banned (soft-deleted) players are invisible everywhere else on the platform, so this list is deliberately the one place they still appear — otherwise a mistaken ban could never be found and undone.

**Why this priority**: Every admin job in the wireframe starts by finding a player. Without this, the player detail (US4/US5) is unreachable except by URL guessing.

**Independent Test**: Seed a few players across teams and statuses; search by partial name, by @handle, and by team name; filter by Suspended and by Banned; page through results; open a row and land on the right player.

**Acceptance Scenarios**:

1. **Given** the users list, **When** the admin searches by partial display name, @handle, or team name, **Then** matching players are returned with player, teams, status, admin marker, and badge count, paginated.
2. **Given** the users list, **When** the admin applies the Active, Suspended, or Banned filter, **Then** only players in that account state are shown, combinable with the search.
3. **Given** a result row, **When** the admin opens it, **Then** they land on that player's admin detail page.
4. **Given** more results than one page, **When** the admin pages forward/back, **Then** the range indicator ("Showing X–Y of Z") stays accurate.
5. **Given** a search with no matches, **When** results come back empty, **Then** a friendly empty state explains it and invites clearing the search/filter.
6. **Given** an account that is a platform administrator, **When** it appears in the list, **Then** it carries a visible Admin marker.

---

### User Story 4 - One player, everything an admin needs (Priority: P2)

As a platform administrator, I open a player's admin detail and see who they are (identity, teams/clubs, positions they play, last activity, player id, recent activity) alongside **account help**: suspend the account (they can no longer sign in; everything they've built stays present and visible — reversible any time), send them a password-reset link (the admin never sees or sets a password), or ban the account (removed from the platform entirely: invisible everywhere, no access, their email blocked from re-registering — undoable by an admin because the data is retained). Every account action is recorded (who did it, to whom, what, when).

**Why this priority**: This is the "account help" half of the admin area's purpose — the operational reason a small community platform needs admins at all. It depends on US3 to reach the page.

**Independent Test**: On a seeded player: suspend → their sign-in is refused while their profile and content remain publicly visible; reinstate → sign-in works again; send reset link → the player receives the standard reset email; ban → they vanish from public surfaces and rosters, cannot sign in, and their email cannot register a new account; unban → everything returns. Each action lands in the record with actor and timestamp.

**Acceptance Scenarios**:

1. **Given** a player's admin detail, **When** it loads, **Then** the admin sees identity (display name, @handle, hometown, joined date), teams/clubs, positions played, last-active indication, player id, recent activity, and current account status.
2. **Given** an active player, **When** the admin suspends them (with a confirmation step), **Then** the player can no longer sign in (and any existing session stops working no later than the platform's normal session-refresh window), while their profile, teams, content, and visibility remain untouched; the list/detail show them as Suspended.
3. **Given** a suspended player, **When** they attempt to sign in, **Then** sign-in is refused with a clear, non-technical message that the account is suspended.
4. **Given** a suspended player, **When** the admin reinstates them, **Then** they can sign in again and nothing else has changed.
5. **Given** a player, **When** the admin sends a password-reset link, **Then** the player receives the platform's standard reset email; the admin is only told it was sent — never any credential.
6. **Given** a player, **When** the admin bans them (with a confirmation step), **Then** the account is deactivated and invisible everywhere (public profile gone, removed from team rosters, player browse, and other public surfaces), sign-in is refused, existing sessions stop working no later than the normal session-refresh window, and the account's email cannot be used to register a new account.
7. **Given** a banned player, **When** an admin reverses the ban, **Then** the account, its visibility, memberships, and content return intact, and the email may register/sign in again.
8. **Given** any account action above, **When** it completes, **Then** a durable record exists of who did what to whom and when.
9. **Given** a player who is a platform administrator (including the acting admin themselves), **When** an admin attempts to suspend or ban them, **Then** the action is refused with an explanation that admin accounts must first be removed from the admin configuration.

---

### User Story 5 - Grant badges & achievements from the player's detail (Priority: P3)

As a platform administrator on a player's admin detail, I see their badges & achievements and an **Assign** button — the one place grants happen. Assign opens a picker over the player with two catalogue tabs (badges and achievements — both fixed; admins pick, don't create). Items the player already holds are marked "Given" and can't be double-granted. An optional note says why, and it lands in the grant record. Each held item can be revoked from the same page. (The grant/revoke capability itself exists from feature 012 — this story gives it its intended home.)

**Why this priority**: The capability already exists via feature 012's surface; this re-homes it into the per-player flow the wireframe defines. Valuable, but nothing is lost if it ships last.

**Independent Test**: From a player's detail: open Assign, see both catalogues with already-held items marked, grant one with a note → it appears in the player's list and the overview's "Recently granted" with attribution; revoke it → it disappears.

**Acceptance Scenarios**:

1. **Given** a player's admin detail, **When** it loads, **Then** the player's badges and achievements are listed with type, grantor, and date, each with a revoke affordance.
2. **Given** the Assign picker, **When** it opens, **Then** badges and achievements appear on separate tabs from the fixed catalogue, with already-held items marked "Given" and not selectable.
3. **Given** a selected catalogue item and an optional note, **When** the admin grants it, **Then** the grant is recorded (who, to whom, what, when, note) and shows immediately on the player's detail.
4. **Given** a held item, **When** the admin revokes it (with confirmation, as feature 012 established), **Then** it is removed and the removal is recorded.
5. **Given** the picker, **When** the admin tries to grant an already-held item, **Then** the system refuses (also server-side).

---

### Edge Cases

- **No admins configured at all**: configuration empty — every admin operation refuses (fail closed) and an operational warning is logged. The platform otherwise works normally.
- **Configured identity doesn't exist yet**: the configured admin identity has not registered — nothing breaks; the account is designated once it exists (at a later startup), and the skip is logged.
- **Identity removed from configuration**: their admin designation is revoked at the next startup; until then they remain an admin (configuration changes require a restart to take effect, which is accepted for this pass).
- **Suspending or banning an administrator (or oneself)**: refused while the target is a designated admin — remove them from the admin configuration first. Since admins come only from configuration, no last-admin lockout is possible in-app.
- **Sessions of a suspended or banned player**: new sign-ins are refused immediately; any existing session stops working no later than the platform's normal session-refresh window.
- **Banned player's email tries to register again**: registration is refused while the email is denylisted, with a message that does not reveal more than necessary.
- **Banned player tries password reset**: completing a reset does not restore access — the ban gates access, not the credential.
- **Suspended player's footprint**: entirely untouched — profile, teams, events, badges, and content stay present and publicly visible; only sign-in is blocked.
- **Banned player's footprint**: retained internally but invisible everywhere player-facing (public profile, team rosters, browse/search, event participant lists); reversal restores it intact. The admin users list is the one place a banned player still appears.
- **Ban of a player who is a team's only admin / an event's only admin**: the team/event keeps functioning for others; the wider "orphaned ownership" problem is out of scope this pass and left to existing platform behavior.
- **Player detail for a handle that doesn't exist**: a not-found state within the admin area, not a crash.
- **Concurrent double-grant**: two admins granting the same item to the same player — one succeeds, the other is refused as already-held.
- **Old allowlist config left in place**: harmless — the same configuration now drives the designation sync; it is never again consulted per-request as the authorization check.

## Requirements *(mandatory)*

### Functional Requirements

**Platform-admin authorization (replaces the interim gate — issue #21)**

- **FR-001**: The system MUST represent platform-administrator status as a per-account designation stored in the platform's user data — one admin role, no tiers.
- **FR-002**: Every admin-only operation MUST be authorized server-side against this designation; client state is never the boundary. With zero administrators, all admin operations MUST fail closed.
- **FR-003**: At startup the system MUST synchronize the set of designated administrators to mirror the environment configuration: configured identities that exist gain the designation, designated accounts no longer configured lose it, and configured identities that don't exist yet are skipped with a log and picked up at a later startup. The sync MUST be idempotent.
- **FR-004**: All admin capabilities shipped by feature 012 MUST work identically under the new mechanism, and the interim per-request email-allowlist check MUST be removed as an authorization path (the same configuration lives on solely as the designation sync source).
- **FR-005**: There is NO in-app or API surface to grant or revoke the admin designation this pass; configuration is the only way. Admin accounts MUST be visibly marked in the admin users list.

**Gated entry**

- **FR-006**: The client MUST render the admin entry (lock-marked navigation item on desktop; account-menu row on mobile) only for platform administrators, and MUST redirect non-admins away from admin URLs — as UX only, with FR-002 as the actual boundary.
- **FR-007**: The admin area MUST be a distinct area with its own header identifying it as admin space, its own navigation (Overview, Users), and an always-available way back to the normal app.

**Overview**

- **FR-008**: The overview MUST show four live counts: total players, total teams, events in the last 30 days, and currently suspended accounts.
- **FR-009**: The overview MUST list recently registered players (this week) linking into their admin detail, and recently granted badges/achievements (what, to whom, by whom, when).
- **FR-010**: The overview MUST offer a player search that leads into user management with the query applied.

**User management**

- **FR-011**: Admins MUST be able to search players by display name, @handle, or team name, filter by account status (All / Active / Suspended / Banned), and page through results; results show player identity, teams, account status, admin marker, and badge count.
- **FR-012**: Opening a result MUST lead to that player's admin detail, addressed by their @handle. Banned players MUST remain findable and openable here (and only here).

**Player detail & account actions**

- **FR-013**: The player admin detail MUST show identity (display name, @handle, hometown, joined date), teams/clubs, positions played, a last-active indication, the player id, recent platform activity, and current account status.
- **FR-014**: Admins MUST be able to suspend an account (with confirmation): a suspended player cannot sign in, and existing sessions stop working no later than the platform's normal session-refresh window; everything else — profile, visibility, teams, content — stays untouched. Any admin can reinstate, restoring sign-in.
- **FR-015**: Admins MUST be able to ban an account (with confirmation): the account is deactivated and removed from all player-facing surfaces (public profile, rosters, browse/search, participant lists), cannot sign in, existing sessions stop working no later than the normal session-refresh window, and the account's email cannot register a new account while denylisted. All data is retained internally; an admin can reverse the ban, restoring the account, its visibility, and its email's ability to be used, intact.
- **FR-016**: Admins MUST be able to trigger the platform's standard password-reset flow for a player. The admin MUST never see or set any credential; the outcome shown is only that the link was sent.
- **FR-017**: Every account action (suspend, reinstate, ban, unban, reset-link sent) MUST be durably recorded with actor, target, action, and time.
- **FR-018**: Suspend and ban MUST NOT destroy data: profile, team memberships, event history, badges/achievements, and content are preserved (visibly for suspension, internally for ban) and return intact on reversal.
- **FR-019**: The system MUST refuse suspend/ban against designated platform administrators, including the acting admin themselves; removing them from the admin configuration is the required first step.

**Badges & achievements on the player detail (integrates feature 012)**

- **FR-020**: The player admin detail MUST list the player's badges and achievements (type, grantor, date, note) with per-item revoke, and provide an Assign picker over the fixed catalogues (badges / achievements tabs) with already-held items marked "Given" and not grantable — enforced server-side as feature 012 already does.
- **FR-021**: Grants from the picker MUST support an optional note that lands in the grant record, consistent with feature 012's existing note behavior.

### Key Entities

- **Platform administrator designation**: a per-account marker of platform-admin status; mirrored from environment configuration at startup (grant and revoke); never tiered; not modifiable at runtime this pass.
- **Account state**: each account is exactly one of Active, Suspended, or Banned. Suspended = sign-in blocked, everything else untouched and visible. Banned = soft-deleted: invisible on all player-facing surfaces, sign-in blocked, email denylisted against re-registration; data retained internally. Both states fully reversible by an admin.
- **Registration denylist**: the set of email addresses currently blocked from registering (populated by bans, cleared by unbans).
- **Admin action record**: an append-only record of administrative account actions — actor, target, action, timestamp. Grant/revoke records for badges/achievements exist from feature 012 and are reused, not duplicated.
- **Admin overview data**: derived counts (players, teams, recent events, suspended) and recent lists (new players, recent grants) — computed from existing data, not stored.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of admin-only operations refuse non-admin callers server-side — verified by tests covering every admin endpoint group, including a caller whose email is in the configuration but whose designation sync has not run (pre-startup state cannot occur at runtime, so: covering that config alone grants nothing per-request).
- **SC-002**: A fresh environment with a configured admin identity has a working administrator after startup with zero manual steps, and removing that identity from configuration removes the administrator at the next startup.
- **SC-003**: An admin can go from anywhere in the app to any specific player's admin detail in under 15 seconds using the admin entry, search, and result row.
- **SC-004**: A suspended player is refused sign-in immediately, with their public footprint verifiably unchanged; reinstatement restores sign-in with zero data change across the round trip.
- **SC-005**: A banned player disappears from every player-facing surface (public profile, rosters, browse, participant lists) on next load, cannot sign in, and their email cannot register; reversing the ban restores all of it without data loss.
- **SC-006**: Every administrative account action taken during verification is attributable afterwards (who, whom, what, when) from durable records.
- **SC-007**: The users list stays responsive with hundreds of players (search-first design), returning filtered results in under 2 seconds.
- **SC-008**: Feature 012's badge/achievement admin flows pass their existing tests unchanged after the authorization migration.

## Assumptions

- **Configuration changes require a restart** to affect the admin set; no hot-reload of the designation sync is expected this pass.
- The existing password-reset (forgot-password) flow is reused as-is; the admin surface only triggers it for a target account.
- "Recent activity" and "last active" on the player detail reuse the platform's existing activity/event data (feature 003's activity model); no new tracking is introduced for this pass.
- The fixed badge/achievement catalogues and their management UI from feature 012 remain; this feature re-homes only the *grant-to-player* flow into the player detail. The existing 012 catalogue-management surface remains reachable within the admin area.
- Account states apply to the account as a whole (not per-team/per-event). No email is sent to the affected player on suspend/ban this pass; a suspended player learns of the state when attempting to sign in.
- The registration denylist matches on the banned account's email address; evasion via a different email address is out of scope this pass.
- Orphaned-ownership situations caused by a ban (e.g. a team or event whose only admin is banned) are left to existing platform behavior this pass.
- The wireframe's "Try next" items are out of scope: moderation/reports queue, bulk grants, admin-created badge types, and a full audit-log UI (records are kept; a browsing UI comes later).
- The wireframe's suspend/ban descriptions ("hide their profile & pause posting") were superseded by the feature owner's clarification (suspend = sign-in block only; ban = soft-delete + denylist); the wireframe remains authoritative for layout and flow.
- Names, counts, and dates in the wireframes are placeholders; flows and states are the requirement.
