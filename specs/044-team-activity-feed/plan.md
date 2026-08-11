# Implementation Plan: Team-internal "What's happening" section

**Branch**: `044-team-activity-feed` | **Date**: 2026-08-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/044-team-activity-feed/spec.md`

## Summary

Add a **members-only "What's happening" card** to the team page listing the last 30 days of
team-internal happenings (max 10): who joined, what the team was awarded, a training series
added, a session cancelled. Rename the existing signed-in-visible "Recent activity" card to
name **events**, which is all it has ever contained, and leave its data path untouched.

**Technically this is small and additive.** One DTO file, one service behind an interface, one
controller action, one Angular component, ~9 i18n keys × 3 languages. **No entity, no migration,
no new dependency, no outbound call.** Every entry is derived on read from records that already
exist (spec D1/FR-010), so the section is correct for every existing team the moment it ships.

The shape is not invented — `HomeService.LoadActivityAsync` + `ActivityListComponent` are the
same pattern (N small queries merged newest-first; server sends `kind` + untranslated names, the
client composes the sentence). This feature copies that pattern with a **separate, team-scoped
DTO**, and the reasons for not reusing the home one are in [research.md](./research.md) R1.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Angular + Nx (frontend)

**Primary Dependencies**: EF Core 10 + Npgsql, Transloco (i18n), Tailwind CSS. **No new
dependency is added.**

**Storage**: PostgreSQL 18 — **read-only for this feature.** No table, column, index, or
migration is added. If a task ever produces an `Add-Migration`, something has gone wrong.

**Testing**: xUnit + `JuggerHub.Api.IntegrationTests` (WebApplicationFactory + Testcontainers)
for the backend; Jest for the Angular component and the i18n catalogue parity guard.

**Target Platform**: Linux containers on AKS (Dev/Prod), docker-compose locally.

**Project Type**: Web application — `backend/` (.NET API) + `frontend/apps/web` (Angular SPA).

**Performance Goals**: The new endpoint issues **five** indexed, `Take(10)`-bounded reads and
merges in memory — the same shape and count as `LoadActivityAsync`, which already runs on every
dashboard load. SC-008 budget: **p95 within the same order as `GET /teams/{slug}/news`** on a
team with history in all four kinds. It is a separate request, so it never blocks the team
page's first paint.

**Constraints**: Members-only, enforced server-side (Principle I). Hard bounds of **30 days**
and **10 entries** as compile-time constants, not configuration (spec FR-012). No pagination and
no "show more" (FR-013). Server must not compose prose (FR-021).

**Scale/Scope**: ~4 backend files (+1 test), ~5 frontend files (+1 test), 2 renamed and 7 new
i18n keys in each of en/de/es. One new endpoint. Zero schema change.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.4.0 before Phase 0, re-checked
after Phase 1.*

| Principle | Verdict | How this feature satisfies it |
|---|---|---|
| **I — Security-first, never trust the client** | **PASS** | Membership is resolved server-side by the existing `TeamMembershipGuard`; a non-member gets the same `404` a non-member already gets from news/roster/activity, so a team's existence is not confirmed to outsiders. The Angular `@if (isMember())` is UX only — the data never leaves the server for a non-member. No new error surface, no exception detail. |
| **II — Thin controllers, service-centric** | **PASS** | One controller action that resolves the user id, calls `ITeamHappeningService`, and maps `null → TeamNotFound()` — identical to the four actions beside it. All logic in the service, DI'd behind an interface. DTOs built with explicit `.Select` projections; **no object mapper**. |
| **III — Disciplined data access** | **PASS with a documented deviation** | All reads `AsNoTracking()` with explicit `.Select` projections; every query carries `Take(MaxEntries)` and a date predicate, so nothing is unbounded. No new entity, so the `BaseEntity`/UUIDv7 rule is not engaged. **Deviation**: the endpoint returns a capped list rather than a `PagedResult<T>` — see [Complexity Tracking](#complexity-tracking). |
| **IV — Auth & sessions** | **PASS** | Untouched. The endpoint inherits the global `FallbackPolicy` (feature 026); no `[AllowAnonymous]`, no cookie or token change. |
| **V — Environment parity** | **PASS** | No infrastructure, configuration, secret, or compose change. Behaves identically in local/Dev/Prod because the two bounds are constants, not settings. |
| **VI — Conventions & tooling** | **PASS** | New Angular component ships separate `.ts` / `.html` / `.css`. No `.sh` script is added. |
| **VII — Resilient by default** | **NOT ENGAGED** | **No outbound integration is added.** This is a local `SELECT` over the app's own database, reached by the browser through the existing interceptor. Reaching for `AddJuggerHubResilience` here would wrap a database read in an HTTP resilience pipeline — review-rejectable. The existing EF `EnableRetryOnFailure` covers it, and there is no multi-step transaction (the feature never writes). |
| **Gate 7 — UI/design compliance** | **REQUIRED** | This ships UI. `checklists/ui-review.md` must be instantiated from `.specify/templates/ui-review-checklist-template.md` and verified against the diff before sign-off. DESIGN.md wins on conflict. |
| **Gate 8 — Resilience review** | **N/A** | No network call or outbound integration added (see VII). |

**Post-Phase-1 re-check**: unchanged. The design added no entity, no dependency, no outbound
call, and no write path. The single deviation is the one recorded below.

## Project Structure

### Documentation (this feature)

```text
specs/044-team-activity-feed/
├── plan.md              # This file
├── research.md          # Phase 0 — the seven decisions that shape the code
├── data-model.md        # Phase 1 — read model, sources, gating, ordering
├── quickstart.md        # Phase 1 — how to prove it works
├── contracts/
│   └── team-happenings.md   # GET /teams/{slug}/happenings
├── checklists/
│   ├── requirements.md  # Spec quality (done)
│   └── ui-review.md     # Gate 7 — instantiated during implementation
└── tasks.md             # Phase 2 — /speckit-tasks, NOT created here
```

### Source Code (repository root)

```text
backend/
├── Dtos/Teams/
│   └── TeamHappeningDtos.cs            # NEW — kind enum, params, entry record
├── Services/Teams/
│   ├── ITeamHappeningService.cs        # NEW
│   ├── TeamHappeningService.cs         # NEW — 5 reads, merge, cap
│   └── TeamMembershipGuard.cs          # unchanged — reused for the members-only gate
├── Controllers/
│   └── TeamsController.cs              # EDIT — one new action
├── Program.cs                          # EDIT — one DI registration
└── tests/JuggerHub.Api.IntegrationTests/Teams/
    └── TeamHappeningsTests.cs          # NEW

