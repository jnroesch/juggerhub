# Feature Specification: Trainings

**Feature Branch**: `018-trainings`

**Created**: 2026-07-15

**Status**: Draft

**Input**: User description: "Trainings — team-scoped recurring internal training events. Trainings live inside a team as the Trainings tab on the team page. They behave like internal recurring events: no fee, no participation cap, everyone on the team is welcome. A team admin creates them (series or one-off), members respond per session with Going/Maybe/Can't, the admin edits this-session-vs-the-series (with detach, skip, cancel), and a series or single session can be made public so signed-in non-members can view and RSVP as guests. A 'Your trainings' agenda on the home dashboard surfaces the member's next sessions across teams."

## Clarifications

### Session 2026-07-15

- Q: When an admin edits the whole series, which schedule fields can change, and how are already-generated upcoming sessions reconciled? → A: Time, location, description, visibility, and end-time apply **in place** to upcoming non-detached sessions (responses preserved). Changing the recurrence **pattern** (weekday or interval) or the **end date** **regenerates** the future session set — new dates get fresh sessions, dropped dates remove their future sessions — while detached sessions and all past sessions are left untouched.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Member responds to upcoming training sessions (Priority: P1)

A team member opens the Trainings tab on their team page and sees the upcoming sessions as a dated list. Each row shows whether it belongs to a series or is a one-off, how many people are going, and the member's own answer inline. The member taps a session, sees the three-way question (Going / Maybe / Can't), picks an answer, and sees who else is coming grouped by response. They can change their answer at any time.

**Why this priority**: RSVP is the core loop of the feature — the reason trainings exist in the app is so players can say whether they'll show up and organisers can plan. Without it, sessions are just a static calendar. It is independently valuable the moment a single session exists.

**Independent Test**: Seed a team with one session and a member. The member can open the Trainings tab, open the session, set Going/Maybe/Can't, change it, and see the who's-coming breakdown reflect their answer. Delivers value even with no series/recurrence, no public sharing, and no dashboard.

**Acceptance Scenarios**:

1. **Given** a team member viewing the Trainings tab with at least one upcoming session, **When** they view the list, **Then** each session shows a Series or One-off badge, the count of people going, and the member's own current response (or a prompt to respond).
2. **Given** a member on a session page who has not answered, **When** they choose "Going", **Then** their response is saved, the going count increments, and they appear in the who's-coming "Going" group.
3. **Given** a member who previously answered "Going", **When** they choose "Can't", **Then** their response is updated (not duplicated), the going count decrements, and they move to the "Can't" group.
4. **Given** a session with several responders, **When** a member views who's coming, **Then** responders are grouped by Going / Maybe / Can't with a headcount per group and no cap is enforced.
5. **Given** a member who is not on the team and the session is team-only, **When** they attempt to view or respond, **Then** access is denied.

---

### User Story 2 - Admin creates a training (series or one-off) (Priority: P1)

A team admin opens the Trainings tab, taps "+ New training", and moves through a calm one-decision-per-screen wizard: choose series or one-off and name it; pick day / time / interval / end date (for a one-off this collapses to a single date); set the location and an optional description; choose team-only or public; then review and create. Creating a series spawns the individual dated sessions up to the end date; the team is given a heads-up.

**Why this priority**: Without creation there is nothing to respond to. Creation and response together form the minimum viable feature. Series creation is the headline capability the feature is named for.

**Independent Test**: As a team admin, complete the wizard for a weekly series with an end date and confirm the expected number of dated sessions are created and appear in the Trainings tab; complete it again choosing one-off and confirm a single session is created.

**Acceptance Scenarios**:

