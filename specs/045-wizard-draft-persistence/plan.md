# Implementation Plan: Wizard drafts survive leaving the page

**Branch**: `fix/training-wizard-draft` | **Date**: 2026-08-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/045-wizard-draft-persistence/spec.md`

## Summary

The create-training and create-event wizards hold every answer in component memory, so leaving the page, hitting back, reloading, or having a backgrounded mobile tab evicted returns the user to a blank step 1 (GH #182). Both wizards get a **draft in `sessionStorage`**: written whenever an answer changes, restored before first render when the wizard is opened again, and cleared on successful create, on the training wizard's Cancel, and on sign-out.

**Frontend only. No backend, no endpoint, no entity, no migration, no new dependency.** If a task here produces an `Add-Migration` or touches `backend/`, something is wrong.

Four things carry the work, and each is a decision rather than a mechanic:

1. **`sessionStorage`, not `localStorage`** — the draft dies with the tab, which is what bounds the exposure of persisting the event wizard's IBAN. See R1.
2. **The training wizard's plain `ngModel` fields become signals** so one `effect()` can persist everything. Saving only on step change would lose the address-and-description step — the expensive one — to exactly the eviction the tester reported. See R2.
3. **Restore happens in the field initialiser, before first render**, because `CityPickerComponent` consumes its `initial` in `ngOnInit` and a value pushed in later never reaches the chip. Both create templates must also start passing `[initialCity]`, which today only the three *edit* forms do. This is the single most likely way to ship a fix that restores 20 of 21 fields and silently drops the city. See R3.
4. **The privacy policy stops claiming nothing is stored on the device.** That sentence is load-bearing for the no-cookie-banner argument and is about to become false. See R8.

## Technical Context

**Language/Version**: TypeScript 6.0, Angular 22.1 (standalone, **zoneless** — no `provideZoneChangeDetection` in `app.config.ts`)

**Primary Dependencies**: none added. Angular core (`signal`, `effect`, `model`), `@jsverse/transloco` 8.4 for the legal catalogues. `@angular/forms` both template-driven (`ngModel`, trainings) and reactive (`FormGroup`, events) — the split is pre-existing and is preserved.

**Storage**: `window.sessionStorage`, one entry per wizard per tab. No server storage of any kind. Two prior storage call sites set the access pattern: [`language.service.ts:82-96`](../../frontend/apps/web/src/app/core/i18n/language.service.ts#L82-L96) and [`chunk-load-error.handler.ts:65-72`](../../frontend/apps/web/src/app/core/chunk-load-error.handler.ts#L65-L72), both wrapping every access in `try`/`catch`.

**Testing**: Jest 30 + `@angular/core/testing`, jsdom. jsdom implements `sessionStorage`, so the store and both wizards are testable without a browser. The legal catalogues are already guarded by [`legal-catalog.spec.ts`](../../frontend/apps/web/src/app/core/i18n/legal-catalog.spec.ts).

**Target Platform**: browsers, mobile-first. The reported failure is iOS Safari / Android Chrome discarding a backgrounded tab.

**Project Type**: web application; this feature touches only `frontend/apps/web`.

**Performance Goals**: a draft write is a synchronous `JSON.stringify` + `setItem` of well under 2 KB on each answer change. No debounce (R2) — imperceptible, and a debounce window is a loss window.

**Constraints**: persistence must never be a precondition for using either wizard (FR-015). Every storage access is wrapped; a throw degrades to today's behaviour.

**Scale/Scope**: 2 wizard components, 1 new store service, 1 auth hook, 2 templates, 3 legal catalogues. 16 training answers + 21 event answers + the step. **Zero new i18n keys in the main catalogues** — the restore is silent (FR-009), so no user-facing copy is added anywhere in the app.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — see the re-evaluation at the end.*

| Principle / Gate | Engaged? | Assessment |
|---|---|---|
| **I. Security-first, never trust the client** | Yes | Nothing about the security boundary moves. The draft is UX state that never reaches the server; the server re-validates every field on create exactly as it does today. No validation is relaxed. The one security-relevant fact is *new data at rest on the device* (the event fee IBAN) — bounded by FR-010/FR-011 and disclosed by FR-019. **PASS** |
| **II. Thin controllers, service-centric backend** | No | No backend change. |
| **III. Disciplined data access** | No | No database, entity, query or migration. |
| **IV. Secure auth & session management** | Yes | The rule is explicit: *"Tokens are **never** stored in `localStorage` — only in secure, `httpOnly` cookies."* No token, session identifier, or credential enters the draft — the persisted set is the wizard's own form answers, enumerated in FR-001/FR-002. The auth cookie is untouched. FR-011 additionally clears drafts on sign-out so wizard state cannot outlive the session it belongs to. **PASS** |
| **V. Environment parity** | Yes | Client-side only and identical in local/Dev/Prod. No configuration, no secret, no infrastructure. **PASS** |
| **VI. Consistent conventions** | Yes | `.html`/`.css`/`.ts` stay separate — this feature adds no template file and edits existing ones in place. No `.sh` script is added. **PASS** |
| **VII. Resilient by default** | **No — and it must stay that way** | This feature adds **no network call**. There is no outbound integration, no `HttpClient` usage, no retry, no timeout, no breaker. Reaching for `AddJuggerHubResilience` or a retry policy around a `setItem` would be review-rejectable. The `try`/`catch` around storage access is *not* resilience-in-the-Principle-VII sense; it is the two-call-site-precedent guard against private-mode quota errors. **NOT ENGAGED** |
| **Gate 7. UI/Design compliance** | **Marginal — see below** | The owner chose a **silent restore with no discard control** (spec: Decision on restore surfacing). The consequence is that this feature ships **no new markup, no new component, no new copy, and no style change** in the application UI. The only visible difference is that fields the user already filled are filled again. The one template change is adding an existing `[initialCity]` binding that three edit forms already use. **A DESIGN.md UI review checklist is therefore not instantiated** — there is no new surface to review against tokens, layout, or states. Recorded here deliberately rather than skipped silently; if the notice variant is ever added, Gate 7 engages in full. The legal prose falls under DESIGN.md's Long-form content section, whose rendering (`LegalPageComponent`) is unchanged. **PASS with the deviation recorded** |
| **Gate 8. Resilience review** | No | No network call added; see VII. |

**No violations. Complexity Tracking is therefore empty and omitted.**

## Project Structure

### Documentation (this feature)

```text
specs/045-wizard-draft-persistence/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 — the nine decisions this plan rests on
├── data-model.md        # Phase 1 — the draft shape (client-side only)
├── quickstart.md        # Phase 1 — how to prove it works, including the eviction case
├── contracts/
│   └── wizard-draft.md  # Phase 1 — the storage contract and its compatibility rules
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 — NOT created by /speckit-plan
```

### Source Code (repository root)

```text
frontend/apps/web/src/app/
├── core/
│   ├── drafts/                              # NEW
│   │   ├── wizard-draft.store.ts            # read/write/clear + shape guard + safe storage
│   │   ├── wizard-draft.store.spec.ts
│   │   └── wizard-draft.models.ts           # TrainingDraft, EventDraft, DRAFT_VERSION
│   └── services/
│       └── auth.service.ts                  # EDIT — clear drafts on logout() and clearSession()
├── features/
│   ├── trainings/training-create/
│   │   ├── training-create.component.ts     # EDIT — plain fields → signals, restore, persist, clear
│   │   ├── training-create.component.html   # EDIT — ngModel → [ngModel]/(ngModelChange), [initialCity]
│   │   └── training-create.component.spec.ts
│   └── events/event-create/
│       ├── event-create.component.ts        # EDIT — restore, persist, clear
│       ├── event-create.component.html      # EDIT — [initialCity]
│       └── event-create.component.spec.ts
└── ...

