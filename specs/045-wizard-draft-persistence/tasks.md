---

description: "Task list for 045 — Wizard drafts survive leaving the page"
---

# Tasks: Wizard drafts survive leaving the page

**Input**: Design documents from `/specs/045-wizard-draft-persistence/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/wizard-draft.md](./contracts/wizard-draft.md), [quickstart.md](./quickstart.md)

**Tests**: **Included.** SC-003 requires all 16 training answers and all 21 event answers to be verified individually, and SC-007 requires proven behaviour when storage throws — neither is checkable by eye. The store's compatibility rule (R5) is likewise only observable through a test.

**Organization**: grouped by user story. US1 alone is a complete, shippable fix for GH #182.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on incomplete work)
- **[Story]**: US1 (training wizard), US2 (event wizard), US3 (privacy policy)
- All paths are relative to the repository root

## Path Conventions

Frontend-only feature. Everything lives under `frontend/apps/web/`. **No task in this feature touches `backend/`, and none produces a migration** — if one appears to, stop and re-read the plan.

---

## Phase 1: Setup

**Purpose**: nothing to install. This phase exists to state the boundary before any code is written.

- [X] T001 Confirm no new dependency is needed: read `frontend/package.json` and verify the feature uses only Angular core (`signal`, `effect`, `model`), `@angular/forms`, and the browser `sessionStorage` API. Do not add a package.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the draft store and its shape. Both wizards depend on it.

**⚠️ CRITICAL**: no user story work can begin until T002–T005 are complete.

- [X] T002 [P] Create the draft shapes in `frontend/apps/web/src/app/core/drafts/wizard-draft.models.ts`: `TrainingDraft` (16 answers + `step` + `v`) and `EventDraft` (21 answers + `step` + `v`) exactly as enumerated in [data-model.md](./data-model.md), plus `export const DRAFT_VERSION = 1`. Include the doc comment stating that `DRAFT_VERSION` MUST be bumped whenever a persisted field is added, removed, renamed or changes meaning, and that `busy`/`submitting`/`error`/`slug` are deliberately excluded.
- [X] T003 Write failing tests for the store in `frontend/apps/web/src/app/core/drafts/wizard-draft.store.spec.ts`: round-trip of both shapes; per-slug isolation of training drafts; `clearAll()` removing every `jh-draft:` key; and the compatibility rule (R5) — unparseable JSON, a non-object value, a missing `v`, and a `v` that differs are each discarded **and removed** from storage. Also assert every operation returns normally when `sessionStorage` throws (FR-015), by stubbing the accessor to throw.
- [X] T004 Implement `WizardDraftStore` in `frontend/apps/web/src/app/core/drafts/wizard-draft.store.ts` (`@Injectable({ providedIn: 'root' })`) satisfying [contracts/wizard-draft.md](./contracts/wizard-draft.md): `readTraining`/`writeTraining`/`clearTraining` keyed `jh-draft:training:<slug>`, `readEvent`/`writeEvent`/`clearEvent` keyed `jh-draft:event`, and `clearAll()`. Wrap **every** storage access in `try`/`catch`, copying the private-mode pattern from `frontend/apps/web/src/app/core/chunk-load-error.handler.ts` lines 65-72. No `HttpClient` dependency — this class must never acquire one. Make T003 pass.
- [X] T005 Clear drafts on sign-out in `frontend/apps/web/src/app/core/services/auth.service.ts` (FR-011, R6): inject `WizardDraftStore` and call `clearAll()` from the `tap` in `logout()` and from `clearSession()` — the two points where the client concludes the session is over. Add the assertion to `frontend/apps/web/src/app/core/services/auth.service.spec.ts` beside the existing "logout clears authenticated state" test.

**Checkpoint**: the store exists, is proven safe when storage fails, and is wired to sign-out. Wizard work can begin.

---

## Phase 3: User Story 1 — A half-filled training survives a detour (Priority: P1) 🎯 MVP

**Goal**: the create-training wizard restores every answer and the step after in-app navigation, a reload, or a discarded tab.

**Independent Test**: quickstart scenarios 1–7. Fill steps 1–4, leave and return, reload, and urgently discard the tab in `chrome://discards` — every answer present, on the step left, with the city chip populated.

