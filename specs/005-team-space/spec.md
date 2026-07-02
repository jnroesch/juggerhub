# Feature Specification: Team Space & Member Handling

**Feature Branch**: `005-team-space`

**Created**: 2026-07-02

**Status**: Draft

**Input**: User description: "Team space for JuggerHub. Every logged-in user can create a team (name + city team *or* Mixteam). The creator becomes admin and can invite others by searching users or by sharing an invite link; invitations expire after 7 days and can be accepted or declined. Accepted users join as normal members. Admins can toggle members between admin and member (at least one admin must remain) and remove members; only admins can delete the team. The team page shows members, the team's recent event activity (like a player profile), and a read-only news feed where admins can post updates. This iteration focuses on the team space and member handling itself — trainings, polls, the news composer, and dedicated public/member views come later."

## Clarifications

### Session 2026-07-02

- Q: Who can open a team's page this iteration? → A: Team **name, type, city, and recent activity are public** information; the **roster, news, and management are team-internal** (members-only). Non-members are blocked from the internal page and there is **no non-member preview** of internal content. The invite-accept landing shows only the public team fields plus who invited the recipient.
- Q: How does a searched (targeted) invitee receive their invite this iteration? → A: **Email link only** — a transactional email containing the accept link (reusing the existing Mailpit/Resend email infrastructure). No in-app invitations list this iteration; a general in-app notification system (reusable for team/event notifications) is captured as a separate Backlog.md item.
- Q: How many teams can one user belong to at once? → A: **Unlimited, with no per-type limit** — a user may belong to any number of teams at once, including **0–unlimited city teams and 0–unlimited Mixteams** in any combination.
- Q: On team deletion, what happens to historical event participations? → A: **Preserve event history** — Event/participation records are kept; the deleted team's attribution becomes blank/"former team". Deleting a team never erases a player's profile activity.
- Q: Unique team names, or duplicate names with a unique slug? → A: **Duplicate display names are allowed**; identity is a unique, **immutable, creator-chosen slug** (`/t/<slug>`) with a live availability check, behaving exactly like the profile `@handle` (restricted charset, reserved words, race-safe uniqueness). The slug is set on the create form (adds a "team address" field vs the original wireframe).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create a team and become its admin (Priority: P1)

Any signed-in player can start a team with a short form: a team **name**, a unique **team address (slug)** — its permanent `/t/<slug>` handle, just like a player's `@handle` — and a team type: a **city team** (which has a home city) or a **Mixteam** (players from different cities who don't train together but enter tournaments as one crew, so no city). The city field appears only for a city team, and the team address is checked for availability live. Whoever creates the team becomes its first admin and lands on the new team's page.

**Why this priority**: A team is the root object of the whole feature — the roster, invitations, roles, activity, and news all hang off it. Nothing else can be demonstrated until a team can be created, so this must land first.

**Independent Test**: Sign in, open "create a team", submit a valid name and available slug as a city team (with a city) and again as a Mixteam (no city), and confirm each team is created, the creator is recorded as its sole admin, and the creator is taken to the team page; confirm a taken/invalid slug is rejected.

**Acceptance Scenarios**:

1. **Given** a signed-in user on the create-team form, **When** they submit a valid name with type "city team" and a city, **Then** the team is created with that city and the creator becomes its first (and only) admin.
2. **Given** a signed-in user on the create-team form, **When** they choose "Mixteam", **Then** the city field is hidden and not required, and the team is created with no city.
3. **Given** the create-team form, **When** the name is blank or exceeds the allowed length (or a city team is submitted with no city), **Then** creation is refused server-side with a clear message and no team is created.
4. **Given** a team was just created, **When** the creator views it, **Then** they see themselves on the roster tagged as admin and see the admin-only controls (invite, manage, settings).
5. **Given** an unauthenticated visitor, **When** they attempt to create a team, **Then** the action is refused and they are directed to sign in.
6. **Given** the create form, **When** the user submits a team address (slug) that is already taken, malformed, or a reserved word, **Then** it is rejected server-side with guidance, and availability/format is indicated live before submission.
7. **Given** an existing team, **When** any attempt is made to change its slug, **Then** the change is refused (there is no supported path to mutate it), while its display name is not required to be unique.

