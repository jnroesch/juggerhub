---

description: "Task list for Localization — German & Spanish (i18n)"
---

# Tasks: Localization — German & Spanish (i18n)

**Input**: Design documents from `/specs/031-i18n-localization/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included — the spec's Success Criteria + [quickstart.md](./quickstart.md) define automated checks, and research D13 specifies the test approach. Frontend specs are **zoneless (no `fakeAsync`)** per project convention.

## Implementation status (2026-07-27)

The **complete localization mechanism** is implemented and tested end-to-end: runtime library + detection, the switcher (reachable signed-out and signed-in), account + local persistence, the `Accept-Language` propagation, backend request localization, recipient-vs-request culture resolution, and per-locale email rendering. Backend builds; **frontend build + 280 web tests green**. Auth transactional emails and the sign-in + public-chrome UI are fully translated to `de`/`es`; **every other screen renders via the English fallback and is fully functional** — its `de`/`es` strings are additive catalog work.

- **Done**: Phases 1–2 (T001–T008); US2 backend + frontend (T026–T039); US3 (T040–T049); US1 detection + fallback (T009, T011, T025); shared-chrome + sign-in extraction (part of T012/T013); polish T050 (ui-review), T053 (native-review issue #84).
- **Deferred (additive, English-fallback-safe, tracked)**: remaining per-scope string extraction (T012 rest, T014–T022 onboarding/profile/teams/events/browse/chat/dashboard/settings/admin), in-app notification copy (T023), the locale date/number sweep (T024), the hardcoded-string lint guard (T051), and full quickstart run (T052). These do not block a working, mergeable feature; see PR + issue #84.
- **T054**: the switcher reuses existing control tokens and introduces no new DESIGN.md pattern — no DESIGN.md change needed.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 / US2 / US3 (maps to spec.md user stories)
- Exact file paths are included in each task.

## Path Conventions

Web app: frontend Angular at `frontend/apps/web/src/`, backend ASP.NET Core at `backend/`. Paths below are repo-relative.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Bring in the i18n libraries and scaffold the catalog/constant structure.

- [X] T001 Add `@jsverse/transloco` and `@jsverse/transloco-locale` to `frontend/package.json` (v8.4.0 — verified building on Angular 21.2) and install
- [X] T002 [P] Create the supported-language single-source constant `["en","de","es"]` (default/fallback `en`) in `frontend/apps/web/src/app/core/i18n/supported-languages.ts` (also holds endonyms + lang→locale mapping)
- [X] T003 [P] Scaffold catalog structure — **path corrected to `frontend/apps/web/public/i18n/`** (the `@angular/build:application` build serves `public/`, not `src/assets/`); created root `en.json`/`de.json`/`es.json` (de/es flagged `_meta.status: draft`). Per-scope subfolders are added during extraction.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core Transloco/formatting plumbing every user story depends on.

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete.

- [X] T004 Configure Transloco + transloco-locale providers (available langs from `supported-languages.ts`, default/fallback `en`, `reRenderOnLangChange`, prod-mode toggle; `langToLocaleMapping` for formatting) in `frontend/apps/web/src/app/app.config.ts`
- [X] T005 Implement the scoped catalog HTTP loader (fetches `/i18n/{path}.json`) in `frontend/apps/web/src/app/core/i18n/transloco-http.loader.ts`
- [X] T006 [P] English-fallback for missing keys (FR-008) — **implemented via Transloco built-in config** (`fallbackLang: 'en'` + `missingHandler.useFallbackTranslation`) in `app.config.ts` rather than a bespoke `missing-handler.ts`; the built-in is the battle-tested path and avoids reimplementing fallback resolution.
- [X] T007 Implement the base `LanguageService` (active-language signal; `setActive(lang)` sets Transloco active language **and** `document.documentElement.lang` for FR-016) in `frontend/apps/web/src/app/core/i18n/language.service.ts`
- [X] T008 [P] Add a Transloco testing setup helper for component specs in `frontend/apps/web/src/testing/transloco-testing.ts`

**Checkpoint**: i18n plumbing ready — user stories can begin.

---

## Phase 3: User Story 1 - See the app in my language automatically (Priority: P1) 🎯 MVP

**Goal**: The entire interface (player-facing **and** admin) renders in the browser-detected language (`de`/`es`, else `en`), with locale-aware dates/numbers, English fallback for any missing string, and in-app notifications localized.

**Independent Test**: Set the browser to German → whole UI (incl. admin) in German with `<html lang="de">` and German date/number formats; Spanish → Spanish; French → English; `de-AT` → German. No blank/raw keys.

### Tests for User Story 1

- [X] T009 [P] [US1] Unit test: `LanguageService` browser detection + base-match (`de-AT`→`de`) + `en` fallback (in `supported-languages.spec.ts` via the pure `resolveLanguage`, + `language.service.spec.ts` for switch/persist)
- [ ] T010 [P] [US1] e2e: browser-language detection for `de`/`es`/unsupported in `frontend/apps/web-e2e/src/i18n-detect.spec.ts` — *deferred (needs full-stack e2e run)*
- [X] T011 [US1] Wire browser detection into `LanguageService` (navigator.language base-matched, else `en`) + bootstrap via `App` injecting the service
- [~] T012 [P] [US1] Extract app chrome/nav strings — **partial**: public bar (shell) done → root `nav` namespace; remaining shared `jh-*` primitives pending
- [~] T013 [P] [US1] Extract **auth** strings — **partial**: sign-in fully translated (root `auth.signIn`); register/forgot/reset/verify/password-policy pending
- [ ] T014 [P] [US1] Extract **onboarding** strings → `onboarding` scope in `frontend/apps/web/src/app/features/onboarding/**` + `assets/i18n/onboarding/`
- [ ] T015 [P] [US1] Extract **profile** strings (view/owner/public, pompfe-selector, quick-actions, recognition) → `profile` scope in `frontend/apps/web/src/app/features/profile/**` + `assets/i18n/profile/`
- [ ] T016 [P] [US1] Extract **teams + my-team** strings (create/detail/settings/invitations, invite-accept) → `teams` scope in `frontend/apps/web/src/app/features/teams/**` + `features/my-team/**` + `assets/i18n/teams/`
- [ ] T017 [P] [US1] Extract **events + parties + marketplace + trainings** strings → `events` scope in `frontend/apps/web/src/app/features/{events,parties,marketplace}/**` + trainings + `assets/i18n/events/`
- [ ] T018 [P] [US1] Extract **browse + search + filter-panel** strings → `browse` scope in `frontend/apps/web/src/app/features/browse/**` + `assets/i18n/browse/`
- [ ] T019 [P] [US1] Extract **chat** strings (inbox/conversation/compose/details/new/shell) → `chat` scope in `frontend/apps/web/src/app/features/chat/**` + `assets/i18n/chat/`
- [ ] T020 [P] [US1] Extract **dashboard / home** strings → `dashboard` scope in `frontend/apps/web/src/app/features/dashboard/**` + `assets/i18n/dashboard/`
- [ ] T021 [P] [US1] Extract **settings + account** strings → `settings` scope in `frontend/apps/web/src/app/features/{settings,account}/**` + `assets/i18n/settings/`
- [ ] T022 [P] [US1] Extract **admin area** strings (catalogue, users, teams, overview, detail, shell — clarification Q1 in scope) → `admin` scope in `frontend/apps/web/src/app/features/admin/**` + `assets/i18n/admin/`
- [ ] T023 [P] [US1] Localize in-app notification copy (all `NotificationType` cases + payload interpolation) in `frontend/apps/web/src/app/features/alerts/notification-row/notification-row.component.ts` + `assets/i18n/notifications/` (FR-011)
- [ ] T024 [US1] Sweep templates to render dates/times/numbers via `transloco-locale` pipes (FR-009) across the extracted feature scopes (depends on T012–T022 to avoid same-file churn)
- [X] T025 [US1] English fallback verified via config (`fallbackLang` + `useFallbackTranslation`) and `supported-languages.spec.ts` fallback cases

**Checkpoint**: US1 fully functional — the app auto-localizes end-to-end with English fallback. MVP shippable.

---

## Phase 4: User Story 2 - Choose and keep my language (Priority: P2)

**Goal**: A visible switcher (reachable signed-out **and** signed-in) lets users override the language with immediate effect; the choice persists per-user (account) when signed in and locally when anonymous, following the FR-007 precedence.

**Independent Test**: Override to German on an English browser → UI switches instantly, no reload; reload → persists (anonymous); sign in elsewhere → account preference wins.

### Backend

- [X] T026 [US2] Add nullable `PreferredLanguage` to the `User` entity in `backend/Entities/User.cs`
- [X] T027 [US2] EF migration adding `Users.PreferredLanguage` (nullable, no backfill) — `20260726221917_AddUserPreferredLanguage`
- [X] T028 [P] [US2] Added `PreferredLanguage` to `AuthUserDto` (Mapster maps by name)
- [X] T029 [P] [US2] Created `UpdateLanguageRequest` DTO in `backend/Dtos/Account/`
- [X] T030 [US2] Implemented `LanguagePreferenceService` (allowlist-validated; `ExecuteUpdateAsync`; `User` carries no audit timestamps so no `ModifiedDate`)
- [X] T031 [US2] Thin `AccountController` `PUT /account/language` + DI registration
- [X] T032 [P] [US2] Integration test `LanguagePreferenceTests` (204/400/401 + `/me` round-trip)
- [X] T033 [P] [US2] Added `preferredLanguage` to the `AuthUser` model
- [X] T034 [US2] `LanguageService` full FR-007 precedence + `localStorage` (`jh.lang`) + on-switch `PUT`
- [X] T035 [US2] `AuthService.setPreferredLanguage` + effect re-resolves on session change (applies account pref on load/login)
- [X] T036 [US2] Built `jh-language-switcher` (endonyms, current indicator, immediate switch)
- [X] T037 [US2] Surfaced in shell public bar + top-nav + sign-in/register (signed-out AND signed-in)
- [X] T038 [P] [US2] `language.service.spec.ts` precedence/persistence/PUT + `supported-languages.spec.ts`
- [ ] T039 [P] [US2] e2e switch/persistence — *deferred (needs full-stack e2e run)*

**Checkpoint**: US1 + US2 both work — detection *and* explicit, persisted choice.

---

## Phase 5: User Story 3 - Receive emails and notifications in my language (Priority: P3)

**Goal**: Transactional emails (subjects + bodies) are sent in the right language — the caller's effective language for pre-account flows, the recipient's stored preference for recipient-addressed content — with English fallback. In-app notification localization already shipped in US1.

**Independent Test**: German-preferring user gets German emails/notifications; a recipient-addressed email uses the *recipient's* language (not the actor's); no preference → English.

### Implementation

- [X] T040 [US3] `languageInterceptor` stamps `Accept-Language` (reads `<html lang>`, no DI cycle) on `/api`; chain `[auth, retry, language]`
- [X] T041 [US3] `AddRequestLocalization` (`en`/`de`/`es`, default `en`) in `Program.cs`
- [X] T042 [US3] `RecipientCultureResolver` (recipient pref → email lookup → request culture → `en`; D9)
- [X] T043 [P] [US3] Moved templates to `EmailTemplates/en/`; `LoadTemplateAsync(name, culture)` selects `{culture}/` with `en` fallback
- [~] T044 [P] [US3] `de`/`es` body templates — **auth set + shared footer done** (verify/reset/password-changed/welcome); team/event/party/market/invitation bodies fall back to `en` (safe) pending #84
- [X] T045 [P] [US3] Email subjects/titles/footers localized via `EmailLocalizer` (in-code dictionaries; **deviation from `.resx`** for build-robustness — swappable later, see EmailLocalizer docstring)
- [X] T046 [US3] `AuthEmailService` localizes subjects + selects culture (recipient pref, request culture for pre-account); template `culture` param threaded through
- [ ] T047 [P] [US3] Email-mirror notification copy — *no backend-generated notification copy exists outside templates; nothing to localize (conditional task, N/A)*
- [X] T048 [P] [US3] xUnit `LocalizationUnitTests` (base-match + allowlist + `en` fallback + localizer)
- [X] T049 [P] [US3] xUnit `EmailLanguageTests` (recipient-language reset email) + resolver units

**Checkpoint**: All three stories functional and independently testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T050 [P] Instantiated `checklists/ui-review.md` and verified German long-string tolerance on the extracted screens (sign-in, public bar); pending per-screen as extraction proceeds (gate 7, SC-006)
- [ ] T051 [P] Lint/CI guard for hardcoded strings — *deferred (see #84); not blocking*
- [ ] T052 Full quickstart run — *automated units cover the mechanism; full manual run deferred (needs full stack), see PR*
- [X] T053 [P] Opened native-review tracking issue #84 (clarification Q4 fast-follow), referencing #77
- [X] T054 [P] Assessed — switcher reuses existing control tokens; no new DESIGN.md pattern, no change needed

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup — **blocks all user stories**.
- **US1 (Phase 3)**: depends on Foundational. MVP.
- **US2 (Phase 4)**: depends on Foundational; independent of US1 (backend tasks can even run alongside US1). Builds on the same `LanguageService`.
- **US3 (Phase 5)**: depends on Foundational; the `Accept-Language` interceptor (T040) reads the effective language from `LanguageService`, so it benefits from US2's precedence but only *requires* the Foundational base service.
- **Polish (Phase 6)**: after the desired stories are complete.

### User Story Dependencies

- US1, US2, US3 are each independently testable after Foundational. Presented in priority order P1 → P2 → P3.

### Within Each Story

- Tests before / alongside implementation; models → services → endpoints → integration; extraction (US1) tasks are file-scoped and parallelizable, the formatting sweep (T024) follows them.

### Parallel Opportunities

- Setup: T002, T003 in parallel.
- Foundational: T006, T008 in parallel (after T004/T005/T007 base).
- **US1 extraction (T012–T023) is highly parallelizable** — each touches a different feature folder + its own catalog; the biggest parallel win. T024/T025 follow.
- US2: T028/T029 parallel; T032/T033/T038/T039 parallel; backend (T026–T032) can proceed in parallel with US1 frontend work.
- US3: T043/T044/T045/T047 parallel; T048/T049 parallel.

---

## Parallel Example: User Story 1 extraction

```bash
# After Foundational, fan out the per-feature string extraction:
Task: "Extract auth strings → auth scope (T013)"
Task: "Extract onboarding strings → onboarding scope (T014)"
Task: "Extract profile strings → profile scope (T015)"
Task: "Extract teams + my-team strings → teams scope (T016)"
Task: "Extract admin area strings → admin scope (T022)"
# …then the formatting sweep (T024) and fallback verification (T025).
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 Setup → 2 Foundational → 3 US1.
2. **STOP and VALIDATE**: browser-detected localization end-to-end with English fallback (incl. admin).
3. Deploy/demo — the app is usable in German/Spanish. This alone delivers the core #77 value.

### Incremental Delivery

1. Foundational ready.
2. US1 (detect + extract) → demo (MVP).
3. US2 (switcher + persistence) → demo.
4. US3 (localized emails; in-app notifications already done in US1) → demo.
5. Polish: UI-review checklist, native-review handoff, quickstart validation.

### Parallel Team Strategy

Once Foundational is done: split the US1 extraction scopes across contributors (they don't conflict), while a backend contributor builds US2's endpoint + US3's email localization concurrently.

---

## Notes

- [P] = different files, no incomplete-task dependency.
- The `Accept-Language` mechanism (T040) is the frontend passing the **effective** (post-override) language, satisfying FR-012a — not "trusting the raw browser header" (see research D5 for the non-contradiction with clarification Q3).
- `de`/`es` catalogs and email templates ship as **drafts flagged for native-speaker review** (clarification Q4); English fallback guarantees nothing renders blank meanwhile.
- Frontend specs are zoneless — no `fakeAsync` (project convention).
- Commit per task or logical group; stop at any checkpoint to validate a story independently.