### Refactor first (R2 — required before persistence can observe anything)

- [X] T006 [US1] Convert the 10 plain instance fields in `frontend/apps/web/src/app/features/trainings/training-create/training-create.component.ts` to signals: `isRecurring`, `name`, `weekday`, `interval`, `startTime`, `endTime`, `startDate`, `endDate`, `description`, `visibility`. The app is zoneless and an `effect()` cannot observe plain properties — the file's own comment at lines 46-49 documents this hazard. Update `summaryCount`, `create()` and `cancel()` to read them as signals.
- [X] T007 [US1] Update `frontend/apps/web/src/app/features/trainings/training-create/training-create.component.html` for the signal conversion: `[(ngModel)]="x"` becomes `[ngModel]="x()" (ngModelChange)="x.set($event)"`, and the toggle `(click)="x = v"` handlers become `(click)="x.set(v)"`. **Copy the existing idiom at line 92** (`virtualLink`), which already uses this exact form. Review-check every one of the 10 — a missed binding silently stops persisting that field.
- [X] T008 [US1] Verify the refactor changed no behaviour: run `npm test` in `frontend/` and confirm the existing training-create tests still pass before any persistence is added. If none exist for a converted field, that gap is closed by T011.

### Persistence

- [X] T009 [US1] Restore in the field initialiser in `frontend/apps/web/src/app/features/trainings/training-create/training-create.component.ts` — **not `ngOnInit`, not asynchronously** (R3): read the draft for `this.slug`, and when present seed every signal and `step` from it. Capture the pristine snapshot **before** restoring, for the FR-013 comparison.
- [X] T010 [US1] Add `[initialCity]="draftCity"` to the `<jh-address-fields>` element in `frontend/apps/web/src/app/features/trainings/training-create/training-create.component.html`, bound to the restored `CityOption` (structurally assignable to `Location`). **Without this the city chip renders empty while the review step prints the restored city** — `CityPickerComponent` consumes `initial` in `ngOnInit` and the ⚠ in `address-fields.component.ts` lines 37-45 says so. This binding does not exist on the create form today; the three edit forms are the precedent.
- [X] T011 [US1] Write the component tests in `frontend/apps/web/src/app/features/trainings/training-create/training-create.component.spec.ts`: **all 16 answers plus the step restored individually** (SC-003 — do not assert a sample); the city chip populated after restore, not just the parent signal; no draft written when nothing was changed from pristine (FR-013, and note pristine is *not* blank — `weekday` defaults to today and the times to 19:00/21:00); a draft with a stale `v` yielding a blank wizard; and the wizard working end to end when storage throws (SC-007).
- [X] T012 [US1] Add the persistence `effect()` in `frontend/apps/web/src/app/features/trainings/training-create/training-create.component.ts`: read every answer signal and `step`, and write the draft when the current state differs from the pristine snapshot. Synchronous, **no debounce** (R2 — a debounce window is a loss window). Never persist `busy` or `error`.
- [X] T013 [US1] Clear the draft on the two exits in the same component: in the `next` handler of the `create()` subscription **after the server has accepted**, before navigating (FR-007) — never optimistically, or a rejected create discards the user's work; and in `cancel()` (FR-008). Confirm the existing `error` path leaves the draft intact and that its `step.set(2)` reaches the draft like any other change.

**Checkpoint**: GH #182 as reported is fixed. This is a complete, shippable increment.

---

## Phase 4: User Story 2 — A half-filled event survives the same detour (Priority: P2)

**Goal**: the same for the create-event wizard, including the fee step.

