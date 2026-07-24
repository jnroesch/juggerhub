# Implementation Plan: Onboarding Team Search

**Branch**: `feat/029-onboarding-team-search` | **Date**: 2026-07-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/029-onboarding-team-search/spec.md`

## Summary

Replace the feature-004 visual placeholder in the onboarding "Find your team" step with a working
search over real teams and a real join **request**. The step opens with beginners-welcome teams and
says plainly that any team can be found by name; typing searches all teams; picking one reveals an
explicit "ask to join" action that posts a join request a team admin still has to approve.

This is **frontend-only**. `SearchService.browseTeams()` (feature 007) and
`TeamService.requestToJoin()` (feature 009) already exist and are already used by the browse screen
and the public team page. No endpoint, DTO, entity, migration, or permission changes. The
onboarding finish payload (`ProfileService.updateMine`) is untouched — the join request is the only
thing this step persists.

The one hard constraint that shapes every decision below: **this step must never block onboarding.**
It is the first screen after registration, so no failure state may disable Continue, "I'm not on a
team yet", or Back. The chosen shape makes that structural rather than careful — **Continue issues
no network call at all** (spec FR-012/FR-018), because asking to join is its own deliberate press.

## Technical Context

**Language/Version**: TypeScript / Angular 21 (Nx workspace), zoneless change detection

**Primary Dependencies**: Angular signals + rxjs (`debounceTime`/`distinctUntilChanged`); existing
`SearchService`, `TeamService`; existing `BrowseList` helper (feature 007); shared UI primitives
`jh-loading`, `jh-alert`, `jhButton` (feature 024); Tailwind + DESIGN.md tokens

**Storage**: None new. Reads `GET /api/v1/teams`; writes one `POST /api/v1/teams/{slug}/join-requests`

**Testing**: Jest (`npx nx test web`) — component specs driving the protected surface, with
`HttpTestingController`. Zoneless: no `fakeAsync`; debounce is tested by driving the search input
subject directly or with `jest.useFakeTimers()`, per existing project convention.

**Target Platform**: Web (mobile-first; the onboarding flow is a `max-w-sm` centered column that
also has to read well on desktop)

**Project Type**: Web application (frontend change only)

**Performance Goals**: One search request per typing pause (250 ms debounce, matching
`BrowseShellComponent`), one request on step entry, at most one join request per completed step.
First page only (20 results) — no paging in the wizard.

**Constraints**: No new visual style (DESIGN.md governs); only **one coral CTA per view**, which is
already the step's Continue button, so any retry/secondary action must be `variant="secondary"`;
the server remains the authorization boundary; the step must stay escapable in every state.

**Scale/Scope**: One template block, one component's worth of logic, plus specs. No new routes, no
new services, no shared-component changes.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Security-First / Never Trust the Client** — PASS. Nothing here is a security boundary.
  `GET /api/v1/teams` already applies feature-026 authenticated-only visibility; `POST
  …/join-requests` authorizes the signed-in subject server-side and answers `409` when the caller is
  already a member. The step renders only server-supplied fields and surfaces no status code or
  internal detail to the reader (FR-009, FR-022, FR-023).
- **II. Thin Controllers, Service-Centric Backend** — N/A. No backend change; no controller or
  service is touched.
- **III. Disciplined Data Access** — N/A. No EF or database work. (The consumed list endpoint is
  already paginated; this step requests the first page only.)
- **IV. Secure Authentication & Session Management** — PASS. Both calls ride the existing cookie
  session through `authInterceptor`; no token handling is added.
- **V. Environment Parity & Reproducible Deployments** — PASS. Pure frontend behaviour, identical
  across local/Dev/Prod.
- **VI. Consistent Conventions & Tooling** — PASS. The onboarding component already keeps separate
  `.ts` / `.html` / `.css`; no new files break that, and no scripts are added.
- **VII. Resilient by Default, Never Amplifying** — PASS **by inheritance, and deliberately so**:
  - The **search is a `GET`**, so `retryInterceptor` already gives it a 15 s per-attempt time limit
    and up to two jittered retries on transient faults. Nothing per-call-site is added — that is the
    principle's "applied by default, never per call site" rule working as intended.
  - The **join request is a `POST`**, so the same interceptor time-limits it and **never retries
    it**. This is exactly the never-retry-a-browser-mutation rule, and it is the right answer here
    even though the endpoint happens to be idempotent while a request is pending: the browser cannot
    know that, and the rule does not bend for endpoints that look safe.
  - **No new resilience code is written.** A hand-rolled retry, a per-call timeout, or a `Task.Delay`
    equivalent would be review-rejectable.
  - The long-search case is covered by `jh-loading`'s own patient line (feature 028), which needs no
    wiring.
- **Quality Gate 7 (UI/Design compliance)** — **APPLIES.** This ships UI. Instantiate
  `specs/029-onboarding-team-search/checklists/ui-review.md` from
  `.specify/templates/ui-review-checklist-template.md` and verify against DESIGN.md before
  verification. The standing app-wide primary-button contrast conflict
  (`docs`/memory: DESIGN.md ≥4.5:1 vs. white-on-coral-4) is the owner's open decision and is **not**
  resolved here — this step introduces no new primary button.
- **Quality Gate 8 (Resilience)** — **APPLIES** (the step adds network calls) and is satisfied by
  inheritance as set out under VII. Nothing new to configure.

**Result**: No violations. Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/029-onboarding-team-search/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (client view-model only — no persisted entities)
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── consumed-endpoints.md   # Existing endpoints consumed — no new API
├── checklists/
│   ├── requirements.md  # spec quality (done)
│   └── ui-review.md     # created during implementation (Gate 7)
└── tasks.md             # /speckit-tasks output (NOT created here)
```

