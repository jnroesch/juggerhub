# Feature Specification: Team-internal "What's happening" section

**Feature Branch**: `044-team-activity-feed`

**Created**: 2026-08-11

**Status**: Draft

**Input**: User description: "Turn the team page's 'Letzte Aktivität' section into a real team activity feed (GitHub issue #178)." — **revised by the owner during clarification** (2026-08-11) into a two-feature split; see *Overview* and *Clarifications*.

**Source**: GitHub issue [#178](https://github.com/jnroesch/juggerhub/issues/178)

## Overview

Issue #178 reports that a player joining a team shows up on the dashboard ("Was ist los" /
"What's going on") but never in the team page's **"Letzte Aktivität" / "Recent activity"**
section, which lists only events the team played. The issue proposed merging everything into
one feed on the team page.

**The owner rejected the merge.** These are two different things and they stay two things:

| Section | Audience | Answers | This feature |
|---|---|---|---|
| **Recent activity** (existing) | any signed-in viewer | "Has this team been playing?" | Renamed, otherwise untouched |
| **What's happening** (new) | team members only | "What's been going on inside my team?" | Built here |

The existing section is a **public-facing record of events the team played**, and as designed it
is correct — features 005/006 built exactly the right thing. Its only fault is its name, which
promises a general activity feed and delivers an event history.

What is actually missing is a **team-internal** section: joins, training cancellations, awards —
the things a member wants to know about their own team, shown only to members. That is the
feature specified here.

### Consequences of the split

- The event history is **not** touched. No entry it shows today can be lost, so the
  regression risk that dominated the merged design disappears.
- The internal section may therefore use a **hard recency window** without costing anything —
  a quiet team's event history still sits in its own section right above.
- Every kind in the internal section is **members-only by construction**, so there is no
  per-kind visibility matrix to get wrong and no way for a team-only training to leak.

### Correction to the issue's stated premises

Two claims in issue #178 were checked against the code and are inaccurate. Requirements below
follow the code.

1. The issue calls the existing section "public (anonymous-reachable per 026 opt-in rules)". It
   is **not** — feature 026 made the team detail surface authenticated-only. Throughout this
   spec, "public" means **visible to any signed-in viewer**, never anonymous.
2. The issue says the event-shaped item DTO is shared with "the profile and admin surfaces". It
   is shared with the **profile** surfaces (public and owner). The admin surface has its own
   separate shape and is unaffected.

## Clarifications

### Session 2026-08-11

- Q: Which entry kinds ship, given that departures, removals and role changes cannot be
  reconstructed from anything the platform records? → A: **Derivable kinds only.** Departures
  and role changes are excluded and become a follow-up issue (decision D1).
- Q: Once team awards become a feed kind, the same award appears both in the feed and in the
  existing "Badges & achievements" card. How should the page be arranged? → A: **Award appears
  in the feed as a dated happening; the card stays and keeps showing the standing collection,
  undated** (decision D2).
- Q: A per-session "training scheduled" entry would flood the feed, because creating one weekly
  recurring training writes up to 520 session rows in a single save. What should the training
  kind show? → A: **Series started (one entry, from the series record) plus session cancelled
  (one per cancellation).** Never one per generated session (decision D3).
- Q: Where should members read the team's full paginated history? → A: **Nowhere — that is not
  wanted.** Only a few entries are needed, bounded by recency: roughly the last 30 days, at most
  ~10 entries (decision D4).
- Q: A hard recency cutoff would blank the section for teams that have not played recently,
  losing event entries the page shows today. How should a quiet team behave? → A: **Question
  superseded.** The premise was the merge; the owner split the feature instead — the event
  history keeps its own section, so a hard cutoff on the new internal section costs nothing
  (decision D5).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A member catches up on their own team (Priority: P1)

A player opens their team's page after a week away. Two players joined, Thursday's training was
called off, and the team picked up a badge. A **"What's happening"** section on the team page
shows those three things, newest first, in plain sentences in the reader's language.

**Why this priority**: This is the gap issue #178 reports, in the form the owner wants it.
Shipping only this already closes the issue — everything else here is tidying around it.

**Independent Test**: Add a member to a team, cancel one of its training sessions, and open the
team page as a member. Both appear in the new section, newest first, and neither appears in the
event section.

