# Feature Specification: Home Participation Makeover

**Feature Branch**: `025-home-participation-makeover`

**Created**: 2026-07-22

**Status**: Draft

**Input**: User description: "Home dashboard makeover — reshape the logged-in home screen (feature 008) around participation and action."

## Overview

The logged-in home screen (feature 008) has a liked visual style but the wrong content. It highlights events the viewer has nothing to do with, shows a section that silently duplicates another, splits the viewer's real commitments across two disconnected cards, and buries the things that actually need a response. This feature re-shapes the home screen's information architecture around two ideas: **participation** (what am I part of?) and **action** (what needs me right now?), while keeping the existing visual language.

The reshaped home reads top to bottom as a clear priority order:

1. **Needs you** — strictly actionable items awaiting the viewer's response; hidden when empty.
2. **Up next** — one unified participation agenda (the viewer's events *and* trainings), soonest-first, with inline RSVP.
3. **News** — authored, human-written broadcast the viewer is meant to read (team + event + party).
4. **What's going on** — a quiet, passive log of system events that happened (FYI, no action).

The new-player (no-team) experience is preserved: a "find a team" prompt plus an "open to everyone" discovery list.

## Clarifications

### Session 2026-07-22

- Q: What exactly feeds the "What's going on" activity log? → A: Broader activity concept — the non-actionable notification events (training reschedules/cancellations, party membership changes, role changes) PLUS participation/social signals: teammates signing up for events, new members joining the viewer's teams, and badges awarded.
- Q: An un-answered training is both actionable and upcoming — where does it show? → A: In "Needs you" only until answered; once the viewer responds (going/maybe/can't) it moves to "Up next" as a normal agenda item. No simultaneous duplication.
- Q: Which trainings count as "needs your response" in the Needs-you block? → A: Only un-answered sessions within a near window (~14 days); further-out un-answered sessions wait in "Up next".

### Session 2026-07-22 (revision, during implementation)

- Q: Trainings appeared to duplicate across "Needs you" and "Up next" (a near session nagging in Needs-you while later sessions of the same series sat in Up-next), and the Needs-you training rows lacked a date and showed the team name instead of the location. Where should training RSVP live? → A: **Reverse the earlier training-placement decision.** "Needs you" is now **invites and requests only** (team invites, party requests, party co-admin invites, marketplace invites/applications). Training RSVP is removed from the top section entirely and lives **only** inline in "Up next", where every upcoming session (answered or not) is shown. This supersedes the near-window rule (former FR-006a/FR-006b), which is withdrawn.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Everything that needs me, in one place (Priority: P1)

As a signed-in player, when I open home I immediately see the invites and requests that are waiting on *my* response — team invites, party requests, party co-admin invites, and marketplace invites and applications — gathered into a single "Needs you" block at the very top, each with a short note of when it arrived. I can accept or decline right there without hunting through notifications or separate pages. Once I have dealt with everything, the block disappears. (Training RSVP is deliberately not here — it lives inline in "Up next".)

**Why this priority**: This is the single highest-value change. Actionable items are currently scattered across the notifications panel and a right-rail marketplace card, so they are easy to miss. Consolidating them at the top turns the home screen into a reliable "what do I owe a response to" surface, which directly drives timely RSVPs, roster fills, and invite acceptances.

**Independent Test**: Seed a viewer with a pending team invite, a party request, a marketplace invite, and an un-responded training. Load home; confirm all four appear in the "Needs you" block at the top, each with in-place actions. Resolve each; confirm it leaves the block and, when the last is resolved, the whole block is gone.

**Acceptance Scenarios**:

1. **Given** I have a pending team invite, **When** I open home, **Then** the "Needs you" block appears at the top of the page with the invite and accept/decline actions.
2. **Given** I have a party request awaiting my response, a marketplace invite, and a training I have not RSVP'd, **When** I open home, **Then** all appear together in "Needs you".
3. **Given** I accept an invite from within "Needs you", **When** the action completes, **Then** that item is removed from the block without a full page reload.
4. **Given** I have no pending actionable items, **When** I open home, **Then** the "Needs you" block is not shown at all.
5. **Given** I have unread chat messages, **When** I open home, **Then** they do **not** appear in "Needs you" (chat has its own inbox).

---

### User Story 2 - One agenda for everything I'm part of (Priority: P1)

As a player, "Up next" shows a single chronological agenda of everything I actually participate in — events I signed up for individually, events my team has entered a party for, and my trainings — soonest first. Each item lets me RSVP or see my status inline. It never shows events I have no relationship to.

**Why this priority**: Today the viewer's real commitments are split between an "Up next" card (events) and a separate "Your trainings" card (trainings), so there is no single answer to "what's next for me?". Unifying them into one participation timeline is the core of a participation-focused home.

**Independent Test**: Seed a viewer with an individual event sign-up, a team-mode event their team entered, and a training — at interleaved dates. Load home; confirm all three appear in one "Up next" list in date order, each with the correct inline action, and that no unrelated event appears.

**Acceptance Scenarios**:

1. **Given** I have an individual event sign-up, a team-party event, and a training upcoming, **When** I open home, **Then** all three appear in one "Up next" list ordered soonest-first.
2. **Given** an "Up next" item is an individuals-mode event I have joined, **When** I view it, **Then** I can toggle my RSVP inline.
3. **Given** an "Up next" item is a training, **When** I view it, **Then** I can set my response (going / maybe / can't) inline.
4. **Given** an "Up next" item is a team-mode event, **When** I view it, **Then** it shows my team is going (read-only), not a personal RSVP.
5. **Given** there is an upcoming tournament I am not part of, **When** I open home, **Then** it does **not** appear anywhere on my home screen.
6. **Given** I have nothing upcoming, **When** I open home, **Then** "Up next" shows an empty state pointing me to browse events.

---

### User Story 3 - Authored news I'm meant to read, kept prominent (Priority: P2)

As a player, the "News" section shows human-written posts meant for me to read — from my teams, from events I'm connected to, and now from parties I'm in (e.g. a party leader informing everyone). This section stays prominent and is never diluted by automatic system messages.

**Why this priority**: News is deliberate broadcast — someone wrote it *for* the audience to read. Party news is currently missing from home even though parties are a primary coordination unit. Adding it (and keeping this stream free of system noise) ensures important messages are seen.

**Independent Test**: Seed a viewer with a team news post, an event news post, and a party news post. Load home; confirm all three appear in "News", newest-first, and that no auto-generated system event (e.g. "training rescheduled") appears in this section.

**Acceptance Scenarios**:

1. **Given** my team, a connected event, and a party I'm in have each posted news, **When** I open home, **Then** all three posts appear in "News", newest-first.
2. **Given** a training in my team was rescheduled (a system event), **When** I open home, **Then** that system event does **not** appear in "News".
3. **Given** there is no news for me, **When** I open home, **Then** "News" shows an empty state.

---

### User Story 4 - A quiet log of what's going on (Priority: P3)

As a player, at the bottom of home there is a quiet "What's going on" feed: a passive, read-only log of things that happened around me — a training was rescheduled, a new member joined my party, my role changed, a teammate signed up for an event, or a badge was awarded. It requires no action from me and is clearly separate from News, so it never competes with authored posts.

**Why this priority**: This replaces the removed, mislabeled "activity" section with something genuinely useful: ambient awareness. It is lowest priority because it is informational only — valuable, but nothing breaks if a player never reads it.

**Independent Test**: Seed both a non-actionable notification event (a party membership change or training reschedule) and a participation signal (a teammate signing up for an event or a badge awarded) for a viewer. Load home; confirm they appear in "What's going on" as read-only entries at the bottom, with no action controls, and separate from both "Needs you" and "News".

**Acceptance Scenarios**:

1. **Given** a new member joined my party, a training was rescheduled, and a teammate signed up for an event, **When** I open home, **Then** all appear in "What's going on" as read-only entries.
2. **Given** an entry in "What's going on", **When** I view it, **Then** it offers no accept/decline/respond controls.
3. **Given** there is no recent activity for me, **When** I open home, **Then** "What's going on" shows an empty state or is hidden.

---

### User Story 5 - New players still get a warm start (Priority: P2)

As a player who is not yet on a team, home still greets me warmly, points me to find a team, and shows events that are open to everyone so I can get involved immediately.

**Why this priority**: The no-team onboarding path already exists and is valued; the makeover must not regress it. It is P2 because it protects existing behavior rather than adding new value.

**Independent Test**: Load home as a viewer with no team memberships; confirm the "find a team" prompt and an "open to everyone" event list appear, and that team-only sections (team-party items, team/party news) are absent.

**Acceptance Scenarios**:

1. **Given** I am on no team, **When** I open home, **Then** I see a welcoming greeting and a "find a team" prompt.
2. **Given** I am on no team, **When** I open home, **Then** I see an "open to everyone" list of events I can join.

---

### Edge Cases

- **Loading / failure**: While home loads, a skeleton is shown; if the composite load fails, the page shows a retry rather than blanking. Individual sections own their own empty states.
- **All sections empty (has team)**: A player on a team with nothing pending, nothing upcoming, no news, and no activity should still see a coherent, non-broken page (greeting + empty states / hidden sections), not a blank screen.
- **Trainings never duplicate across sections**: A training only ever appears in "Up next" (with an inline going/maybe/can't). It is never in "Needs you". This removes the duplication that arose when a near session nagged in "Needs you" while later sessions of the same series sat in "Up next".
- **Item resolved elsewhere**: The viewer accepts an invite in the notifications panel in another tab, then returns to home — home should not show a stale actionable item once refreshed.
- **Activity vs. news overlap**: A team news post is authored (News); a training reschedule is a system event (What's going on). These must never appear in the same stream.
- **Multi-team viewer**: A viewer on two teams that both entered the same event sees that event once in "Up next".

## Requirements *(mandatory)*

### Functional Requirements

**Needs you (actionable)**

- **FR-001**: The home screen MUST present a "Needs you" section, pinned above all other sections, containing only items that require the viewer's direct response.
- **FR-002**: "Needs you" MUST include, at minimum: pending team invites, party requests awaiting the viewer, party co-admin invites, and marketplace invites and applications. It MUST consist of invites and requests only.
- **FR-006a** *(revised 2026-07-22)*: "Needs you" MUST NOT contain training RSVP items. Training RSVP lives inline in "Up next" only. *(This withdraws the former near-window rule; trainings never appear in the top section.)*
- **FR-003**: Each "Needs you" item MUST offer its resolving action(s) inline (e.g. accept / decline / respond) without navigating away.
- **FR-004**: When a "Needs you" item is resolved, it MUST be removed from the section without requiring a full page reload.
- **FR-005**: The "Needs you" section MUST be hidden entirely when there are no actionable items.
- **FR-006**: "Needs you" MUST NOT include chat/direct messages, nor any passive informational (non-actionable) items.

**Up next (unified participation agenda)**

- **FR-007**: The home screen MUST present a single "Up next" agenda combining the viewer's upcoming events and trainings, ordered soonest-first.
- **FR-008**: "Up next" MUST include the viewer's individual event sign-ups, events where a team the viewer belongs to has entered a party (team-mode sign-ups), and all of the viewer's upcoming trainings (answered or not), each with the appropriate inline action.
- **FR-009**: "Up next" MUST exclude any event or tournament the viewer has no participation relationship with.
- **FR-010**: For individuals-mode events, "Up next" MUST allow the viewer to RSVP / withdraw inline and reflect current status.
- **FR-011**: For trainings, "Up next" MUST allow the viewer to set a response (going / maybe / can't) inline.
- **FR-012**: For team-mode events, "Up next" MUST show a read-only "your team is going" indicator rather than a personal RSVP.
- **FR-013**: A viewer belonging to multiple teams that entered the same event MUST see that event only once in "Up next".
- **FR-014**: "Up next" MUST provide a way to see the full upcoming list beyond the home preview ("see all").

**Removals**

- **FR-015**: The home screen MUST NOT display the "Tournaments" highlight section (upcoming tournaments regardless of participation).
- **FR-016**: The home screen MUST NOT display the desktop "Team snapshots" rail.
- **FR-017**: The home screen MUST NOT display the previous "Your teams activity" section (which duplicated team-news data).
- **FR-018**: The standalone right-rail "Your trainings" card MUST be removed; trainings are surfaced via "Up next" (agenda) and "Needs you" (un-responded).

**News (authored broadcast)**

- **FR-019**: The home screen MUST present a "News" section of authored, human-written posts intended to be read.
- **FR-020**: "News" MUST aggregate team news, event news, and party news, merged newest-first.
- **FR-021**: "News" MUST NOT contain system-generated / automatic activity entries.
- **FR-022**: "News" MUST provide a way to see the full news feed beyond the home preview ("see all").
- **FR-023**: News from a party MUST only be shown to viewers who are members of that party.

**What's going on (passive activity)**

- **FR-024**: The home screen MUST present a "What's going on" section as the last section, containing passive, read-only records of system events relevant to the viewer.
- **FR-025**: "What's going on" entries MUST NOT offer actionable controls (accept / decline / respond).
- **FR-026**: "What's going on" MUST be visually and structurally distinct from "News" so authored posts are never buried by system events.
- **FR-027**: "What's going on" MUST surface a broader set of passive signals scoped to the viewer or the viewer's teams/parties, comprising: (a) the non-actionable notification events — training reschedules/cancellations, party membership changes, role changes — and (b) participation/social signals — teammates signing up for events, new members joining the viewer's teams, and badges awarded. All entries are read-only and carry no actions.
- **FR-027a**: Signals in category (b) of FR-027 that are not already emitted as notifications MUST be captured from their originating domain events; visibility MUST remain scoped to the viewer's teams/parties and enforced server-side.

**New-player variant & general**

- **FR-028**: For viewers on no team, the home screen MUST retain the "find a team" prompt and an "open to everyone" list of joinable events, and MUST omit team/party-only sections.
- **FR-029**: The home screen MUST show a loading state while the composite loads and a retry affordance if the composite load fails, rather than blanking the page.
- **FR-030**: Each section MUST present an appropriate empty state (or be hidden) when it has no content, without breaking page layout.
- **FR-031**: All authorization for which items, agenda entries, news, and activity a viewer may see MUST be enforced server-side; visibility rules are never trusted from the client.
- **FR-032**: The reshaped home MUST preserve the existing visual style and comply with DESIGN.md; this feature changes content and information architecture, not the visual language.

### Key Entities *(include if feature involves data)*

- **Home composite**: The aggregate payload backing the home screen. After this feature it comprises: viewer summary, the viewer's teams, "Needs you" actionable items, unified "Up next" agenda items, "News" posts, "What's going on" activity entries, and (no-team only) an "open to everyone" list. It no longer carries tournaments, team snapshots, or a separate teams-activity list.
- **Actionable item ("Needs you")**: A pending item requiring the viewer's response, with a type (team invite, party request, marketplace invite/application, training-needs-response), the context needed to display it, and the actions that resolve it.
- **Up-next item**: An upcoming participation entry — an event (individuals or team mode) or a training — with date/time, location/label, the viewer's current status, and the inline action appropriate to its kind.
- **News post**: An authored, human-written broadcast from a source the viewer is connected to — a team, an event, or a party — with source identity, body, and timestamp.
- **Activity entry ("What's going on")**: A passive, read-only record of a signal relevant to the viewer, with a description and timestamp; carries no actions. Covers both non-actionable notification events (training rescheduled/cancelled, party member joined/left, role changed) and participation/social signals (a teammate signed up for an event, a new member joined one of the viewer's teams, a badge was awarded).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From the home screen, a player can identify and act on every item awaiting their response without leaving the page or opening the notifications panel — 100% of the actionable types listed in FR-002 are resolvable inline.
- **SC-002**: A player can see all of their upcoming commitments (events and trainings) in a single ordered list; in a seeded scenario with mixed event and training commitments, every one appears in "Up next" and nothing the player is unrelated to appears.
- **SC-003**: The home screen shows zero events, tournaments, or highlights the viewer has no participation relationship with (for team-affiliated viewers).
- **SC-004**: No system-generated activity entry appears in the "News" section, and no authored news post appears in the "What's going on" section — the two streams are fully disjoint.
- **SC-005**: A returning player can, within roughly five seconds of opening home, tell (a) what needs their response, (b) what's coming up for them, and (c) what's new to read — validated by the top-to-bottom ordering and section labeling.
- **SC-006**: New-player (no-team) home retains a find-a-team prompt and a list of joinable open events, with no regression from the prior experience.

## Assumptions

- **Reuse of existing action endpoints**: Resolving a "Needs you" item (accepting an invite, responding to a party/marketplace request, RSVPing a training) reuses the existing per-domain actions rather than introducing new action semantics; "Needs you" is a consolidated surface over them.
- **Notifications plus new signals as the activity source**: "What's going on" draws from the system events that already generate in-app notifications (feature 010) *and* from additional participation/social signals (teammate event sign-ups, new team members, badges awarded) per FR-027. Signals not already emitted as notifications are captured from their originating domain events (feature 006 events, team membership, feature 012 badges).
- **Party news exists as authored content**: Party news is assumed to be authored broadcast content (like team/event news), suitable for the "News" stream; party-membership changes are system events belonging to "What's going on", not News.
- **Near-window horizon**: The "~14 days" figure in FR-006a is the working default for which un-answered trainings nag in "Needs you"; the exact value may be tuned during planning without changing the rule's intent.
- **Composite contract may change**: The home composite payload will change shape (sections added and removed). Because the home screen and its data source ship together, no backward-compatibility layer for the old shape is required.
- **Visual system unchanged**: DESIGN.md remains the source of truth for tokens, layout, and components; this feature introduces no new visual style.
- **Pagination preserved**: "See all" surfaces for Up next and News remain paginated per the platform's mandatory pagination rules.
