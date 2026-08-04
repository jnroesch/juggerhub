# Phase 0 Research: Browse Public Trainings

**Feature**: 043 | **Date**: 2026-08-04 | **Spec**: [spec.md](./spec.md)

Every finding below was verified by reading the code named in it, not from memory. Line references
are against `main` at `521f93d`.

---

## R1 — FR-024: a recurring series under the nearest-first ordering *(resolves the spec's one open question)*

**The problem, stated precisely.** The two existing proximity sorts order by
`orderby d.DistanceKm, <entity>.Id` (`TeamSearchService.cs:112`, `EventSearchService.cs:115`). For
trainings the natural stable equivalent is `d.DistanceKm, s.SessionDate, s.Id`. Distance is a
property of the *city*, so every session in the nearest city sorts ahead of every session in the
second-nearest — and within a city, sessions interleave by date across teams. With 20 rows per page,
page 1 is "the nearest city's next 20 sessions". Two teams training weekly in that city fill it with
ten weeks of themselves, and the viewer never reaches city 2.

Note this is slightly *better* than the spec's framing: teams in the same city do interleave, so it
is one **city** that monopolises the page, not one team. It is still wrong for a screen whose
promise is "trainings near me".

**Decision**: order by `(DistanceKm, SessionDate, Id)` **and**, when the viewer switches to
nearest-first without having set an explicit date range, default the range's upper bound to
**today + 14 days**. The bound is applied as an ordinary date filter, is rendered as a normal
removable filter chip, and the viewer can widen or clear it like any other.

**Rationale**:

- It keeps **one row per session everywhere**, so the owner's decision stands unqualified and the
  two sort modes return the same *kind* of row.
- A weekly series contributes 2 rows to a 14-day window, bi-weekly 1, monthly 0–1. A 20-row page
  therefore shows roughly 7–10 distinct trainings — enough to compare teams, which is what
  nearest-first is for.
- The mechanism already exists and is already visible. `From`/`To` are in the query contract, the
  chip row already renders a date chip, and `removeChip('dates')` already clears it. The whole
  change is one conditional default plus a translated chip label.
- It is honest. The alternative ways to keep a page diverse — grouping, or a per-series cap — both
  change the result set *invisibly*. A date chip states exactly what was done and undoes it in one
  tap.

**Alternatives considered**:

- **(a) Under nearest-first only, show each series' next upcoming session.** The best raw UX, and
  what was originally recommended for the whole feature. Rejected here because it makes a row mean
  something different in one sort mode than the other, and because the owner explicitly chose
  sessions over series; reintroducing series rows through a side door contradicts that decision
  rather than implementing it.
- **(c) Accept it as-is.** Defensible — someone sorting by distance is choosing a team, so that
  team's schedule is arguably the point. Rejected because the entry point is a home-screen button
  for a player with *no* team, who needs to compare options, not read one team's calendar.
- **A per-series row cap per page.** Rejected: the cap is arbitrary, it makes `Skip`/`Take` paging
  unstable (the cap depends on what is already on the page), and the dropped rows are invisible.

**Reversibility**: the decision is one `if` plus one i18n key, local to the proximity path; it does
not touch the projection, the DTO, or the row.