---

### User Story 2 - Visit the team space (members, activity, news) (Priority: P1)

A team member opens the team page and sees a cover header (team name, city, member count) and underline tabs: **Members**, **Activity**, **News**, and a greyed-out **Trainings** tab (coming later). The Members tab is a flat roster where each row shows the member's name, their pompfen/positions (from their player profile), and a role tag (admin or member). The Activity tab lists the events the team has taken part in (month, event name, location — no scores), like the activity section on a player profile. The News tab is a read-only feed of team updates (author, role, when, body).

**Why this priority**: The "space" itself is the headline value — once a team exists and can be viewed, the feature is demonstrable end to end for its owner. Reading the roster is also a precondition for every management action.

**Independent Test**: As a member of a seeded team, open each tab and confirm the roster shows every member with correct role tags and positions, the activity list shows the team's seeded events newest-first, the news feed shows seeded posts newest-first, and the Trainings tab is visibly disabled.

**Acceptance Scenarios**:

1. **Given** a member on the team page, **When** the Members tab is shown, **Then** every current member appears once with their name, profile positions, and a role tag of admin or member.
2. **Given** a team with recorded event participation, **When** the Activity tab is shown, **Then** the events the team played appear newest-first, each with month/date, event name, and location, bounded to a recent window.
3. **Given** a team with no event participation, **When** the Activity tab is shown, **Then** a friendly empty state is shown instead of an error.
4. **Given** a team with news posts, **When** the News tab is shown, **Then** posts appear newest-first with author name, the author's role, a relative time, and the body; admins additionally see a "+ Post" affordance (composer deferred).
5. **Given** any team page, **When** it is rendered, **Then** the Trainings tab is present but visibly disabled, and the page is responsive and legible on phone and desktop per DESIGN.md.
6. **Given** a user who is not a member of the team, **When** they attempt to open the internal team page, **Then** they are blocked with a friendly not-found and receive no roster or news data — only the team's public fields (name, type, city, member count, recent activity) are ever exposed outside the membership.

---

### User Story 3 - Invite people by link or by searching users (Priority: P2)

An admin opens the Invitations screen from the Members tab. They can copy the team's single active **invite link** (which anyone can use to join and which expires after 7 days) and share it in their own tools, or — as a backup — search existing users by name and invite them **directly**. The screen lists all pending invitations (the link plus targeted invites), each showing how long until it expires, and lets the admin **revoke** any of them before they're accepted. A user who already has a pending invite is shown as "invited".

**Why this priority**: Growing the roster is the point of a team, but a team is still viable for its creator without invitations, so this comes after the space itself. It pairs with Story 4 (accept/decline) to complete the join loop.

**Independent Test**: As an admin, open Invitations, copy the invite link and confirm its form and 7-day expiry; search for a user and send a direct invite; confirm both appear in the pending list with expiry, that re-inviting the same user is prevented/shown as already invited, and that revoking removes a pending invite.

**Acceptance Scenarios**:

1. **Given** an admin on the Invitations screen, **When** they view the invite link, **Then** a single active link is shown with its remaining validity (7-day lifetime) and a copy affordance.
2. **Given** an admin, **When** they revoke the active invite link and generate a new one, **Then** the old link no longer admits anyone and only the new link is active.
3. **Given** an admin searching users by name, **When** they invite a matching user, **Then** a targeted invitation is created with a 7-day expiry, a transactional email with the accept link is sent to that user, and the invite appears in the pending list.
4. **Given** a user who already has a pending targeted invite to this team, **When** the admin views them in search, **Then** they are shown as "invited" and a duplicate invite is not created.
5. **Given** a pending invitation (link or targeted), **When** the admin revokes it, **Then** it is cancelled and can no longer be accepted.
6. **Given** a non-admin member (or a non-member), **When** they attempt any invite, revoke, or user-search action for the team, **Then** it is refused server-side regardless of client state.

