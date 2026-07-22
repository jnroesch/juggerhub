# Implementation Plan: Home Participation Makeover

**Branch**: `025-home-participation-makeover` | **Date**: 2026-07-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/025-home-participation-makeover/spec.md`

## Summary

Reshape the logged-in home dashboard (feature 008) around **participation** and **action**, keeping the existing visual language. The home composite (`GET /api/v1/home`) is re-composed into four ordered sections — **Needs you** (actionable, top, hidden when empty), **Up next** (unified events + trainings agenda), **News** (authored team/event/party posts), **What's going on** (quiet passive activity) — and three current sections are removed (Tournaments, Team snapshots, the duplicate "Your teams activity").

The work is overwhelmingly **recomposition of existing data**, not new domains: the `NotificationType` discriminator already separates actionable rows (TeamInvite, PartyRequest, MarketInvite) from passive ones; per-domain services already expose the actionable inboxes (`IMarketRequestService.ListMineAsync`, team/party invitation services) and the un-answered-training agenda (`ITrainingResponseService.GetMyAgendaAsync`); and the News merge already aggregates team + event posts (party posts are added). The only genuinely new read work is the **What's going on** activity feed, built derive-on-read from domain tables (no new table, no fan-out writes), consistent with how the old "teams activity" was derived.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Angular (Nx workspace, zoneless, signals) (frontend)

**Primary Dependencies**: Entity Framework Core + PostgreSQL 18 (backend); Angular + Tailwind CSS (frontend). Reuses feature 008 (home), 010 (notifications), 016 (parties/party news), 017 (marketplace requests), 018 (trainings), 012 (badges).

**Storage**: PostgreSQL via EF Core. **No new tables or migrations expected** — all sections read from existing entities (EventSignup, TeamMembership, PartyMember, PartyNewsPost, BadgeAward, Notification, TrainingResponse/Session).

**Testing**: xUnit integration tests (`backend/tests/JuggerHub.Api.IntegrationTests`); Angular component specs (zoneless — no `fakeAsync`, per feature 014 note); Playwright e2e (`frontend/apps/web-e2e`).

**Target Platform**: Web (responsive; desktop right-rail + mobile stacked, per feature 008 layout).

**Project Type**: Web application (backend + frontend).

**Performance Goals**: First-paint composite remains a single `GET /api/v1/home` returning capped top-N per section; every read projected + `AsNoTracking`. No new N+1s; activity feed reads a bounded window per source and merges in memory (mirrors the existing up-next/news merge pattern).

**Constraints**: Never trust the client — all section visibility/authorization enforced server-side (FR-031, FR-023, FR-027a). Preserve the existing visual system (DESIGN.md); this is content/IA, not restyle (FR-032). List surfaces ("see all" for Up next and News) stay paginated per the constitution.

**Scale/Scope**: Single composite endpoint reshaped; ~4 DTO records changed/added; 1 backend service (`HomeService`) reworked; 1 new derive-on-read activity query; frontend dashboard component + modules restructured (2 modules removed, 2 added, 1 unified). No auth/schema/billing changes.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|-----------|------------|
| **I. Security-first, never trust client** | PASS. Every section is entitlement-scoped in `HomeService` server-side; party news gated to `In` members (FR-023); activity signals scoped to the viewer's teams/parties (FR-027a); notification payloads are render hints, never authorization inputs — Needs-you reads authoritative source domains, not the notification display-cache. |
| **II. Thin controllers, service-centric** | PASS. `HomeController` stays a thin cross-resource *view* controller; all composition in `IHomeService`. Home projects DTOs directly (established feature-008 pattern for a read-only view — not the entity→Mapster CRUD path); no new controllers. |
| **III. Disciplined data access** | PASS. Reads are `.Select(...)` projections + `AsNoTracking`; bounded windows merged in memory; "see all" endpoints paginate via `PaginationRequest`/`PagedResult`. No unbounded lists. No `ExecuteUpdate` paths. |
| **IV. Auth & sessions** | PASS. No auth changes; endpoint keeps the JWT-cookie scheme. |
| **V. Environment parity / containers** | PASS. No infra change; behaves identically local/Dev/Prod. |
| **VI. Conventions & tooling** | PASS. Frontend keeps separate `.html`/`.css`/`.ts`; only `.ps1` scripts; enums serialized by name (global `JsonStringEnumConverter` already registered — see EF gotchas). |
| **Quality Gate 7 (UI/DESIGN)** | REQUIRED. Instantiate `specs/025-home-participation-makeover/checklists/ui-review.md` from the template and verify the reshaped home against DESIGN.md before verification. |

**Result**: No violations. Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/025-home-participation-makeover/
├── plan.md              # This file
├── spec.md              # Feature specification (with clarifications)
├── research.md          # Phase 0 — architecture decisions
├── data-model.md        # Phase 1 — DTO/contract changes
├── quickstart.md        # Phase 1 — validation guide
├── contracts/
│   └── home-api.md      # Phase 1 — reshaped GET /api/v1/home contract
└── checklists/
    ├── requirements.md  # Spec quality (from /speckit-specify, passing)
    └── ui-review.md     # UI review (instantiated during implementation)
```

