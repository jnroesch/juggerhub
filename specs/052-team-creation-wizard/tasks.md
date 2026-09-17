---

description: "Task list for feature 052 — Team Creation Wizard"
---

# Tasks: Team Creation Wizard

**Input**: Design documents from `/specs/052-team-creation-wizard/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/wizard-flow.md](./contracts/wizard-flow.md)

**Tests**: Included. Not TDD-by-request — the project already specs every component, and
`team-create.component.spec.ts` (186 lines) tests the screen being rewritten, so it *will* break
and must be rewritten with it. `quickstart.md` names the four specs that must be green.

**Organization**: grouped by user story. US1 alone is a shippable replacement for today's form.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1 / US2 / US3, mapping to the user stories in spec.md

## Path Conventions

Frontend only — `frontend/apps/web/`. **No task in this feature touches `backend/`.** If one seems
to, re-read plan.md: every call already exists.

---

## Phase 1: Setup

**Purpose**: nothing to install or configure. This phase exists to record that, so it is not
re-derived.

- [ ] T001 Confirm no setup is required: no dependency is added, no config changes, no migration, no backend file is touched. Verify by re-reading `specs/052-team-creation-wizard/plan.md` §Technical Context before starting T002.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the step machine every later phase hangs off. **Blocks US1, US2 and US3.**

- [ ] T002 Define the step machine in `frontend/apps/web/src/app/features/teams/team-create/team-create.component.ts`: `type Step = 'basics' | 'type' | 'review' | 'logo' | 'invite'` and `const STEPS: readonly Step[]`, plus the `step` signal and `stepIndex` computed, mirroring `features/events/event-create/event-create.component.ts`.
- [ ] T003 Add the `createdSlug = signal<string | null>(null)` latch to `team-create.component.ts` with the doc comment from `data-model.md` explaining that it is set once, never cleared, and that the Back affordance, the optional steps' reachability and the final navigation all derive from it — not from `stepIndex`.
- [ ] T004 Add the round-knob progress markup to `frontend/apps/web/src/app/features/teams/team-create/team-create.component.html`, lifted verbatim from `event-create.component.html` lines 4-9 so the two wizards cannot drift visually. Five knobs, rendered from `steps`/`stepIndex()`.

**Checkpoint**: the component compiles with a five-step skeleton and a progress bar.

---

## Phase 3: User Story 1 — Create a team, one question at a time (P1) 🎯 MVP

**Goal**: replace the single-screen form with the three pre-create steps, ending in a created team.

**Independent test**: create a team through the flow without touching the logo or invite steps, and
confirm it is identical in every stored and displayed respect to one created by today's form
(SC-002).

### Implementation

- [ ] T005 [US1] Move the name + slug `FormGroup`, `IDENTIFIER_*` bounds and `reasonParams` into the `basics` step in `team-create.component.ts`, unchanged from today's definitions.
- [ ] T006 [US1] Move the slug-availability pipeline into the new component **unchanged, all three explanatory comments included** — `distinctUntilChanged()` → `tap(clear verdict)` → `debounceTime(300)` → `switchMap` with `catchError` **inside**. Do not re-derive it; see `research.md` R7. In `team-create.component.ts`.
- [ ] T007 [US1] Move the `type` signal, `setType()` (including the Mixteam city clear) and `selectedCity`/`onCitySelected()` into the `type` step in `team-create.component.ts`.
- [ ] T008 [US1] Add per-step advance guards in `team-create.component.ts`: `basics` requires a valid name and slug plus a *completed positive* availability check (never merely the absence of a negative one); `type` requires a city iff `type() === 'CityTeam'`. These are the two halves of today's `canSubmit`, split across two step boundaries (FR-005, FR-006). App is zoneless — expose them as computed signals, not plain getters read from the template.
- [ ] T009 [US1] Add `next()` / `back()` to `team-create.component.ts`. `back()` must not be callable once `createdSlug()` is non-null. Answers live in signals and the `FormGroup`, so moving between steps preserves them with no extra work (FR-004).
- [ ] T010 [US1] Build the `basics` and `type` step blocks in `team-create.component.html` as `@if (step() === ...)` sections, reusing today's markup for the name field, the `/t/` slug field with `jhLowercase`, the availability status lines (`slug-ok`, `slug-bad`, `slug-check-failed`), the type toggle and `jh-city-picker`. Keep every existing `data-testid`.
- [ ] T011 [US1] Build the `review` step block in `team-create.component.html`: name, address, type and — for a city team — city, each with a control returning to the step that owns it (FR-007). It must read as a summary, not a form; see plan.md §Gate 7 risk 2.
- [ ] T012 [US1] Implement `create()` in `team-create.component.ts`: send the **identical** `CreateTeamRequest` today's `submit()` sends (`name.trim()`, `slug.trim()`, `type`, `location` only for a city team), call `membership.load()` on success as today does, latch `createdSlug`, and advance to `logo`. Never retry automatically — a replayed `POST /teams` creates a second team (plan.md §Principle VII).
- [ ] T013 [US1] Implement the FR-009 recovery in `team-create.component.ts`: on error, branch on **`err.status === 409`** and nothing else — return to `basics`, surface the reason through the existing `slugStatus` display path, and clear the stale positive verdict so Continue is held until a fresh check lands. Every other answer is retained. **Never match on the server's message**: it is English-only (`research.md` R3).
- [ ] T014 [US1] Handle every other create failure in `team-create.component.ts`: stay on `review`, show `problemDetail(err)`, clear `submitting`, allow another press (FR-010).

### Tests

- [ ] T015 [US1] Rewrite `frontend/apps/web/src/app/features/teams/team-create/team-create.component.spec.ts` against the new flow, keeping every existing assertion that is still true (availability states, the Mixteam city clear, the payload shape). Add: step advance and back with answers preserved; advance blocked without a positive check; advance blocked for a city team with no city.
- [ ] T016 [US1] Add a spec to `team-create.component.spec.ts` for the 409 recovery: a create rejected with status 409 returns to the `basics` step, retains name/type/city, and holds Continue until a fresh check resolves. Assert a **400** does *not* do this — it stays on `review`.
- [ ] T017 [US1] Add a spec to `team-create.component.spec.ts` asserting the payload sent by `create()` is byte-identical to the one today's form sends for the same answers (FR-027, SC-002), and that a failed create sends exactly one request (no automatic retry).

**Checkpoint**: US1 is independently shippable. The logo and invite steps can be stubbed as
"continue" for now, or the flow can navigate to `/t/{slug}` from `review` until Phase 4 lands.

---

## Phase 4: User Story 2 — Give the team a logo while creating it (P2)

**Goal**: an optional, skippable logo step acting on the team just created.

**Independent test**: create a team, upload a logo on the step, confirm it appears on the team page;
create another, skip, confirm the letter placeholder and an otherwise identical team.

### Implementation

- [ ] T018 [US2] Add the logo state to `team-create.component.ts`: `uploadingLogo` and `hasLogo` signals, and a `logoUrl` computed reading `teams.logoUrl(createdSlug()!)`.
- [ ] T019 [US2] Implement `onLogoSelected(event)` in `team-create.component.ts` following `features/teams/team-settings/team-settings.component.ts` `onLogoSelected()`: upload immediately via `teams.uploadLogo(slug, file)`, guard re-entry while busy, set `hasLogo` on success, and rely on the service's per-slug revision bump so the new image actually renders (the GH #283 fix — `research.md` R4). **No held `File`, no pre-upload preview** (FR-016 as amended).
- [ ] T020 [US2] Handle a refused or failed upload in `team-create.component.ts`: report it on the step, leave the team untouched, clear `uploadingLogo`, and keep both retry and skip available (FR-018). No automatic retry.
- [ ] T021 [US2] Build the `logo` step block in `team-create.component.html`: the current logo or the letter placeholder (the same fallback every other surface uses), a file input restricted to the image types 051 accepts, a Continue and a Skip. Both lead to the `invite` step. No Back is rendered (FR-011).
- [ ] T022 [US2] Add a spec to `team-create.component.spec.ts`: choosing a file uploads immediately and flips to the uploaded logo; skipping uploads nothing; a failed upload shows an error and leaves both retry and skip usable; no Back control is rendered on this step.

**Checkpoint**: US1 + US2 deliverable together.

---

## Phase 5: User Story 3 — Invite people while creating the team (P3)

**Goal**: an optional, skippable invite step, sharing one component with the existing invitations
screen.

**Independent test**: create a team, invite two players from the step, confirm both are pending on
the team's invitations screen and received the same invitation that screen would have sent.

### Extraction (do this before the wizard's step — T023-T025 block T026)

- [ ] T023 [US3] Create `frontend/apps/web/src/app/features/teams/components/invite-search/invite-search.component.ts` per `contracts/wizard-flow.md` §Extracted component contract: required `slug` input, `invited` output, owning the search control, the 300ms debounce, the results signal, the optimistic `Invitable → Invited` flip, and its own error line. Lift the logic verbatim from `team-invitations.component.ts` (`searchControl` pipeline and `invite()`).
- [ ] T024 [US3] Create `invite-search.component.html` by lifting `team-invitations.component.html` lines 47-78 **unchanged** — markup, tokens, the `@switch (u.relation)`, and the `user-search` / `invite-<handle>` `data-testid`s — then add the empty state FR-025 requires (a search matching nobody says so instead of rendering an empty list). DESIGN.md §empty states governs the wording.
- [ ] T025 [US3] Rewrite `team-invitations.component.html` to render `<jh-invite-search [slug]="slug()" (invited)="reload()" />` in place of the removed block, and delete the now-unused search state from `team-invitations.component.ts` (`results`, `searching`, `searchControl` and its pipeline, and the `invite()` method). The invite link, pending list, rotate and revoke halves are untouched.

### Implementation

- [ ] T026 [US3] Build the `invite` step block in `team-create.component.html`: `<jh-invite-search [slug]="createdSlug()!" />` with a Finish and a Skip, both navigating to `/t/{createdSlug()}` (FR-014). No Back is rendered. Register the component in the `imports` array in `team-create.component.ts`.

### Tests

- [ ] T027 [P] [US3] Create `frontend/apps/web/src/app/features/teams/components/invite-search/invite-search.component.spec.ts`: the debounce; the three relation renderings; that a `Member` and an `Invited` row offer no invite action; that inviting flips the row to `Invited` and emits `invited`; that a failed invitation shows an error without affecting other rows (FR-026); and the empty state.
- [ ] T028 [P] [US3] Update `frontend/apps/web/src/app/features/teams/team-invitations/team-invitations.component.spec.ts` so the extraction is proven behaviour-preserving: the screen still searches, still invites, and still reloads its pending list after an invite. Keep the existing link/pending/revoke assertions untouched.
- [ ] T029 [US3] Add a spec to `team-create.component.spec.ts` asserting the invite step renders `jh-invite-search` bound to the created slug, and that both Finish and Skip navigate to `/t/{slug}`.

**Checkpoint**: all three stories complete.

---

## Phase 6: Polish & Cross-Cutting

- [ ] T030 Add every new copy key to **all three** catalogues in the same change — `frontend/apps/web/public/i18n/en.json`, `de.json` and `es.json`. Step titles and helper text for the five steps, the review step's labels, the Continue/Back/Skip/Finish actions, the logo step's prompt and errors, and the invite step's prompt plus the new empty state. Editing `en.json` alone turns `core/i18n/catalog-parity.spec.ts` red. Keep the existing `teams.create.*` and `teams.invitations.*` keys that still apply; do not rename what the extraction moved.
- [ ] T031 Verify the copy follows DESIGN.md's encouraging, low-pressure voice — in particular that Skip on both optional steps reads as a choice declined, never as leaving the team unfinished (FR-013, plan.md §Gate 7 risk 3).
- [ ] T032 **Gate 7**: copy `.specify/templates/ui-review-checklist-template.md` to `specs/052-team-creation-wizard/checklists/ui-review.md` and verify every item against the diff. DESIGN.md wins on any conflict; report conflicts rather than resolving them silently. Check at minimum the five knobs at 375px, the **review step in German at 375px** (the binding case), the review step reading as a summary not a form, and the empty/loading/error states on both optional steps.
- [ ] T033 Confirm the route is untouched: `teams/new` is still one route, one component, behind `authGuard`, in `frontend/apps/web/src/app/app.routes.ts`. Steps are not routed (plan.md §Structure Decision).
- [ ] T034 Re-read the diff against plan.md's boundaries: no file under `backend/`, no migration, no new dependency in `frontend/package.json`, no `AddJuggerHubResilience`/retry/backoff anywhere, and no automatic retry of any of the three mutations.
- [ ] T035 Run `cd frontend && npm test` (`nx test web --watch=false`). The four specs `quickstart.md` names must be green: `team-create`, `invite-search`, `team-invitations`, `catalog-parity`.
- [ ] T036 Run `cd frontend && npm run lint` and record the result. Do not claim a clean run if pre-existing warnings remain — state the counts.
- [ ] T037 Run `cd frontend && npm run build` and confirm the production build succeeds.
- [ ] T038 Walk `quickstart.md` scenarios 1-6 by hand, in particular scenario 2 (the 409 race, which needs two browsers) and scenario 6 (the existing invitations screen still works).

---

## Dependencies

```
Phase 1 (T001)
   └─► Phase 2 (T002-T004)  ◄── blocks everything
          ├─► Phase 3 US1 (T005-T017)   🎯 MVP, independently shippable
          │       └─► Phase 4 US2 (T018-T022)   needs the latch from T003 and the created team from T012
          │              └─► Phase 5 US3 (T023-T029)
          └─────────────────► Phase 6 (T030-T038)
```

- **US2 depends on US1** only for the created team to act on — not for its logic.
- **US3 depends on US1** for the same reason. It does **not** depend on US2: the invite step can
  follow `review` directly if US2 is dropped.
- **T023-T025 (the extraction) block T026** — the wizard's invite step renders the component the
  extraction creates.
- **T030 (i18n) should land with the markup it serves**, not at the very end, or every intermediate
  checkpoint renders raw keys.

## Parallel Opportunities

- **T027 and T028** are different spec files with no shared state — run together.
- **T010 and T011** touch the same template and must not be parallelised, despite being separate
  steps.
- **T023/T024** (new files) can proceed while T025 (editing the existing screen) is being written,
  but T025 must not be committed before them or the invitations screen references a component that
  does not exist.
- The three catalogues in T030 are one task deliberately — splitting them is how parity goes red.

## Implementation Strategy

**MVP is Phase 1 + Phase 2 + Phase 3 (US1) + the T030 keys those steps need.** That alone replaces
today's form with the house wizard and loses nothing — the logo and invite capabilities remain
exactly where they have always been, in team settings and the invitations screen (FR-029).

Then US2, then US3, each a self-contained increment with its own checkpoint. Small commits per
phase, verification after each, per CLAUDE.md §Execution.