---

### User Story 4 - Accept or decline an invitation (Priority: P2)

A person who opens an invitation (via the shared link or a direct invite) sees a preview of the team — logo, name, city, member count, and who invited them — and can **Accept & join** or **Decline**. Accepting adds them to the roster as a regular member. A link that has aged past its 7-day life shows an "invite has expired" state that tells them to ask a team admin for a fresh one.

**Why this priority**: This is the other half of the join loop; without it invitations are inert. It depends on Story 3 producing invitations and Story 1 producing a team to join.

**Independent Test**: Open a valid invite as a signed-in user and confirm the public team info renders and "Accept & join" adds you to the roster as a member; open a declined/expired/revoked invite and confirm the appropriate terminal state with no way to join.

**Acceptance Scenarios**:

1. **Given** a valid, unexpired invitation, **When** the invitee opens it (via the emailed or shared link), **Then** they see the team's public info (name, city, member count, inviter) with clear accept and decline actions — and no roster or news.
2. **Given** a valid invitation, **When** the invitee accepts, **Then** they are added to the team as a regular member, the member count reflects them, and the invitation is marked accepted (and cannot be reused).
3. **Given** a valid invitation, **When** the invitee declines, **Then** they are not added and the invitation is marked declined.
4. **Given** an invitation link older than 7 days (or a revoked/already-used one), **When** it is opened, **Then** an expired/invalid state is shown with guidance to request a fresh invite, and no join occurs.
5. **Given** an invitee who is already a member of the team, **When** they open an invite for it, **Then** they are not added twice and are shown they already belong.
6. **Given** an unauthenticated visitor opening an invite link, **When** they proceed, **Then** they are asked to sign in or register first and, on return, land back on the accept screen for that invitation.

---

### User Story 5 - Manage roles and remove members, with a last-admin guard (Priority: P2)

From a member's "…" menu an admin can promote a member to admin or demote an admin back to member, and can remove a member from the team. From team settings an admin can step down to member. The system enforces a hard guardrail: **at least one admin must always remain** — the last admin cannot be demoted, removed, or stepped down in a way that would leave the team without any admin.

**Why this priority**: Role management is essential for a real team but the team functions with its founding admin alone, so it follows the create/view/join slices. The last-admin guard is a correctness/safety requirement that must be enforced server-side.

**Independent Test**: As an admin of a team with two admins, demote one and confirm the role tag changes; as the sole admin, attempt to demote/remove/step-down yourself and confirm every path is blocked with a clear reason; remove a member and confirm they leave the roster.

**Acceptance Scenarios**:

1. **Given** an admin viewing another member, **When** they choose "make admin", **Then** that member becomes an admin and the control now offers "remove admin".
2. **Given** an admin viewing another admin, **When** they choose "remove admin", **Then** that member is demoted to member (provided at least one admin remains).
3. **Given** an admin viewing a member, **When** they remove them, **Then** the member is taken off the roster and loses access to member-only team surfaces.
4. **Given** the only admin of a team, **When** they try to demote themselves, remove themselves, or step down to member, **Then** the action is blocked server-side with a message to appoint another admin first.
5. **Given** a non-admin, **When** they attempt any role change or removal, **Then** it is refused server-side.
6. **Given** an admin who is not the last admin, **When** they step down to member from settings, **Then** they become a regular member and the team still has at least one admin.

---

### User Story 6 - Delete a team (Priority: P3)

From team settings, an admin can delete the team in a clearly marked danger zone. Deletion is irreversible and removes the team along with its news and roster for everyone. Only admins can do it.

**Why this priority**: Destructive lifecycle management is important but least frequently used and not required for the core loop, so it is the final slice.

