# Phase 0 Research: Team-internal "What's happening" section

**Feature**: 044 | **Date**: 2026-08-11 | **Plan**: [plan.md](./plan.md)

Every decision below was settled by reading the code, not by preference. File and line
references are to the state of `044-team-activity-feed` at the time of writing.

---

## R1 — Do not reuse `ActivityEntryDto`; introduce a team-scoped DTO

**Decision**: Add `TeamHappeningKind` / `TeamHappeningParamsDto` / `TeamHappeningDto` in
`backend/Dtos/Teams/TeamHappeningDtos.cs`. Copy the *pattern* of
`backend/Dtos/Home/HomeDtos.cs`; share no type with it.

**Rationale**: This is issue #178's open question 2, and the answer is forced by the consumer.
`ActivityKind` is a closed enum with six members that `ActivityListComponent` switches over
exhaustively, ending in `default: return ''` with the comment *"An unrecognized kind (a newer
server) yields no sentence — drop it rather than render a blank line."* Adding
`TrainingSeriesCreated` to that enum makes the dashboard silently discard rows it can never
receive, and every new team kind would widen `ActivityParamsDto` with fields the dashboard never
populates. The two feeds also have genuinely different semantics: home entries are scoped *to
the viewer* ("a teammate", `IsMine`), team entries are scoped *to the team* and have no notion
of "mine".

**Alternatives considered**:
- *Reuse `ActivityEntryDto` as-is* — rejected: cannot express "training series created" or
  "session cancelled on date X" without new params, and couples two feeds that will diverge.
- *Extract a shared generic `FeedEntry<TKind>`* — rejected: an abstraction over two call sites
  that share four lines of structure, against the constitution's "avoid unnecessary
  abstractions". The duplication is a record declaration; the coupling would be permanent.

**Consequence**: `home.activity.*` keys are untouched (FR-027) and the new keys live under
`teams.detail.happening.*`.

---

## R2 — Player identity: sub-project, return `null`, let the client translate

**Decision**: Read the actor's name and handle with the sub-projection pattern from
`HomeService.LoadActivityAsync`:

```
ActorName = _db.PlayerProfiles.Where(p => p.UserId == m.UserId).Select(p => p.DisplayName).FirstOrDefault()
```

Return `null` when there is none. Do **not** call `MemberPlaceholder`.

**Rationale**: three separate findings converge here.

1. **Banned accounts.** `AppDbContext.cs:149` puts
   `HasQueryFilter(p => p.User.Status != AccountStatus.Banned)` on `PlayerProfiles`. Navigating
   `m.User.Profile!.DisplayName` from a membership row therefore behaves differently from the
   sub-projection for a banned member. The sub-projection yields `null`, which is exactly what
   FR-025 wants — suppression, not disclosure.
2. **Deleted accounts.** Feature 037 neutralises profile columns rather than deleting the row.
   The same `null`-or-empty path applies, and the client's translated stand-in covers it.
3. **Which stand-in.** `TeamNewsService` resolves the request culture server-side
   (`MemberPlaceholder.For(_culture.ResolveFromRequest())`) to render "A former player". The
   activity pattern deliberately does not: `ActivityParamsDto`'s doc comment states that a
   server-composed summary *"would be English on a German dashboard, and no key-parity guard
   could ever catch it because there would be no key."* FR-021 and FR-024 codify that. So the
   feed follows `HomeService`, not `TeamNewsService`.

**Alternatives considered**:
- *Use `MemberPlaceholder` for consistency with team news* — rejected: it is the wrong
  consistency. News is server-rendered prose already; this feed is explicitly not.
- *Filter out entries with no name* — rejected: the join happened; hiding it because the person
  was later banned would silently shrink the feed below its cap for no stated reason.

**Bonus consequence (edge case, free)**: because the join entry is derived from the live
`TeamMemberships` row, a member who leaves takes their join entry with them. The spec's "a
player who joined is no longer a member" edge case is satisfied structurally. This only holds
while the feed is derived — a persisted snapshot would reintroduce the bug.

---

## R3 — Training: read `Trainings.CreatedDate`, never `TrainingSessions.CreatedDate`

**Decision**: The `TrainingSeriesCreated` kind reads the **series** row. The
`TrainingSessionCancelled` kind reads **session** rows filtered to
`Status == TrainingSessionStatus.Cancelled`.

**Rationale**: `RecurrenceExpander.MaxSessions` is **520** (`~10 years weekly`,
`backend/Services/Trainings/RecurrenceExpander.cs:20`), and `TrainingSeriesService` materialises
the expanded dates as real `TrainingSession` rows in one save. A per-session "scheduled" kind
would therefore emit up to 520 entries sharing a single timestamp — burying every other kind,
making the tie-break load-bearing for no benefit, and blowing the 10-entry cap on one action.
Owner decision D3 settles this; the number is why.