> ### ✅ SUPERSEDED BY OWNER DECISION, 2026-08-04 — there is no window at all
>
> **The owner rejected the window outright**, choosing alternative **(c)** below: "when selecting
> nearest first the system automatically adds the 14 days filter which does not make sense."
>
> That is right, and the reasoning above under-weighted it. A **sort** control that silently
> applies a **filter** is surprising in a way no chip fully repairs — the viewer asked for a
> different ordering and got a different result set. And the premise was thin: someone choosing
> "nearest first" is asking which teams train within reach, and truncating that to a fortnight
> answers a question they did not ask.
>
> `onSortChange` now sets the sort and nothing else. The `chipNearbyWindow` key is removed from all
> three catalogues, and a unit test pins that switching sort leaves `from`/`to` untouched.
>
> The density concern remains real and is now simply accepted, as FR-024 records. If it ever bites,
> alternative (a) — one row per series under this sort only — is still the better remedy than a
> hidden filter.
>
> <details><summary>Superseded reasoning, kept for the record</summary>
>
> ### ⚠ REFINED DURING IMPLEMENTATION — the window lives in the frontend, not the service
>
> The plan put the 14-day default in the service and echoed the effective range back on the
> response, which meant adding `appliedFrom`/`appliedTo` to the page envelope and breaking the
> uniform `PagedResult<T>` shape the constitution asks for.
>
> It is simpler and more honest in the component: `onSortChange('Proximity')` sets the `to` signal
> to today + 14 days, so the bound travels as an ordinary `to` query parameter, renders through the
> **existing** chip machinery, and clears through the **existing** `removeChip('dates')`. No
> envelope change, no server special-case, and the value is genuinely in the filter state rather
> than being a hidden server behaviour the client has to describe.
>
> Consequence, accepted: a direct API call with `sort=Proximity` and no `to` gets no window. That is
> correct — FR-024 is a requirement about the browse *surface*, not a security or correctness
> boundary, and the API staying unopinionated is a feature.
>
> The component tracks `autoWindowApplied` so switching back to soonest-first drops the window
> instead of silently narrowing that view; both behaviours are pinned by a unit test.
>
> </details>

⚠ **This resolves a `[NEEDS CLARIFICATION]` marker by decision, not by owner answer.** It is flagged
in `plan.md` under Open Decisions so it can be overridden cheaply before implementation starts.

---

## R2 — "Upcoming" is day-granular, and that is the existing convention

**Finding**: every trainings query in the codebase filters upcoming as `s.SessionDate >= today`
against a `DateOnly` — `TrainingSeriesService.cs:138,172,196,296`, `TrainingResponseService.cs:146`.
Events use a different rule, `e.EndsAt >= now` against a `DateTime` (`EventSearchService.cs:44`),
because an event *has* an absolute end instant. A training session stores `SessionDate` (`DateOnly`)
plus an effective `EndTime` (`TimeOnly`, itself an override expression), which cannot be composed
into a comparable instant in SQL without materialising every row.

**Decision**: filter `s.SessionDate >= today` where `today` is `DateOnly.FromDateTime(DateTime.UtcNow)`.

**Consequence, stated plainly**: a session that ended three hours ago still appears until midnight
UTC. Spec FR-006 says "not yet ended"; the implementation delivers "not on an earlier day".

**Why accept it**: it matches what every other trainings surface already does, so a session does not
appear on the team tab and vanish from browse (or the reverse) on the same afternoon. The spec's own
edge case — "a session happening right now counts as upcoming" — is satisfied by construction. The
alternative (materialise and post-filter) breaks pagination, because `Skip`/`Take` would run before
the filter.

**Recorded as spec drift** in `plan.md`; FR-006's wording should be read as day-granular.

---

## R3 — The address block, and the one field where `??` is correct

**Finding**: `TrainingSession`'s four address override columns resolve as an indivisible block keyed
on `CityIdOverride` (`TrainingSession.cs:41-59`, `TrainingSeriesService.RowProjection` at
`TrainingSeriesService.cs:417-439`). Writing `VenueNameOverride ?? Training.VenueName` is a defect.

**But the city itself is different.** The block is *keyed* on the city, so for the city — and only
the city — the two forms are equivalent:

```csharp
s.CityIdOverride != null ? s.CityIdOverride : s.Training.CityId   ==   s.CityIdOverride ?? s.Training.CityId
```

**Decision**: use the explicit ternary form everywhere anyway, including for the city id in the
proximity join and the city name in the city filter. The `??` shorthand is correct here but reads
exactly like the defect the entity comment forbids, and a reviewer following that comment would be
right to flag it. Consistency costs nothing.

**Consequence for filtering**: the city and country filters must resolve the block in the `WHERE`
clause, not against `Training.City` alone. A session relocated to another city must match a filter
on *its* city and must not match one on the series' city (spec SC-004, edge case 1).

---

## R4 — The location label is composed in memory, after materialisation