**Independent Test**: As an admin, delete a team and confirm it (and its roster, invitations, and news) are gone and can no longer be opened; as a non-admin, confirm the delete action is unavailable and refused server-side.

**Acceptance Scenarios**:

1. **Given** an admin in team settings, **When** they confirm delete, **Then** the team, its roster, its pending invitations, and its news are removed and the team page is no longer reachable.
2. **Given** a non-admin member, **When** they view settings, **Then** no delete affordance is offered and a direct delete attempt is refused server-side.
3. **Given** a deleted team, **When** anyone opens a former link or invite for it, **Then** a friendly not-found/expired state is shown with no leakage of internal detail.
4. **Given** a team whose members played events, **When** the team is deleted, **Then** the historical event records themselves are not destroyed (only the team, its roster, invites, and news are removed) — see Assumptions.

---

### Edge Cases

- **Duplicate team names**: Two teams may legitimately share a display name (e.g. across cities); the **slug** (its `/t/<slug>` address and the `<slug>` in invite links) is still unique and is chosen once at creation.
- **Slug collision / reserved slug**: Two creators requesting the same free slug — exactly one wins and the other is rejected cleanly; route/system words (e.g. `new`, `join`, `settings`, `t`) cannot be claimed as a slug. Team slugs and profile handles are separate namespaces (`/t/…` vs `/u/…`), so a team slug may coincide with a user handle.
- **Invite link rotation**: Revoking/regenerating the link must immediately invalidate the previous link; only one link is ever active per team.
- **Invite to an existing member**: Inviting (by search or link) someone already on the roster must not create a duplicate membership.
- **Simultaneous accepts of one link**: The shared link may be opened by many people; each distinct accepting user joins exactly once, with no duplicate rows under concurrency.
- **Accepting after being removed/revoked**: An invitation revoked, expired, or already used cannot be accepted; the terminal state is shown.
- **Last-admin race**: Two admins demoting each other at the same time must not be able to drive the team to zero admins; the guard is enforced atomically server-side.
- **Non-member direct API access**: Reading a roster/invitations/news, inviting, managing roles, or deleting via direct API by a non-member/non-admin is refused regardless of client state (never trust the client).
- **Self-removal vs leave**: An admin stepping down, and a member leaving, are subject to the same last-admin guard when the actor is an admin.
- **Empty states**: A brand-new team (only its founder, no events, no news) shows friendly empty states on Activity and News, not errors.
- **Deleted inviter/account**: An invitation whose inviter later leaves or is removed still shows a sensible preview (team-centric), not a broken one.
- **Position display**: A member with no pompfen selected on their profile shows no position sub-label rather than a placeholder implying data.

## Requirements *(mandatory)*

### Functional Requirements

#### Team creation & identity

- **FR-001**: Any authenticated user MUST be able to create a team by providing a team name and selecting a team type of either **city team** or **Mixteam**.
- **FR-002**: When the type is **city team**, the system MUST require a city; when the type is **Mixteam**, the system MUST NOT require or store a city and MUST hide the city input.
- **FR-003**: The team name MUST be validated server-side against published length limits; blank or over-limit names MUST be rejected without creating a team.
- **FR-004**: On creation, the creating user MUST be recorded as the team's first member with the **admin** role.
- **FR-005**: Each team MUST have a unique, URL-safe, **immutable slug** chosen by the creator at creation (its permanent `/t/<slug>` address and the `<slug>` in invite links), exactly like the profile handle: validated server-side against a restricted character set and a reserved-words list, rejected race-safely if already taken, and never changeable through any supported path afterward.
- **FR-006**: Team display **names** need NOT be globally unique and are not part of a team's identity (the slug is); the create form MUST indicate slug availability and format validity live before submission, without relying on that check as the security boundary.

#### Team space (page & tabs)