Cancellation is safe to read per-session: `TrainingSessionService.CancelAsync` (line 206) sets
both `Status` and `CancelledDate` via `ExecuteUpdateAsync`, and refuses when the session is past
or not `Scheduled` — so cancellations are individual, deliberate, and rare.

**Alternatives considered**:
- *Group session-scheduled entries into "N sessions added"* — rejected: it is the same
  information as "a series was added", stated more confusingly, and needs a count param.
- *Read `TrainingSession.CreatedDate` and de-duplicate by `TrainingId`* — rejected: does the same
  job as reading the series, with a `GROUP BY` and a wrong date (the session's, not the series').

**Verified**: `TrainingSession.TeamId` is denormalised on the session
(`backend/Entities/TrainingSession.cs:22`), so the cancellation query filters directly without
joining through `Training`.

---

## R4 — Awards: filter `Status == AwardStatus.Active`, read `EarnedAt`

**Decision**: Query `BadgeAwards` and `AchievementAwards` separately, both filtered
`TeamId == teamId && Status == AwardStatus.Active && EarnedAt >= cutoff`, and merge them into
one `RecognitionAwarded` kind carrying `Definition.Name`.

**Rationale**: Both entities are polymorphic (`PlayerProfileId` XOR `TeamId`, DB CHECK), so
`TeamId != null` is what makes it a team award. Their doc comments state *"revoked rows are
retained for audit and allow a later re-grant"* — so without the `Active` filter the section
would keep announcing an award the team no longer holds, contradicting the spec's
"a badge is revoked" edge case. `EarnedAt` is set to `DateTime.UtcNow` at grant time in both
`BadgeService.cs:295` and `AchievementService.cs:292` — **never backdated**, so it is a sound
basis for a 30-day window.

**Alternatives considered**:
- *One kind per award type (`BadgeAwarded` / `AchievementAwarded`)* — rejected: doubles the
  i18n keys for a distinction the reader does not need in a one-line log. The existing
  `jh-recognition-display` card is where the two are told apart.
- *Reuse `IRecognitionService.ForTeamAsync`* — rejected: it returns the full standing collection
  with icons for the card, unbounded by date, and gives no per-award moment in the shape needed.

---

## R5 — The window predicate uses each kind's own moment

**Decision**: `occurredAt` per kind is `TeamMembership.JoinedDate`, `BadgeAward.EarnedAt`,
`AchievementAward.EarnedAt`, `Training.CreatedDate`, `TrainingSession.CancelledDate`. Each
query's 30-day predicate is written against that same column.

**Rationale**: `CreatedDate` from `BaseEntity` is uniform and therefore tempting, and it is
wrong for two of the five. A session's `CreatedDate` is when the series generated it — possibly
years before it was cancelled — so a cancellation dated by `CreatedDate` would fall outside the
window and never appear, while the row itself is brand-new information. `JoinedDate` and
`EarnedAt` exist precisely because the domain moment differs from the row's insertion moment.

**Consequence**: the filter must be applied *per query*, not once over the merged list. Applying
it after the merge would still be correct but would read 5× more rows than needed.

---

## R6 — Ordering: `OccurredAt` desc, then a total tie-break

**Decision**: Each query orders and `Take(10)`s on its own moment; the merged list is sorted
`OccurredAt` descending, then by `Kind`, then by a stable per-entry string key, then `Take(10)`.

**Rationale**: FR-015 requires a repeatable total order. Collisions are realistic, not
theoretical: creating a training series and cancelling one of its sessions in the same request
batch, or a bulk award grant, can share a timestamp to the tick. Sorting on `OccurredAt` alone
leaves the residual order to whatever the merge produced, which is stable in practice and
guaranteed by nothing. Two extra sort keys cost nothing on ten rows.

**Alternatives considered**:
- *Sort in SQL via a `UNION`* — rejected: the five sources have different shapes and different
  sub-projections; EF cannot union them without a lowest-common-denominator projection, and the
  merge is over at most 50 rows in memory. `HomeService` merges the same way.

---

## R7 — Divergences from `ActivityListComponent`, deliberate and enumerated

**Decision**: `TeamHappeningsComponent` copies `ActivityListComponent`'s structure —
`toSignal(langChanges$)` to re-translate on language switch, `computed` rows, a `text()` switch
keyed on kind, a `link()` switch, `injectRelativeTime()` for the timestamp — with three changes:

1. **Renders an empty state instead of nothing.** The dashboard wraps everything in
   `@if (hasAny())`. FR-014 requires a visible "nothing lately" state, so the card always
   renders for a member and falls back to `jh-empty-state` — matching the sibling cards on the
   team page (`noActivity`, `noNews`), which is the local convention.
2. **Takes the team slug as an input.** `TrainingSeriesCreated` has no per-series route
   (`app.routes.ts` has `t/:slug/trainings` and `trainings/sessions/:id`, nothing for a series
   id), so the component builds `['/t', slug, 'trainings']` from its own input rather than the
   server shipping a `linkTarget` that resolves to nothing.
3. **No `IsMine` branch.** Team entries have no viewer-relative form; the sentences are all
   third-person about the team.

**Rationale**: copying wholesale would violate FR-014, and inventing a fresh structure would
lose the language-switch handling and the "drop unrecognised kinds" guard, both of which are
non-obvious and already correct.

---

## R8 — Endpoint shape: a new members-only action, not an addition to the page payload

**Decision**: `GET /teams/{slug}/happenings`, returning a bare `IReadOnlyList<TeamHappeningDto>`;
`null` from the service maps to the existing `TeamNotFound()` helper.

**Rationale**: the team page already works this way. `TeamPublicDetailDto` is the
signed-in-visible payload fetched by `getPublicDetail`, and members-only sections are loaded
*afterwards* from their own endpoints — `loadMembers()`, `loadNews()`, `loadPartyRequests()` in
`team-detail.component.ts:95-99`, each gated on `viewerRelation`. Putting happenings on the
public payload would mean shipping a members-only array inside the response every non-member
also receives, which is the exact mistake FR-002/FR-003 guard against; it would also make the
field null-or-absent for most callers.

Returning `null` for both "no such team" and "not a member" is deliberate and pre-existing:
`TeamMembershipGuard`'s doc comment states *"non-members are indistinguishable from unknown
teams to callers"*. Matching it keeps the enumeration property intact.

**Alternatives considered**:
- *Extend `GET /teams/{slug}` (`TeamDetailDto`, members-only header)* — rejected: it is a small
  header DTO fetched on a different path, and folding a list into it would put a five-query read
  behind every consumer of the header.
- *Reuse `GET /teams/{slug}/activity`* — rejected: that endpoint serves the **event** history
  (spec FR-018 keeps it as-is), and overloading it would recreate exactly the naming collision
  issue #178 complains about.

---

## R9 — Renaming the existing card

**Decision**: rename the i18n **keys** `teams.detail.recentActivity` → `teams.detail.recentEvents`
and `teams.detail.noActivity` → `teams.detail.noEvents`, updating all three catalogues and the
one template usage.

**Rationale**: FR-017 requires the heading to name events. Changing only the *value* would leave
a key called `recentActivity` holding "Recent events" sitting next to a genuinely
activity-shaped `happening.*` block — a trap for the next reader. The keys have exactly one
consumer each (`team-detail.component.html:152,163`), and `catalog-parity.spec.ts` fails loudly
if a rename is applied to one catalogue and not the others, so the rename is cheap and guarded.

**Wording** (sentence case per DESIGN.md, no Title Case):

| Key | en | de | es |
|---|---|---|---|
| `recentEvents` | Recent events | Letzte Events | Eventos recientes |
| `noEvents` | No events yet. | Noch keine Events. | Aún no hay eventos. |
| `happeningTitle` | What's happening | Was passiert gerade | Qué está pasando |

`Was passiert gerade` is deliberately **not** `Was ist los` — that is the dashboard's heading
(`home.activityTitle`), and reusing it would recreate the two-sections-one-name confusion in
German specifically, which is the language issue #178 was reported in. SC-010 checks this.

---

## R10 — Performance: five reads is the established shape, not a new cost

**Decision**: accept five queries; do not pre-aggregate, cache, or persist.

**Rationale**: `HomeService.LoadActivityAsync` issues five reads plus a sixth for notifications
on **every dashboard load** for every user, and has done since feature 025. This endpoint issues
five equivalently-bounded reads on a team page load, for members only, in a request parallel to
the page's own. Each has a `WHERE teamId = …` plus a date predicate and `Take(10)`.

Issue #178's open question 3 asked whether this is the point where a persisted activity table
earns its place. It is not: owner decision D1 removed the only kinds that would have *required*
writing (departures, role changes), so a table would buy nothing but fan-out writes on four
existing paths, a migration, and a backfill — while *losing* the derived feed's self-correcting
properties (a revoked award and a departed member both vanish for free, per R2/R4).

**Revisit if**: a future kind is not derivable, or profiling shows the merge dominating the team
page. Neither is true today.