frontend/apps/web/
├── public/i18n/{en,de,es}.json         # EDIT — 7 new keys, 2 renamed
└── src/app/
    ├── core/models/team.models.ts      # EDIT — TeamHappening types
    ├── core/services/team.service.ts   # EDIT — getHappenings()
    └── features/teams/team-detail/
        ├── team-detail.component.ts    # EDIT — load when member
        ├── team-detail.component.html  # EDIT — new card + heading rename
        └── happenings/
            ├── team-happenings.component.ts    # NEW
            ├── team-happenings.component.html  # NEW
            ├── team-happenings.component.css   # NEW
            └── team-happenings.component.spec.ts # NEW
```

**Structure Decision**: The existing two-project web layout is used as-is. The new service joins
the four services already in `backend/Services/Teams/`; the new component lives under the team
page that owns it, mirroring how `features/dashboard/modules/activity-list.component.*` sits
under the dashboard. Nothing is promoted to a shared location — there is exactly one call site.

## Key implementation notes

These are the points where a reasonable-looking implementation would be wrong. Full reasoning in
[research.md](./research.md).

1. **Do not reuse `ActivityEntryDto` / `ActivityKind`** (issue #178 open question 2). Adding
   team-only members to the home enum forces every dashboard consumer to ignore kinds it can
   never receive, and `ActivityListComponent`'s `switch` would silently drop them via its
   `default: return ''`. A separate `TeamHappeningKind` keeps the two feeds independently
   evolvable. **Copy the pattern, not the type.** *(R1)*

2. **Project player names defensively.** `PlayerProfiles` carries
   `HasQueryFilter(p => p.User.Status != Banned)`, so navigating `m.User.Profile!.DisplayName`
   from a membership row throws or yields a surprise for a banned member. Use the
   `_db.PlayerProfiles.Where(p => p.UserId == …).Select(…).FirstOrDefault()` sub-projection
   `HomeService` already uses — it yields `null`, which the client turns into a **translated**
   stand-in. *(R2)*

3. **Do not use `MemberPlaceholder` here.** `TeamNewsService` resolves the request culture
   server-side to render "A former player". The activity pattern deliberately does the opposite:
   send `null` and let the client translate (FR-021/FR-024). Server-side culture guessing is
   what `ActivityParamsDto`'s doc comment exists to warn against. *(R2)*

4. **One entry per training *series*, never per session** (spec D3/SC-004).
   `RecurrenceExpander.MaxSessions` is **520** — one weekly recurring training writes up to 520
   session rows in a single save, all sharing a timestamp. Read `Trainings.CreatedDate`, not
   `TrainingSessions.CreatedDate`. *(R3)*

5. **Filter awards on `Status == AwardStatus.Active`.** Revoked rows are retained for audit; an
   unfiltered read would keep claiming an award the team no longer holds. *(R4)*

6. **The 30-day window uses each kind's own moment**, not `CreatedDate` uniformly:
   `TeamMembership.JoinedDate`, `BadgeAward/AchievementAward.EarnedAt`, `Training.CreatedDate`,
   `TrainingSession.CancelledDate`. Using `CreatedDate` for a cancellation would date the entry
   to when the session was *scheduled*, putting it outside the window. *(R5)*

7. **Ordering needs a total tie-break.** A series creation and its first session cancellation
   can share a timestamp. Sort by `OccurredAt` desc, then `Kind`, then a stable per-entry key,
   so two runs never disagree (FR-015). *(R6)*

8. **The empty state is required, unlike the dashboard's.** `ActivityListComponent` renders
   nothing at all when empty (`@if (hasAny())`). FR-014 requires a visible "nothing lately"
   state instead — a member must see that the section exists and is quiet, not wonder where it
   went. This is a deliberate divergence from the component being copied. *(R7)*

9. **Departed members self-correct.** The join entry is derived from the live
   `TeamMemberships` row, so when someone leaves, their join entry disappears with them. This
   satisfies the spec's "a player who joined is no longer a member" edge case structurally,
   with no extra predicate — but only because the feed is derived, so do not "optimise" it into
   a snapshot. *(R2)*

## Complexity Tracking

One deviation from the constitution, recorded rather than resolved.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Principle III: *"any endpoint or service method returning a list must paginate via `Skip`/`Take`… use a shared `PaginationRequest` and a `PagedResult<T>` envelope"* — this endpoint returns a bare capped list instead. | The spec forbids pagination outright (**FR-013**: no paging, no "show more"), and the result is hard-bounded twice over — at most **10** rows, none older than **30 days**. The principle's stated purpose, *"never return unbounded collections"*, is satisfied more strictly than a `PagedResult` would satisfy it, since a `PagedResult` lets a caller walk the whole table. | A `PagedResult<T>` envelope would advertise paging the feature deliberately does not offer, and `TotalCount` would invite the "show more" affordance FR-013 rules out. Precedent on the very same page: `TeamPublicDetailDto.Roster` (`Take(48)`) and `RecentActivity` (`Take(6)`) are already capped, un-paginated lists. The bound lives in one named constant so the cap is auditable at a glance. |