- **FR-007**: The team page MUST present a cover header (team name, city if any, and current member count) and tabs for Members, Activity, News, and a disabled Trainings tab.
- **FR-008**: The Members tab MUST show a flat roster listing every current member once, each with their display name, their selected pompfen/positions (sourced from their player profile), and a role tag of admin or member.
- **FR-009**: The roster MUST be paginated (bounded), never returned as an unbounded list.
- **FR-010**: The Activity tab MUST show the events the team has participated in, newest-first, each with month/date, event name, and location, bounded to a recent window, with a friendly empty state when there are none.
- **FR-011**: The News tab MUST show team news posts newest-first, each with author name, the author's team role, a relative time, and the post body, with a friendly empty state when there are none. Creating/editing/deleting posts (the composer) is out of scope this iteration; the feed is read-only and admins see a disabled/deferred "+ Post" affordance.
- **FR-012**: Admin-only controls (invite, manage member, team settings/delete, post) MUST be shown only to admins and MUST be authorized server-side, never on the basis of client state.

#### Membership & roles

- **FR-013**: A team MUST support exactly two roles: **admin** and **member**.
- **FR-014**: An admin MUST be able to promote a member to admin and demote an admin to member.
- **FR-015**: An admin MUST be able to remove a member from the team.
- **FR-016**: An admin MUST be able to step down to member from team settings.
- **FR-017**: The system MUST guarantee that **at least one admin always remains**: any demotion, removal, step-down, or leave that would leave the team with zero admins MUST be blocked server-side with a clear reason, enforced atomically under concurrency.
- **FR-018**: A user MAY belong to any number of teams simultaneously (unlimited), in any combination of types — **0 or more city teams and 0 or more Mixteams**, with no cap per type or overall; membership and role are tracked per-team.
- **FR-019**: All role and membership mutations MUST be authorized server-side (admin-only) and refused for non-admins and non-members regardless of client state.

#### Invitations

- **FR-020**: Only admins MUST be able to create, view, and revoke invitations and to search users to invite.
- **FR-021**: Each team MUST have at most one **active invite link** at a time; the link MUST expire 7 days after issuance and MUST be revocable, and revoking or regenerating it MUST immediately invalidate the prior link.
- **FR-022**: Anyone presenting a valid, unexpired, unrevoked invite link MUST be able to accept it and join as a regular member.
- **FR-023**: An admin MUST be able to search existing users by name and create a **targeted invitation** for a specific user; the targeted invitation MUST be delivered to that user as a transactional email containing the accept link (reusing the existing email infrastructure), MUST expire 7 days after issuance, and MUST be revocable before acceptance.
- **FR-024**: The system MUST NOT create a duplicate pending targeted invitation for a user who already has one for the team, and MUST surface such a user as already "invited".
- **FR-025**: The Invitations screen MUST list all pending invitations (the link and targeted invites) with their remaining validity, and revoking one MUST prevent its future acceptance.
- **FR-026**: The system MUST NOT allow an invitation to add a user who is already a member (no duplicate membership).

#### Accept / decline

- **FR-027**: Opening a valid invitation MUST present the team's **public** info only — team name, city (if any), current member count, and who invited them — with distinct accept and decline actions, and MUST NOT expose the roster or news (there is no non-member preview of internal content).
- **FR-028**: Accepting a valid invitation MUST add the accepting user to the team as a **member**, reflect them in the member count, and mark the invitation used so it cannot be reused (for a targeted invite) — a shared link remains usable by other distinct users until it expires or is revoked.
- **FR-029**: Declining an invitation MUST NOT add the user and MUST record the invitation as declined.
- **FR-030**: Opening an expired, revoked, or already-consumed invitation MUST show a friendly terminal state (e.g. "this invite has expired") with guidance to request a fresh one and no path to join.
- **FR-031**: Accepting or declining MUST require authentication; an unauthenticated visitor MUST be able to sign in or register and return to the same invitation to act on it.

#### Team deletion

