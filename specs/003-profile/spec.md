# Feature Specification: Player Profile & Public Share Link

**Feature Branch**: `003-profile`

**Created**: 2026-07-01

**Status**: Draft

**Input**: User description: "User profile feature for JuggerHub. Every authenticated user has an editable profile and a public, shareable, unauthenticated profile page reached by a short PayPal.me-style URL (/u/<slug>). Profile fields: display name, profile picture, short description/bio, hometown, favorite pompfen (multi-select from the canonical Jugger set plus Läufer/Runner). Username/slug chosen at registration, unique, immutable. Recent activity backed by a minimal real events + participation model. Teams and Badges are UI-only stub sections. Public view never exposes email or sensitive data."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Claim an immutable handle at registration (Priority: P1)

A person signing up chooses a personal handle (username/slug) in addition to email and password. The handle is unique across all players, URL-safe, and permanent — it becomes the address of their public profile (`/u/<handle>`) and the `@handle` shown on every profile. Because it can never change, shared links never break.

**Why this priority**: The handle is the foundation of the whole feature — the public URL, the profile identity, and the share affordance all depend on it. Without it there is no addressable profile. It also modifies the existing registration flow, so it must land first.

**Independent Test**: Register a new account supplying a handle; confirm the account is created only when the handle is valid and unused, and that the handle is rejected when it collides with an existing one or contains disallowed characters. Confirm the handle cannot be altered afterward.

**Acceptance Scenarios**:

1. **Given** a visitor on the registration screen, **When** they submit a valid, unused handle with valid email/password, **Then** the account is created and the handle is permanently associated with it.
2. **Given** a handle already taken by another player, **When** a visitor tries to register with it, **Then** registration is rejected with a clear "handle unavailable" message and no account is created.
3. **Given** a handle containing spaces, uppercase, or symbols outside the allowed set, **When** the visitor submits it, **Then** it is rejected with guidance on the allowed format, enforced server-side.
4. **Given** an existing account, **When** any attempt is made to change its handle, **Then** the change is refused (no supported path to mutate it).
5. **Given** a visitor typing a handle, **When** they pause, **Then** availability is indicated live before submission (UX aid only; uniqueness is still enforced server-side).

---

### User Story 2 - View and share a public profile (Priority: P1)

Anyone with the link (`/u/<handle>`) can view a player's public profile without signing in. It shows the player's display name, `@handle`, hometown, profile picture, short description, their selected pompfen, and their recent activity (events with the team each was played for). It never shows email or any account/sensitive data. The link is easy to copy.

**Why this priority**: The public, no-login share page is the headline value of the feature — it is what players hand to teammates, tournaments, and social media. It is independently demonstrable as soon as a profile exists.

**Independent Test**: Open `/u/<handle>` in a signed-out session and confirm the public fields render, the email and account fields are absent from the response and page, and an unknown handle yields a friendly not-found page. Confirm the copy-link affordance yields the correct short URL.

**Acceptance Scenarios**:

1. **Given** a signed-out visitor, **When** they open `/u/<existing-handle>`, **Then** the public profile renders with display name, `@handle`, hometown, picture, description, selected pompfen, and recent activity — and no email or account data anywhere in the page or its underlying data.
2. **Given** a signed-out visitor, **When** they open `/u/<unknown-handle>`, **Then** a friendly "profile not found" state is shown (no error leakage, no distinction that reveals account internals).
3. **Given** a viewer on a public profile, **When** they use the copy-link affordance, **Then** the short `/u/<handle>` URL is placed on their clipboard.
4. **Given** a player who has selected only some pompfen, **When** their public profile is viewed, **Then** only the selected pompfen are shown (never the unselected/available ones).
5. **Given** a public profile viewed on a phone and on a desktop, **When** rendered, **Then** the layout is responsive and legible at both sizes per DESIGN.md.

---

### User Story 3 - Edit my own profile (Priority: P2)

A signed-in player opens their own profile in an editable mode and sets their display name, uploads/replaces a profile picture, writes a short description, sets their hometown, and picks their favorite pompfen from the full set (selected shown filled, the rest available). Changes are saved and immediately reflected on their public profile.

**Why this priority**: Editing is what makes the public profile meaningful, but a profile with default/empty values is already viewable and shareable (US2), so this is P2 rather than P1.

**Independent Test**: As a signed-in owner, change each field and the pompfen selection, save, then reload the owner view and the public view and confirm both reflect the changes. Confirm validation limits (length, image type/size) are enforced server-side.

**Acceptance Scenarios**:

1. **Given** the signed-in owner, **When** they open their profile, **Then** an Edit affordance and the full pompfen selector (selected + available) are shown.
2. **Given** the owner in edit mode, **When** they change display name, description, or hometown within allowed limits and save, **Then** the values persist and appear on both the owner and public views.
3. **Given** the owner in edit mode, **When** they upload a picture of an allowed type within the size limit, **Then** it replaces the previous picture on both views; an invalid file is rejected with a clear message.
4. **Given** the owner in edit mode, **When** they toggle pompfen selections (multiple allowed, including Läufer) and save, **Then** exactly those selections are stored and shown as selected.
5. **Given** a non-owner signed-in user, **When** they view someone else's profile, **Then** no Edit affordance is available and edit actions are refused server-side.