**Acceptance Scenarios**:

1. **Given** a team where a player joined yesterday and a session was cancelled this morning,
   **When** a member opens the team page, **Then** the new section lists the cancellation above
   the join, each with its own date.
2. **Given** a team that earned a badge last week, **When** a member opens the team page,
   **Then** the new section describes the award as something that happened, with its date.
3. **Given** a team with nothing recent, **When** a member opens the team page, **Then** the new
   section shows an empty state saying nothing has happened lately — it is not hidden and it is
   not an empty box.
4. **Given** the viewer's language is German, **When** the section renders, **Then** all
   connecting prose is German and only user-supplied names (player, training, badge) are
   untranslated.
5. **Given** a team that created a weekly recurring training covering two years,
   **When** a member opens the team page, **Then** the section shows **one** entry for the
   series — never one per generated session.

---

### User Story 2 - The team page stops contradicting itself (Priority: P2)

The team page carries two sections about "what this team has been up to". A reader can tell at a
glance which is which: one is the team's **event record**, the other is what has been
**happening lately inside the team**.

**Why this priority**: Issue #178's underlying complaint is that a section named "Recent
activity" does not contain the activity a reader expects. Adding a second section next to it
without renaming the first makes that worse, not better. This must ship with User Story 1.

**Independent Test**: Open the team page and confirm the two section headings name two
distinguishable things, in all three languages.

**Acceptance Scenarios**:

1. **Given** any team page, **When** a member views it, **Then** the section listing events the
   team played is headed by wording that names **events**, not general activity.
2. **Given** a member views the page, **When** both sections have content, **Then** no single
   happening appears in both.
3. **Given** the reader uses German or Spanish, **When** the page renders, **Then** both
   headings are translated and remain distinguishable from each other and from the dashboard's
   own "Was ist los".

---

### User Story 3 - A non-member's view is unchanged (Priority: P3)

A signed-in player looking at a team they do not belong to sees exactly what they saw before
this feature. Nothing about the team's internal life is disclosed to them.

**Why this priority**: It is the safety property of the whole design, but it is satisfied by
building the section members-only from the start rather than by separate work. It is listed so
it gets its own tests.

**Independent Test**: Load a team page as a signed-in non-member before and after the feature
and compare what is shown.

**Acceptance Scenarios**:

1. **Given** a signed-in non-member, **When** they open a team page, **Then** the new section is
   absent entirely — not present-but-empty.
2. **Given** a team with a team-only training that was cancelled, **When** a non-member opens
   the team page, **Then** nothing on the page reveals that training's existence, name, date, or
   location.
3. **Given** a non-member, **When** they request the team's internal section data directly,
   **Then** access is refused the same way the team's other members-only data is refused today.
4. **Given** a signed-in non-member, **When** they open a team page, **Then** the event section
   shows exactly the entries it showed before this feature.

---

### Edge Cases

- **A named person is banned.** Existing rules hide banned players' profile data. The entry must
  degrade to a translated stand-in ("Someone") exactly as the dashboard feed already does —
  never to an English word inside a German page, and never to a blank.
- **A named person deleted their account.** Feature 037 keeps their traces readable as
  "A former player". A join entry for a since-deleted account reads the same way and must not
  resurrect their name or handle.
- **A player joins, leaves, and rejoins.** Only their current membership is recorded, so the
  section must not imply a history it cannot substantiate.
- **A player who joined is no longer a member.** Their join can still fall inside the window
  while they are gone. The section must not present a departed player as a current one.
- **Two entries share the same instant.** A recurring series creation and its first cancellation
  could collide. Ordering must be total and repeatable.
- **The window is empty but the team is old.** Expected and acceptable — the event section above
  still carries the team's history. The empty state must read as "nothing lately", not "this
  team has never done anything".
- **More happened than the section shows.** The cap is a hard stop with no "show more"
  (decision D4). The section must not imply there is more to open.
- **A cancelled session is later un-cancelled, or a badge is revoked.** The entry must disappear
  with the fact it describes rather than persist as a claim that is no longer true.
- **375 px screens.** Each entry is a name, a sentence, and a date. It must degrade without
  horizontal scrolling, including for the longest German wording.

## Requirements *(mandatory)*

