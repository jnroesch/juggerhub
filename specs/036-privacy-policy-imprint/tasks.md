---

description: "Task list for feature 036 — Privacy Policy & Imprint"
---

# Tasks: Privacy Policy & Imprint

**Input**: Design documents from `/specs/036-privacy-policy-imprint/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Included. Two of them are **guards** rather than conventional coverage — the spec's own risk register turns on them (plan.md → Risks & gotchas), so they are first-class tasks, not polish.

**Organization**: Grouped by user story so each is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 / US2 / US3 / US4 — maps to spec.md user stories
- Exact file paths are given in every task

## Path Conventions

Frontend-only feature (plan.md Summary). All source paths are under `frontend/apps/web/` unless
stated otherwise. **`backend/` is not touched by any task in this list** — no endpoint, no entity,
no migration.

---

## Phase 1: Setup

**Purpose**: Get the shared scaffolding in place. No user-visible behaviour yet.

- [X] T001 Add a **Long-form content** section to `DESIGN.md` per research.md R7 — measure (`container-sm`, 640px), body step, heading hierarchy, section rhythm, in-prose link treatment (underlined, unlike navigation links), list styling, and the meta-line treatment for the "last updated" date and authoritative-language notice. Build it from **existing tokens only**; introducing a new colour, width, or type step is a deviation and must be flagged, not made silently.
- [X] T002 [P] Create the feature directory `frontend/apps/web/src/app/features/legal/` and the empty catalog folder `frontend/apps/web/public/i18n/legal/`.
- [X] T003 [P] Instantiate the UI review checklist: copy `.specify/templates/ui-review-checklist-template.md` to `specs/036-privacy-policy-imprint/checklists/ui-review.md` (Constitution Gate 7). Add a note that the known DESIGN.md contrast conflict (≥4.5:1 vs. white-on-`coral-4`) is **out of scope here** — this feature ships no primary button.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The route, layout, content-loading and guard machinery every story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Catalog structure and its guards

- [X] T004 Create `frontend/apps/web/public/i18n/legal/en.json`, `de.json`, `es.json` with the **full key skeleton** from data-model.md §Content model — `meta`, `privacy.sections.*` (16 sections), `imprint.*` — every leaf set to the sentinel `__TODO__`. Structure first, prose later, so the guards in T005/T006 exist before any content does.
- [X] T005 Write the **catalog completeness guard** in `frontend/apps/web/src/app/core/i18n/legal-catalog.spec.ts`: assert `legal/en.json`, `de.json` and `es.json` have **recursively identical key sets**. This is the mitigation for the single worst failure mode in this feature — `app.config.ts` sets `useFallbackTranslation: true` with `fallbackLang: 'en'`, so a paragraph missing from `de.json` renders **English text inside the legally authoritative German document, with no visible signal**. Do **not** fix this by disabling the global fallback: that would break 031's guarantee for the other ~2000 keys in the app.
- [X] T006 Extend the same spec file with the **placeholder sentinel guard**: assert no leaf value in any legal catalog contains `__TODO__`. Also assert data-model.md's DM-4 (no leaf contains `<`) and DM-5 (`meta.lastUpdated` is a valid ISO `YYYY-MM-DD`). **⚠️ T006 is EXPECTED TO FAIL from now until T024 supplies the owner's imprint particulars (spec Q1). That red build is the feature working, not broken.** It exists because "we'll fill it in before deploy" is exactly how a placeholder reaches Prod inside the one document whose content is legally prescribed. Do not skip, `xit`, or soften it — it turns green on its own when the particulars arrive.
- [X] T007 Add the short **footer/nav labels** under a `legal.*` key in the main catalogs `frontend/apps/web/public/i18n/{en,de,es}.json` — link text and page titles only. These stay in the main catalogs (which load on every page) because the footer renders everywhere and cannot wait for a lazy scope; the **prose does not** (research R2).

### Layout and reachability

- [X] T008 [P] Create `jh-legal-links` in `frontend/apps/web/src/app/shared/ui/legal-links/legal-links.component.{ts,html,css}` per contracts/routes.md §2.1 — a `variant: 'footer' | 'inline'` input, privacy + imprint anchors, a `©` line, `data-testid="legal-links"` / `legal-link-privacy` / `legal-link-imprint`. Presentation-only: no injected service, no state, no API call. Separate `.html`/`.css`/`.ts` files (Constitution VI).
- [X] T009 Create `jh-app-footer` in `frontend/apps/web/src/app/layout/app-footer/app-footer.component.{ts,html,css}`, wrapping `jh-legal-links variant="footer"`.
- [X] T010 Mount `<jh-app-footer />` in `frontend/apps/web/src/app/layout/shell/shell.component.html`, **after `<main>` and before `<jh-bottom-nav />`**, outside the `@if (anonymous())` branch so it renders in both states. Placement is load-bearing: `<main>` already carries `pb-[76px] md:pb-0`, so a footer following it clears the fixed mobile bottom bar. Register the import in `shell.component.ts`.
- [X] T011 [P] Export `LegalLinksComponent` from `frontend/apps/web/src/app/shared/ui/index.ts` alongside the other 024 primitives.

### Page shell and routing

- [X] T012 Create `LegalPageComponent` in `frontend/apps/web/src/app/features/legal/legal-page.component.{ts,html,css}` — the shared long-form shell implementing contracts/routes.md §3: `h1`, the `caption`-step meta line (locale-formatted last-updated date + the authoritative-language notice), optional anchored table of contents, `h2`/`h3` section rendering from the catalog, the cross-link to the sibling document, brand mark and language switcher. Constraints: content column capped at `container-sm` via `jh-page-container width="sm"`; **no `[innerHTML]` binding anywhere** (PC-8, Constitution I); heading levels never skip (PC-3); stable section `id`s for deep links (PC-4).
- [X] T013 Add the **error state** to `LegalPageComponent`: if the `legal` scope fails to load, render a visible error rather than an empty document (PC-7, Constitution VII). A blank privacy policy reads as a policy that says nothing — worse than an honest error.
- [X] T014 Add `/privacy` and `/imprint` to `frontend/apps/web/src/app/app.routes.ts` as **top-level routes outside `ShellComponent`**, siblings of the auth screens: lazy `loadComponent`, **no guard of any kind**, each providing the scope via `providers: [provideTranslocoScope('legal')]`. Add a comment stating the no-guard/no-API-call rule and why (contracts/routes.md RC-1/RC-2), so a future reader does not add `authGuard` by reflex.

**Checkpoint**: Both routes resolve and render a skeleton page with placeholder text, signed out. T006 is red — expected.

---

## Phase 3: User Story 1 — Find out what the platform does with my data, without an account (Priority: P1) 🎯 MVP

**Goal**: A complete, accurate privacy policy, reachable with no session from anywhere in the app.

**Independent Test**: In a browser with no session, follow the privacy link from a signed-out screen; the full policy renders with no sign-in prompt and no redirect. Every category of personal data the platform actually processes appears in it.

### Content

- [X] T015 [US1] Write the **English** privacy policy prose into `frontend/apps/web/public/i18n/legal/en.json`, covering all 16 sections in data-model.md §Content model. Every claim must be traceable to the verification tables in contracts/content-catalog.md §6 — this is a legal document making factual assertions about a running system, so an unverified sentence is a false statement, not a rough draft.
- [X] T016 [US1] Within T015, write the **analytics** section to FR-006 in full: self-hosted with nothing sent to a third party; no cookie and nothing stored on the device; no identifier of the *viewer*; **and that page addresses are recorded verbatim, so an address naming a member profile or team page records that subject**. Also state that query strings are *not* recorded (033 FR-008a). The verbatim-path disclosure MUST NOT be omitted or softened — it is the reason this feature exists.
- [X] T017 [US1] Within T015, write the **legal basis** section to FR-014: keep the two questions visibly separate — (a) nothing is written to the device and the auth cookie is strictly necessary, so the storage-consent rule is not engaged at all; (b) the path data rests on **legitimate interest**, with the balancing test written out (what the interest is, why the impact is limited, how to object). Assert no banner is needed and say why.
- [X] T018 [US1] Within T015, write the **processors** section to FR-008: Resend (email, Dev/Prod) and Microsoft Azure (hosting, in-cluster Postgres, Blob Storage). Read the **actual configured region** from `infra/` rather than assuming one. State the transfer basis where processing leaves the EU. Name the notable *absences* too — no third-party analytics, no geocoding processor, and **no Google Fonts** (faces ship via `@fontsource`), since that last one is a well-litigated German exposure the platform genuinely does not have.
- [X] T019 [US1] Within T015, write the **rights** and **retention** sections to FR-009/FR-005 honestly: there is **no self-service export or account deletion**, so every right names the manual contact route and the policy describes **no control that does not exist**; and **no automated retention or deletion runs anywhere in the platform**, so the policy says data is kept until the account is deleted on request. Do not invent a retention period the system does not enforce — if a sentence reads badly, that is a signal to raise a retention issue, not to write nicer wording.
- [X] T020 [US1] Within T015, disclose the two non-obvious items the code forces (data-model.md): **`RefreshToken.CreatedByIp` retains an originating IP address per session** for security auditing, and **chat conversations are snapshotted, not deleted**, when a team is deleted or an event cancelled — a reader who assumes "team gone ⇒ messages gone" would be wrong.
- [X] T021 [US1] Set `meta.lastUpdated` and add the anchored table of contents entries for the privacy policy's sections.

### Wiring and tests

- [X] T022 [US1] Create `PrivacyComponent` in `frontend/apps/web/src/app/features/legal/privacy/privacy.component.{ts,html}` on top of `LegalPageComponent`, rendering the `legal.privacy.*` tree with the table of contents enabled and a cross-link to `/imprint`.
- [X] T023 [P] [US1] Unit-test `PrivacyComponent` in `frontend/apps/web/src/app/features/legal/privacy/privacy.component.spec.ts`: renders every section heading, heading levels never skip (PC-3), the cross-link to `/imprint` is present (RC-5), no `[innerHTML]` is used, and the error state renders when the scope fails to load (PC-7).

**Checkpoint**: The privacy policy is complete and readable in English, signed out, from any screen. This alone closes the live disclosure gap and is a deployable MVP — T006 still red pending US2's particulars.

---

## Phase 4: User Story 2 — Reach the operator's legal identity (Priority: P1)

**Goal**: A German imprint at its own address, reachable with no session, cross-linked with the policy.

**Independent Test**: With no session, follow the imprint link from a signed-out screen; the operator's legal identity and contact details render with no sign-in prompt, in ≤2 clicks from any screen.

- [X] T024 [US2] **⚠️ BLOCKED ON SPEC Q1 — needs the owner.** Replace the `__TODO__` sentinels in `imprint.*` across all three legal catalogs with the operator's real particulars per FR-015: name, postal address, electronic contact (at minimum an email address), legal form, and any further applicable particulars. **These values enter PUBLIC git history permanently and cannot be retracted** (research R4) — the repository is public. Runtime injection was evaluated and rejected as a false solution: the address is legally required to be published on the live site and is crawled within days, so hiding it from git protects nothing while making the legally-prescribed text unreviewable before Prod. The real mitigation is *which* address — a `c/o` or business address is the established German practice. **Make that choice before this commit, not after.** Completing this task is what turns T006 green.
- [X] T025 [US2] Write the remaining imprint text (headings, the responsibility statement, the data-protection contact per FR-015/US2-AS3) into all three catalogs, and add the cross-link to `/privacy` (FR-016).
- [X] T026 [US2] Create `ImprintComponent` in `frontend/apps/web/src/app/features/legal/imprint/imprint.component.{ts,html}` on top of `LegalPageComponent`, rendering `legal.imprint.*` with no table of contents (it is short) and a cross-link to `/privacy`.
- [X] T027 [P] [US2] Unit-test `ImprintComponent` in `frontend/apps/web/src/app/features/legal/imprint/imprint.component.spec.ts`: the particulars render, the data-protection contact is present or linked, and the cross-link to `/privacy` exists.
- [X] T028 [US2] Add `<jh-legal-links variant="inline" />` to the bottom of the content column on the **nine screens that render outside the shell** (contracts/routes.md §2.3), registering the import in each `.ts`:
  `features/auth/register/register.component.html` (**most important — the screen where an email address is actually handed over**), `features/auth/sign-in/sign-in.component.html`, `features/auth/forgot-password/`, `features/auth/reset-password/`, `features/auth/verify-email/`, `features/onboarding/onboarding.component.html`, `features/teams/invite-accept/`, `features/events/event-invite-accept/`, `features/parties/party-invite-accept/`. The admin shell is deliberately excluded — platform admins reach the footer through the rest of the app.
- [X] T029 [P] [US2] Unit-test `LegalLinksComponent` in `frontend/apps/web/src/app/shared/ui/legal-links/legal-links.component.spec.ts`: both variants render both links with the right `routerLink` targets and test ids.

**Checkpoint**: Both documents live, cross-linked, reachable in ≤2 clicks from every screen in every state. T006 is now green.

---

## Phase 5: User Story 3 — Read the policy in my language (Priority: P2)

**Goal**: All three languages, German authoritative, English and Spanish clearly informational.

**Independent Test**: Switch the app to German, then Spanish, then English, opening both pages in each; the legal text appears in that language, the non-German versions state that German governs, and no raw placeholder or blank section appears anywhere.

- [X] T030 [US3] Write the **German** legal text into `frontend/apps/web/public/i18n/legal/de.json` — the **authoritative version**. Draft-then-review, matching the precedent 031 set for translated content: the draft ships, a native-speaker/legal review pass is a tracked fast-follow. This version is the binding text, so mark it for that review explicitly rather than treating it as one translation among three.
- [X] T031 [US3] Write the **Spanish** legal text into `frontend/apps/web/public/i18n/legal/es.json` as an informational translation.
- [X] T032 [US3] Implement `meta.authoritativeNotice` rendering in `LegalPageComponent`: shown on `en` and `es`, **not** on `de`, in the meta line under the `h1` at the `caption` step, stating in the reader's own language that the German version governs in case of divergence (FR-019, DM-3).
- [X] T033 [P] [US3] Unit-test the authoritative-notice rule in `frontend/apps/web/src/app/features/legal/legal-page.component.spec.ts`: present for `en` and `es`, absent for `de`; and that a language switch re-renders in place without navigating away (PC-6).
- [X] T034 [US3] Run the completeness guard (T005) against the now-populated catalogs and fix any key drift. **Fix the catalog, never relax the test** — it is the only thing standing between a missing German paragraph and English text inside the legally binding document.

**Checkpoint**: All three languages complete and consistent; SC-009 satisfied.

---

## Phase 6: User Story 4 — Object to being measured (Priority: P3)

**Goal**: The objection route the policy describes actually works, verified rather than asserted.

**Independent Test**: Follow the opt-out route exactly as the policy words it, then browse several pages and confirm zero analytics events were recorded for that session.

- [X] T035 [US4] Write the **objection** section into all three catalogs (FR-013): the Do Not Track / Global Privacy Control route described in terms a non-technical reader can actually follow — where the setting lives, what it does, and what stops happening. No jargon, no "configure your user agent".
- [ ] T036 [US4] **Verify the opt-out end to end** against a local stack with analytics enabled (quickstart.md §5): enable DNT/GPC, browse several pages including `/privacy`, confirm **zero** events in the Umami dashboard; then disable DNT, browse again, and confirm events **do** appear — so the first result proves suppression rather than a broken pipeline. Record the outcome in this file. Not automatable: Playwright cannot set the DNT signal reliably across engines, and this is stated plainly rather than quietly dropped. If the behaviour does **not** hold, the policy text is false and this becomes a blocker, not a note.

**Checkpoint**: Every right the policy describes is one the system actually honours (SC-006, SC-004).

---

## Phase 7: Polish & Cross-Cutting

- [X] T037 [P] Extend `frontend/apps/web-e2e/src/authenticated-only.spec.ts` with the **inverse** assertions for `/privacy` and `/imprint` (the file currently proves gated paths redirect): with cookies cleared, both render, the URL is unchanged, and there is no `returnUrl` (RC-3, SC-008).
- [X] T038 [P] Add an e2e assertion that **no `/api/**` request is issued** while either legal page loads, via Playwright route interception (RC-2, SC-008). A 401-triggered refresh would redirect a reader away from a page they are legally entitled to see.
- [X] T039 [P] Add e2e reachability checks (FR-002, SC-001): ≤2 clicks to both pages from a signed-out screen and from a signed-in one, at desktop **and** mobile viewports — including that the footer is **not occluded** by the fixed bottom bar on mobile.
- [X] T040 [P] Add an e2e responsive check: **no horizontal overflow at 320px** on both pages (PC-2, SC-007).
- [X] T041 Complete `specs/036-privacy-policy-imprint/checklists/ui-review.md` against the diff (Constitution Gate 7). Specific to this feature: measure is `container-sm` not `container-md`; in-prose links are underlined while navigation links are not (intentional, research R7); heading levels never skip. **Do not re-open** the known app-wide DESIGN.md contrast conflict — no primary button ships here.
- [X] T042 Run the **content accuracy review** (contracts/content-catalog.md §6, SC-002/SC-003/SC-005/SC-006): audit the data-category list against `backend/Entities/` and confirm zero categories are missing; re-check the analytics claims against `specs/033-umami-analytics/spec.md`; confirm zero undisclosed processors in Prod; confirm every right names a route that exists.
- [X] T043 Run the full verification set from quickstart.md §4: `npx nx test web --watch=false`, `npx nx run-many -t lint`, `npx nx build web --configuration=production`, `npx nx e2e web-e2e`. On the production build, **confirm the legal catalogs are emitted as separate files under `public/i18n/legal/` and are not inlined into the initial chunk** — a non-legal route must fetch no `legal/*.json`.
- [X] T044 [P] Open a follow-up GitHub issue for **self-service data export and account deletion**, referencing #92 and this feature's FR-009. The policy currently documents a manual route because no self-service control exists; that is honest but not a good long-term answer.
- [X] T045 [P] Open a follow-up GitHub issue for **retention policy and automation**. No automated retention or deletion runs anywhere today; the policy states this honestly, which makes the gap visible rather than fixing it.
- [X] T046 [P] Open a follow-up GitHub issue for a **native-speaker/legal review of the German authoritative text** (and the en/es translations), matching the draft-then-review precedent from 031. Note that the German version is the *binding* one, so this review carries more weight than a normal translation pass.
- [X] T047 Report spec/design drift and close issue #92 with a summary of what shipped, what is deferred (T044–T046), and the manual verification result from T036.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: no dependencies.
- **Phase 2 (Foundational)**: depends on Phase 1. **Blocks every user story.**
- **Phase 3 (US1)** and **Phase 4 (US2)**: both P1, both depend on Phase 2. US1 is the MVP; US2 is blocked on the owner's answer to spec Q1 (T024) but its *structure* (T025–T029) is not.
- **Phase 5 (US3)**: depends on Phase 2; the German/Spanish text is easier to write once the English exists (T015), so in practice it follows US1.
- **Phase 6 (US4)**: depends on Phase 2 only; T036 needs a running local stack with analytics.
- **Phase 7 (Polish)**: depends on the stories being complete.

### Critical dependencies within Phase 2

- T005/T006 depend on T004 (the skeleton must exist to be checked).
- T010 depends on T009, which depends on T008.
- T014 depends on T012; T013 depends on T012.

### The one external blocker

**T024 needs the owner** (spec Q1). Everything else in the feature — all of US1, US3, US4, the whole
foundation, and all of US2 except the particulars themselves — proceeds without it. T006 stays red
until T024 lands, which is the designed behaviour, not a broken build.

### Parallel Opportunities

- T002, T003 (Phase 1).
- T008 and T011 alongside T004–T007 (different files).
- T023, T027, T029, T033 — all unit specs, all different files.
- T037–T040 — all e2e additions; note they touch the **same file**, so they parallelize as authoring but must be merged in one edit.
- T044, T045, T046 — three independent follow-up issues.

---

## Implementation Strategy

### MVP (US1 only)

1. Phase 1 → Phase 2 → Phase 3.
2. **Stop and validate**: the privacy policy renders in full for a signed-out visitor, from any screen, and every claim in it is traceable.
3. This alone closes the live exposure — the platform stops measuring EU visitors with zero disclosure. Deployable.

### Incremental delivery

1. Setup + Foundational → skeleton routes live.
2. + US1 → privacy policy live in English (**MVP — closes the live gap**).
3. + US2 → imprint live, both cross-linked (needs the owner's particulars).
4. + US3 → all three languages, German authoritative.
5. + US4 → the objection route verified, not merely described.
6. Polish → e2e, UI review, content audit, follow-up issues.

**Do not ship US2 with `__TODO__` in the imprint.** T006 makes that impossible by design; if it is
ever tempting to skip it, that is precisely the moment it is doing its job.

---

## Notes

- `[P]` = different files, no dependencies.
- Commit after each task or logical group; keep the German authoritative text in its own commit so it can be reviewed as a document.
- **Two tests are guards, not coverage.** T005 stops English text appearing inside the legally binding German document; T006 stops a placeholder reaching Prod inside a legally-prescribed page. Neither should be weakened to make a build pass.
- No task in this list touches `backend/`, `infra/`, or any migration.