1. **Given** a team admin on the Trainings tab, **When** they open the create flow, choose "Recurring series", name it, pick a weekday, start/end time, interval, and end date, and confirm, **Then** the review step states the approximate number of sessions and, on create, the series and its dated sessions are persisted and shown.
2. **Given** a weekly series starting on a chosen weekday with an end date N weeks out, **When** it is created, **Then** one session is generated for each occurrence of that weekday from the start through the end date inclusive.
3. **Given** an "Every 2 weeks" or "Monthly" interval, **When** the series is created, **Then** sessions are spaced every 2 weeks / on the same weekday-of-month position respectively, up to the end date.
4. **Given** a team admin choosing "One-off session", **When** they proceed, **Then** the schedule step collapses to a single date/time (no interval or end date) and exactly one session is created.
5. **Given** a non-admin team member, **When** they view the Trainings tab, **Then** they do not see a create button and see a gentle "nothing scheduled" empty state when there are no sessions.
6. **Given** a team admin creating a training, **When** the series/one-off is created, **Then** team members receive a heads-up notification.
7. **Given** the schedule step, **When** the chosen end date is before the start date, or the end time is not after the start time, **Then** creation is blocked with a clear message.

---

### User Story 3 - Admin edits a session vs the whole series (Priority: P2)

An admin editing a recurring session is first asked the scope: change just this session, or the whole series. Editing the whole series applies to all upcoming sessions and notifies responders — except any session that was already edited on its own, which stays detached and keeps its values. Editing a single session detaches it: it keeps its own date/time/location and no longer follows future series edits. The admin can also skip a single date (it quietly disappears) or cancel a session (it stays visible marked-off and responders are told). Past sessions never change.

**Why this priority**: Real training schedules move — a one-week location change, a permanent time shift, a public holiday. Editing makes the feature usable over a season rather than a single setup. It builds on P1 (sessions must exist first).

**Independent Test**: From an existing series, edit the whole series time and confirm all upcoming (non-detached) sessions shift while past ones don't; separately edit one session's location and confirm only that session changes and later series edits skip it; skip a date and confirm it disappears; cancel a session and confirm it stays marked-off.

**Acceptance Scenarios**:

1. **Given** an admin editing a recurring session, **When** they start the edit, **Then** they are asked whether the change applies to "This session only" or "The whole series" before entering values.
2. **Given** an admin chooses "The whole series" and changes the time, **When** they save, **Then** all upcoming sessions that have not been individually detached update to the new time, past sessions are unchanged, and responders are notified.
3. **Given** an admin chooses "This session only" and changes its location, **When** they save, **Then** only that session changes, it is marked detached from the series, and it no longer inherits subsequent whole-series edits.
4. **Given** a series with one detached session, **When** the admin later edits the whole series, **Then** the detached session keeps its own values and every other upcoming session follows the series change.
7. **Given** an admin extends the series end date, **When** they save, **Then** new sessions are generated for the added occurrences and existing upcoming sessions (with their responses) are preserved.
8. **Given** an admin changes the series weekday or interval, **When** they save, **Then** the future session set is regenerated to the new pattern, past and detached sessions are untouched, and responses on dates that still exist are preserved.
5. **Given** an admin skips a specific date, **When** they confirm, **Then** that session is removed from the upcoming list quietly (no responder notification) and the rest of the series is unaffected.
6. **Given** an admin cancels a session, **When** they confirm, **Then** the session remains visible marked as cancelled, its responders are notified, and no new responses can be added.

---

### User Story 4 - Admin makes a training public; outsiders join as guests (Priority: P2)

An admin flips a whole series or a single session to public. Anyone with the link who is signed in but not on the team can view the session and RSVP openly with no approval — an open mat. These outsiders appear in the attendance list as guests with a quiet guest tag; they count in the headcount, can be removed by the admin, and are never added to the team. Team-only trainings stay hidden from outsiders. Visibility can be toggled at any time and can apply to the whole series or just one session.

**Why this priority**: Public/open trainings widen reach and are a distinct organiser goal, but the feature is fully useful for internal team use without it. It builds on P1/P2.

**Independent Test**: Make a session public, share its link, and confirm a signed-in non-member can open it and RSVP as a guest; confirm the guest shows with a guest tag in attendance, counts in the going total, can be removed by the admin, and is not added to the team roster; confirm a team-only session is not visible to that same non-member.

**Acceptance Scenarios**:

