# Feature Specification: Badges & Achievements

**Feature Branch**: `012-badges-achievements`

**Created**: 2026-07-09

**Status**: Draft

**Input**: User description: "Badges and achievements for players and teams. Both players (user profiles) and teams can earn badges and achievements, displayed on the Player Profile and the Team Page. A system administrator can define the rules that award them (name, description, icon, criteria). Examples — badges: beta-tester, active for X years, took part in 10 tournaments. Achievements: won the championship in year Y, received positive feedback 10 times in a row."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin curates and awards recognitions manually (Priority: P1) 🎯 MVP

A system administrator defines a badge or achievement (name, description, visual icon, which subject type it applies to — player and/or team) and manually grants it to a specific player or team. The award immediately appears on that player's profile or the team's page, visible to anyone who can view that page.

**Why this priority**: This is the smallest end-to-end slice that delivers the core value — recognitions can be created and shown — without depending on any automatic evaluation engine or external data domains. It covers real cases today (beta-tester badge, "champion 2026" achievement) purely through admin action.

**Independent Test**: As an admin, create a "Beta Tester" badge, grant it to a player and to a team; open the player profile and team page and confirm the badge is displayed with its name, description, icon, and earned date. Confirm a non-admin cannot create or grant.

**Acceptance Scenarios**:

1. **Given** an admin is signed in, **When** they create a recognition definition with a name, description, icon, and applicable subject type(s), **Then** it is saved and appears in the admin catalog.
2. **Given** a saved definition applicable to players, **When** the admin grants it to a specific player, **Then** the player's profile shows the earned recognition with its earned date.
3. **Given** a saved definition applicable to teams, **When** the admin grants it to a specific team, **Then** the team page shows the earned recognition.
4. **Given** a player already holds a recognition, **When** an admin attempts to grant the same recognition again, **Then** the system prevents a duplicate award.
5. **Given** a non-admin user, **When** they attempt to create a definition or grant a recognition (including by calling the operation directly), **Then** the request is rejected server-side.

---

### User Story 2 - Players and teams display their recognitions (Priority: P1)

Anyone viewing a player profile or team page sees that subject's earned badges and achievements presented in a dedicated area, grouped so badges and achievements are distinguishable, each showing its icon, name, description, and when it was earned. When a subject has none, a friendly empty state is shown.

**Why this priority**: Display is half of the value and reuses the badges area already stubbed on the profile (feature 003) and the team public page (feature 009). It is P1 because US1 is not demonstrable without it, but it is listed separately because it is independently testable against seeded data.

**Independent Test**: With seeded awards on a player and a team, load each page and confirm badges and achievements render correctly and are grouped; load a page for a subject with no awards and confirm the empty state.

**Acceptance Scenarios**:

1. **Given** a player with several earned recognitions, **When** their profile is viewed, **Then** badges and achievements are shown in their designated area, visually grouped, each with icon/name/description/earned-date.
2. **Given** a team with earned recognitions, **When** the team page is viewed, **Then** the recognitions are shown in the same consistent style.
3. **Given** a subject with no recognitions, **When** their page is viewed, **Then** an empty state is shown rather than a broken or blank area.
4. **Given** a recognition was revoked, **When** the subject's page is viewed, **Then** the revoked recognition is not displayed.

---

### User Story 3 - Automatic awarding from admin-defined criteria (Priority: Deferred — not in v1)

Instead of granting by hand, an admin attaches criteria to a definition (for example: account active for X years; participated in N events) and the system automatically grants the recognition to any player or team that meets the criteria, without further admin action.

**Status**: **Out of scope for v1** (see "Out of Scope"). Captured here so the v1 data model is shaped to accommodate it later. Manual granting (US1) already covers every example case in the interim.

**Independent Test** *(when built)*: Define a recognition with an "account active ≥ 1 year" criterion; for a subject that meets it, confirm the recognition is granted automatically within the defined evaluation window and appears on the page; for a subject that does not meet it, confirm no award.

**Acceptance Scenarios** *(future)*:

1. **Given** a definition with an automatic criterion, **When** a subject first satisfies the criterion, **Then** the recognition is granted automatically and dated.
2. **Given** a subject that already satisfies a criterion, **When** the definition is created, **Then** existing qualifying subjects are (eventually) awarded, not only future ones.
3. **Given** an automatically-awarded recognition, **When** the underlying data later changes, **Then** an already-earned recognition is not silently removed (earned recognitions are durable unless explicitly revoked).

---

### Edge Cases