**Independent Test**: quickstart scenario 8. Fill through the fee step with a recipient name and IBAN, leave and return, reload — everything restored on the step left.

**Note**: no signal refactor here. The event wizard already holds its answers in a `FormGroup` plus signals, so `form.valueChanges` and an `effect()` cover it (R2).

- [X] T014 [US2] Restore in the field initialiser in `frontend/apps/web/src/app/features/events/event-create/event-create.component.ts` (R3): read the event draft and, when present, `patchValue` the `FormGroup` and set the 7 signals and `step`. Capture the pristine snapshot before restoring.
- [X] T015 [US2] Add `[initialCity]` to the `<jh-address-fields>` element in `frontend/apps/web/src/app/features/events/event-create/event-create.component.html`, bound to the restored city — same trap as T010.
- [X] T016 [US2] Write the component tests in `frontend/apps/web/src/app/features/events/event-create/event-create.component.spec.ts`: **all 21 answers plus the step restored individually** (SC-003), explicitly including `feeRecipientName` and `feeIban`; the city chip populated; the empty-draft rule against the wizard's non-blank defaults (limit 16, cap 8, `EUR`); and a stale-`v` draft yielding a blank wizard.
- [X] T017 [US2] Add persistence in `frontend/apps/web/src/app/features/events/event-create/event-create.component.ts`: one `effect()` covering `step` and the 7 signals (`type`, `locationKind`, `participantMode`, `isPaid`, `venueName`, `street`, `postalCode`, `selectedCity`) **plus** a `form.valueChanges` subscription for the 13 controls. **Count 21 at review** — the signals sit outside the `FormGroup` and are the easy half to forget (plan, risk 3). Never persist `submitting` or `error`.
- [X] T018 [US2] Clear the event draft in the `next` handler of the `publish()` subscription, after the server accepts and before navigating (FR-007). The event wizard has no Cancel, so this and sign-out are its only in-app clears — the accepted consequence is recorded in the spec's Decision on restore surfacing and needs no code.

**Checkpoint**: both wizards restore. The feature is functionally complete.

---

## Phase 5: User Story 3 — The privacy policy says what is now kept in the browser (Priority: P3)

**Goal**: the policy stops claiming nothing is stored on the device, and names the draft.

**Independent Test**: quickstart scenario 13 — read `/privacy` in all three languages.

**Order matters**: German is authoritative and is written first; en/es follow it. All three are edited in the same change or `legal-catalog.spec.ts` DM-1 fails — which is the guard working, not a broken test.

- [X] T019 [US3] Edit `frontend/apps/web/public/i18n/legal/de.json` (**authoritative — write this one first**): in `privacy.sections.storage.body`, add an entry naming the unfinished create-form draft — that it stays on the device, never reaches the server, and goes when the form is finished or cancelled, when the tab is closed, or on sign-out; say that for an event it can include the fee recipient and account number (FR-019). In `privacy.sections.legalBasis.body`, replace the clause asserting nothing at all is stored ("Unsere speichert dort nichts — kein Cookie, kein Local Storage, nichts") so the § 25 TDDDG argument rests on the strictly-necessary limb, which covers the sign-in cookie, the language preference and the draft of the form the reader is filling in (FR-020). Match the document's existing plain, non-euphemistic voice.
- [X] T020 [P] [US3] Apply the same two edits to `frontend/apps/web/public/i18n/legal/en.json`, following the German. The sentence to replace is at line 162; the `storage` section is at lines 167-174.
- [X] T021 [P] [US3] Apply the same two edits to `frontend/apps/web/public/i18n/legal/es.json`, following the German.
- [X] T022 [US3] Run `npm test` in `frontend/` and confirm `core/i18n/legal-catalog.spec.ts` is green — DM-1 (identical key sets across en/de/es) is what enforces FR-021. **Add no new guard**; it already exists and was verified by reading it (R8).
- [X] T023 [US3] Do **not** touch the `privacy.sections.analytics` "stores nothing on your device" claims (`en.json:147`, `:154` and their de/es counterparts). They are scoped to the analytics tool and remain true — feature 038 verified the recorder uses no client-side storage API. This task is a review check, not an edit.