### Source Code (repository root)

```text
backend/
├── Controllers/
│   └── HomeController.cs                      # unchanged surface; optional /home/activity see-all (see research)
├── Dtos/Home/
│   └── HomeDtos.cs                            # HomeDto reshaped; +NeedsYouItemDto, +AgendaItemDto, +ActivityEntryDto; −Tournaments/Snapshots/TeamsActivity
├── Services/Home/
│   ├── HomeService.cs                         # recompose: build Needs-you, unified Up-next, party-news merge, activity feed; drop removed sections
│   ├── IHomeService.cs                        # composite + up-next + news see-all (signatures largely unchanged)
│   └── HomeProjections.cs / HomeOptions.cs    # projection helpers + caps (add ActivityCap, NearWindowDays)
└── tests/JuggerHub.Api.IntegrationTests/Home/ # extend: Needs-you aggregation, trainings-in-up-next, party news, activity feed, removals

frontend/apps/web/src/app/
├── features/dashboard/
│   ├── dashboard.component.{ts,html,css}      # reorder sections; remove tournaments/snapshots/activity; add needs-you + activity
│   ├── modules/
│   │   ├── needs-you-card.component.{ts,html,css}     # NEW — actionable block
│   │   ├── up-next-card.component.{ts,html,css}       # extend to render event OR training agenda items
│   │   ├── activity-list.component.{ts,html,css}      # NEW — passive "What's going on"
│   │   ├── news-list.component.*                       # unchanged shape (party source flows through existing DTO)
│   │   ├── market-card.component.*                     # REMOVE (folded into Needs-you)
│   │   └── your-trainings-card.component.*             # REMOVE (folded into Up next / Needs-you)
│   └── see-all/                                # up-next-list handles unified agenda items; news-page unchanged
└── core/
    ├── models/home.models.ts                  # mirror reshaped DTOs
    └── services/home.service.ts               # unchanged endpoints; types updated
```

**Structure Decision**: Web application. Backend change is concentrated in `HomeService`/`HomeDtos`; frontend change is concentrated in the `features/dashboard` module tree. No new modules, controllers, or migrations.

## Phase 0 — Research

See [research.md](./research.md). Key decisions resolved:

1. **Needs-you sourced from authoritative domains, not the notification cache** — aggregate pending team invites, party participation/co-admin requests, marketplace invites+applications (`ListMineAsync`), and near-window un-answered trainings; each resolved in place via its existing action endpoint.
2. **Unified agenda via a `Kind`-discriminated `AgendaItemDto`** — one ordered Up-next list carrying event or training items, so the client renders a single timeline. Near-window un-answered trainings are pulled out into Needs-you (FR-006a/b), so Up-next trainings are the answered/far-out ones.
3. **What's-going-on is derive-on-read, no new table** — participation/social signals (teammate event sign-up, new team member, badge awarded, party member joined) derived from domain rows; pure state-changes (role change, training reschedule/cancel) read from the viewer's passive notification rows; merged + capped.
4. **Party news added to the existing News merge** — `PartyNewsPost` for parties the viewer is an `In` member of, tagged `source = "party"`.
5. **Removals** — Tournaments, Team snapshots, and the derived "teams activity" queries/DTOs deleted; the standalone trainings card removed.

## Phase 1 — Design & Contracts

- **Data model**: [data-model.md](./data-model.md) — reshaped `HomeDto`, new `NeedsYouItemDto`, `AgendaItemDto`, `ActivityEntryDto`; removed `TournamentCardDto`, `TeamSnapshotDto`, `TeamActivityDto` (and `NextFixtureDto` if unused elsewhere).
- **API contract**: [contracts/home-api.md](./contracts/home-api.md) — the reshaped `GET /api/v1/home` payload plus the paginated see-all surfaces.
- **Quickstart**: [quickstart.md](./quickstart.md) — end-to-end validation scenarios per user story.
- **Agent context**: update the plan reference in the `CLAUDE.md` SPECKIT markers to this plan.

## Post-Design Constitution Re-check

No change from the initial gate: the design adds no new tables, no new controllers, keeps composition in `HomeService`, preserves pagination on list surfaces, and enforces all visibility server-side. **PASS.**
