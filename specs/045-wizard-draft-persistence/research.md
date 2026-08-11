# Phase 0 Research: Wizard drafts survive leaving the page

Nine decisions. Each was taken against the code as it is, not against a general preference.

---

## R1 — Storage mechanism: `sessionStorage`

**Decision**: `window.sessionStorage`, one entry per wizard.

**Rationale**:

- It survives every failure mode in the report. In-app navigation destroys the component but not the page; a reload keeps the same tab; and — the case that matters — when iOS Safari or Android Chrome discards a backgrounded tab, `sessionStorage` is persisted and **restored with the tab**. That is the mechanism's defining behaviour and it is precisely the tester's "leave the application and come back after a short while".
- It dies with the tab, which is what makes persisting the event wizard's IBAN bounded rather than open-ended (FR-010). Closing the tab is a reliable, user-comprehensible "throw this away" — which matters more than usual, because the owner chose **no discard control** (spec: Decision on restore surfacing), leaving tab-close and sign-out as the only escape hatches.
- Two tabs get independent drafts for free, which is the behaviour a user would predict.

**Alternatives considered**:

- **`localStorage`** — rejected. It survives the browser closing, so an abandoned event draft would hold a bank account number on the device indefinitely, with no expiry mechanism anywhere in the product (there is no client-side retention job, and GH #106 records that there is none server-side either). The gain over `sessionStorage` is surviving a full browser restart — which is not a failure mode anyone reported.
- **IndexedDB** — rejected. Asynchronous, needs a schema and a migration story, and buys nothing for <2 KB of form state.
- **In-memory service at root injection scope** — rejected outright. It survives in-app navigation only. It does nothing for a reload and nothing for an eviction, which is the entire report.

**Confirmed against the code**: no storage abstraction exists to reuse. The only two `sessionStorage`/`localStorage` call sites are [`chunk-load-error.handler.ts:65-72`](../../frontend/apps/web/src/app/core/chunk-load-error.handler.ts#L65-L72) and [`language.service.ts:82-96`](../../frontend/apps/web/src/app/core/i18n/language.service.ts#L82-L96). Both wrap every access in `try`/`catch` with a comment naming private mode. That pattern is copied, not reinvented.

---

## R2 — How each wizard observes its own state

**Decision**: one `effect()` per wizard that reads the whole answer set and writes the draft. To make that possible in the training wizard, its plain instance fields become signals.

**Rationale**:

The two wizards are built differently and the difference is load-bearing:

| | create-training | create-event |
|---|---|---|
| Holds answers in | plain fields (`name`, `description`, `startDate`, …) **and** signals (`locationKind`, `venueName`, `street`, `postalCode`, `selectedCity`, `virtualLink`) | a reactive `FormGroup` (13 controls) **and** 7 signals |
| Binding style | `[(ngModel)]` template-driven | `formControlName` reactive |

- **Event wizard**: `form.valueChanges` covers the 13 controls and an `effect()` covers the 7 signals. No refactor needed.
- **Training wizard**: an `effect()` cannot see plain fields. The app is **zoneless**, and the component already carries a comment about exactly this hazard — `locationKind` and `virtualLink` were made signals because *"a `computed()` over plain properties never recomputes"*, which had left Continue permanently disabled for virtual trainings. Converting the remaining 10 answers to signals continues a correction the file has already started, rather than inventing a pattern.

Template cost of the conversion: `[(ngModel)]="name"` becomes `[ngModel]="name()" (ngModelChange)="name.set($event)"`. **That exact form is already in this template** at line 92 for `virtualLink`, so it is the file's own idiom.

Writes are **synchronous and undebounced**. The payload is under 2 KB; a debounce window is a window in which the eviction we are defending against loses the answer.

**Alternatives considered**:

- **Save inside `next()`/`back()` only** — rejected. It cannot satisfy FR-005 for the step being worked on. The address-and-description step is the most expensive to retype and the likeliest place to be interrupted; losing it to an eviction that arrives before "Continue" reproduces the bug in the exact scenario reported.
- **Add `(ngModelChange)="save()"` to each of the ~10 training inputs** — rejected. Ten places to forget, and a field added later is silently not persisted. It also leaves the zoneless plain-field hazard in place for the next `computed()` someone writes.
- **Wrap the training steps in an `NgForm` and use `form.valueChanges`** — rejected, and it is a trap worth naming: each step lives inside an `@if`, so its `ngModel` controls **unregister when the step is left** and their values disappear from `form.value`. It would persist only the current step.

---

## R3 — Restore timing, and the city chip

**Decision**: restore in the field initialiser (construction), before first render. Add `[initialCity]` to both create wizards' `<jh-address-fields>`.

**Rationale**: this is the one place where a plausible implementation is quietly wrong.

[`CityPickerComponent`](../../frontend/apps/web/src/app/shared/city-picker/city-picker.component.ts#L49-L53) consumes `@Input() initial` in `ngOnInit` and sets its display label once. [`AddressFieldsComponent`](../../frontend/apps/web/src/app/shared/address-fields/address-fields.component.ts#L37-L45) carries an explicit warning about it:

> ⚠ `CityPickerComponent` consumes its `initial` in `ngOnInit`, so this must be set at FIRST render — a value pushed in after the picker exists never reaches the chip.

Two consequences:

1. A draft restored in `ngOnInit`, or asynchronously, is too late for the city. Restoring in the field initialiser is early enough, and is also simplest: the wizard is constructed once per activation.
2. **Neither create wizard passes `[initialCity]` today.** Only the three edit forms do (`training-edit` ×2, `event-edit` ×1) — they had a stored city to read back and the create forms never did. With drafts they do. Without this binding, a restored wizard shows every address field filled and the **city chip empty**, while the review step confidently prints the restored city. That is worse than not restoring at all.

`CityOption` carries every field of `Location` plus `latitude`/`longitude`, so the stored option is assignable to `[initialCity]` directly — no conversion helper is needed.

**Alternatives considered**: making `CityPickerComponent` react to later `initial` changes — rejected as out of scope. It is shared by five forms and changing its input semantics to serve this feature risks the edit forms for no gain here.

---

## R4 — Draft identity and keys

**Decision**:

- Training: `jh-draft:training:<slug>` — one draft per team (FR-006).
- Event: `jh-draft:event` — one draft; the event wizard is not scoped to anything.

**Rationale**: the `jh-` prefix matches `jh.lang` and `jh-chunk-reloaded`. A per-slug training key falls straight out of FR-006 and is free — the slug is already read from the route in the field initialiser, before any restore.

**Alternative considered**: keying by user id as well, to make FR-011 structural rather than an explicit clear. Rejected — the id would have to be read from `AuthService` at construction, the wizards are already behind the auth guard, and an explicit clear on sign-out (R6) is one line in one place and is easier to test than a key convention.

---

## R5 — Compatibility guard

**Decision**: every draft carries `v: DRAFT_VERSION`. On read, anything whose version differs, whose JSON fails to parse, or whose top-level shape is not an object is **discarded and removed**, and the wizard opens blank.

**Rationale**: field sets change between releases, and a long-lived tab can outlive a deploy — the app already assumes this, which is why `ChunkLoadErrorHandler` exists. A partially-applied draft is worse than none: it restores some answers, leaves others at defaults, and the user cannot tell which. `JSON.parse` on hand-edited or truncated storage throws, and that must not reach the error handler.

`DRAFT_VERSION` is bumped whenever a persisted field is added, removed or changes meaning. Adding a field without bumping is the failure this guard is for, so the contract says so explicitly.

---

## R6 — Clearing on sign-out

**Decision**: clear all drafts from `AuthService` — in the `tap` of `logout()` and in `clearSession()`.

**Rationale**: those two are the only places the client concludes the session is over (`clearSession()` is called by the interceptor when refresh fails). Putting the clear there covers both the deliberate sign-out and the expired session, and it means no component has to remember.

This is what stops a shared device handing the next person an unfinished event including the previous person's fee recipient and account number — the concrete privacy consequence of the owner's decisions to persist everything (FR-002) and to add no discard control (FR-009).

**Note on direction**: `AuthService` gains a dependency on the draft store, not the reverse. The store knows nothing about auth, so it stays trivially testable.

---

## R7 — The empty-draft rule

**Decision**: write nothing until the current answers differ from the pristine snapshot taken at construction.

**Rationale**: FR-013. Without it, merely opening a wizard leaves an entry behind, and the next visit "restores" a draft the user never made — indistinguishable to them from the app inventing input.

**The catch**: pristine is **not** all-fields-blank. The training wizard defaults `weekday` to today's weekday and the times to `19:00`/`21:00`, and defaults `isRecurring` to `true` and `visibility` to `TeamOnly`; the event wizard defaults `participationLimit` to 16, `rosterCap` to 8 and `feeCurrency` to `EUR`. So the rule is "differs from the initial snapshot", compared against a snapshot captured before restore — never a hand-written list of emptiness checks, which would drift from the defaults.

---

## R8 — The privacy-policy edit

**Decision**: edit two sections in each of `en.json`, `de.json`, `es.json`:

1. **`privacy.sections.storage`** ("Cookies and what's kept in your browser") — add an entry naming the unfinished create-form draft: that it stays on the device, never reaches the server, and goes when the form is finished or cancelled, when the tab is closed, or on sign-out.
2. **`privacy.sections.legalBasis`** ("Why there's no cookie banner") — the sentence *"Ours stores nothing there — no cookie, no local storage, nothing"* becomes false and must go. The no-banner conclusion **survives**, on a corrected argument: what § 25 TDDDG exempts is storage strictly necessary for a service the user explicitly requested, and a draft of the form the user is at that moment filling in is exactly that — as is the sign-in cookie, and as is the language preference the same paragraph already had to accommodate.

**Rationale**: FR-019/FR-020. The German text is authoritative and gets written first; en/es follow it.

**Scope boundary, deliberately drawn**: the **analytics** section's own "stores nothing on your device" claims (`en.json:147`, `:154`) are **not** touched. They are scoped to the analytics tool and remain true of it — feature 038 verified that the recorder uses no client-side storage API at all. Widening the edit into that section would weaken a claim that is accurate.

**Confirmed, not assumed**: [`legal-catalog.spec.ts`](../../frontend/apps/web/src/app/core/i18n/legal-catalog.spec.ts) DM-1 compares complete key sets across all three languages and DM-2 rejects `__TODO__`. FR-021 is therefore already enforced — editing `en.json` alone turns the suite red. **No new guard is needed**, and none should be added.

---

## R9 — What is deliberately not built

Recorded so a reviewer can tell an omission from a decision.

- **No `canDeactivate` guard, no `beforeunload` handler.** Owner decision. A draft that survives leaves nothing to warn about, and a `beforeunload` prompt would fire on the ordinary leave that now costs nothing.
- **No step in the route.** The step travels in the draft. Encoding it in the URL would mean a route refactor for both wizards and would still not survive a reload on its own.
- **No restore notice and no "start over" control.** Owner decision (spec: Decision on restore surfacing), with the consequence — an abandoned event draft, IBAN included, returns in the same tab — recorded there. Nothing built here would have to be removed to add the notice later.
- **No change to the onboarding wizard.** It writes to the server as it goes (FR-017).
- **No draft expiry.** The tab's lifetime is the expiry.
- **No `RouteReuseStrategy` override.** It would keep the component alive across in-app navigation but do nothing for a reload or an eviction, and it changes behaviour for every other route in the app.
