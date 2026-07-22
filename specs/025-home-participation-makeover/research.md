# Phase 0 Research: Home Participation Makeover

All decisions below are grounded in the existing feature-008 `HomeService`, the feature-010 notification spine, and the feature 016/017/018 service surfaces read during planning.

---

## R1 — Source of truth for "Needs you"

**Decision**: Aggregate "Needs you" from the **authoritative per-domain sources**, not the `Notification` table. Reuse existing services; `HomeService` composes their pending inboxes into one list.

| Actionable item | Source | Existing action to resolve inline |
|---|---|---|
| Pending team invite | Team invitation service (feature 002/005) | accept / decline invite endpoint |
| Party participation request (field a party for an event) | Party service (feature 016) | I'm-in / can't-make-it endpoint |
| Party co-admin invite | `IPartyInvitationService` | accept / decline |
| Marketplace invite **and** application | `IMarketRequestService.ListMineAsync` (already powers the current market card) | accept / decline (invite) — applications show pending |
| Near-window un-answered training | `ITrainingResponseService.GetMyAgendaAsync` filtered to `myAnswer == null` and session within `NearWindowDays` | going / maybe / can't (`SetResponseAsync`) |

**Rationale**: The `Notification` entity's own docs state the payload is *"never an authorization input"* — it is a display cache that can be marked read or dropped independently of whether the underlying item is still actionable. Reading source domains guarantees "Needs you" reflects *actual* pending state (a stale/read notification never leaves a ghost action, and a resolved-elsewhere item disappears on refresh — spec edge case). It also gives each row the correct action endpoint for in-place resolution (FR-003).

**Alternatives considered**:
- *Read actionable notification types (TeamInvite/PartyRequest/MarketInvite) from the Notification table.* Rejected: couples the actionable block to notification retention/read-state and risks ghost actions; violates the "authoritative source" intent.
- *New aggregate endpoint.* Rejected: the composite `GET /home` already fans out to services; adding a section is consistent and avoids a round-trip.

**Note for tasks**: The exact party-participation-request query surface (feature 016 "request to field a party") must be pinned to its service method during `/speckit-tasks`; the marketplace and training surfaces are already confirmed (`ListMineAsync`, `GetMyAgendaAsync`).

---

## R2 — Unified "Up next" agenda (events + trainings)

**Decision**: Introduce a `Kind`-discriminated **`AgendaItemDto`** (`Kind = Event | Training`) and return a single ordered `UpNext` list. Events carry the existing up-next fields (mode, viewer signup, team-going); trainings carry session id, date/time, location, and the viewer's answer. The frontend renders one timeline, branching card layout by `Kind`.

**Interaction with the near-window rule (FR-006a/b)**: Un-answered trainings whose session is within `NearWindowDays` (~14) are surfaced in **Needs you**, not Up-next. Therefore Up-next trainings are the **answered** ones plus **far-out un-answered** ones. An answered training moves from Needs-you into Up-next. This keeps each training in exactly one place at a time.

**Rationale**: The spec demands *one* participation agenda (FR-007). A discriminated item is the honest model and keeps ordering/paging in the backend (single sorted list) rather than asking the client to merge two arrays by date. `GetMyAgendaAsync` already yields the viewer's cross-team session agenda with the answer, so the training half is a projection, not new logic.

**Alternatives considered**:
- *Two parallel arrays (`upNextEvents`, `upNextTrainings`) merged client-side.* Rejected: breaks single-list "see all" pagination and duplicates ordering logic on the client.
- *Extend `UpNextItemDto` with nullable training fields (no `Kind`).* Rejected: a discriminator is clearer and matches the polymorphic-DTO patterns already used (e.g. notification payloads, event signups).

**See-all impact**: `ListUpNextAsync` (`GET /home/up-next`) must also merge trainings into its paged result, consistent with the composite.

---

## R3 — "What's going on" activity feed source

**Decision**: Build the activity feed **derive-on-read from domain tables**, with a small hybrid for pure state-changes — **no new activity table, no fan-out writes**. Read a bounded window per source, project to a common `ActivityEntryDto`, merge newest-first, cap for the home preview.

| Signal (FR-027) | Source | Scoping |
|---|---|---|
| Teammate signed up for an event | recent `EventSignup` rows where `TeamId ∈ myTeams` (or actor ∈ my teammates), excluding self | viewer's teams |
| New member joined one of my teams | recent `TeamMembership` rows in `myTeams`, excluding self | viewer's teams |
| Badge awarded | recent active `BadgeAward` to the viewer or to players in `myTeams` | self + teammates |
| Party member joined | recent `PartyMember` rows in the viewer's parties | viewer's parties |
| Role changed (state change) | viewer's passive `Notification` rows of type `TeamRoleChanged` | recipient = viewer |
| Training rescheduled / cancelled (state change) | viewer's passive `Notification` rows of type `TrainingUpdated` | recipient = viewer |

