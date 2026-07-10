# Feature Specification: Admin catalogue management (badges, achievements & team awards)

**Feature Branch**: `014-admin-catalogue`

**Created**: 2026-07-10

**Status**: Draft

**Input**: User description: "Admin catalogue management for badge & achievement types (GitHub issue #24). Build out the placeholder /admin/catalogue page into the management surface for the fixed badge and achievement catalogues, and restore admin award-assignment for teams."

## Overview

Platform admins govern two fixed catalogues — **badges** (recognition) and
**achievements** (milestones) — whose entries are the only things that can be
granted to players and teams. Today `/admin/catalogue` is a placeholder: admins
can grant what already exists (to players only) but cannot create, edit,
illustrate, retire, or bring back a type, and have no way to award anything to a
**team**. This feature turns the placeholder into the full management surface and
restores team award assignment.

## Clarifications

### Session 2026-07-10

- Q: Where should admin team-award assignment mount? → A: A dedicated admin
  teams area — a searchable `/admin/teams` list plus an `/admin/teams/{slug}`
  team detail carrying the Assign picker, mirroring the existing `/admin/users`
  area, with its own admin-nav entry (sibling to Users and Catalogue).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse both catalogues at a glance (Priority: P1)

An admin opens Catalogue and sees every badge and every achievement behind one
toggle. Each entry shows its icon, name, description, who it applies to (players,
teams, or both), how many times it has been granted, and whether it is Active or
Retired. They can narrow to All / Active / Retired. On desktop it is a calm
table; on a phone it folds into cards.

**Why this priority**: This is the foundational surface every other action hangs
off, and even read-only it already replaces the placeholder with real oversight
of what can be granted.

**Independent Test**: Sign in as an admin, open `/admin/catalogue`, and confirm
both catalogues render with icon, applies-to, grant count, and status, that the
toggle switches catalogues, and that the status filter narrows the list — on both
a wide and a narrow viewport.

**Acceptance Scenarios**:

1. **Given** seeded badges and achievements, **When** the admin opens Catalogue,
   **Then** the badges catalogue is shown with each type's icon, name,
   description, applies-to, grant count, and status.
2. **Given** the badges catalogue is shown, **When** the admin switches the
   toggle to Achievements, **Then** the achievements catalogue replaces it.
3. **Given** a catalogue containing active and retired types, **When** the admin
   selects the Retired filter, **Then** only retired types remain; All shows both.
4. **Given** a narrow (mobile) viewport, **When** the admin opens Catalogue,
   **Then** the list is presented as cards rather than a table, with the same
   information.
5. **Given** a non-admin (or signed-out) visitor, **When** they attempt to reach
   the catalogue or its data, **Then** access is refused by the server.

---

### User Story 2 - Create a new type (Priority: P1)

An admin adds a new badge or achievement: they pick the kind, give it a name and
description, and choose who it applies to (players, teams, or both). It appears in
the catalogue immediately and becomes grantable to the matching subjects.

**Why this priority**: Creating types is the headline capability the placeholder
lacks and the first bullet of the source issue.

**Independent Test**: From Catalogue, create a badge that applies to players,
confirm it appears in the list as Active with a zero grant count, and confirm it
then appears in the player Assign picker.

**Acceptance Scenarios**:

1. **Given** the create form, **When** the admin submits a valid name,
   description, kind, and at least one applies-to, **Then** the type is created
   and shown in its catalogue as Active.
2. **Given** the create form, **When** the admin selects neither players nor
   teams, **Then** the form is rejected with a clear message and nothing is
   created.
3. **Given** a name or description longer than allowed, **When** the admin
   submits, **Then** the request is rejected with a clear message.
4. **Given** a newly created players-only badge, **When** an admin opens the
   player Assign picker, **Then** the new badge is offered there.

---

### User Story 3 - Edit an existing type (Priority: P2)

An admin opens a type and changes its name, description, or applies-to using the
same form as create, pre-filled. The kind cannot be changed (a type never crosses
catalogues). While editing they can see how many hold it and when it was created.

**Why this priority**: Correcting wording and applies-to is essential upkeep, but
depends on types existing (US1/US2) first.

**Independent Test**: Edit an existing badge's description, save, and confirm the
new text shows in the catalogue and that the kind control is not changeable.

**Acceptance Scenarios**:

1. **Given** an existing type, **When** the admin opens it to edit, **Then** the
   form is pre-filled with its current values and shows its grant count and
   created date.
2. **Given** the edit form, **When** the admin changes the description and saves,
   **Then** the catalogue reflects the new description.
3. **Given** the edit form, **When** the admin views the kind control, **Then**
   the kind is fixed and cannot be switched to the other catalogue.
4. **Given** an edit that removes an applies-to that still has holders, **When**
   the admin saves, **Then** the change is accepted and already-granted awards are
   unaffected (see Edge Cases).

---

### User Story 4 - Give a type an icon (Priority: P2)

An admin uploads an image for a type (PNG, JPEG, or WebP), replaces it later, or
removes it. They see it previewed at the real sizes it appears (small list,
picker, and profile sizes), with a badge shown as a circle and an achievement as
a rounded square, before committing. Replacing swaps it everywhere at once.