> ### ⚠ CORRECTED DURING IMPLEMENTATION
>
> **The conclusion below — "call `LocationLabelFor`" — was wrong, and the SC-003 integration test
> caught it.** `LocationLabelFor` → `HomeProjections.LocationLabel` returns the **city alone**
> ("Berlin"), while `EventSearchService`'s browse card builds `"City, Country"` inline
> (`EventSearchService.cs:133-140`) → "Berlin, Germany". Calling the shared helper produced two
> visibly different browse lists — the precise opposite of what SC-003 asks for.
>
> The real situation is that there are **two labels on two surfaces**, and both are internally
> consistent already:
>
> | Surface | Form | Events | Trainings |
> |---|---|---|---|
> | Browse row | `"City, Country"` | inline in the card projection | now via `LocationLabels.Display` |
> | Dashboard agenda | city alone | `HomeProjections.LocationLabel` | `HomeProjections.LocationLabel` |
>
> Feature 042's "one shared helper makes SC-003 structural" claim was about the **agenda**, and this
> plan over-generalised it to browse. The implementation uses `LocationLabels.Display` — the same
> formatting helper the `"City, Country"` form comes from — and `TrainingSearchService.BrowseLocationLabel`
> carries the whole explanation so the next reader does not "fix" it back.
>
> The rest of this finding (two-step raw→memory projection, why the helper cannot run in SQL) stands.

**Finding**: `TrainingSeriesService.LocationLabelFor` (`:465`) delegates to
`HomeProjections.LocationLabel` (`HomeProjections.cs:37`), which uses `string.IsNullOrWhiteSpace`
and **cannot be translated to SQL** — the XML doc at `TrainingSeriesService.cs:393-398` says so
explicitly. The established shape is a two-step: project a raw record in SQL
(`SessionRowRaw`, `:399`), then compose the DTO in memory (`ToRow`, `:442`), paging in SQL between
the two (`PageRowsAsync`, `:381`).

**Decision**: the browse service uses the same two-step. Page in SQL, compose the label in memory.

**Why it matters for SC-003**: the identical-label guarantee is structural only if there is one
implementation. `LocationLabelFor` is `internal static` and the new service lives in the same
assembly, so it is callable directly. **Do not copy it** into `Services/Search/`; a second copy is
exactly how the two labels drift apart.