### The new members-only section

- **FR-001**: The team page MUST gain a **new** section, distinct from the existing event
  section, presenting recent team-internal happenings newest first.
- **FR-002**: The section MUST be visible **only to members of that team**. For any other
  viewer — signed-in non-member included — the section MUST be absent, not empty.
- **FR-003**: The underlying data MUST be refused to non-members by the same server-side rule
  that protects the team's other members-only data. Hiding the section client-side is not
  sufficient (constitution Principle I).
- **FR-004**: The section MUST include an entry when a player **joins the team**, carrying the
  joiner's display identity and the moment they joined.
- **FR-005**: The section MUST include an entry when a **badge or achievement is awarded to the
  team**, carrying the recognition's name and the moment it was earned.
- **FR-006**: The section MUST include an entry when a **training series is created**, carrying
  the training's name and the moment it was created — **one entry per series**, never one per
  generated session (decision D3).
- **FR-007**: The section MUST include an entry when a **training session is cancelled**,
  carrying the session's date and the moment it was cancelled.
- **FR-008**: The section MUST NOT include events the team played. Those belong to the event
  section and MUST NOT be duplicated (FR-016).
- **FR-009**: Departures, removals, and role changes MUST NOT appear (decision D1). They cannot
  be reconstructed — membership records are deleted outright and roles overwritten in place — and
  this feature does not add the recording that would be needed.
- **FR-010**: This feature MUST NOT add any new recording of team happenings. Every entry kind
  MUST be reconstructible from records the platform already keeps, so the section is correct for
  every existing team on the day it ships.

### Bounding what the section shows

- **FR-011**: The section MUST show only happenings from the **last 30 days** and MUST show at
  most **10** entries, whichever binds first (decision D4).
- **FR-012**: The 30-day window and the 10-entry cap MUST be **fixed constants**, not
  configuration (decision D4). They are stated in one place so a later change is a one-line
  edit, but no setting, environment variable, or per-team override is introduced.
- **FR-013**: The section MUST NOT paginate and MUST NOT offer a "show more" affordance. If more
  happened in the window than the cap allows, the excess is simply not shown (decision D4).
- **FR-014**: When the window contains nothing, the section MUST render an empty state whose
  wording means "nothing lately" — it MUST NOT be hidden, and MUST NOT imply the team has no
  history.
- **FR-015**: Entries MUST be ordered newest first with a deterministic tie-break, so two
  happenings sharing an instant always render in the same order.

### The existing event section

- **FR-016**: The existing section listing events the team played MUST keep its current
  contents, cap, ordering, and audience. Nothing it shows today may be lost, reworded, or
  reordered.
- **FR-017**: Its **heading MUST be renamed** in all three languages to name *events* rather
  than general activity, so it no longer overstates what it contains and no longer collides with
  the new section or with the dashboard's own feed.
- **FR-018**: The existing members-only paginated endpoint that serves this event history MUST
  keep working exactly as it does today. It is out of scope (decision D4).

### The awards overlap

- **FR-019**: The existing "Badges & achievements" section MUST be retained and MUST keep
  showing the team's **current standing collection, undated** (decision D2). The new section
  describes the same awards as **dated happenings**.
- **FR-020**: The two MUST be visually distinguishable as a **standing collection** versus a
  **log**, so one award described in both places does not read as two separate happenings. The
  standing collection MUST NOT gain dates or date-ordering as part of this feature.

### Presentation and localisation

- **FR-021**: Each entry MUST carry a machine-readable kind plus the named values that kind
  needs, and MUST NOT carry a server-composed sentence. The reader's language is known only on
  the client (feature 031), so a server-rendered summary would be the wrong language.
- **FR-022**: Where a sensible destination exists, an entry MUST link to the thing it is about.
  Where none exists, or the target has since disappeared, the entry MUST render as plain text
  rather than a dead link.
- **FR-023**: Every entry kind MUST have prose keys in English, German, and Spanish, and the
  three catalogues MUST stay at key parity — a missing translation MUST NOT silently render
  English inside a translated page.
- **FR-024**: Entry prose MUST tolerate a missing name by substituting a **translated**
  stand-in, never an empty gap and never an untranslated placeholder.