- **Duplicate awards**: the same recognition must not be granted to the same subject twice.
- **Revocation**: an admin can revoke an award (e.g., granted in error); revoked awards stop displaying but remain auditable.
- **Subject deletion**: when a player or team is deleted, their awards are handled without orphaned records or errors on any page.
- **Definition deletion/retirement while awards exist**: retiring a definition must not break already-earned awards or the pages that show them.
- **Subject-type mismatch**: a definition applicable only to players cannot be granted to a team, and vice-versa.
- **Granting to a non-existent or ineligible subject**: attempting to grant to a missing player/team, or one already holding the recognition, is rejected cleanly.
- **Visibility**: because profiles and team pages can be publicly linked, awards shown there are effectively public — no private-only award data should be exposed beyond name/description/icon/earned-date.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow a system administrator to create, edit, and retire recognition definitions, each with a name, description, and visual icon.
- **FR-002**: Each definition MUST declare which subject type(s) it applies to — players, teams, or both.
- **FR-003**: Badges and achievements MUST be modeled as two separate systems, each with its own catalog of definitions and its own awards. Badges represent status / membership / milestone recognitions (e.g., beta-tester, tenure). Achievements represent competitive accomplishments and MAY carry accomplishment context such as a year or the competition/event they relate to. They are managed on separate admin surfaces and displayed as two distinct groups.
- **FR-004**: A system administrator MUST be able to manually grant a recognition to a specific player or team, and MUST be able to revoke a previously granted award.
- **FR-005**: The system MUST prevent granting a recognition whose applicable subject type does not match the target subject.
- **FR-006**: The system MUST prevent duplicate active awards of the same recognition to the same subject.
- **FR-007**: The system MUST record, for each award, when it was earned and whether it was granted manually (by which admin) or automatically (by the system).
- **FR-008**: Earned recognitions MUST be displayed on the player profile and the team page in a dedicated, consistently-styled area, grouped by category, each showing icon, name, description, and earned date, with a defined empty state.
- **FR-009**: The system MUST NOT display revoked awards on public pages, but MUST retain them for audit.
- **FR-010**: All authorization for defining, granting, and revoking recognitions MUST be enforced server-side; client-side gating is for UX only.
- **FR-011**: For this version, awarding is **manual only** — every badge and achievement is granted by an administrator. Automatic, criteria-based awarding is explicitly out of scope for v1 (see "Out of Scope"), but the definition and award model MUST be shaped so an automatic award source can be added later without reworking earned-award history.
- **FR-012**: Earned recognitions MUST be durable — once granted, a recognition MUST NOT be silently removed; removal happens only via explicit admin revocation.
- **FR-013**: The system MUST gate all administrative capabilities behind a server-side authorization check. Because no platform system-administrator role exists yet, v1 MUST use a temporary configuration-driven gate (a configured allowlist of administrator identities, sourced like other secrets/config), enforced server-side. This is an interim measure: the model MUST allow replacing it with a real administrator role later without changing the badge/achievement behavior. Proper admin-role management is tracked in **GitHub issue #21**.
- **FR-014**: Recognition lists on any page MUST be bounded/paginated rather than returning unbounded collections.

### Key Entities *(include if feature involves data)*

Badges and achievements are two separate systems (FR-003) but share a parallel shape:

- **Badge Definition**: the catalog template for a badge — name, description, visual icon, applicable subject type(s), lifecycle status (active/retired). Represents status / membership / milestone recognitions.
- **Achievement Definition**: the catalog template for an achievement — the same core fields as a badge definition, plus optional accomplishment context it is meant to record (e.g., a year or the competition/event). Represents competitive accomplishments.
- **Badge Award / Achievement Award**: a grant of a definition to a Subject, capturing earned date, the administrator who granted it (award source is "manual" in v1, with room for "automatic" later), any recorded context, and status (active / revoked with reason). The record of what *has been* earned.
- **Subject**: the earner of an award — a player (user profile) or a team. Awards are polymorphic over these two subject types.

*(Deferred to a future version — see "Out of Scope": an **Award Criterion** describing a machine-evaluable rule and its parameters, which would let the system grant awards automatically.)*

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An administrator can define a new recognition and grant it to a player or team, and it becomes visible on that subject's page, in under 2 minutes and without engineering involvement.
- **SC-002**: 100% of define/grant/revoke operations are rejected for non-administrators, verified server-side (no client-only enforcement).
- **SC-003**: A player profile and a team page each render their earned badges and achievements — grouped and with an empty state — with no broken or blank areas across the supported viewports.
- **SC-004**: The same recognition can never appear more than once as an active award on the same subject (0 duplicate awards).
- **SC-005**: Revoking an award removes it from all public pages immediately while preserving an auditable record.
- **SC-006**: Only configured administrators can reach define/grant/revoke operations; all such attempts by anyone else are refused server-side (verified independent of the UI).

## Assumptions

- **Display surfaces already exist**: the player profile (feature 003) shipped with a stubbed badges area intended to host this, and the team page (feature 009) provides the team display surface. This feature fills those areas rather than introducing new pages.
- **Awards are publicly visible**: profiles and team pages are shareable/public, so earned recognitions shown there are treated as public information limited to name/description/icon/earned-date.
- **Subject targeting**: a single definition may apply to players, teams, or both, chosen at definition time.
- **Manual awarding only in v1**: all badges and achievements are granted by an administrator; there is no automatic evaluation engine in this version (that is deferred — see "Out of Scope").
- **Temporary admin gate**: because no platform system-administrator role exists yet, administrative operations are protected by a configuration-driven allowlist of administrator identities in v1, enforced server-side, to be replaced by a proper admin role in a tracked follow-up.
- **Notifications**: informing a subject that they earned a recognition is desirable and would reuse the notifications system (feature 010), but is treated as a follow-up rather than core to this feature's MVP.
- **Icons**: "visual icon" means an admin-selectable/uploadable image or icon reference; exact asset handling is an implementation detail for planning.

## Out of Scope (v1)

- **Automatic, criteria-based awarding** (User Story 3): the system does not evaluate rules or grant awards on its own in v1. Definitions and awards are shaped so this can be added later without reworking earned history.
- **New underlying data domains** the examples imply but that do not exist yet — tournament results/standings/championships and a positive-feedback/rating system. Recognitions that conceptually depend on them are simply granted manually in v1.
- **A full system-administrator role and admin-role management** — replaced in v1 by the temporary configuration gate (FR-013); proper role management is tracked in **GitHub issue #21**.
- **Earn notifications** — reusing feature 010 to notify a subject when they earn something is a follow-up, not part of this MVP.

## Dependencies

- **Administrator authorization**: requires a system-administrator authorization level to gate management operations (see FR-013 clarification).
- **Profile (003)** and **Team page (009)** provide the display areas.
- **Events (006)** provides participation signals for any automatic event-based criteria.
- **Tournaments/championships and feedback/ratings domains** are *not* assumed to exist; example criteria that need them are out of automatic scope until those features land.