1. **Given** an admin on a session or series, **When** they toggle visibility to Public (choosing whole-series or just-this-session scope), **Then** the affected session(s) become viewable and RSVP-able by signed-in non-members via a shareable link.
2. **Given** a public session, **When** a signed-in non-member opens it, **Then** they can set Going/Maybe/Can't without approval and are recorded as a guest.
3. **Given** a public session with guests, **When** the admin views attendance, **Then** guests appear with a guest tag, are counted in the headcount alongside members, and can be removed individually.
4. **Given** an admin removes a guest, **When** the removal completes, **Then** the guest's response is dropped from the session and the person is not a member of the team.
5. **Given** a team-only session, **When** a signed-in non-member tries to view it, **Then** it is not visible/accessible to them.
6. **Given** a public session an outsider joined, **When** the admin flips the series/session back to team-only, **Then** the outsider can no longer RSVP and the session is no longer surfaced to them.

---

### User Story 5 - Member sees a "Your trainings" agenda on the dashboard (Priority: P3)

On the home dashboard, a member sees a "Your trainings" agenda that surfaces their next upcoming sessions across every team they belong to, plus any public sessions they've joined as a guest, each with an inline RSVP so they can answer without leaving home.

**Why this priority**: A convenience aggregation that increases engagement but is not required for the core team-page loop. Depends on P1 existing.

**Independent Test**: A member on two teams with upcoming sessions (and one public session joined as guest) sees all of them merged and date-ordered on the dashboard, and can change an RSVP inline.

**Acceptance Scenarios**:

1. **Given** a member on multiple teams with upcoming sessions, **When** they view the dashboard, **Then** the "Your trainings" agenda lists their next sessions across all those teams in chronological order.
2. **Given** a member who joined a public session as a guest, **When** they view the dashboard agenda, **Then** that session also appears.
3. **Given** a session in the dashboard agenda, **When** the member changes their RSVP inline, **Then** the response is saved the same as on the session page.
4. **Given** a member with no upcoming sessions, **When** they view the dashboard, **Then** the agenda shows a gentle empty state.

---

### Edge Cases

- **Series with no valid occurrences**: If the chosen weekday never falls between start and end date (e.g., end date same day, wrong weekday), creation is blocked with a clear message rather than creating an empty series.
- **Editing a past or cancelled session**: Past sessions are read-only; a cancelled session cannot be edited or receive new responses (it can be viewed).
- **Skipping/cancelling the only session of a one-off**: Cancelling a one-off leaves it visible marked-off; skipping a one-off effectively removes it — behaviour must be unambiguous (a one-off is cancelled, not skipped).
- **Whole-series edit when all remaining sessions are detached**: The series edit changes the template for future generation only; no existing session changes.
- **Guest on a session that reverts to team-only**: Existing guest responses stop counting / are no longer accessible to the guest; the admin's attendance view reflects the change.
- **Member leaves the team**: Their responses to that team's upcoming sessions are no longer shown as member responses (their access to team-only sessions ends).
- **Interval date arithmetic across month boundaries / DST**: Monthly-by-weekday must resolve a consistent occurrence (e.g., 3rd Tuesday) even when a month has fewer such weekdays; times are stored/compared unambiguously across daylight-saving transitions.
- **Concurrent RSVP changes**: A member changing their answer from two places results in one current response, not duplicates.
- **Admin removes themselves as team admin / last admin**: Training management follows the team's existing admin model; a team with no admin cannot create/manage trainings (out of scope to change the team last-admin guard).
- **Very long series**: An end date far in the future produces many sessions; the system must bound generation to the end date and keep the tab/list paginated.

## Requirements *(mandatory)*

### Functional Requirements

**Creation & structure**