---

### User Story 4 - See genuine recent activity (Priority: P3)

A player's profile lists the events they have taken part in — each showing the event name, its date/month, its location, and which team it was played with — newest first and capped to a recent window. The data is real (backed by event participation records), not placeholder.

**Why this priority**: Activity enriches the profile and is explicitly wanted, but the profile is viewable and editable without it, so it is the last slice. It also introduces the minimal events model, which other features will later build on.

**Independent Test**: With seeded event-participation records for a player, load their profile and confirm the activity list shows the correct events with team attribution, ordered newest-first and capped; with no participation, confirm a friendly empty state.

**Acceptance Scenarios**:

1. **Given** a player with several event participations, **When** their profile is viewed, **Then** recent activity lists those events newest-first, each with name, date/month, location, and the associated team, capped to the recent window.
2. **Given** a player with no participations, **When** their profile is viewed, **Then** the activity section shows a friendly empty state.
3. **Given** more participations than the display cap, **When** the profile is viewed, **Then** only the most recent up to the cap are returned (bounded, paginated response).
4. **Given** the public view, **When** activity is shown, **Then** it exposes only event and team display information — no owner-private or account data.

---

### Edge Cases

- **Handle reuse after deletion**: If an account is ever deleted, is its handle released or permanently retired? Assumption: retired (never reissued) to keep old links unambiguous — see Assumptions.
- **Reserved handles**: System/route words (e.g. `admin`, `api`, `u`, `login`, `settings`) must not be claimable as handles.
- **Handle collision race**: Two simultaneous registrations requesting the same free handle — exactly one must win; the other is rejected cleanly.
- **Empty/default profile**: A brand-new player with no picture/description/pompfen still has a valid, viewable public profile (placeholders/empty states, not errors).
- **Oversized or wrong-type image upload**: Rejected server-side with a clear message; the existing picture is unchanged.
- **Overlong free-text**: Display name, description, and hometown that exceed limits are rejected server-side (client also guides).
- **No pompfen selected**: Valid state; public "Plays" section shows an empty state rather than the full available set.
- **Unicode/confusable handles**: Handles are normalized to a restricted, unambiguous character set to avoid impersonation via look-alikes.
- **Direct API access to a non-owner's edit endpoint**: Refused server-side regardless of client state (never trust the client).
- **Stub sections**: Teams and Badges render as empty/placeholder sections and must not imply data that does not exist.

## Requirements *(mandatory)*

### Functional Requirements

#### Handle / Username (extends registration)

- **FR-001**: The registration flow MUST require the user to choose a handle (username/slug) alongside the existing credentials.
- **FR-002**: A handle MUST be unique across all accounts; the system MUST reject a registration whose handle is already in use, enforced server-side and race-safe.
- **FR-003**: A handle MUST conform to a URL-safe format (a restricted lowercase alphanumeric-plus-separator character set with bounded length); non-conforming handles MUST be rejected server-side.
- **FR-004**: A handle MUST be immutable after account creation — the system MUST provide no supported way for a user to change it.
- **FR-005**: The system MUST reserve a set of route/system words so they cannot be claimed as handles.
- **FR-006**: The registration UI MUST indicate handle availability and format validity live before submission, without relying on that check as the security boundary.

#### Profile data & editing

- **FR-007**: Each account MUST have exactly one profile, addressable by its handle.
- **FR-008**: The profile owner MUST be able to set and update their display name, short description, and hometown, each within published length limits enforced server-side.
- **FR-009**: The owner MUST be able to upload and replace a profile picture; the system MUST validate image type and size server-side and reject invalid uploads without altering the existing picture.
- **FR-010**: The owner MUST be able to select zero or more favorite pompfen from the canonical set — Stab/Staff, Langpompfe/Long, Schild/Shield, Q-Tip, Kette/Chain, Doppel-Kurz/Double-Short — plus the Läufer/Runner position, and the selection MUST persist.
- **FR-011**: Only the authenticated owner MUST be able to edit their own profile; all edit operations MUST be authorized server-side and refused for non-owners regardless of client state.
- **FR-012**: The owner (editable) view MUST present the full pompfen set distinguishing selected from available; a new/empty profile MUST remain valid and viewable.

#### Public profile & sharing