- **FR-025**: Entries naming a player MUST respect the platform's existing suppression rules for
  banned and deleted accounts.
- **FR-026**: The section MUST be read-only. It MUST NOT offer any way to act on an entry.

### Boundaries

- **FR-027**: The dashboard "What's going on" feed MUST continue to behave exactly as it does
  today — entries, ordering, cap, and wording unchanged.
- **FR-028**: The profile surfaces that display the event-shaped activity item MUST continue to
  work unchanged.
- **FR-029**: This feature MUST NOT send, suppress, or alter any notification. Showing a
  happening is not the same as telling someone about it.

### Key Entities *(include if feature involves data)*

- **Team happening**: One thing that occurred inside a team. Carries its kind, the moment it
  occurred, the named values needed to phrase it, and an optional link destination. Heterogeneous
  in origin, uniform in shape. Always members-only.
- **Happening kind**: The closed set of four — member joined, team recognition awarded, training
  series created, training session cancelled. Each determines which named values are populated
  and which prose key renders it.
- **Team membership**: Who belongs to a team, in what role, since when. Records the join moment;
  retains nothing about departures or role transitions.
- **Team recognition award**: A badge or achievement earned by a team, with the moment earned.
- **Training series**: A team's recurring or one-off training definition, with its creation
  moment and its own visibility.
- **Training session**: A dated occurrence of a series, with a scheduled/cancelled state and the
  moment of cancellation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A player who joins a team appears in that team's new section on the next load,
  for members — the specific gap reported in issue #178 is closed and verifiable in one scenario.
- **SC-002**: 100% of the entries visible in the event section before this feature are still
  visible and unchanged after it. Zero regressions in the event history.
- **SC-003**: A signed-in non-member sees zero internal happenings, verified per kind, both in
  the page and by requesting the data directly.
- **SC-004**: Creating a two-year weekly recurring training adds exactly **one** entry to the
  section, not one per session.
- **SC-005**: The section never renders more than 10 entries, and never an entry older than 30
  days.
- **SC-006**: Every entry kind renders correct, complete prose in all three languages, with no
  English text appearing in the German or Spanish section, verified by key-parity checks.
- **SC-007**: The section renders without horizontal scrolling at 375 px for every entry kind,
  including the longest German wording.
- **SC-008**: Loading a team page for a member is no slower than a budget agreed in the plan,
  measured against a team holding history in all four kinds.
- **SC-009**: The dashboard feed's output is byte-identical before and after this feature for
  the same fixture data.
- **SC-010**: A reader shown only the two section headings, in each of the three languages, can
  correctly say which one lists events — validating that FR-017's rename did its job.

## Assumptions

- **The two sections stay disjoint.** No happening appears in both. Events are the event
  section's subject; the four internal kinds are the new section's.
- **The internal section sits on the team page**, not on a route of its own. Decision D4 rules
  out a full-history page, so there is nothing to navigate to.
- **A hard recency cutoff is acceptable** because the event history is untouched in its own
  section. A quiet team's page is not blank; only the "lately" section is.
- **"Public" means signed-in**, never anonymous — the team detail surface has required
  authentication since feature 026.
- **Members see the same section regardless of role.** Admins get nothing extra here; admin
  tools already have their own place on the page.
- **Whether entries are computed on demand or written down as they happen** is an implementation
  question for the plan. Decision D1 removed the only kinds that would have *forced* new
  recording, so a persisted table must justify itself on cost alone, not on capability.
- **Entries carry no counts, avatars, or actions** — a sentence and a moment, matching the
  dashboard feed.
- **Existing members-only gating is reused** rather than a new access rule being invented.

## Implementation Notes & Drift (2026-08-11)

Recorded during implementation. Nothing here changes a requirement; it documents what was found.

- **No drift on any FR.** All 29 requirements were implementable as written. No entity, column,
  migration, dependency, outbound call, or write path was added, as FR-010 requires.
- **A banned player's *handle* is suppressed as well as their name.** FR-025 asked only that
  identity not be disclosed; in practice the `PlayerProfiles` ban filter removes both, so the
  entry also loses its link target and renders as plain text. This is stronger than the
  requirement and is now asserted by test.
- **The renamed card's `data-testid` was left as `activity`.** Only its heading changed
  (FR-017). Renaming the hook would be churn with no test depending on it, but it is now
  mildly stale — worth tidying if that card is next touched.