**Checkpoint**: the policy is accurate in all three languages.

---

## Phase 6: Polish & Cross-Cutting

- [X] T024 Run the full frontend gate from `frontend/`: `npm test`, `npm run lint`, `npm run build`. All three green.
- [ ] T025 Walk [quickstart.md](./quickstart.md) scenarios 1–13 by hand, including scenario 3 (`chrome://discards` → Urgent Discard), which is the reported failure and the only one that proves the eviction case.
- [ ] T026 Verify SC-006 with DevTools → Network: fill both wizards completely without submitting and confirm no request carries draft content. The only traffic should be the city picker's pre-existing lookups.
- [X] T027 Confirm the constitution boundaries held: no file under `backend/` changed, no migration exists, no dependency was added, no `.sh` script was added, and **no retry/timeout/breaker was introduced** — Principle VII is not engaged and wrapping a `setItem` in resilience is review-rejectable (plan, Constitution Check).
- [ ] T028 Update GH #182 with the outcome: which of the issue's three options was built and why (draft persistence only), the two owner decisions that shape it (persist everything including the IBAN; silent restore with no discard control), and the accepted residual that an abandoned event draft returns in the same tab.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (T001)**: no dependencies.
- **Foundational (T002–T005)**: blocks everything. T002 → T003 → T004 → T005 is a chain; only T002 is parallel-safe as the first task.
- **US1 (T006–T013)**: needs Foundational. Internally near-sequential — all but T011 touch the same two files.
- **US2 (T014–T018)**: needs Foundational. **Independent of US1** — different files, no shared code beyond the store.
- **US3 (T019–T023)**: needs nothing but itself. Can be done at any point; T020 and T021 are parallel once T019 lands.
- **Polish (T024–T028)**: after the stories that are being shipped.

### Within US1

T006 → T007 → T008 (refactor proven inert) → T009 → T010 → T011 (failing) → T012 → T013. The refactor is separated from the persistence deliberately: if `npm test` breaks, T008 tells you it was the signal conversion and not the draft logic.

### Parallel Opportunities

- **US1 and US2 are genuinely independent** — different components, different templates, different specs. Two people can take one each after T005.
- **US3 is independent of both** and touches only JSON, so it never conflicts.
- T020 and T021 are parallel with each other, after T019.
- Within a story, the `[P]` marker is mostly absent by design: US1's tasks nearly all edit the same two files and would collide.

## Parallel Example

```text
# After the Foundational checkpoint (T005), three tracks can run at once:
Track A (US1): T006 → T007 → T008 → T009 → T010 → T011 → T012 → T013
Track B (US2): T014 → T015 → T016 → T017 → T018
Track C (US3): T019 → (T020 ∥ T021) → T022 → T023
```

## Implementation Strategy

### MVP (US1 only)

1. T001 → T005 (foundation)
2. T006 → T013 (training wizard)
3. **STOP and validate**: quickstart scenarios 1–7, especially scenario 3
4. This alone closes GH #182 as reported

### Full delivery

Add US2 for the event wizard, then US3. **US3 must ship in the same release as US1 or US2** — the moment either wizard persists a draft, the privacy policy's "stores nothing on your device" claim is false, and it must not be false in production even briefly (spec US3, "Why this priority").

## Notes

- Commit per task or per logical group.
- The two things most likely to produce a fix that looks finished and is not: the **city chip** (T010, T015) and **persisting only on step change** (T012 must be an `effect()`, not a call inside `next()`).
- Nothing in this feature adds a user-visible string to the main i18n catalogues. If a task starts writing one, the silent-restore decision has been misread.