- **FR-013**: The system MUST expose a public profile at a short URL of the form `/u/<handle>` that is viewable without authentication.
- **FR-014**: The public profile MUST include only non-sensitive fields: display name, `@handle`, hometown, profile picture, short description, selected pompfen, recent activity (event + team), and the Teams/Badges stub sections.
- **FR-015**: The public profile MUST NOT expose email, account status, security data, or any owner-private field — neither in the rendered page nor in the data delivered to the client.
- **FR-016**: The public "Plays" section MUST show only the player's selected pompfen, never the unselected/available ones.
- **FR-017**: Requesting an unknown handle MUST return a friendly not-found result that does not leak system internals.
- **FR-018**: The profile view MUST offer a copy-link affordance that yields the canonical `/u/<handle>` short URL.

#### Recent activity & minimal events model

- **FR-019**: The system MUST persist a minimal model of events and player participation sufficient to derive recent activity: an event with a name, date, and location, and a participation linking a player to an event with an associated team reference.
- **FR-020**: A profile MUST show the player's recent activity newest-first, each item showing event name, date/month, location, and the associated team, capped to a recent window.
- **FR-021**: The activity list MUST be bounded (paginated/capped) — never an unbounded collection.
- **FR-022**: When a player has no participation, the activity section MUST show a friendly empty state.

#### Stub sections

- **FR-023**: The profile MUST include Teams and Badges sections as placeholder/empty-state UI only, with no backing data model in this feature, and MUST NOT imply data that does not exist.

#### Cross-cutting

- **FR-024**: Profile and activity responses MUST be shaped so that public responses carry strictly the public field set (sensitive fields stripped at the response boundary, not merely hidden in the UI).
- **FR-025**: The feature MUST be responsive and legible on phone and desktop viewports per DESIGN.md, including empty, loading, and error states.

### Key Entities *(include if feature involves data)*

- **Player Profile**: The public-facing identity for an account. Attributes: display name, immutable handle, hometown, short description, profile picture reference, and the set of selected pompfen/position. One-to-one with an account.
- **Handle**: The unique, immutable, URL-safe identifier that addresses a profile (`@handle`, `/u/<handle>`). Owned by exactly one account; drawn from a restricted character set; excludes reserved words.
- **Pompfe Selection**: The set of favorite pompfen/position a profile has chosen, drawn from the fixed canonical catalog (six pompfen + the Läufer position). Zero or more per profile.
- **Event**: A minimal record of a Jugger event: name, date, location. Foundation for later event features; not managed through UI in this feature.
- **Event Participation**: A link recording that a player took part in an event with a particular team; the basis for recent activity. Bounded per profile in display.
- **Team (reference only)**: Referenced as an attribution on participation and shown in stub sections; no owned team model is created in this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new user can complete registration including choosing a valid, available handle in a single pass, with availability feedback shown before they submit.
- **SC-002**: 100% of public profile responses for signed-out viewers contain zero email or account/sensitive fields (verified by inspecting the delivered data, not just the UI).
- **SC-003**: A signed-out visitor can open a shared `/u/<handle>` link and see the player's public profile without any sign-in step.
- **SC-004**: An owner can update every profile field (name, picture, description, hometown, pompfen) and see the change reflected on the public profile immediately after saving.
- **SC-005**: Attempts to edit a profile by anyone other than its authenticated owner are refused in 100% of cases, including direct API attempts.
- **SC-006**: A handle, once set, cannot be changed through any supported path; duplicate-handle registrations are rejected 100% of the time.
- **SC-007**: Recent activity displays the player's real event participations newest-first, correctly attributed to a team, and never returns an unbounded list.
- **SC-008**: The profile (public and owner) is usable and correct on both phone and desktop viewports, including empty, loading, and error states.

## Assumptions

- **Registration is the only place a handle is set.** Existing accounts created before this feature (if any in local/dev) will need a one-time backfill/migration of handles; production has no live users yet, so this is a local/dev concern. (Confirm during planning.)
- **Handles are retired, not reused**, if an account is ever deleted, to keep shared links unambiguous.
- **Handle format**: lowercase letters, digits, and a single separator (hyphen or underscore) between segments, with a sensible min/max length; normalized to avoid confusable/look-alike impersonation. Exact bounds finalized in planning.
- **Profile picture** is stored via the project's standard file/asset approach; a default placeholder avatar is shown when none is set. Concrete storage mechanism (DB, disk, object storage) is a planning decision, kept consistent with the constitution and environment parity.
- **Description and display-name limits** follow common social-profile norms (short bio, not long-form) with exact limits set in planning.
- **Teams and Badges** carry no data model this round; their sections exist purely to reserve layout and set expectations.
- **The minimal events model** is a foundation only: no event creation/editing/management UI, no join/leave flows, no notifications — those are explicitly out of scope and will be seeded (local/dev) or arrive with a future events feature.
- **Team attribution on activity** references a team by identifier/label; since no team model exists, this is stored as a lightweight reference/label sufficient to display "with <Team>" until a real teams feature lands.
- **Authentication/session, email, and password infrastructure** from feature 002 are reused unchanged except for the additive handle field in registration.
- **Out of scope**: real teams model, badges model/awarding, event management UI, following/friends, notifications, profile privacy levels beyond public/owner, and search/discovery of profiles.