- **The three catalogues are not Prettier-clean**, and neither is the rest of the frontend
  (18 untouched dashboard files fail the same check). New code was formatted to match its
  neighbours rather than reformatted, so no unrelated churn entered the diff.
- **`nx` commands do not work in a git worktree here.** `nx test` / `nx build` resolve the
  workspace root to the *main* checkout and report that tree's results — a real hazard, since a
  suite can appear green while never having run the branch's code. Verification used Jest and
  the Angular compiler directly. Worth a follow-up so CI and local runs cannot diverge silently.

## Resolved Decisions

All five were put to the owner on 2026-08-11 and answered. D5 is the one that reshaped the
feature.

### D1 — Derivable kinds only

**Decision**: Ship only kinds reconstructible from existing records. Departures, removals, and
role changes are excluded.

**Rationale**: They are the only candidates the platform cannot reconstruct — memberships are
hard-deleted, roles overwritten in place. Shipping them would mean new recording on every
membership-delete and role-update path, and would place two permanently-thin kinds beside real
history.

**Consequence**: Follow-up issue, not silent scope. Adding them later requires the recording
first. *(FR-009, FR-010)*

### D2 — Award appears as a happening; the card keeps the standing collection

**Decision**: Team awards are an entry kind, phrased as a dated happening. The "Badges &
achievements" card stays and keeps showing the standing collection without dates.

**Rationale**: The two answer different questions — "what has this team been up to lately?"
versus "what has this team earned?" — and the trophy display is worth keeping at a glance.

**Consequence**: One award is described twice on one screen for members. The arrangement must
make the card read unmistakably as a trophy shelf, not a second log. This is the feature's main
UI risk; DESIGN.md governs. *(FR-019, FR-020)*

### D3 — Training: series created, plus session cancelled

**Decision**: One entry when a training series is created; one entry per cancelled session.
Never an entry per generated session.

**Rationale**: Creating one weekly recurring training writes **up to 520 session rows in a
single save**, all sharing one timestamp. A per-session "scheduled" kind would bury every other
kind and would make the ordering tie-break load-bearing for no benefit.

**Consequence**: A one-off training still produces exactly one "series created" entry, which is
the correct reading. *(FR-006, FR-007, SC-004)*

### D4 — Bounded window, no full-history surface

**Decision**: Show the last **30 days**, at most **10** entries, as **hardcoded constants**. No
pagination, no "show more", no dedicated route. The existing paginated event endpoint is left
alone.

**Rationale**: Owner: *"we only need a few entries, ideally with a date cutoff"* and, on whether
to make them tunable: *"I don't think that we need to make the number of entries/days
configurable, you can just hardcode it and we can change it later when we feel this doesn't fit
anymore."* A catch-up section answers "what did I miss?", which is inherently recent and short;
a settings surface for two numbers nobody has yet wanted to change is cost without a payer.

**Consequence**: Issue #178's User-Story-3-style "read back through the whole history" is
explicitly **not** a goal. Changing either number later means editing code and shipping a
release — accepted. The two constants must live in one named place rather than being scattered
across query, interface, and tests, so that edit stays a one-liner. *(FR-011, FR-012, FR-013,
FR-018)*

### D5 — Two features, not one merged feed *(the reshaping decision)*

**Decision**: Do **not** merge everything into the existing section. The existing "Recent
activity" section stays a public-facing list of events the team played — as designed, it is
correct. Build a **separate, members-only** "What's happening" section for internal happenings.

**Rationale**: Owner: *"I think these are two completely separate features. The activity is a
public facing list of events etc, so the old spec is true. What I want is a team-internal
'What is happening' section which shows the internal events like joiners, training
cancellations, awards."* The two answer different questions for different audiences; merging
them forced a per-kind visibility matrix, a leak path through team-only trainings, and a
recency window that would have destroyed the public event history.

**Consequence**: Three of the merged design's hardest problems dissolve — no visibility matrix,
no training leak path, no regression risk from a recency cutoff. The cost is one new section on
the page and the rename in FR-017, without which the page now carries two similarly-named
sections instead of one badly-named one. *(FR-001, FR-008, FR-016, FR-017)*
