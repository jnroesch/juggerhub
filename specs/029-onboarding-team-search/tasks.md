---

description: "Task list for feature 029 — Onboarding Team Search"
---

# Tasks: Onboarding Team Search

**Input**: Design documents from `specs/029-onboarding-team-search/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/](contracts/consumed-endpoints.md),
[quickstart.md](quickstart.md)

**Tests**: Included. The repo's convention is a component spec per feature, and this feature's
load-bearing guarantees are negative ones ("Continue sends nothing", "a failed search doesn't
disable the way out") that only a test can hold in place.

**Organization**: Grouped by user story so each is independently deliverable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: `[US1]`, `[US2]`, `[US3]` — maps to the user stories in [spec.md](spec.md)

## Path Conventions

Frontend-only feature. All paths are relative to the repository root; the app lives at
`frontend/apps/web/src/app/`.

**Reality check on parallelism**: this feature edits three files
(`onboarding.component.ts`, `.html`, `.spec.ts`). Most tasks touch one of the first two, so `[P]` is
rare and honest here rather than sprinkled for appearance.

---

## Phase 1: Setup

**Purpose**: Nothing to scaffold — no new project, dependency, or tooling. This phase exists only to
confirm the ground is where the plan says it is.

- [ ] T001 Confirm the working tree is on `feat/029-onboarding-team-search` and the frontend builds clean before any edit: `cd frontend; npx nx test web --testPathPattern=onboarding` and `npx nx lint web`
- [ ] T002 Re-read the three reuse targets so the copy matches them exactly: the row markup in `frontend/apps/web/src/app/features/browse/browse-teams/browse-teams.component.html` (lines 20–41), the state machine in `frontend/apps/web/src/app/features/browse/browse-list.ts`, and the debounce in `frontend/apps/web/src/app/features/browse/browse-shell/browse-shell.component.ts` (250 ms + `distinctUntilChanged`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Remove the feature-004 placeholder and put the component's new state in place. Every
user story below edits the same two files, so this must land first.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T003 Delete the `teamStub` signal and its comment from `frontend/apps/web/src/app/features/onboarding/onboarding.component.ts` (lines 52–53)
- [ ] T004 Add the new imports to `frontend/apps/web/src/app/features/onboarding/onboarding.component.ts`: `SearchService` and `TeamService` (injected), `BrowseList`, `TeamCard`, `LoadingComponent`, `Subject`/`debounceTime`/`distinctUntilChanged`, `takeUntilDestroyed`, `DestroyRef`, and register `LoadingComponent` in the component's `imports` array
- [ ] T005 Add the transient state signals per [data-model.md](data-model.md) to `frontend/apps/web/src/app/features/onboarding/onboarding.component.ts`: `teamQuery`, `selectedTeam`, `requestedSlugs`, `askingSlug`, `teamRequestError`
- [ ] T006 Instantiate `teams = new BrowseList<TeamCard>(...)` in `frontend/apps/web/src/app/features/onboarding/onboarding.component.ts`, fetching via `SearchService.browseTeams({ q, activeOnly: true, beginnersWelcome: <only when q is empty>, sort: 'NameAsc', skip, take })`, and implement `OnDestroy` to call `teams.destroy()`
- [ ] T007 Replace the entire `@case ('team')` block in `frontend/apps/web/src/app/features/onboarding/onboarding.component.html` (lines 127–184) with an empty-but-valid shell: heading, the guidance paragraph, an **enabled** search input (`data-testid="onboarding-team-search"`), and the existing Continue / "I'm not on a team yet" footer unchanged. Deletes the disabled field, the two hardcoded rows, and the "coming soon" note in one stroke (FR-020)

**Checkpoint**: The placeholder is gone, the step still renders, and onboarding still completes.
`npx nx test web --testPathPattern=onboarding` must pass — the existing five specs are untouched by
this feature and must stay green.

---

## Phase 3: User Story 1 — Find my team and ask to join (Priority: P1) 🎯 MVP

**Goal**: A live search over real teams, a single-select list, and an explicit ask-to-join that
creates a pending request and says so honestly.

**Independent Test**: Enter onboarding on a fresh account, reach the step, confirm real teams are
listed, type a query and confirm results change, pick a team, press ask-to-join, then verify a
pending request exists in that team's admin queue and the account is **not** a member.

### Tests for User Story 1

> Write these first; they must fail before the implementation below.

- [ ] T008 [P] [US1] In `frontend/apps/web/src/app/features/onboarding/onboarding.component.spec.ts`, extend the `OnboardingApi` test interface with the new protected members (`teamQuery`, `selectedTeam`, `requestedSlugs`, `teams`, `onTeamQuery`, `selectTeam`, `askToJoin`) and add a `reachTeamStep(fixture)` helper that advances the wizard and flushes the opening search
- [ ] T009 [US1] Add a spec asserting the **opening** request is `GET /api/v1/teams` carrying `beginnersWelcome=true`, `activeOnly=true`, `sort=NameAsc` and **no** `q`
- [ ] T010 [US1] Add a spec asserting a typed query issues `GET /api/v1/teams` with `q` set and **no** `beginnersWelcome` parameter, and that clearing the query returns to the beginners-welcome request shape
- [ ] T011 [US1] Add a spec asserting the debounce: several rapid `onTeamQuery` calls under `jest.useFakeTimers()` produce exactly one request for the final value (no `fakeAsync` — the app is zoneless)
- [ ] T012 [US1] Add a spec asserting `selectTeam` is single-select (selecting a second card replaces the first) and that selection alone issues **no** request
- [ ] T013 [US1] Add a spec asserting `askToJoin` posts to `/api/v1/teams/{slug}/join-requests`, and that on `204` the slug lands in `requestedSlugs` and the confirmation element renders

### Implementation for User Story 1

- [ ] T014 [US1] Add the debounced query pipeline to `frontend/apps/web/src/app/features/onboarding/onboarding.component.ts`: a `Subject<string>` piped through `debounceTime(250)` + `distinctUntilChanged()` + `takeUntilDestroyed()`, setting `teamQuery` and calling `reloadTeams()`, plus an `onTeamQuery(value: string)` entry point for the template
- [ ] T015 [US1] Add `reloadTeams()` to `frontend/apps/web/src/app/features/onboarding/onboarding.component.ts` that sets `teams.filtered` to `Boolean(teamQuery().trim())` before calling `teams.reload()` — this one line is what makes empty and no-results distinguishable (FR-006)
- [ ] T016 [US1] Load the opening list from `ngOnInit` in `frontend/apps/web/src/app/features/onboarding/onboarding.component.ts` (independent of the existing `getMine()` prefill — neither may block the other)
- [ ] T017 [US1] Add `selectTeam(card: TeamCard)` to `frontend/apps/web/src/app/features/onboarding/onboarding.component.ts`: sets `selectedTeam`, clears `teamRequestError`, issues no request
- [ ] T018 [US1] Add `askToJoin()` to `frontend/apps/web/src/app/features/onboarding/onboarding.component.ts`: guards on `selectedTeam`, an empty `askingSlug`, and the slug not already in `requestedSlugs`; calls `TeamService.requestToJoin(slug)`; on success adds the slug to `requestedSlugs`; always clears `askingSlug`. **No retry, no timeout, no backoff** — the interceptor owns both (constitution VII)
- [ ] T019 [US1] Render the results list in the `@case ('team')` block of `frontend/apps/web/src/app/features/onboarding/onboarding.component.html`, copying the row treatment from `browse-teams.component.html` (initial chip, name, `city ·` mono `playerCount` players, "Beginners" pill), as `<button>` rows with `data-testid="onboarding-team-row"`, `track team.slug`, a visible selected state, and an "asked" marker for slugs in `requestedSlugs`
- [ ] T020 [US1] Add the opening-list guidance copy to `frontend/apps/web/src/app/features/onboarding/onboarding.component.html` — it must name what is being shown *and* point at searching for any other team (FR-002); DESIGN.md voice: sentence case, "you", no emoji
- [ ] T021 [US1] Add the ask-to-join action to `frontend/apps/web/src/app/features/onboarding/onboarding.component.html`: a **secondary** `jhButton` (`data-testid="onboarding-team-ask"`) shown only when a team is selected and not yet asked, labelled with the team's name, reading "Asking…" while `askingSlug` is set. Secondary because Continue is already this view's single coral CTA (DESIGN.md)
- [ ] T022 [US1] Add the pending confirmation to `frontend/apps/web/src/app/features/onboarding/onboarding.component.html` (`data-testid="onboarding-team-confirmation"`), worded so the approval is unmistakably still pending and membership is never implied (FR-013)

**Checkpoint**: The step finds real teams and produces a real pending request. This is the MVP and
closes the substance of issue #74.

---

## Phase 4: User Story 2 — Never be trapped by this step (Priority: P1)

**Goal**: Prove, and keep proving, that nothing on this step can hold a brand-new player in the
wizard.

**Independent Test**: Force the search to fail and the join request to fail; confirm Continue, "I'm
not on a team yet", and Back all still work and onboarding still completes.

**Note on ordering**: same priority as US1 and largely *verification* of a property the chosen design
already has — Continue was never given a network call (research §3). These tasks exist to make that
property permanent rather than accidental.

### Tests for User Story 2

- [ ] T023 [P] [US2] Add a spec to `frontend/apps/web/src/app/features/onboarding/onboarding.component.spec.ts` asserting that advancing past the team step issues **zero** HTTP requests — with no selection, with a selection, and after a successful ask (`httpMock.verify()` carries the assertion)
- [ ] T024 [US2] Add a spec asserting that after the opening search errors, the step renders its error state **and** `next()` still advances the flow
- [ ] T025 [US2] Add a spec asserting that a failed `askToJoin` (500) sets `teamRequestError`, leaves `requestedSlugs` empty, and still lets the flow advance and `finish()` complete
- [ ] T026 [US2] Add a spec asserting that a `409` from `askToJoin` produces the already-a-member message rather than the generic failure message, and is not retried (exactly one POST recorded)
- [ ] T027 [US2] Add a spec asserting the finish payload is byte-identical to today's after selecting a team but never asking — i.e. `PUT /api/v1/profiles/me` body unchanged and no join request sent (FR-019, SC-005)

### Implementation for User Story 2

- [ ] T028 [US2] Add the join-request failure line to `frontend/apps/web/src/app/features/onboarding/onboarding.component.html` (`data-testid="onboarding-team-request-error"`), rendering `teamRequestError` quietly — one plain sentence, no status code, no `jh-alert` shouting where a line will do
- [ ] T029 [US2] Map failures to copy in `askToJoin()` in `frontend/apps/web/src/app/features/onboarding/onboarding.component.ts` per [research.md](research.md) §7: `409` → "You're already on that team."; anything else → "We couldn't send that request just now." Never surface the status itself
- [ ] T030 [US2] Audit the `@case ('team')` block in `frontend/apps/web/src/app/features/onboarding/onboarding.component.html` and confirm no state binds `[disabled]` on Continue, "I'm not on a team yet", or Back, and that `next()`/`back()` in the component remain free of any team logic (FR-017, FR-018)

**Checkpoint**: Every failure path is quiet, honest, and escapable.

---

## Phase 5: User Story 3 — A calm, honest step that matches the app (Priority: P2)

**Goal**: The step is visually and verbally indistinguishable from the rest of JuggerHub, and its
empty and error states never impersonate each other.

**Independent Test**: Compare rows, loading line, empty state, and error state against the teams
browse screen and DESIGN.md; confirm a search returning nothing and a search that fails differ in
both wording and available action.

### Tests for User Story 3

- [ ] T031 [P] [US3] Add a spec to `frontend/apps/web/src/app/features/onboarding/onboarding.component.spec.ts` asserting a query with zero results renders `onboarding-team-empty` with no retry control, while a failed search renders `onboarding-team-error` **with** one
- [ ] T032 [US3] Add a spec asserting the step renders `jh-loading` (not a spinner) while the search is in flight

### Implementation for User Story 3

- [ ] T033 [US3] Add the four-state `@switch` on `teams.state()` to the `@case ('team')` block in `frontend/apps/web/src/app/features/onboarding/onboarding.component.html`: `loading` → `<jh-loading>` (`data-testid="onboarding-team-loading"`); `error` → message + **secondary** "Try again" calling `teams.reload()` (`data-testid="onboarding-team-error"`); `no-results` and `empty` → their two distinct messages (`data-testid="onboarding-team-empty"`); default → the rows from T019
- [ ] T034 [US3] Write the state copy in `frontend/apps/web/src/app/features/onboarding/onboarding.component.html` per [research.md](research.md) §7 — "no teams match that" invites another search and offers no retry; the failure offers one; the genuinely-empty opening list reads as "nothing here yet", never as an error
- [ ] T035 [US3] Verify the step's responsiveness inside the `max-w-sm` onboarding column on phone and desktop widths, including a long team name and a long city, and that rows keep a ≥44px touch target (DESIGN.md layout rules)

**Checkpoint**: All three stories work; the step reads as part of the product.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T036 Instantiate `specs/029-onboarding-team-search/checklists/ui-review.md` from `.specify/templates/ui-review-checklist-template.md` and verify every item against the diff — Quality Gate 7 is mandatory for UI-bearing changes
- [ ] T037 [P] Update the component doc comment in `frontend/apps/web/src/app/features/onboarding/onboarding.component.ts` so it no longer describes the team step as a stub, and note that Continue deliberately carries no network call
- [ ] T038 [P] Add a short amendment note to `specs/004-onboarding/spec.md` beside FR-021 pointing at `specs/029-onboarding-team-search/spec.md` — FR-021's text stays as the historical record; only a pointer is added
- [ ] T039 Run the full frontend verification: `cd frontend; npx nx test web`, `npx nx lint web`, `npx nx build web`
- [ ] T040 Walk [quickstart.md](quickstart.md) scenarios 1–9 against the running local stack, including the offline/throttled paths, which no unit test covers
- [ ] T041 Confirm no accessibility regression on the step: the search field is labelled, rows are real `<button>`s reachable by keyboard with a visible focus ring, and `jh-loading` still announces via `role="status"`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: after Setup — **blocks everything**, since it removes the placeholder and creates the state every story reads
- **US1 (Phase 3)**: after Foundational
- **US2 (Phase 4)**: after US1 — its tests assert behaviour of the ask action US1 builds
- **US3 (Phase 5)**: after Foundational; the state markup (T033) slots into the same block US1 fills, so in practice run it after US1
- **Polish (Phase 6)**: after all stories

### The honest picture

The three stories are *conceptually* independent but share two files, so they are **sequential in
practice**. Treat US1 as the deliverable increment and US2/US3 as the passes that make it fit to
ship. Splitting this across people would cost more in merge friction than it saves.

### Parallel Opportunities

Genuinely parallel (different files or additive-only test blocks):

- T008, T023, T031 — the three test-scaffolding tasks, each appending an independent `describe` block
- T037 and T038 — different files entirely

Everything else queues on `onboarding.component.ts` or `onboarding.component.html`.

---

## Implementation Strategy

### MVP (User Story 1)

1. Phase 1 → Phase 2 → Phase 3.
2. **Stop and validate**: quickstart scenarios 1–3. A real team is found and a real pending request
   exists in the admin queue.
3. This alone closes issue #74's substance.

### Then

4. Phase 4 — make "never blocked" permanent rather than incidental (quickstart 4–6).
5. Phase 5 — the calm states (quickstart 7–8).
6. Phase 6 — UI review gate, verification, and the 004 amendment pointer.

---

## Notes

- Commit per task or per logical group; keep the placeholder removal (T003/T007) in its own commit so
  the diff reads clearly.
- Reference `#74` in commit messages; the PR closes it.
- **Do not** add retry, timeout, or backoff anywhere in this feature — `retryInterceptor` already
  covers both calls correctly, and hand-rolled resilience is review-rejectable under constitution
  Principle VII.
- **Do not** reach for `BrowseShellComponent`; see [research.md](research.md) §4 for why.