**Note**: `LocationLabelFor` returns `string.Empty` for a virtual training (Training must keep
`null`/empty where Event returns `"Online"` — 042's deliberate divergence). The frontend renders the
"Online" wording from `locationKind`, as the trainings tab already does.

---

## R5 — Proximity: the two existing implementations disagree, and Teams is the correct one

**Finding**: both proximity paths inner-join `CityDistances` anchored on the home city, which
silently drops rows with no cached distance. They differ in the total they report:

- `TeamSearchService.cs:94-97` recomputes the total with the *same* exclusion
  (`t.CityId != null && _db.CityDistances.Any(...)`), so the count matches the page.
- `EventSearchService.cs:92-95` filters only `e.CityId != null` and then counts, **before** the
  join. If a distance row were missing, the reported total would exceed what the view can ever
  produce, and "load more" would stall short of the count.

`CityService.cs:232` writes distance rows at city-creation time (bidirectional plus a self-row), so
in practice the set is complete and the events bug is latent, not live.

**Decision**: follow **TeamSearchService**. Compute the proximity total with the same `Any()`
predicate as the join.

**This is how FR-023 is satisfied**: a session whose distance is unknown is excluded *and* not
counted, so it does not "vanish without trace" — the count tells the truth about what the view
contains, which is the same treatment virtual sessions get.

**Out of scope**: fixing the events count. It is a real latent defect but touching it would violate
FR-030/SC-010. → filed as a follow-up in `plan.md`.

---

## R6 — Where the endpoint lives

**Finding**: browse endpoints hang off the entity's own controller as the root `HttpGet` —
`GET /api/v1/teams` (`TeamsController.cs:82`), `GET /api/v1/events`, `GET /api/v1/profiles`
(`ProfilesController.cs:64`). `TrainingsController` is routed at `api/v{version}/trainings`
(`:22`) and has **no root `HttpGet`** — its routes are all `{trainingId:guid}` or `sessions/...`.

**Decision**: add `GET /api/v1/trainings` to the existing `TrainingsController`, delegating to a new
`ITrainingSearchService` in `Services/Search/`. No new controller.

**Route-collision check**: the root `[HttpGet]` cannot shadow `sessions/{id}` or `{trainingId:guid}`
— those carry literal or constrained segments and rank higher in ASP.NET Core's route table. Verified
by reading the existing attributes; a smoke assertion is included in the quickstart anyway.

**Auth**: `TrainingsController` already carries a class-level `[Authorize]` with the JWT scheme
(`:23`), so FR-007 is satisfied by placement — no `[AllowAnonymous]`, and none of the feature-026
OpenAPI-allowlist hazards apply.

---

## R7 — The 409 for a missing home city is a controller concern

**Finding**: `TeamsController.cs:88-104` resolves the caller *before* looking at any query value,
then resolves the home city via `_profiles.GetHomeCityIdAsync` and returns `409` when it is null,
with the service treating a null anchor as "fall back to the default order" as a second line of
defence (`IEventSearchService` remarks, `EventSearchService.cs:14-18`).

**Decision**: copy that shape exactly, including the belt-and-braces service fallback. The 409 detail
string becomes "Set your home city to sort trainings by distance."

This satisfies FR-021 with no new mechanism, and the frontend already handles it: `BrowseList` shows
the error state on any failed fetch, and the sort option is not offered without a home city
(`browse-events.component.ts:44-53`), so the 409 is only reachable by a hand-made request.

---

## R8 — The city filter is a genuine gap in the existing browse pages

**Finding**: `EventBrowseQuery` and `TeamBrowseQuery` both declare `City` **and** `Country`
(`SearchDtos.cs`), and `EventSearchService.cs:64-77` implements both. But the frontend never sends
`city` — `toEventParams` (`search.service.ts:59-71`) has no `city` line, and
`browse-events.component.ts:228` maps its misleadingly-named `city` signal onto `country:`, feeding
a `jh-country-picker`. **No browse page in the product offers a city filter today.**

**Decision**: trainings ship **both** — `jh-city-picker` for the city and `jh-country-picker` for the
country — and `toTrainingParams` sends both.

**Rationale**: spec FR-014 and FR-015 both require it, and city is the filter that matters for a
weekly local practice in a way it does not for a national tournament. SC-005 permits it: it requires
identical *behaviour*, "differing only in the filter set and what a row contains".

**Fit check**: `jh-city-picker` is `@Input() initial` / `@Output() selectedChange` with plain
`FormsModule` (`city-picker.component.ts:20-36`) — form-API agnostic, as 042 established. It emits a
`CityOption`; the filter sends `option.name` as the `city` query value, which the backend matches
accent-insensitively against `City.Name`.

**Not in scope**: retrofitting a city filter onto teams/events (FR-030).

---

## R9 — The fourth tab is the one real UI risk

**Finding**: the tab strip is three hardcoded `<a>` elements, each `flex-1`, inside a single flex row
(`browse-shell.component.html:9-28`). A fourth divides the row into four `flex-1` cells. At 375px,
inside the shell's `px-md` padding, each cell is roughly 80px of usable width. Current labels:

| key | en | de | es |
|-----|----|----|-----|
| `browse.tabTeams` | Teams | Teams | Equipos |
| `browse.tabEvents` | Events | Events | Eventos |
| `browse.tabPlayers` | Players | Spieler | Jugadores |
| `browse.tabTrainings` *(new)* | Trainings | Trainings | Entrenamientos |

Spanish is the binding case: "Entrenamientos" (14 chars) alongside "Jugadores" (9) will not fit an
~80px cell at the current `text-body-sm`. English and German are borderline rather than broken.

**Decision**: do not solve this in the plan. Treat it as a DESIGN.md question resolved during
implementation, with the constraint that **the fix must not be a smaller font or a truncation** —
DESIGN.md governs, and an unreadable or clipped tab fails FR-026/SC-008.

Candidate directions, to be chosen against DESIGN.md at implementation time: a horizontally
scrollable strip with the active tab scrolled into view; a 2×2 grid under `sm`; or shorter Spanish
copy. Recorded as an implementation-time decision, not a spec question.

**Verification**: SC-008 is checked with the UI review checklist (constitution gate 7) plus a
Playwright assertion at 375px in each language, since this is precisely the kind of regression that
passes a desktop review.

---

## R10 — Principle VII is not engaged

**Finding**: this feature adds no outbound HTTP call. `CityService` resolves against the seeded local
`CityReference` table (042's finding: "a local SQL query, not an external geocoder"; Photon was never
deployed), the distance cache is a local table, and the only new network hop is browser→backend
`GET`, which the existing interceptor stack already covers.

**Decision**: no `HttpClient`, no retry policy, no circuit breaker, no new resilience configuration
belongs in this diff. Constitution gate 8 is satisfied by not engaging it.

⚠ This is the same trap 042 called out. A reviewer reaching for `AddJuggerHubResilience` because the
feature says "search" and "distance" would be adding a handler around a `SELECT`.

---

## R11 — i18n parity is already guarded

**Finding**: `catalog-parity.spec.ts` (added by 042) walks the whole parsed tree of
`public/i18n/{en,de,es}.json` and asserts identical key sets, excluding only `_meta`. It is generic,
so every key added under `browse.trainings.*` is covered the moment it exists in `en.json`.

**Decision**: add nothing. SC-009 is satisfied by the existing guard — but **run it**, do not assume
it (036's lesson, restated in 041). A key added to `en.json` alone turns it red, which is the point.

---

## R12 — What the row shows, and what it deliberately does not

**Finding**: `TrainingSessionRowDto` carries `GoingCount`/`MaybeCount`/`CantCount` and `MyAnswer`
(`TrainingDtos.cs:13-30`). Computing them costs three correlated subqueries per row
(`RowProjection`, `:435-438`), each re-evaluating the visibility expression.

**Decision**: the browse card carries **none** of them. A new `TrainingCardDto` is defined for this
surface with only what FR-008 requires: session id, training id, name, team slug + name, one-off
flag, date, start/end time, location kind, location label.

**Rationale**: the events browse card is equally lean (`EventCardDto` has no signup counts). FR-008
does not ask for attendance, three subqueries per row on a cross-team query is real cost for
decoration, and "9 going" on a discovery card invites reading it as capacity — a concept trainings
do not have (spec Assumptions). The counts are on the session page, one tap away.

**Team identity**: the card carries `teamSlug` + `teamName`. `TrainingSession.TeamId` is
denormalised (`TrainingSession.cs:21-22`), so this is a single join, and the slug is what makes the
team name a link.

---

## Resolved unknowns summary

| # | Question | Resolution |
|---|----------|------------|
| R1 | FR-024 recurrence flooding | `(distance, date, id)` + a 14-day default upper bound under nearest-first, shown as a removable chip — **decided, not owner-answered** |
| R2 | "Upcoming" granularity | `SessionDate >= today`, day-granular, matching 018 — recorded as spec drift on FR-006 |
| R3 | Address block in filters | Explicit ternary everywhere; city id is the one coalescable field but is written the long way anyway |
| R4 | Label composition | Two-step raw→memory, reusing `LocationLabelFor`; never copy it |
| R5 | Proximity total | Follow `TeamSearchService`, not `EventSearchService` (latent defect, left alone) |
| R6 | Endpoint placement | Root `GET` on the existing `TrainingsController`; auth inherited |
| R7 | Missing home city | Controller-level 409, service falls back defensively |
| R8 | City filter | Ship city + country pickers; first city filter in the product |
| R9 | Fourth tab at 375px | Implementation-time DESIGN.md decision; no font shrink, no truncation |
| R10 | Resilience | Not engaged — no outbound call |
| R11 | i18n parity | Existing generic guard covers it; run it |
| R12 | Card contents | Lean card, no RSVP counts |
