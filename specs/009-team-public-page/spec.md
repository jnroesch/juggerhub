# Feature Specification: Public team page & request to join

> **⚠️ Superseded in part by [feature 026](../026-authenticated-only-access/spec.md) (2026-07-22):**
> the team "public" page is no longer **anonymous** — it is authenticated-only like the rest of the
> team space. The public/internal *content* split still holds within the app (a signed-in
> non-member sees the limited view; the viewer relation is now never "Anonymous"). Request-to-join
> and the roster/limited-fields behavior otherwise stand.

**Feature Branch**: `008-home-dashboard-nav` (stacked) → `009-team-public-page`

**Created**: 2026-07-08

**Status**: Draft

**Input**: "The team browser only makes sense when we can click a team and see a public view — their activity, members, trainings — and request to join."

## Overview

Today a team browse row links to `/t/:slug`, the **members-only** team space (auth-guarded), so a non-member who finds a team just hits a sign-in bounce (a known feature-007 limitation). And there is **no way to ask to join** a team — membership is invite-only.

This feature makes `/t/:slug` a **public team page** anyone can view — the team's overview, roster, recent activity, and upcoming trainings — with a **request-to-join** action for signed-in non-members and an **approval queue** for team admins. Members and admins see the same page with their extra sections (news, contact, tools) rendered inline. This turns Browse from a dead-end list into a real discovery-to-join funnel.

## Clarifications

### Session 2026-07-08

- Q: What becomes publicly visible? → A: Public = overview (name/city/activity level, beginners-welcome), the **roster** (display name + position, no contact details), recent **activity**, and upcoming **trainings**. Internal (members/admins only) = team **news**, member **contact details**, and all **admin tools**. This widens feature 005's public/internal split (roster was members-only).
- Q: How does "request to join" work? → A: A **join request + admin approval** workflow (new `TeamJoinRequest`): a signed-in non-member requests; team admins approve (creates membership) or decline; the requester sees their status. Notify is a placeholder (notifications deferred, per feature 008).
- Q: Public page vs members' space? → A: **One URL** `/t/:slug` — public to everyone; members additionally see internal sections and admins see tools, chosen server-side by the viewer's relation to the team.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See a team before joining (Priority: P1)

Anyone (signed in or not) can open a team from Browse and see who they are: name, city, whether they're active and beginners-welcome, their roster (names + positions), recent activity, and upcoming trainings — enough to decide whether to reach out.

**Why this priority**: Without a public page, Browse is a dead end; this is the core value.

**Independent Test**: As an anonymous visitor, open `/t/:slug` for a real team and see the overview, roster, activity, and trainings — with no sign-in bounce and no internal data (news/contact) shown.

**Acceptance Scenarios**:

1. **Given** an anonymous visitor, **When** they open a team's page, **Then** they see the team overview, public roster (display name + position, no email/contact), recent activity, and upcoming trainings — and never a sign-in redirect.
2. **Given** an anonymous visitor, **When** they view the page, **Then** team news, member contact details, and admin tools are absent.
3. **Given** a team browse row, **When** clicked, **Then** it lands on this public page (no auth bounce).

### User Story 2 - Ask to join a team (Priority: P1)

A signed-in player who isn't on the team taps "Request to join". A pending request is recorded; the button reflects "Requested". They can't spam duplicate requests.

**Why this priority**: The requested outcome — turning discovery into membership.

**Independent Test**: As a signed-in non-member, request to join a team; the button becomes "Requested"; requesting again does not create a second pending request; an anonymous visitor sees a "sign in to join" affordance instead.

**Acceptance Scenarios**:

1. **Given** a signed-in non-member, **When** they request to join, **Then** a pending join request is recorded and the page shows a "Requested" state.
2. **Given** a pending request, **When** they revisit, **Then** the page shows "Requested" (no duplicate is created on re-request).
3. **Given** an anonymous visitor, **When** they view the page, **Then** the join action prompts them to sign in rather than posting a request.
4. **Given** an existing member, **When** they view the page, **Then** no join action is shown.

### User Story 3 - Approve or decline requests (Priority: P1)

A team admin sees pending join requests on the team page and approves (the requester becomes a member) or declines them.