- **FR-032**: Only admins MUST be able to delete a team; deletion MUST be authorized server-side and refused for everyone else.
- **FR-033**: Deleting a team MUST remove the team, its roster (memberships), its pending invitations, and its news; the action is irreversible and MUST be clearly confirmed before it proceeds.
- **FR-034**: After deletion, the team page and any former invite links MUST resolve to a friendly not-found/expired state without leaking internal detail.

#### Activity model (team-side)

- **FR-035**: Team activity MUST be derived from real event-participation records, reusing the existing minimal events model; the participation record's current lightweight team **label** MUST be replaced by a real team **reference** so activity can be attributed to the actual team without changing the shape of the activity item shown to users.
- **FR-036**: Deleting a team MUST NOT delete the underlying historical event records; only team-owned data (membership, invites, news, and the team itself) is removed (see Assumptions).

#### Team visibility & data classification

- **FR-040**: The team's **internal** surfaces — the member roster (member identities and roles), the news feed, and all management controls — MUST be restricted to team members and authorized server-side; a non-member MUST receive a friendly not-found/blocked response and MUST NOT be sent any roster or news data.
- **FR-041**: A team's **public**, non-sensitive fields — display name, type, city, member count, and recent event activity — MAY be exposed outside the membership (e.g. on the invite-accept landing and any future public team view); member identities/roles and news MUST NOT be exposed publicly.
- **FR-042**: There MUST be no non-member "preview" of internal team content; opening an invitation shows only the public team fields plus who invited the recipient, alongside accept/decline.

#### Cross-cutting

- **FR-037**: Every authorization decision in this feature MUST be enforced server-side; client-side gating exists only for UX and is never the security boundary.
- **FR-038**: Every list surface (roster, invitations, activity, news, user search results) MUST be paginated/bounded.
- **FR-039**: All screens MUST be responsive and legible on phone and desktop per DESIGN.md, including empty, loading, and error states, and MUST NOT leak raw exceptions or sensitive data to the client.

### Key Entities *(include if feature involves data)*

- **Team**: A group players belong to. Attributes: display name (free text, need not be unique), type (city team or Mixteam), optional city (only for city teams), a unique **immutable, creator-chosen slug** (its `/t/<slug>` address, exactly like the profile handle — restricted charset, reserved words excluded), and (deferred) logo/cover. Has many memberships, invitations, and news posts.
- **Team Membership**: A link between a team and a user carrying that user's role in the team (admin or member) and when they joined. A user has at most one membership per team; a team always has ≥ 1 admin membership.
- **Team Invitation**: A pending or resolved invitation to join a team. Distinguishes a shared **link** invite (reusable by distinct users until expiry/revocation) from a **targeted** invite (bound to one user). Attributes: kind, opaque token, target user (targeted only), issuing admin, issued time, 7-day expiry, and status (pending / accepted / declined / revoked / expired). A targeted invite is delivered to its user by transactional email.
- **Team News Post**: A team update shown in the read-only News feed. Attributes: author (a member), body, and time. Authored/edited through a composer only in a later iteration.
- **Event / Event Participation (existing, extended)**: The existing minimal events model that backs both profile and team activity. The participation's lightweight team label becomes a real reference to a Team so activity is attributed to the actual team; the user-facing activity item (event name, date, location, team) is unchanged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A signed-in user can create a team (city team or Mixteam) in a single short form in under 1 minute and is immediately its admin on the team page.
- **SC-002**: 100% of admin-only actions (invite, revoke, role change, remove, delete, post) are refused for non-admins and non-members when attempted directly, independent of the client UI.
- **SC-003**: An admin can invite a person by link or by search, and that person can open the invite, see the public team info, and join as a member — completing the invite→accept loop end to end.
- **SC-004**: 100% of invitations become unusable exactly 7 days after issuance (or immediately upon revoke), and expired/revoked invites never add anyone.
- **SC-005**: In 100% of attempts, the team cannot be driven below one admin — the last admin can never be demoted, removed, or stepped down.
- **SC-006**: An admin can toggle any member between admin and member and remove any member, with the roster reflecting the change immediately.
- **SC-007**: Deleting a team removes its roster, invitations, and news and makes the team and its links unreachable, in 100% of cases, while historical event records remain intact.
- **SC-008**: The team page's Members, Activity, and News tabs render correct, bounded data with friendly empty states, and are usable on phone and desktop including loading and error states.
- **SC-009**: A team slug, once set, cannot be changed through any supported path, and duplicate-slug creations are rejected 100% of the time; team display names may duplicate freely (creation never blocks on a duplicate name).

