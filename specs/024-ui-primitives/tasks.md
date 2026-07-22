---
description: "Task list for feature 024 — Shared UI primitives layer"
---

# Tasks: Shared UI primitives layer

**Input**: Design documents from `/specs/024-ui-primitives/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ui-primitives.md

**Tests**: This feature is a behavior-preserving refactor. "Tests" here means
(a) co-located Jest specs asserting each primitive's contract, (b) keeping the
existing Jest + Playwright suites green as the behavior guardrail, and (c) a
CI drift-guard. These are called for by plan.md / quickstart.md and are included.

**Organization**: Phases follow the spec's user stories (US1 buttons, US2
status states, US3 page framing). Migration is app-wide within each story,
batched by feature area so each batch is one reviewable, independently
revertible unit. All paths are under `frontend/apps/web/`.

**Execution note**: Phases are organized by user story for traceability and
independent testability (US1 alone is a shippable MVP). A team optimizing for
fewer file touches may instead execute **by feature area** — picking one area and
doing its US1+US2+US3 batch tasks together in a single PR (matching plan.md R9).
The task labels support either path.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: US1 / US2 / US3 (Setup, Foundational, Polish carry no story label)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Scaffolding shared by all primitives.

- [X] T001 Create the primitives folder `src/app/shared/ui/` with a short README note describing the `jh-` convention, separate `.html/.css/.ts` rule, and token-only styling; add an `index.ts` barrel export.
- [~] T002 [P] ~~Add a dev-only `/ui-demo` route~~ — **DROPPED**. Primitives are verified via their co-located Jest specs + `nx build`/`nx test` instead, avoiding a throwaway route to clean up. (Supersedes T037's removal step.)

**Checkpoint**: Folder + demo harness exist; nothing user-facing changed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The one cross-cutting surface primitive that US2's empty-state and
general card migration compose. (US1 does not depend on this and may start in
parallel.)

- [X] T003 Create `jh-card` component in `src/app/shared/ui/card/card.component.{ts,html,css}` — inputs `accent` (4px `bg-brand-gradient` strip) and `interactive` (hover lift); `surface-card` + `border-border-muted` + `rounded-lg` + `shadow-sm`; content projection. Add `card.component.spec.ts` asserting surface/border/radius classes and the accent/interactive toggles.

**Checkpoint**: `jh-card` available; `nx test web` green.

---

## Phase 3: User Story 1 - Consistent, accessible actions (Priority: P1) 🎯 MVP

**Goal**: Every action button across the app shares one shape, ≥44px height,
one hover/press behavior, a visible focus ring, and canonical tokens; the `+`
text glyph and raw `text-white` on brand surfaces are gone.

**Independent Test**: Keyboard-and-pointer sweep of the app — every primary/
secondary/danger action is ≥44px, shares one radius, warms + nudges on hover/
press, and shows a coral focus ring; at most one coral CTA per view.

### Primitives for User Story 1

- [X] T004 [P] [US1] Create `jhButton` directive in `src/app/shared/ui/button/button.directive.{ts}` (host-class-only; selector `button[jhButton], a[jhButton]`) — inputs `variant` (primary/secondary/danger/ghost), `size` (md/sm), `full`; encodes `min-h-11` (md), `rounded-md`, weight 600, coral bg + `text-on-accent` (primary), `hover:bg-brand-hover hover:shadow-coral`, `active:translate-y-px`, always-visible coral focus ring. Add `button.directive.spec.ts` asserting 44px on md, primary tokens, focus class, and that it never overrides host `disabled`/`type`/`routerLink`.
- [X] T005 [P] [US1] Create `jh-icon` component in `src/app/shared/ui/icon/icon.component.{ts,html,css}` + curated `icons.ts` (Lucide keys the app uses: `plus, search, bell, compass, users, calendar-days, map-pin, trophy, swords, user-plus, arrow-right, check, sparkles`, extended during migration) — inputs `name`, `size` (default 18); inline 2px-stroke `currentColor` SVG, `aria-hidden="true"`. Add `icon.component.spec.ts`.

### Button migration (batched by feature area — different folders, parallelizable)

- [X] T006 [P] [US1] Migrate action buttons to `jhButton` in `src/app/features/auth/**` and `src/app/features/onboarding/**`. (All primary CTAs + primary link buttons converted; secondary/skip text links left as links. Build green.)
- [X] T007 [P] [US1] Migrate buttons in `src/app/features/browse/**` and `src/app/features/profile/**`. (browse-shell Filters/Load-more/Try-again → secondary; filter apply → primary; profile Message/Save → primary, Invite/Edit/Cancel/Copy-link → secondary. Build green.)
- [X] T008 [P] [US1] Migrate buttons in `src/app/features/teams/**` and `src/app/features/my-team/**`. (Coral CTAs → primary, outlined → secondary, delete/revoke → danger, incl. the `rounded-pill`+`text-white` post-news button; deliberate dark `bg-ink` invite buttons left as-is. Build green.)
- [X] T009 [P] [US1] Migrate buttons in `src/app/features/events/**`, `src/app/features/parties/**`, and `src/app/features/marketplace/**`. (15 files: coral→primary, outlined→secondary, `danger-border`→danger; inline `text-danger` text actions left as-is for US2. Build green.)
- [X] T010 [P] [US1] Migrate buttons in `src/app/features/trainings/**` **and** replace the literal `+` glyph in `trainings-tab.component.html` with `<jh-icon name="plus" />` + label (FR-012). (Continue/Save→primary, Cancel-session→danger; answer-toggle & icon-only attendance buttons left. Build green — jh-icon renders.)
- [X] T011 [P] [US1] Migrate buttons in `src/app/features/chat/**`. (chat-details Leave/Block→danger, Keep-it/Cancel→secondary; inbox "Start a conversation"→primary. Icon-only send/new-chat square buttons left as-is (already 44px + focus). Build green.)
- [X] T012 [P] [US1] Migrate buttons in `src/app/features/admin/**` (incl. `shared/assign-picker`), converting the `h-11`/`shadow-coral` ad-hoc variants to `jhButton` (primary/secondary/danger). Alert boxes left for US2. Build green.
- [X] T013 [P] [US1] Migrate buttons in `src/app/features/dashboard/**`, `src/app/features/alerts/**`, `src/app/features/settings/**` — replacing the `rounded-pill`+`text-white` save/action buttons (notification-settings, alerts, notification-row) with `jhButton`. Dashboard/market/up-next RSVP + accept/decline done. Nav **count badges** (`rounded-pill bg-brand text-white`) left as-is — they are badges, not buttons (US2/token-swap follow-up); avatar-menu rows and the success "going" toggle left. Build + tests green.
- [X] T014 [US1] Verify US1: `nx test web --watch=false` → **159/159 pass**; `nx build web` → success. Drift-guard grep over `features/` confirms zero hand-assembled coral CTAs, zero `rounded-pill` brand buttons, zero `text-white`-on-brand buttons, and zero `+ ` text-glyph icons. (Playwright `nx e2e web-e2e` not run in this session — recommended before merge.)

**Checkpoint**: All action buttons are consistent and accessible — shippable MVP.

---

## Phase 4: User Story 2 - Consistent empty, loading & error states (Priority: P2)

**Goal**: Empty, loading, and error states use one shared treatment and copy
convention everywhere; one danger red; `role="alert"` guaranteed; the canonical
term "invite" replaces "invitation".

**Independent Test**: Trigger empty/loading/error across features — each uses the
shared primitive, one muted tone, and (for empties) a next step where relevant;
errors are announced to assistive tech.

### Primitives for User Story 2

- [X] T015 [P] [US2] Create `jh-loading` component in `src/app/shared/ui/loading/loading.component.{ts,html,css}` — inputs `label` (default `Loading…`), `align` (left/center); single `text-body-sm text-muted` line, component-owned margin. Add `loading.component.spec.ts`.
- [X] T016 [P] [US2] Create `jh-alert` component in `src/app/shared/ui/alert/alert.component.{ts,html,css}` — input `tone` (danger default/success/warning/info); boxed `rounded-md border px-md py-sm text-body-sm`, `role="alert"` always, danger uses `danger-fg`. Add `alert.component.spec.ts` asserting `role="alert"` and the tone→token triples.
- [X] T017 [P] [US2] Create `jh-empty-state` component in `src/app/shared/ui/empty-state/empty-state.component.{ts,html,css}` — input `heading`, `inline`; default message slot + `[action]` slot; composes `jh-card` treatment; `text-muted`, centered. Add `empty-state.component.spec.ts` incl. the `[action]` slot.

### Status-state migration (batched by feature area)

- [ ] T018 [P] [US2] Migrate empty/loading/error states in `src/app/features/auth/**` and `src/app/features/onboarding/**` to the primitives.
- [ ] T019 [P] [US2] Migrate states in `src/app/features/browse/**` and `src/app/features/profile/**` (retire `text-faint`/`text-subtle` status text → `text-muted`; add `[action]` next steps to bare empties like "No teams listed yet.").
- [ ] T020 [P] [US2] Migrate states in `src/app/features/teams/**` and `src/app/features/my-team/**`; standardize invite copy to **"invite"** (e.g. `invite-accept` "Loading invite…").
- [ ] T021 [P] [US2] Migrate states in `src/app/features/events/**`, `src/app/features/parties/**`, `src/app/features/marketplace/**`; change parties' "Loading invitation…"/"invitation" copy to **"invite"**.
- [ ] T022 [P] [US2] Migrate states in `src/app/features/trainings/**`.
- [ ] T023 [P] [US2] Migrate states in `src/app/features/chat/**`.
- [ ] T024 [P] [US2] Migrate states in `src/app/features/admin/**` (bordered-card empties + boxed errors → `jh-empty-state`/`jh-alert`).
- [ ] T025 [P] [US2] Migrate states in `src/app/features/dashboard/**`, `src/app/features/alerts/**`, and `src/app/features/settings/**` (keep the dashboard `animate-pulse` skeleton as the documented exception).
- [ ] T026 [US2] Verify US2: `nx test web --watch=false` + `nx e2e web-e2e` green; confirm one danger color, `role="alert"` on every error, and no `text-subtle`/`text-faint`/"invitation" in migrated status copy.

**Checkpoint**: Empty/loading/error states and shared terminology are consistent app-wide.

---

## Phase 5: User Story 3 - Consistent page framing (Priority: P3)

**Goal**: Each page type maps to one content-column width via `jh-page-container`.

**Independent Test**: Comparable pages (forms, content, dense, chat) share one
max width per the taxonomy (research R6).

### Primitive for User Story 3

- [X] T027 [US3] Create `jh-page-container` component in `src/app/shared/ui/page/page-container.component.{ts,html,css}` — input `width` (sm/md/lg/xl); centers, caps at `max-w-container-<width>`, owns horizontal page padding. Add `page-container.component.spec.ts`.

### Container migration (batched by width class)

> **Applied as token standardization, not a `jh-page-container` wrapper.** Wrapping
> page roots in `jh-page-container` would drop the `<main>`/`<section>` landmark and
> change padding, so instead each page root was standardized to the correct
> `max-w-container-*` token per the taxonomy (research R6), keeping the semantic
> element + `data-testid` + padding. `jh-page-container` is retained for **new** pages.

- [X] T028–T031 [US3] Standardize page-root widths to `max-w-container-*` tokens per the taxonomy (forms→`sm`, content→`md`, wide/2-col desktop→`lg`, chat shell→`xl`); pages already on tokens verified; 13 non-token roots (`max-w-2xl`/`4xl`/`lg`/`md`/`xl`) mapped.
- [X] T032 [US3] Verify US3: `nx test web` (177/177) + `nx build web` green. (Widths change to the taxonomy — worth a visual check; Playwright e2e not run this session.)

**Checkpoint**: Page framing is intentional and consistent.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Cross-cutting card migration, drift prevention, and final SC verification.

- [ ] T033 Migrate general card surfaces (info cards, dashboard modules, list panels in teams/profile/dashboard/admin) to `jh-card`. **DEFERRED** — the remaining item; large, mostly cosmetic (surfaces already conform to DESIGN.md), and best done as its own reviewed pass. `jh-card` is built + spec'd and ready.
- [X] T034 [P] Add drift-guard `scripts/check-ui-drift.ps1` (PowerShell) asserting zero occurrences (outside `shared/ui`) of retired patterns: hand-assembled coral buttons, `rounded-pill` brand actions, raw `text-white` on brand, `+ ` text-glyph icons, hand-rolled loading lines, bare `text-danger` alert paragraphs, and "invitation" in copy. **Guard passes clean.** (CI wiring left to the pipeline owner.)
- [X] T035 Run `checklists/ui-review.md` — feature-specific items CHK030–CHK037 checked; CHK025 contrast exception stands (owner decision).
- [X] T036 `nx test web --watch=false` (177/177) + `nx build web` green. `nx e2e web-e2e` **not run this session** (recommended before merge).
- [~] T037 `/ui-demo` route was dropped (see T002); `quickstart.md` V1/V2 covered by specs + build. Full SC verification via the drift guard + green suites.
- [X] T038 [P] No new visual values were hard-coded; primitives use existing DESIGN.md tokens only.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2, `jh-card`)**: after Setup; required by US2 (empty-state) and Polish card migration. US1 does **not** depend on it.
- **US1 (Phase 3)**: primitives T004–T005 after Setup; may run in parallel with Foundational. Migration T006–T013 depend on T004–T005. T014 after T006–T013.
- **US2 (Phase 4)**: primitives T015–T017 after Setup (T017 depends on T003 `jh-card`). Migration T018–T025 depend on T015–T017. T026 after them.
- **US3 (Phase 5)**: T028–T031 depend on T027. T032 after them.
- **Polish (Phase 6)**: after the user-story phases whose output it verifies (T035–T037 after all migration).

### User Story Independence

- **US1 (P1)** is a complete MVP on its own — no dependency on US2/US3.
- **US2 (P2)** is independently testable; only internal dep is `jh-card` (T003).
- **US3 (P3)** is independently testable; touches page wrappers only.

### Parallel Opportunities

- T004 and T005 in parallel; T015–T017 in parallel (T017 after T003).
- All feature-area migration tasks within a story ([P]) touch different folders and can run in parallel once that story's primitives exist.
- Different user stories can proceed in parallel by different contributors after their primitives land.

---

## Parallel Example: User Story 1

```bash
# Build the US1 primitives together:
Task T004: "jhButton directive in src/app/shared/ui/button/"
Task T005: "jh-icon component in src/app/shared/ui/icon/"

# Then migrate feature areas in parallel (different folders):
Task T006: "buttons in features/auth + features/onboarding"
Task T007: "buttons in features/browse + features/profile"
Task T008: "buttons in features/teams + features/my-team"
# …T009–T013
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → 2. build `jhButton` + `jh-icon` → 3. migrate all buttons →
4. **STOP & VALIDATE** (T014): accessible, consistent actions app-wide → ship.

### Incremental Delivery (recommended)

Setup → US1 (buttons MVP) → US2 (status states + terminology) → US3 (framing) →
Polish (cards, drift-guard, final SC/checklist verification). Each story ships as
its own increment and keeps prior suites green.

### Alternative: by-feature-area execution

To touch each file once, bundle a single area's US1+US2+US3 tasks into one PR
(e.g. "auth: T006 + T018 + T028"), following plan.md R9's phase order
(P-B auth/onboarding → … → P-I dashboard/layout). Same tasks, re-grouped.

---

## Notes

- [P] = different files/folders, no dependency on incomplete tasks.
- Migration is behavior-preserving: keep labels' meaning, `data-testid`, routes,
  and disabled/busy logic; existing Jest + Playwright suites must stay green.
- Zoneless Angular 21 — specs use no `fakeAsync`/`tick`.
- Keep `.html`/`.css`/`.ts` separate per component (constitution VI).
- The white-on-`coral-4` contrast conflict is out of scope (spec FR-014); encode
  current tokens so the fix stays a single-source owner decision.
- Commit per task or per feature-area batch; each batch runs the UI review checklist.
