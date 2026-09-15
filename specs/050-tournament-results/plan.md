# Implementation Plan: Tournament Results

**Branch**: `050-tournament-results` | **Date**: 2026-09-15 | **Spec**: [spec.md](./spec.md) | **Issue**: [#295](https://github.com/jnroesch/juggerhub/issues/295)

**Input**: Feature specification from `/specs/050-tournament-results/spec.md`

## Summary

Tournament events get a **results** section: a final ranking (with ties), and for tournaments imported from Tugeny, every match with its set scores. Teams get a **placement history**. JuggerHub works with Tugeny, the tool most Jugger tournaments are run on, at both hand-offs:
- the confirmed team list goes **in** as plain text Tugeny's *Import Team Names* accepts
- results come **out**, either pasted from Tugeny's *Export Ranking for JTR* or imported from Tugeny's public MIT-licensed data interface once a tournament is finalized there

Placements stay **plain names** until connected to a JuggerHub team. Two actors may connect:
- an **event's own admins**, only to teams with a confirmed JuggerHub sign-up for that event (`Joined`)
- **platform admins**, to any team, one placement at a time, from a new admin work queue. This covers every team that played **without** a JuggerHub sign-up: all teams of past tournaments, sign-ups run elsewhere, guest teams. The platform admin checks by hand that the team really played

Teams can never claim a placement. No connection is ever reused or suggested across placements or tournaments (owner decisions, spec Clarifications).

**The spec's one open item is closed by research, not by asking.** Tugeny's desktop binary was read (not run). Its `JtrJsonService::composeJson` references exactly the keys `position` and `name`, so the ranking export is `[{name, position}, …]`. `extractNames` shows the team import accepts plain text one per row ([research.md](./research.md) R1). All 46 finalized tournaments in Tugeny's interface were then surveyed (R2); real responses are committed as contract samples. The survey found what the client must handle:
- an unknown slug is **HTTP 200 with body `null`**
- an unfinalized tournament serves `rankings: []`
- 318 matches are draws with no winner
- the largest body is 246 KB

**Technically this is a medium, additive feature**: three new tables and one migration, one outbound integration (the first backend code to **parse** an external JSON body), ~13 endpoints across three controllers, four Angular surfaces, and i18n in three catalogues.

**Server-side, "past-dated tournaments" is already done.** Past dates are accepted today, and every sign-up, party and market path already closes once `EndsAt` has passed. What's built is a small frontend fix: the join and enter-party buttons still appear on ended events (R12).

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Angular 22 + Nx (frontend)

**Primary Dependencies**:
- Backend: EF Core 10 + Npgsql, `Microsoft.Extensions.Http.Resilience` (Polly v8, already referenced), `System.Text.Json` (BCL).
- Frontend: Transloco, Tailwind CSS.
- **No new package is added.**

**Storage**: PostgreSQL 18.
- **3 new tables**: `TournamentResults`, `TournamentPlacements`, `TournamentMatches`. Match scores are Npgsql `int[]` (precedent `Party.PositionsNeeded`).
- **1 migration** (`AddTournamentResults`). No existing column changes, no backfill.

**Testing**:
- Backend: xUnit, `JuggerHub.Api.IntegrationTests` (WebApplicationFactory + Testcontainers). Tugeny is faked with a scripted primary handler serving the committed samples, following the `OutboundResilienceHarness` / `MediaOutageTests` override patterns.
- Frontend: Jest (pure parser and builder, components, `catalog-parity.spec.ts`).
- **No test ever calls the real tugeny.org.**

**Target Platform**: Linux containers on AKS (Dev/Prod), docker-compose locally.

**Project Type**: Web application: `backend/` (.NET API) + `frontend/apps/web` (Angular SPA).

**Performance Goals**:
- The event page makes one extra request (`GET …/results`: 1 result row + ≤ 128 placements + 1 count) and a paged matches request only when that section is opened or scrolled to.
- Team history: one indexed, paged query.
- Tugeny import: ≤ 2 GETs per preview and per commit. The largest real payload (528 matches) parses in milliseconds.
- **SC-001** (24 teams under 1 min from paste or import) is dominated by the admin's matching clicks, not the system.

**Constraints**:
- Tugeny is contacted **only on an event admin's action**; nothing is fetched on page views (FR-018).
- The host is configuration, never input (R6).
- Response ≤ 4 MiB (R5).
- 128 placements per ranking (R8).
- No reading of turniere.jugger.org, ever (FR-019).
- Signed-in only (026 unchanged).

**Scale/Scope**:
- ~22 backend files (3 entities + enums, DTOs, 5 services, 2 controllers + 1 action, options, handler, rate-limit policy, migration) and ~7 test files.
- ~20 frontend files (4 surfaces, 2 pure helpers, service + models, admin nav).
- ~60 i18n keys × 3.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` **v1.4.0** before Phase 0; re-checked after Phase 1 (below).*

| Principle / Gate | Verdict | How this feature satisfies it |
|---|---|---|
| **I. Security-first, never trust the client** | ✅ | **Authorization** is server-side: `EventAdminGuard` for event writes, the `PlatformAdmin` policy for connecting, and the signed-up-team rule (R4) plus the unchanged-connection rule (R8) enforced in the service. **Provenance**: the pasted export is parsed in the browser for convenience only; the saved rows go through the same validated `PUT` as hand entry. Import provenance is server-established by re-fetching on commit (R7). **SSRF**: none, since only a regex-validated slug reaches a path on a configured host (R6). **Limits**: size-capped responses (R5). **Errors**: generic problem details; no body in any log |
| **II. Thin controllers, service-centric** | ✅ | Two new controllers and one action only map outcomes to status codes. Logic lives in `ITournamentResultService`, `ITugenyImportService`, `ITugenyClient`, `ITeamPlacementService` and `IAdminPlacementService`. DTOs come from explicit `.Select` projections; no mapper |
| **III. Disciplined data access** | ✅ with one recorded deviation | New entities derive from `BaseEntity`. Reads are `AsNoTracking` projections. A ranking replace or import commit is a tracked load plus `RemoveRange`/`AddRange` plus **one** `SaveChanges`, which is atomic with no explicit transaction. **Deviation**: the placement list (≤ 128) is a bare capped list, not `PagedResult` (R8; see Complexity Tracking). Matches, team history and the admin queue are paged |
| **IV. Auth & sessions** | ✅ n/a | No auth change. Every route stays under the class-level `[Authorize]` (026) |
| **V. Parity** | ✅ | `Tugeny:BaseUrl` and `Resilience:Outbound:Tugeny` go in `appsettings.json`, `.env.sample`, `docker-compose.yml` and `JuggerHubApiFactory`, the same four places as `Resend`/`MediaStore`. Terraform needs no key (appsettings defaults apply, the same as the existing integrations); only the egress comment in `network-policy.tf` gains tugeny.org |
| **VI. Conventions** | ✅ | Separate `.html`/`.css`/`.ts`; no `.sh`; PowerShell only |
| **VII. Resilience** — **ENGAGED** | ✅ | See the breakdown below |
| **Gate 7 — UI/Design** — **ENGAGED** | ⏳ at implementation | `checklists/ui-review.md` from the template, against DESIGN.md. Known risks are listed in R15: the fifth admin tab at 375 px in German; one coral CTA; mono scores; error vs empty |
| **Gate 8 — Resilience** | ✅ | As Principle VII |

**Principle VII breakdown**:
- **Opting in**: one named client `"Tugeny"` plus `.AddJuggerHubResilience(config, "Tugeny")` plus one config section. There is no per-call-site retry, no `HttpClient.Timeout`, and no stacked resilience handler.
- **Time limits**: 10 s per attempt and 30 s total; the body is read inside each attempt.
- **Retry**: 2 retries, jittered and exponential. It is GET-only, so it's idempotent and there's no mutation judgement to record.
- **Breaker**: opens at 4 failed **attempts** (ratio 0.5, 120 s window, 60 s break). This is derived from the real volume of about 5 calls per import session (R5); the library default of 100/30 s would never open.
- **429 has two meanings, both explicit**: Tugeny's 429 is retried, honouring `Retry-After`. Our own `tugeny` rate-limit policy's 429 is never retried by the browser interceptor.
- **Size**: oversize is a permanent failure via the inner `ResponseSizeLimitHandler`, which throws a non-HTTP exception so it is not retried (R5).
- **Logging**: status and length only.

**Post-design re-check**: ✅ **no new violations.** Phase 1 added the `id`-preserving ranking write (R8), the stateless re-fetch import (R7), and attribution columns instead of a log (R10). Each reduces surface compared with its alternative. The single deviation is unchanged.

## Project Structure

### Documentation (this feature)

```text
specs/050-tournament-results/
├── spec.md
├── plan.md               # this file
├── research.md           # R1–R15
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── results-api.md    # JuggerHub endpoints
│   ├── tugeny.md         # what is read from / handed to Tugeny
│   └── tugeny-samples/   # real responses, 2026-09-15 (MIT data)
├── checklists/
│   ├── requirements.md
│   └── ui-review.md      # created at implementation (Gate 7)
└── tasks.md              # /speckit-tasks
```

### Source Code

```text
backend/
├── Entities/TournamentResult.cs, TournamentPlacement.cs, TournamentMatch.cs, ResultEnums.cs   # new
├── Data/AppDbContext.cs                              # + "Feature 050" block (3 entities, FKs, indexes)
├── Data/Migrations/*_AddTournamentResults.cs         # new
├── Common/TugenyOptions.cs                           # new: SectionName "Tugeny", BaseUrl, ResilienceName, MaxResponseBytes
├── Resilience/ResponseSizeLimitHandler.cs            # new: inner size guard (R5)
├── Security/RateLimitPolicies.cs                     # + "tugeny" policy (10/min/user)
├── Dtos/Results/ResultDtos.cs                        # new
├── Dtos/Admin/AdminResultDtos.cs                     # new
├── Services/Results/
│   ├── TournamentResultService.cs (+ interface)      # read, ranking replace, clear, link/unlink
│   ├── TournamentRanking.cs                          # pure: competition-rank normalisation (R8)
│   ├── TugenyClient.cs (+ interface)                 # the only code that talks to Tugeny; tolerant parsing (R2)
│   ├── TugenyLinkParser.cs                           # pure: address/slug → slug (R6)
│   ├── TugenyImportService.cs (+ interface)          # preview / commit (R7)
│   └── TeamPlacementService.cs (+ interface)         # team history
├── Services/Admin/AdminPlacementService.cs (+ interface)   # queue, connect, disconnect
├── Services/Parties/PartyService.cs                  # CanForm respects PartyAccess.IsEventOpen (R12)
├── Controllers/EventResultsController.cs             # new
├── Controllers/Admin/AdminResultsController.cs       # new
├── Controllers/TeamsController.cs                    # + GET {slug}/placements
├── Program.cs                                        # DI, named client, options
├── appsettings.json                                  # + Tugeny, Resilience:Outbound:Tugeny
└── tests/JuggerHub.Api.IntegrationTests/
    ├── Results/RankingTests.cs, TugenyLinkTests.cs, TugenyImportTests.cs,
    │   TeamPlacementHistoryTests.cs, AdminPlacementTests.cs, TournamentRankingTests.cs,
    │   TugenyLinkParserTests.cs, Fixtures/*.json     # fixtures copied from contracts/tugeny-samples
    ├── Resilience/TugenyResilienceTests.cs           # twin of CircuitBreakerTests / wiring guard
    └── JuggerHubApiFactory.cs                        # + fast Tugeny resilience values

frontend/apps/web/src/app/
├── core/models/results.models.ts                     # new
├── core/services/results.service.ts                  # new (event + team endpoints)
├── core/services/admin.service.ts                    # + placement queue/connect/disconnect
├── features/events/event-detail/
│   ├── event-detail.component.html                   # results card; "Results" in manage menu (tournaments); live link
│   └── components/event-results.component.{ts,html,css}        # new: ranking, winner, provenance, matches (load more)
│   └── components/join-actions.component.ts          # hidden after endsAt (R12)
├── features/events/event-results/                    # new page: events/:id/results (event admins)
│   ├── event-results.component.{ts,html,css}         # ranking editor, Tugeny card, team-list box
│   ├── tugeny-export.parser.ts (+ .spec.ts)          # pure (contracts/tugeny.md §2)
│   └── tugeny-team-list.ts (+ .spec.ts)              # pure (contracts/tugeny.md §3)
├── features/teams/team-detail/placements/team-placements.component.{ts,html,css}   # new card
├── features/admin/results/admin-results.component.{ts,html,css}                    # new queue
├── features/admin/shared/team-picker.component.{ts,html,css}                       # new, from admin-teams search
├── features/admin/shell/admin-shell.component.{ts,html}                            # + Results nav (both navs)
└── app.routes.ts                                     # + events/:id/results, admin/results

frontend/apps/web/public/i18n/{en,de,es}.json         # events.results.*, teams.placements.*, admin.results.*, admin.nav.results
.env.sample, docker-compose.yml                       # RESILIENCE_TUGENY_* (4 keys) + TUGENY_BASE_URL
infra/modules/app/network-policy.tf                   # egress comment: + tugeny.org (comment only)
```

**Structure Decision**: The existing web-application layout. Results get their **own** backend namespace (`Services/Results`) and controller rather than growing `EventsController` (536 lines) or `EventService`: they have their own lifecycle and the only outbound dependency (R11). The admin queue joins the existing `Services/Admin` + `Controllers/Admin` family under the `PlatformAdmin` policy.

## Complexity Tracking

| Deviation | Why needed | Simpler alternative rejected because |
|---|---|---|
| Placement list returned unpaged (Principle III "pagination is mandatory") | A ranking is one unit: the winner callout, ties and "N teams ranked" must render together, and a page boundary inside a tie would misstate positions. Hard cap 128, where the largest real tournament has 72 | `PagedResult` would advertise paging that the UI must defeat by fetching every page. Precedent for capped lists on the same kind of page: team `Roster` (48), `RecentActivity` (6), happenings (10) |
| An extra `DelegatingHandler` on one named client, next to the shared resilience handler | An oversized body must fail **permanently**. The framework's `MaxResponseContentBufferSize` throws `HttpRequestException`, which the shared retry treats as transient, so it would fetch the oversized body three times | Reading with `ResponseHeadersRead` and a manual cap moves the body read outside the attempt timeout and needs a hand-rolled timeout, which Principle VII rejects. The handler is a size guard, not a resilience handler, so "no stacked resilience handlers" is untouched |

## Spec drift (recorded, not silent)

- **Key Entity "where it came from" lists three sources**; the model has two plus `None`. A **pasted** export is stored as `Manual`: the browser does the parsing, so "pasted" would be the client's unverifiable word (Principle I). The admin vouches for it exactly as for typing. Only a server-fetched import is labelled as from Tugeny (FR-017 is about imports only).
- **FR-020 "grouped by stage"**: Tugeny names a stage only for **group** matches. Knockout matches carry their round only in the match name ("QF1 1-8", "F 1-2"), so they render under one "Knockout" heading in time order, each showing its name.
- **FR-016 "winner"**: 9% of real matches are draws with no winner. They are shown as draws.
- **Edge case "unfinished final" via paste**: Tugeny refuses to export a ranking with an undecided rank (R1), so that case is served by hand entry only.