frontend/apps/web/public/i18n/legal/
├── en.json                                  # EDIT — storage + legalBasis sections
├── de.json                                  # EDIT — authoritative
└── es.json                                  # EDIT
```

**Structure Decision**: the draft store lives in `core/drafts/` beside the other cross-cutting client concerns (`core/i18n/`, `core/services/`), because two unrelated features consume it and `AuthService` clears it. It is not put in `shared/` — that directory holds UI, and this is state.

## Risks & gotchas

Ordered by how likely each is to produce a fix that looks finished and is not.

1. **The city chip is the trap.** `CityPickerComponent` reads `@Input() initial` in `ngOnInit` ([city-picker.component.ts:49-53](../../frontend/apps/web/src/app/shared/city-picker/city-picker.component.ts#L49-L53)) and `AddressFieldsComponent` carries an explicit ⚠ about it. Restoring `selectedCity` into the parent signal alone leaves the picker's chip **blank** while the parent believes a city is set — the review step then shows a city the address step does not. Fix: restore in the field initialiser and add `[initialCity]` to both create templates. `CityOption` is structurally a superset of `Location`, so it can be passed directly. **Verify by test, not by eye** (SC-003).
2. **Persisting only on step change loses the reported case.** The tester's eviction happens mid-step. FR-005 requires earlier answers to be safe without reaching a later step; R2's `effect()` satisfies it, a save inside `next()` does not.
3. **A field added later that nobody persists.** The whole-state `effect()` (R2) makes this structural for the training wizard once its fields are signals, and `form.valueChanges` covers the event wizard's `FormGroup` — but the event wizard's four *toggle* signals (`type`, `locationKind`, `participantMode`, `isPaid`) plus the three address signals sit outside the form and must be in the same effect. Count them at review: 21.
4. **`busy`/`submitting`/`error` must not be persisted.** Restoring `busy: true` yields a permanently disabled Create button with no way out. Persist answers and step only.
5. **The training wizard sets `step` to 2 on a create error.** That is a real state change and must reach the draft like any other, or a reload after a failed create lands on the review step with a stale error cleared.
6. **Clearing on create must follow the server's acceptance, not the click.** Clear in the `next` handler of the create subscription, before navigating — never optimistically, or a rejected create leaves the user with nothing (FR-007, US1 scenario 6).
7. **An empty draft that looks like restored input** (FR-013). Opening a wizard writes nothing until an answer actually differs from the pristine state. Note the training wizard's defaults are *not* empty — `weekday` is today's weekday and the times default to 19:00/21:00 — so "pristine" means equal to the initial snapshot, not "all fields blank".
8. **A stale draft from an older release.** `DRAFT_VERSION` plus a shape check; anything unrecognised is discarded and removed. The store must also survive `JSON.parse` throwing on hand-edited storage.
9. **The privacy policy's parity guard already exists** — `legal-catalog.spec.ts` DM-1 compares full key sets across en/de/es and DM-2 rejects `__TODO__`. Confirmed by reading the file, so FR-021 needs **no new test**; it needs the three catalogues edited together. Adding a paragraph to `en.json` alone turns the suite red, which is the guard working.

## Phase 0 — Research

See [research.md](./research.md). Nine decisions: the storage mechanism (R1), how each wizard observes its own state (R2), restore timing and the city chip (R3), draft identity and keys (R4), the compatibility guard (R5), clearing on sign-out (R6), the empty-draft rule (R7), the privacy-policy edit (R8), and what is deliberately *not* built (R9).

## Phase 1 — Design

- [data-model.md](./data-model.md) — the two draft shapes, field by field, with the transient fields called out as excluded.
- [contracts/wizard-draft.md](./contracts/wizard-draft.md) — the client-side storage contract: keys, version, lifecycle, and the compatibility rule.
- [quickstart.md](./quickstart.md) — how to prove it, including how to reproduce a mobile tab eviction on a desktop browser.

## Constitution Check — post-design re-evaluation

Re-run against the Phase 1 artifacts. **No gate changes state.** The design introduces no network call (VII stays not engaged), no backend surface (II, III stay not engaged), no token in storage (IV stays passing — `data-model.md` enumerates every persisted field and none is a credential), and no new UI surface (Gate 7 stays at the recorded deviation). The one item worth restating: `data-model.md` persists `feeIban` and `feeRecipientName` by owner decision, and Principle I is satisfied not by omitting them but by bounding them (tab lifetime, sign-out) and disclosing them (FR-019).