**Why this priority**: Icons make the catalogue legible and awards recognizable,
but a type is usable without one.

**Independent Test**: Upload a square PNG to a type, confirm it previews and then
appears in the catalogue and Assign picker; replace it and confirm the new image
shows; remove it and confirm the fallback returns.

**Acceptance Scenarios**:

1. **Given** a type with no icon, **When** the admin uploads a valid image,
   **Then** it is stored and shown for that type wherever the type appears.
2. **Given** the icon chooser, **When** the admin selects an image, **Then** a
   preview is shown at the sizes the icon appears, masked to the type's shape,
   before it is committed.
3. **Given** an unsupported file type or an over-limit file, **When** the admin
   tries to use it, **Then** it is refused with a clear message and the existing
   icon is unchanged.
4. **Given** a type with an icon, **When** the admin replaces it, **Then** the new
   image appears everywhere and the previous image is not retained.
5. **Given** a type with an icon, **When** the admin removes it, **Then** the type
   falls back to the default placeholder.

---

### User Story 5 - Retire and reinstate a type (Priority: P2)

An admin retires a type they no longer want granted. Retiring is not deleting: it
stays on everyone who already earned it and simply leaves the Assign picker. The
confirm spells out exactly what changes and is framed as gentle and reversible.
Later, the admin can reinstate the type from the catalogue, returning it to the
picker.

**Why this priority**: Retiring keeps the grantable set current without ever
breaking existing profiles or history; reinstatement makes it safe by being fully
reversible.

**Independent Test**: Retire an active type, confirm it disappears from the Assign
picker while any existing award on a profile remains, then reinstate it and
confirm it returns to the picker.

**Acceptance Scenarios**:

1. **Given** an active type, **When** the admin retires it and confirms, **Then**
   it is marked Retired and no longer offered in the Assign picker.
2. **Given** a subject that already holds a now-retired type, **When** their
   profile or team page is viewed, **Then** the award still shows.
3. **Given** the retire confirm, **When** the admin reads it, **Then** it states
   plainly that holders keep it, that it leaves the picker, and that it can be
   reinstated — and is presented as a reversible (not destructive) action.
4. **Given** a retired type, **When** the admin reinstates it, **Then** it becomes
   Active again and is offered in the Assign picker.
5. **Given** any type, **When** an admin looks for a way to permanently delete it,
   **Then** none is offered (retire is the only removal).

---

### User Story 6 - Assign and revoke awards for teams (Priority: P2)

An admin grants a badge or achievement to a **team** and can revoke it later,
mirroring how awards are assigned to players. This restores the team-award
capability that existed before the admin-area rework.

**Why this priority**: The grant/revoke-by-team capability exists in the system
but has no admin surface today, so teams cannot currently be recognized at all —
a functional gap this feature must close.

**Independent Test**: As an admin, open the admin teams area, find a team by
search, open its detail, grant a team-applicable badge, confirm it appears on
that team's public page, then revoke it and confirm it is removed.

**Acceptance Scenarios**:

1. **Given** the admin area, **When** the admin opens Teams and searches,
   **Then** matching teams are listed and a team opens to its admin detail.
2. **Given** a team's admin detail and a team-applicable type, **When** the admin
   assigns the award, **Then** it is granted and shown on the team's page.
3. **Given** a team that already holds an award, **When** the admin revokes it,
   **Then** it is removed from the team.
4. **Given** the team assign picker, **When** the admin browses it, **Then** only
   types that apply to teams are offered, and retired types are not.
5. **Given** a team that already holds a type, **When** the admin views the
   picker, **Then** that type is marked as already given and cannot be granted
   twice.

---

### Edge Cases

- **Duplicate grant**: assigning a type a subject already holds is refused as a
  conflict rather than creating a second award.
- **Granting a retired type**: retired types are never offered in the picker and a
  direct attempt to grant one is refused.
- **Applies-to narrowed after grants exist**: editing a type to no longer apply to
  a subject kind does not remove or alter awards already granted to that kind; it
  only affects future grants.
- **Icon that is not square / below the recommended size**: accepted if it is a
  supported format within the size limit; the recommendation is guidance, not a
  hard rejection. Corrupt or non-image files are refused.
- **Empty catalogue / empty filter**: a catalogue (or a filter result) with no
  types shows a friendly empty state, not a blank screen.
- **Concurrent edits**: if a type is retired or changed in another session, the
  admin's next action reflects the current server state rather than a stale view.
- **Name reuse**: type names are not required to be unique (admins curate a small
  set); creating a type whose name matches an existing or retired type is allowed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The catalogue MUST present both the badge and achievement catalogues
  behind a single toggle, one catalogue at a time.
- **FR-002**: Each listed type MUST show its icon (or a placeholder), name,
  description, applies-to (players / teams / both), grant count, and status
  (Active or Retired).
- **FR-003**: The catalogue MUST let the admin filter each catalogue by All,
  Active, or Retired.
- **FR-004**: The catalogue MUST be legible and operable on both wide and narrow
  viewports (a table that folds into cards).