**Rationale**: Deriving social/participation signals on read mirrors exactly how the old (removed) "teams activity" was built, avoids writing N notification rows every time a teammate acts (fan-out), and keeps the feed free of retention coupling. The two pure *state-change* signals (role change, training reschedule/cancel) have no clean derivable domain row — they are mutations — so they are read from the already-persisted, already-scoped passive notification rows for that user. This is a deliberate, bounded hybrid.

**Exclusions**: `TeamNews`/`PartyNews`/`EventNews` notification types are **never** in this feed — they are authored broadcast and belong to News (SC-004: the two streams are disjoint). `TrainingScheduled` is treated as a "heads-up" that overlaps Needs-you/Up-next rather than passive history; it is **not** duplicated into activity (tasks may revisit, but default: exclude to avoid noise).

**Alternatives considered**:
- *Dedicated append-only `ActivityEvent` table all producers write to.* Rejected for v1: cleanest long-term but a real build (new entity, migration, producer wiring across features 005/006/012/016) for the lowest-priority (P3) section. Documented as the future path if the feed grows.
- *Read the entire feed from the Notification table (passive types) + emit new notification types for social signals.* Rejected: reintroduces fan-out writes for teammate signals.

**Pagination**: For v1 the activity feed is **home-preview only** (capped top-N in the composite), no dedicated "see all" — consistent with keeping it quiet and lowest-priority. If a full activity page is later wanted, add a paginated `GET /home/activity` (keyset by `occurredAt`). This keeps the constitution's pagination rule satisfied (no unbounded list is exposed).

---

## R4 — Party news in the News merge

**Decision**: Extend `LoadNewsAsync` to include `PartyNewsPost` rows for parties where the viewer is an `In` member, tagged `source = "party"` with the party/event context as the link target. Merge alongside team news and event news, newest-first.

**Rationale**: `PartyNewsPost` already exists and is defined as authored, crew-private broadcast (mirrors `TeamNewsPost`); it is exactly the "party leader informs everyone" content the spec calls out (US3). The `HomeNewsDto.Source` field already anticipates additional source values ("a future 'league' adds a value; contract unchanged"), so adding `"party"` is contract-compatible.

**Scoping (FR-023)**: Only `PartyMemberStatus.In` members may read party news — enforced in the query's `WHERE`, server-side.

**Alternatives considered**: None material — this is the intended extension point.

---

## R5 — Removals

**Decision**: Delete from the composite and its queries:
- **Tournaments** (`TournamentCardDto` + the all-published-tournaments query) — FR-015. It highlights events regardless of participation.
- **Team snapshots** (`TeamSnapshotDto`, `NextFixtureDto`, the desktop rail) — FR-016. Each team's next event is already an Up-next item once the agenda is unified.
- **Teams activity** (`TeamActivityDto` + the `TeamNewsPosts` re-read) — FR-017. It silently duplicated News. Replaced by the genuine activity feed (R3).
- **Standalone trainings card** and **market card** frontend modules — folded into Up-next / Needs-you (FR-018).

**Verify during tasks**: `NextFixtureDto` and `TeamActivityDto` are not referenced outside Home before deleting (grep). Discovery `open-to-everyone` (no-team variant) is **retained** (FR-028).

---

## R6 — Frontend restructure

**Decision**: Reorder `dashboard.component.html` to the four-section vertical priority (Needs you → Up next → News → What's going on), collapsing the desktop right-rail usage since snapshots/tournaments are gone. New components `needs-you-card` and `activity-list`; `up-next-card` extended to branch on `AgendaItem.kind`. Keep `.html`/`.css`/`.ts` separate (Principle VI). Reuse existing tokens/components per DESIGN.md — no new visual style (FR-032). Preserve loading skeleton + retry (FR-029) and per-section empty states (FR-030), and the no-team variant (FR-028).

**UI review**: Instantiate `checklists/ui-review.md` from `.specify/templates/ui-review-checklist-template.md` and check the diff before verification (Quality Gate 7). The known app-wide DESIGN.md contrast conflict (primary buttons white-on-coral) is pre-existing and out of scope here; do not regress beyond it.

---

## Resolved unknowns summary

| Question | Resolution |
|---|---|
| Needs-you: notifications or domains? | Authoritative domains (R1) |
| One agenda: how to unify events + trainings? | `Kind`-discriminated `AgendaItemDto` (R2) |
| Activity: new table, notifications, or derived? | Derive-on-read hybrid, no new table (R3) |
| Party news: does the content exist? | Yes, `PartyNewsPost`; add to News merge (R4) |
| Migration needed? | No — all reads from existing entities (R3/R5) |
| Activity "see all"? | Deferred; home-preview cap only for v1 (R3) |