- **FR-001**: A team admin MUST be able to create a training as either a recurring series or a one-off session, scoped to a single team.
- **FR-002**: A training MUST carry a name, an optional description, a location that is either an in-person address or a virtual link, and a start/end time.
- **FR-003**: A recurring series MUST carry a day of the week, an interval of Weekly, Every 2 weeks, or Monthly (same weekday-of-month position), and an end date.
- **FR-004**: On creating a series, the system MUST generate one dated session per interval occurrence of the chosen weekday from the start through the end date inclusive.
- **FR-005**: A one-off MUST consist of exactly one dated session with no interval or end date.
- **FR-006**: The system MUST block creation when the schedule is invalid (end date before start, end time not after start time, or a series that would produce zero sessions) with a clear, non-technical message.
- **FR-007**: The create flow MUST present a review summary stating the approximate number of sessions before the training is created.
- **FR-008**: Only team admins MUST be able to create, edit, skip, cancel, or change the visibility of a team's trainings; members MUST NOT see creation/management controls.

**Response (RSVP)**

- **FR-009**: A team member MUST be able to respond to any upcoming, non-cancelled session they can access with exactly one of Going / Maybe / Can't.
- **FR-010**: A member MUST be able to change their response at any time before the session, resulting in a single current response (no duplicates).
- **FR-011**: The system MUST NOT enforce any participation cap on responses — every eligible person may respond.
- **FR-012**: Members MUST be able to see who's coming, grouped by Going / Maybe / Can't, with a headcount per group.
- **FR-013**: Each session in a list MUST display a Series or One-off badge, the going count, and the viewer's own current response inline.

**Editing (this-vs-series, skip, cancel)**

- **FR-014**: When editing a session that belongs to a series, the admin MUST first choose the scope: this session only, or the whole series.
- **FR-015**: A whole-series edit MUST apply to all upcoming sessions that have not been individually detached, MUST leave past sessions unchanged, and MUST notify responders of the change.
- **FR-015a**: A whole-series edit to time-of-day, location, description, visibility, or end-time MUST apply **in place** to each upcoming non-detached session, preserving existing responses.
- **FR-015b**: A whole-series edit that changes the recurrence **pattern** (weekday or interval) or the **end date** MUST **regenerate** the future session set: occurrences that map to unchanged dates keep their sessions and responses, newly introduced dates get fresh sessions, and dropped future dates have their sessions removed. Detached sessions and all past sessions MUST be left untouched.
- **FR-016**: Editing a single session MUST detach it from the series: it keeps its own values and no longer inherits subsequent whole-series edits.
- **FR-017**: The admin MUST be able to skip a single date, which quietly removes that session from the upcoming list without notifying responders.
- **FR-018**: The admin MUST be able to cancel a session, which keeps it visible marked as cancelled, notifies its responders, and prevents further responses.
- **FR-019**: Past sessions MUST be read-only and never altered by series edits.

**Visibility (public / guests)**

- **FR-020**: An admin MUST be able to set visibility to team-only or public for a whole series or a single session, and MUST be able to change it at any time.
- **FR-021**: A team-only session MUST be viewable and RSVP-able only by members of that team.
- **FR-022**: A public session MUST be viewable and RSVP-able by any signed-in user who is not a member of the team (an outsider), via a shareable link, with no approval step.
- **FR-023**: An outsider who responds to a public session MUST be recorded as a guest, tagged as a guest in attendance, counted in the headcount, and MUST NOT be added to the team.
- **FR-024**: An admin MUST be able to remove a guest from a session, which drops that guest's response without affecting team membership.
- **FR-025**: When a session or series reverts to team-only, outsiders MUST lose the ability to view/RSVP it and it MUST no longer be surfaced to them.

**Surfacing & notifications**

- **FR-026**: The team page MUST include a Trainings tab listing upcoming sessions as a dated list, with an admin-only "+ New training" affordance and an active-series overview for admins.
- **FR-027**: The Trainings tab MUST show a role-appropriate empty state: admins are prompted to set up a training; members see a gentle "nothing scheduled".
- **FR-028**: The home dashboard MUST include a "Your trainings" agenda surfacing the member's next upcoming sessions across all their teams plus public sessions they've joined as a guest, each with an inline RSVP.
- **FR-029**: Creating a series or one-off MUST notify the team; series edits and session cancellations MUST notify affected responders; skipping a date MUST NOT notify responders.
- **FR-030**: All list/agenda surfaces returning sessions MUST be paginated/bounded and never return unbounded collections.