## Assumptions

The following reasonable defaults were chosen where the description left a detail open. The four genuine product decisions were locked via `/speckit-clarify` (see `## Clarifications`, Session 2026-07-02) and are marked *(resolved in clarification)* below.

- **Multiple team membership** *(resolved in clarification)* — a user may belong to any number of teams at once (unlimited), including **0–unlimited city teams and 0–unlimited Mixteams** in any combination; there is no per-user or per-type team cap.
- **Team-page visibility** *(resolved in clarification)* — the team's **name, type, city, member count, and recent activity are public** (non-sensitive) information; the **roster, news, and management are team-internal** and restricted to members. Non-members are blocked from the internal page and there is **no non-member preview** of internal content — the invite-accept landing shows only the public fields plus the inviter. A dedicated browsable public team page and team discovery/search remain deferred; the public fields are ready for them.
- **Targeted-invite delivery** *(resolved in clarification)* — targeted invites are delivered by **transactional email** containing the accept link (reusing the existing Mailpit/Resend infrastructure from feature 002). No in-app "your invitations" list is built this iteration; a general **in-app notification system** (usable for team/event notifications including invites) is captured as a separate Backlog.md item for a later iteration.
- **Team identity (name vs slug)** *(resolved in clarification)* — display names are not required to be globally unique and are not part of identity; each team has a unique, **creator-chosen**, immutable, URL-safe **slug** (its `/t/<slug>` address) with a live availability check, validated server-side against a restricted charset and a reserved-words list — exactly the profile-handle model, reusing that machinery. Team slugs and profile handles are **separate namespaces** (`/t/…` vs `/u/…`), so a team slug may coincide with a user handle. A team-rename affordance is deferred, but the model does not make the display name immutable.
- **Who can invite** — only admins can invite, revoke, and search users; regular members cannot.
- **City input** — the city is short free text (mirroring the existing free-text profile hometown), not a controlled list, this iteration.
- **Team name bounds** — a sensible short length limit (on the order of the profile display-name limit, ~2–50 characters); exact bounds finalized in planning.
- **No hard member cap** — there is no maximum roster size this iteration, but every roster read is paginated.
- **News posts are seeded** — because the composer is deferred, news posts exist via local/dev seed data only; the News tab renders them read-only.
- **Invite link semantics** — exactly one active link per team; regenerating replaces the previous. The link is a bearer credential (anyone with it can join as a member), so it carries an unguessable token and a 7-day expiry, and can be revoked.
- **Event history preserved on delete** *(resolved in clarification)* — deleting a team removes team-owned data (membership, invitations, news, the team) but does **not** delete shared historical `Event`/participation records; participations that referenced the deleted team keep their historical event data, with team attribution becoming blank/"former team".
- **Authentication/session reuse** — the existing auth, session, and account model from features 002–004 are reused unchanged; accepting an invite requires a signed-in account, and registration/sign-in redirect back to the pending invite.
- **Reuse of profile data** — member position sub-labels come from each user's existing player-profile pompfen selection (feature 003); no team-specific position data is stored.
- **Out of scope this iteration**: the Trainings tab and public training discovery; polls; the news composer (create/edit/delete); a dedicated browsable **public team page** and team discovery/search (the public fields above are still exposed on the accept landing and are ready for such a page); team logo/cover image upload; event creation/management UI; and any **in-app notification system** (captured as a separate Backlog.md item) — targeted invites use email this iteration.