**Why this priority**: A request workflow is incomplete without the admin side; it closes the loop into membership.

**Independent Test**: As a team admin with a pending request, approve it and confirm the requester is now a member; decline another and confirm it leaves membership unchanged; a non-admin never sees the queue.

**Acceptance Scenarios**:

1. **Given** a team admin, **When** they open the team page, **Then** they see the list of pending join requests (requester name + when).
2. **Given** a pending request, **When** the admin approves it, **Then** the requester becomes a team member and the request is marked approved.
3. **Given** a pending request, **When** the admin declines it, **Then** membership is unchanged and the request is marked declined.
4. **Given** a non-admin (member or visitor), **When** they view the page, **Then** the request queue and approve/decline actions are not available (server-enforced).

### Edge Cases

- Requesting to join a team you already belong to → no request; the page shows the member view.
- Approving a request for someone who joined in the meantime → idempotent (no duplicate membership; request resolves).
- A declined requester may request again later (a new pending request); a pending one blocks duplicates.
- Team not found / deleted → a friendly not-found, not an error.
- Long rosters/activity/trainings → the public page shows a bounded, paginated/capped set.
- Viewer relation is always decided **server-side** from the session; the client cannot reveal internal sections by asking.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `/t/:slug` MUST be viewable by anyone (anonymous or signed in); it MUST NOT redirect anonymous visitors to sign-in.
- **FR-002**: The public view MUST show the team overview (name, city, active status, beginners-welcome, member count), the public roster (display name + position; **no** contact details), recent activity, and upcoming trainings.
- **FR-003**: Team **news**, member **contact details**, and **admin tools** MUST be shown only to members/admins respectively, decided server-side by the viewer's relation to the team.
- **FR-004**: A signed-in non-member MUST be able to request to join; the system MUST record a pending request and prevent duplicate pending requests for the same player+team.
- **FR-005**: The page MUST reflect the viewer's join state (none / requested / member) so the action shows request / "requested" / nothing accordingly; an anonymous viewer MUST be prompted to sign in to request.
- **FR-006**: Team admins MUST be able to see pending join requests for their team and approve or decline each; approval MUST create the membership and mark the request approved; decline MUST mark it declined without changing membership. Non-admins MUST NOT access the queue or the approve/decline actions (server-enforced).
- **FR-007**: All public reads MUST return public/permitted fields only and MUST paginate/cap any list; the join-request queue MUST be paginated.
- **FR-008**: The page and its states (loading/empty/error) MUST follow DESIGN.md and be responsive; copy follows the JuggerHub voice.

### Key Entities

- **TeamJoinRequest** (new): a player's pending/approved/declined request to join a team — team, requester, status, who decided + when. One pending request per (team, player).
- **Public team view** (read model): the team overview + viewer relation (anonymous / non-member / requested / member / admin) + capped roster, activity, and upcoming trainings.
- **Public roster row**: display name, handle, position (pompfen), role — no email/contact.
- **Training**: an upcoming event the team is entered in (name, date, location) — public.

## Success Criteria *(mandatory)*

- **SC-001**: An anonymous visitor can view any team's overview, roster, activity, and trainings without signing in, and never sees internal data.
- **SC-002**: A signed-in non-member can request to join in one tap and see the request acknowledged; duplicate pending requests are impossible.
- **SC-003**: A team admin can turn a pending request into a member in one tap; a declined request never creates a membership.
- **SC-004**: A non-admin can never see or act on the request queue, and no viewer can surface internal sections they aren't entitled to — verified by access tests.
- **SC-005**: Browse → team page → request is a continuous flow with no sign-in dead-end for viewing.

## Assumptions & Out of Scope

- Reuses existing teams, memberships, activity (event participations), team-mode event sign-ups (trainings), and the admin guard. Notifications for request/approval are **placeholder** (deferred with feature 008).
- Widens feature 005's public/internal split (roster becomes public: names + positions only). Member **contact details** and **news** stay internal.
- **Out of scope**: instant-join for beginners-welcome teams (chosen: approval for all), per-team privacy toggles, messaging/DMs, withdrawing a request UI (a declined/duplicate is handled, but a self-cancel button is not required), and any real notification delivery.