### Key Entities *(include if feature involves data)*

- **Training Series**: A recurring training template owned by one team. Attributes: name, description, location (in-person address or virtual link), start/end time-of-day, weekday, interval (weekly / every-2-weeks / monthly), end date, default visibility (team-only / public), lifecycle. Spawns Training Sessions. A one-off is represented as a series-less single session (or a degenerate single-session series — resolved in planning).
- **Training Session**: A single dated occurrence a member can respond to. Attributes: date, start/end time, location (may override the series), visibility (may override the series), status (scheduled / cancelled / skipped), detached flag (edited independently of its series), belongs to at most one series (null for one-off). Carries Responses.
- **Training Response**: One person's answer to one session. Attributes: the session, the responder (member or guest), the answer (Going / Maybe / Can't), whether the responder is a guest (outsider). Exactly one current response per person per session.
- **Guest**: A signed-in user who is not a member of the owning team but has responded to a public session. Distinguished from members by a guest tag; removable; never added to the team.
- **Team** (existing): Owns trainings; its admin/member roles govern who can manage vs. respond.
- **Notification** (existing): Reused spine for team heads-up on creation, responder notices on series edit and cancellation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A team admin can create a recurring series (name, weekday, time, interval, end date) and see its dated sessions in the Trainings tab in under 2 minutes.
- **SC-002**: For a weekly series over any end date, the number of generated sessions exactly equals the number of occurrences of the chosen weekday between start and end date inclusive (100% accuracy across intervals).
- **SC-003**: A member can record or change an RSVP on a session and see the updated who's-coming breakdown reflect it immediately, with exactly one current response retained.
- **SC-004**: A whole-series time change updates every upcoming non-detached session and no past session, and leaves previously detached sessions unchanged — verifiable in a single edit.
- **SC-005**: A signed-in non-member can open a public session via its link and RSVP as a guest without any approval step, and the admin sees that guest counted and removable — while the same non-member cannot access any team-only session.
- **SC-006**: A member on multiple teams sees all their upcoming sessions (plus public sessions joined as guest) merged in chronological order on the dashboard agenda.
- **SC-007**: No trainings list, attendance list, or dashboard agenda returns an unbounded collection; every such surface is paginated or explicitly bounded.

## Assumptions

- **Data model**: Trainings are modelled with new, dedicated entities (Training Series / Session / Response / Guest) rather than by extending the existing Event/EventSignup model, which carries conflicting semantics (fees, participation caps, waitlists, approval). Confirmed with product owner.
- **Monthly interval**: "Monthly" means the same weekday-of-month position as the start date (e.g., "every 3rd Tuesday"), not a fixed 28-day cadence. Confirmed with product owner.
- **Outsider identity**: A public-session guest must be a signed-in JuggerHub user who is simply not on the team (they carry a @handle); anonymous/unauthenticated RSVP is out of scope. Confirmed with product owner.
- **Replaces the current placeholder**: The team page's existing "upcoming trainings" section (which currently lists events a team signed up for) is superseded by real trainings from this feature.
- **Admin model reuse**: "Team admin" uses the team's existing admin role; no new per-training role is introduced. Any team admin can manage that team's trainings.
- **Management scope**: Trainings are managed on the team's internal page; the public team page may surface public trainings for outsiders but is not the management surface.
- **Out of scope**: Session reminders/scheduled notifications, calendar/month grid views, attendance history and who-came statistics, recurring exceptions beyond skip/cancel/detach, and any in-app payment (trainings are always free).
- **Notifications reuse**: The existing notification spine and preference categories are extended with new training notification types rather than building a separate channel.
- **Time zone**: Session times are handled consistently with the rest of the app (stored in UTC, presented in local context), and monthly/interval arithmetic resolves a stable occurrence across month-length and daylight-saving boundaries.
