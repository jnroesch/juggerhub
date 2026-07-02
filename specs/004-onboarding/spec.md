# Feature Specification: First-Login Onboarding Flow

**Feature Branch**: `004-onboarding`

**Created**: 2026-07-01

**Status**: Draft

**Input**: User description: "First-login onboarding flow — a guided, skippable wizard that helps a new player complete their profile after they register, verify their email, and log in for the first time. One calm question per screen (Welcome → display name → city → pompfen → team → photo+bio → Done), mobile-first, with round-knob progress. Only the display name is required; every other step is skippable, and the whole flow can be dismissed with 'I'll do this later'. It appears once. Saves through the existing profile endpoints (feature 003); the Team step is a visual stub (no Teams model yet). Wireframe: wireframes/Onboarding Wireframes.html, 'Minimal centered' variant."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Be guided into onboarding once after first login (Priority: P1)

A person who has registered, verified their email, and signs in for the first time is taken straight into a guided onboarding flow instead of the normal app. The moment they finish the flow **or** dismiss it, the system remembers that onboarding is done: on every later sign-in they go directly to the app and are never automatically shown onboarding again.

**Why this priority**: This is the trigger and the gate — the reason the feature exists. Without a reliable "show it exactly once" mechanism, everything else is either never seen or shown repeatedly. It is the foundational slice: even a Welcome → Done flow with nothing in between is a viable, demonstrable MVP of "guided once, then out of the way."

**Independent Test**: Sign in as a freshly verified account that has never onboarded and confirm the onboarding flow appears. Complete or dismiss it, then sign out and back in and confirm the app opens directly with no onboarding. Confirm the "already onboarded" state is decided by the server, not by anything the client could forge.

**Acceptance Scenarios**:

1. **Given** a verified account that has not yet completed onboarding, **When** the user signs in, **Then** they are taken into the onboarding flow rather than the normal app landing.
2. **Given** a user who has just finished onboarding, **When** they sign out and sign in again, **Then** they land directly in the app and onboarding does not reappear.
3. **Given** a user who dismissed onboarding without completing any step, **When** they sign in again later, **Then** onboarding does not reappear (dismissal is permanent).
4. **Given** a returning session that is restored automatically (not a fresh sign-in), **When** the user has already onboarded, **Then** they are not redirected into onboarding.
5. **Given** a user who has not signed in, **When** they attempt to open the onboarding flow directly, **Then** they are sent to sign in first (the flow requires an authenticated user).

---

### User Story 2 - Complete my profile through guided steps (Priority: P1)

Inside the flow the player answers one calm question per screen: they confirm the name they want shown, optionally add their city, optionally pick the pompfen they play, optionally look for their team, and optionally add a photo and a short bio. When they finish, the answers they gave are saved to their profile and immediately visible on their own profile and their public share page.

**Why this priority**: Guided profile completion is the headline value — it turns empty default profiles into rich ones at the moment a player is most motivated. It is independently demonstrable: walk the steps, finish, and see a populated profile.

**Independent Test**: Enter the flow, set a display name, fill the optional steps, finish, then open the owner profile and the public page and confirm every entered value persisted. Confirm the entered picture and bio appear, and the selected pompfen match.

**Acceptance Scenarios**:

1. **Given** the display-name step, **When** the flow opens it, **Then** the field is pre-filled with the player's current display name (which defaults to their handle) and a hint reminds them their handle stays the same.
2. **Given** the display-name step, **When** the player submits an empty name, **Then** they cannot continue until a name is provided (this is the only required step).
3. **Given** the pompfen step, **When** the player selects one or more pompfen (including the Läufer position) and continues, **Then** exactly those selections are saved to their profile.
4. **Given** the photo + bio step, **When** the player adds a valid picture and a short bio and finishes, **Then** the picture and bio are saved and shown on their profile and public page.
5. **Given** a completed flow, **When** the player reaches the final screen and enters the app, **Then** every value they provided is already reflected on their owner profile and public profile.
6. **Given** any profile write during the flow, **When** it is submitted, **Then** it is applied only to the signed-in player's own profile, authorized server-side, and the immutable handle is never altered.

---

### User Story 3 - Skip any step or the whole flow (Priority: P2)

The player is never forced to fill anything beyond their name. Each optional step offers a quiet Skip that moves on without saving that field, and the Welcome screen offers "I'll do this later" that leaves the flow entirely. Whichever way they leave, onboarding is considered handled and won't nag them again; they can always finish their profile later from the normal profile screen.

**Why this priority**: Respecting the player's time is essential to a good first impression, but it builds on the flow existing (US2) and the gate (US1), so it is P2. It is independently testable by leaving the flow at various points.

**Independent Test**: From the Welcome screen choose "I'll do this later" and confirm the app opens and onboarding is marked done. Separately, enter the flow, skip every optional step, finish, and confirm no optional data was saved and onboarding is marked done.

**Acceptance Scenarios**:

1. **Given** the Welcome screen, **When** the player chooses "I'll do this later", **Then** they leave onboarding for the app and it is marked complete (won't auto-appear again).
2. **Given** an optional step (city, pompfen, team, photo+bio), **When** the player chooses Skip, **Then** the flow advances to the next step without saving that step's field.
3. **Given** a player who skipped every optional step but set a name, **When** they finish, **Then** only the name is saved and the rest of the profile keeps its defaults.
4. **Given** a player who leaves the flow by any exit, **When** they later open their profile screen normally, **Then** they can still set every field they skipped.

---

### User Story 4 - A calm, guided, responsive experience (Priority: P3)

The flow feels light and focused: one centered question per screen, a clear sense of progress, and easy movement backward and forward. A "Find your team" step is present so the flow matches the intended shape, shown as a clear placeholder until real teams exist. It reads and works well on a phone and on a desktop, with sensible loading and error handling.

**Why this priority**: Polish and the team placeholder make the flow feel complete and on-brand, but the feature delivers value without them, so this is the final slice. Independently testable by inspecting progress, navigation, the team placeholder, and behavior across viewports.

**Independent Test**: Walk the flow and confirm the progress indicator advances per step, Back returns to the previous step preserving entered values, the team step is clearly a not-yet-functional placeholder, and every screen is legible and usable on phone and desktop with graceful loading/error states.

**Acceptance Scenarios**:

1. **Given** any step after Welcome and before Done, **When** it is shown, **Then** a progress indicator communicates how far through the flow the player is, and a Back control returns to the previous step.
2. **Given** the player moves Back to an earlier step, **When** the step re-displays, **Then** the value they previously entered is still present.
3. **Given** the team step, **When** it is shown, **Then** it is presented as a clear placeholder for a future teams feature and any selection made there is not persisted.
4. **Given** the flow on a phone and on a desktop, **When** each screen renders, **Then** the layout is responsive and legible per DESIGN.md, including loading and error states for the saving actions.
5. **Given** a save action that fails (e.g. an invalid picture or a network error), **When** it happens, **Then** the player sees a clear, friendly message and can retry or continue, without the flow breaking or leaking system internals.

---

### Edge Cases

- **Unverified or mid-verification account**: Onboarding is only for verified accounts that have signed in; an unverified user never reaches it (the sign-in gate from feature 002 still applies).
- **Direct navigation to the flow after already onboarding**: A user who has completed onboarding and opens the flow's address directly is sent to the normal app rather than re-run through it.
- **Direct navigation while signed out**: Opening the flow's address without a session sends the user to sign in first.
- **Abandon by closing the browser mid-flow**: If the user leaves without any terminal exit (never finished, never dismissed), onboarding may still appear on their next sign-in — only an explicit finish or dismiss marks it complete. (See Assumptions.)
- **Invalid or oversized picture at the photo step**: Rejected with a clear message; the player can pick another file or skip; the rest of their entered data is not lost.
- **Overlong display name or bio**: Prevented or rejected against the same published limits as the normal profile editor, enforced server-side.
- **Marking complete more than once**: Finishing and then somehow triggering completion again is harmless (idempotent) — the first completion stands.
- **Team step**: Renders as a placeholder only; it must not imply that team membership is stored, and choosing a sample team changes nothing persistent.
- **Empty everything**: A player who provides only a name (skipping all else) still ends with a valid, complete-enough profile and a finished onboarding state.

## Requirements *(mandatory)*

### Functional Requirements

#### Trigger, gate & completion state

- **FR-001**: The system MUST record, per account, whether that account has completed (or dismissed) onboarding.
- **FR-002**: On a successful interactive sign-in by a verified user who has not completed onboarding, the system MUST route the user into the onboarding flow instead of the normal app landing.
- **FR-003**: On sign-in (or automatic session restore) by a user who has already completed onboarding, the system MUST NOT route them into onboarding.
- **FR-004**: The onboarding flow MUST require an authenticated user; an unauthenticated attempt to open it MUST redirect to sign-in.
- **FR-005**: The system MUST provide a way to mark onboarding complete that is authorized server-side for the signed-in account only, and is idempotent (repeated completion has no additional effect).
- **FR-006**: Both finishing the flow and dismissing it (via "I'll do this later" or leaving after any step) MUST mark onboarding complete so it never auto-appears again.
- **FR-007**: Whether an account has completed onboarding MUST be determined server-side; any client-held indication is a convenience only and MUST NOT be the authority for showing or hiding the flow.

#### Flow, steps & content

- **FR-008**: The flow MUST present, in order: a Welcome screen, a required display-name step, an optional city step, an optional pompfen step, an optional team step, an optional photo-and-bio step, and a final Done screen.
- **FR-009**: The flow MUST present one primary question per screen, centered and mobile-first, per the "Minimal centered" wireframe (layout guidance only; visual style comes from DESIGN.md).
- **FR-010**: The Welcome screen MUST offer a primary "get started" action and a quiet "I'll do this later" action that dismisses the whole flow.
- **FR-011**: The display-name step MUST pre-fill the current display name (which defaults to the handle), MUST show a hint that the handle is unchanged, and MUST NOT allow continuing with an empty name.
- **FR-012**: The pompfen step MUST let the player select zero or more items from the canonical catalog (Stab, Langpompfe, Schild, Q-Tip, Kette, Doppel-Kurz) plus the Läufer position, consistent with the existing profile selector.
- **FR-013**: The photo-and-bio step MUST let the player add a profile picture and a short bio on a single screen, subject to the same server-side validation as the normal profile editor.
- **FR-014**: Every step except the display-name step MUST offer a Skip (or equivalent quiet dismissal) that advances without saving that step's field.
- **FR-015**: Steps after Welcome and before Done MUST show a progress indicator and a Back control that returns to the previous step with previously entered values preserved.
- **FR-016**: The Done screen MUST confirm success and offer a single primary action that enters the app.

#### Persistence

- **FR-017**: Values the player provides (display name, city/hometown, description/bio, selected pompfen, profile picture) MUST be saved to the signed-in player's own profile using the same profile-update and picture-upload capabilities as the normal profile editor.
- **FR-018**: The feature MUST NOT introduce new profile fields beyond the onboarding-completion state.
- **FR-019**: Saved values MUST be reflected on both the owner profile view and the public share page immediately after the flow completes.
- **FR-020**: Skipping a step MUST leave that field at its existing/default value (no write for skipped fields).

#### Team placeholder

- **FR-021**: The team step MUST be presented as a clear placeholder for a future teams feature; any selection there MUST NOT be persisted and MUST NOT imply stored team membership.

#### Security & cross-cutting

- **FR-022**: All profile writes and the completion marking during onboarding MUST be authorized server-side against the authenticated subject; requests targeting another account MUST be refused regardless of client state.
- **FR-023**: The immutable handle MUST NOT be editable anywhere in the onboarding flow.
- **FR-024**: Errors during saving MUST surface as clear, friendly messages with a retry/continue path, and MUST NOT leak stack traces, secrets, or system internals.
- **FR-025**: The flow MUST be responsive and legible on phone and desktop viewports per DESIGN.md, including loading and error states.

### Key Entities *(include if feature involves data)*

- **Onboarding State**: A per-account record of whether onboarding has been completed or dismissed, and when. One-to-one with the existing player profile; it is the sole new piece of persisted data this feature introduces.
- **Player Profile (existing, feature 003)**: The account's profile whose fields (display name, hometown, description, selected pompfen, picture) the flow populates. Reused unchanged apart from carrying the onboarding-completion state.
- **Pompfe Selection (existing, feature 003)**: The set of pompfen/position the player chooses; the pompfen step reuses this catalog and selection model.
- **Team (reference only)**: Shown in the placeholder team step; no team data is created, read, or stored by this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A freshly verified user signing in for the first time is taken into onboarding in 100% of cases; a user who has already completed onboarding is taken there in 0% of cases.
- **SC-002**: After a user finishes or dismisses onboarding once, it never automatically reappears on any subsequent sign-in.
- **SC-003**: A user can get from the Welcome screen to the app in one action ("I'll do this later") without providing any information.
- **SC-004**: A user who walks the full flow can complete it in under two minutes, and every value they entered is visible on their profile and public page immediately afterward.
- **SC-005**: The display name is the only field that can block progress; all other steps can be skipped, and a name-only completion yields a valid profile.
- **SC-006**: 100% of onboarding profile writes apply only to the signed-in user's own profile; attempts to target another account are refused, and the handle is never changed.
- **SC-007**: The flow is usable and correct on phone and desktop viewports, including loading and error states, with the team step clearly non-functional.
- **SC-008**: Determination of "already onboarded" cannot be bypassed by manipulating client state — the server is the authority.

## Assumptions

- **Reuses feature 003 profile capabilities.** Display name, hometown, description, pompfen selection, and picture upload persist through the existing owner-profile update and avatar-upload capabilities; onboarding adds no new profile fields beyond the completion state.
- **Reuses feature 002 auth/session.** Registration, email verification, sign-in, and session handling are unchanged; onboarding keys off a successful, verified sign-in.
- **Display name pre-fill.** New profiles default the display name to the handle (feature 003), so the display-name step is pre-filled and simply asks the player to confirm or change it.
- **"Shown once" is driven by an explicit completion mark.** Onboarding auto-appears only while the account has no completion mark; finishing or dismissing sets it. Merely abandoning the flow (closing the tab) without a terminal exit does not set it, so it may appear again next sign-in — an acceptable, non-naggy default.
- **Post-onboarding destination is the app dashboard.** The Done screen's primary action and the "I'll do this later" action both enter the normal app landing.
- **Team step persists nothing.** Because no teams model exists (feature 003 shipped teams as a stub), the team step is purely visual this round; a real teams feature will supersede it.
- **Full-screen flow outside the app shell**, reached at a dedicated address behind the authenticated-user gate, consistent with how the auth screens sit outside the shell.
- **Photo + bio share one screen** (not split), matching the wireframe; splitting them is a possible future refinement.
- **Same validation limits as the profile editor** apply to display name, bio/description, hometown, and picture (type/size), enforced server-side.
- **Out of scope**: a real teams model or team search; badges; changing the immutable handle; new picture-storage infrastructure; re-running onboarding on demand from settings; progress persistence across devices/sessions mid-flow; analytics/telemetry on step drop-off.