- **FR-005**: Admins MUST be able to create a type by specifying kind (badge or
  achievement), name, description, and applies-to (at least one of players or
  teams).
- **FR-006**: The system MUST reject a create/edit that has an empty name, an
  over-length name or description, or no applies-to selected, with a clear
  message, and MUST NOT persist it.
- **FR-007**: Admins MUST be able to edit a type's name, description, and
  applies-to via a pre-filled form; the kind MUST NOT be changeable on edit.
- **FR-008**: While editing, the system MUST show the type's grant count and the
  date it was created.
- **FR-009**: Admins MUST be able to upload, replace, and remove a type's icon,
  accepting PNG, JPEG, and WebP within an enforced size limit; unsupported or
  over-limit files MUST be refused without altering the existing icon.
- **FR-010**: Before committing an icon, the system MUST preview it at the sizes
  it appears, masked to the type's shape (circle for badges, rounded square for
  achievements).
- **FR-011**: Replacing an icon MUST update it everywhere the type appears and
  MUST NOT retain the previous image.
- **FR-012**: Admins MUST be able to retire a type; a retired type MUST remain on
  every award already granted and MUST NOT be offered in any Assign picker.
- **FR-013**: The retire confirmation MUST state plainly that holders keep it, that
  it leaves the picker, and that it can be reinstated, and MUST be presented as a
  reversible action.
- **FR-014**: Admins MUST be able to reinstate a retired type, returning it to
  Active and to the Assign picker.
- **FR-015**: The system MUST NOT offer any permanent deletion of a type; retire is
  the only removal.
- **FR-016**: Admins MUST be able to grant a badge or achievement to a team and to
  revoke a team's award, mirroring the player flow.
- **FR-017**: Any Assign picker MUST offer only Active types that apply to the
  subject kind, MUST mark types the subject already holds, and MUST prevent
  granting the same type to the same subject twice.
- **FR-018**: All authorization and validation MUST be enforced server-side under
  the platform-admin boundary; client navigation and controls are UX only and are
  never the security boundary.
- **FR-019**: Catalogue listings MUST be paginated (no unbounded lists).
- **FR-020**: Retiring, reinstating, editing, creating, and granting/revoking MUST
  NOT change how existing awards are displayed on public profile and team pages.
- **FR-021**: Team award assignment MUST live in a dedicated admin teams area: a
  searchable, paginated teams list (parallel to the users list) and a per-team
  admin detail that carries the Assign picker, reachable from an admin-nav entry
  alongside Users and Catalogue.
- **FR-022**: The admin teams list MUST let an admin find a team by name (and,
  where available, location), and opening a team MUST show its admin detail with
  its current awards and the assign/revoke controls.

### Key Entities *(include if feature involves data)*

- **Badge type / Achievement type (catalogue entry)**: a grantable definition with
  a name, description, applies-to (players and/or teams), an optional icon, a
  retired flag, a created date, and a count of how many active awards reference it.
  Badges are recognition; achievements are milestones and may additionally carry
  accomplishment context on each award.
- **Award**: a single grant of a type to exactly one subject — a player or a team —
  which persists independently of the type's later retirement.
- **Icon**: an image bound to a single type, stored in one of the accepted formats,
  replaced or removed as a whole.
- **Subject**: the recipient of an award — a player (by handle) or a team (by slug).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An admin can create a new grantable type and see it offered in the
  Assign picker in under 2 minutes, without leaving the admin area.
- **SC-002**: 100% of retired types disappear from every Assign picker while 100%
  of already-granted awards of those types remain visible on the holders' pages.
- **SC-003**: A retired type can be reinstated and be grantable again with no data
  loss and no residual gap in its award history.
- **SC-004**: An admin can grant an award to a team and see it on that team's page,
  and later revoke it — a capability that is currently impossible.
- **SC-005**: The catalogue is fully usable on a phone-width screen: every type's
  status, applies-to, and grant count are readable and every action is reachable.
- **SC-006**: No non-admin can read or change any catalogue or award data, verified
  by server-side refusal regardless of client state.
- **SC-007**: The feature ships without any destructive data migration; existing
  types, awards, and icons are preserved unchanged.

## Assumptions

- The existing platform-admin boundary (server policy) and the admin shell,
  including its Catalogue navigation entry, are reused as-is.
- The badge/achievement domain, its grant/revoke behavior, and the public display
  of awards are those established by the prior badges feature; this feature adds
  management and team assignment on top without changing display semantics.
- Grant count means the number of currently-active awards of a type (revoked
  awards are not counted).
- The created date already exists for every type (standard audit timestamp) and
  needs no new data capture.
- Reinstatement, grant counts, and created dates are surfaced within the catalogue
  (and the edit form) and are not additionally exposed on public pages.
- Icon editing is upload / replace / remove with live preview; interactive
  in-browser cropping is out of scope — the provided image is stored as-is.
- Out of scope: new award families beyond badges and achievements; tiers/levels;
  bulk edit or bulk retire; reordering how awards appear on a profile; permanent
  deletion of types; any change to public award display.