### Source Code (repository root)

```text
frontend/apps/web/src/app/
├── features/onboarding/
│   ├── onboarding.component.ts        # EDIT — drop teamStub; add search/select/request state
│   ├── onboarding.component.html      # EDIT — replace the @case ('team') block
│   ├── onboarding.component.css       # unchanged
│   └── onboarding.component.spec.ts   # EDIT — new tests for search, select, request, failures
├── features/browse/
│   └── browse-list.ts                 # REUSED unchanged (state machine + fetch)
├── core/services/
│   ├── search.service.ts              # REUSED unchanged (browseTeams)
│   └── team.service.ts                # REUSED unchanged (requestToJoin)
└── shared/ui/                         # REUSED unchanged (jh-loading, jh-alert, jhButton)
```

**Structure Decision**: The work stays inside the existing onboarding component rather than becoming
a new shared component. The step is one screen in one wizard with wizard-specific rules (never
blocking, no paging, no filters), and extracting a component would create a shared surface with
exactly one consumer. `BrowseList` already provides the reusable part — the fetch/state machine — so
the reuse that matters is had without a new abstraction.

**Explicitly not reused**: `BrowseShellComponent`. It carries a page title, the three-way
Teams/Events/Players nav, a filters button and panel, chips, sort, a count line, and "load more" —
none of which belong in a `max-w-sm` onboarding step. Reusing it would mean suppressing more than it
renders. The row markup is instead *copied* from `browse-teams.component.html`, which is honest: the
rows must look identical, but they are 12 lines of markup, not a component.

## Implementation Shape

Sketch only — `/speckit-tasks` turns this into ordered tasks.

**State added to `OnboardingComponent`** (replacing `teamStub`):

| Signal | Purpose |
|---|---|
| `teamQuery` | Applied search text (drives the fetch; set by the debounced input) |
| `selectedTeam` | The picked `TeamCard`, or `null` |
| `requestedSlugs` | Slugs already asked in this flow — satisfies FR-015 with no network call |
| `askingTeam` | In-flight guard so the ask action can't be double-fired |
| `teamRequestError` | Quiet failure line for a refused/failed join request |
| `teams` (`BrowseList<TeamCard>`) | Results + `loading`/`ready`/`empty`/`no-results`/`error` states |

**Fetch shape**: `browseTeams({ q, activeOnly: true, beginnersWelcome: <only when q is empty>, sort:
'NameAsc', take: 20 })`. The `beginnersWelcome` filter is applied **only** when there is no query, so
the opening list is beginner-friendly (FR-002) and any search covers all teams (FR-003).
`BrowseList.filtered` is set to `Boolean(q)` so an empty result reads as "no matches for that search"
when searching and as "nothing here yet" when the opening list is genuinely empty.

**Debounce**: a `Subject<string>` piped through `debounceTime(250)` + `distinctUntilChanged()` +
`takeUntilDestroyed()`, mirroring `BrowseShellComponent` exactly so the two feel the same.

**Asking to join**: selecting a row reveals a secondary `jhButton` labelled with the team's name
("Ask to join Berlin Jugger"). Pressing it calls `requestToJoin(slug)`. On success the slug joins
`requestedSlugs`, the row shows an "asked" marker, and the pending confirmation line appears. On
failure `teamRequestError` carries a plain sentence — `409` means "you're already on that team",
anything else means "we couldn't send that just now". **No automatic retry** (constitution VII); the
player may press again if they want to.

**Continue / "I'm not on a team yet" / Back**: unchanged from today — `next()` and `back()`, no
network call, no guard, no disabled state. That is the whole of FR-017/FR-018, and it is why they
cannot regress.

**Removed**: the `teamStub` signal, the disabled input, the two hardcoded rows, and the "coming
soon" line.

## Complexity Tracking

No constitution violations; section intentionally empty.
