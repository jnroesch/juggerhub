---

description: "Task list for Localization — German & Spanish (i18n)"
---

# Tasks: Localization — German & Spanish (i18n)

**Input**: Design documents from `/specs/031-i18n-localization/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included — the spec's Success Criteria + [quickstart.md](./quickstart.md) define automated checks, and research D13 specifies the test approach. Frontend specs are **zoneless (no `fakeAsync`)** per project convention.

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

- [ ] T009 [P] [US1] Unit test: `LanguageService` browser detection + base-match (`de-AT`→`de`) + `en` fallback in `frontend/apps/web/src/app/core/i18n/language.service.spec.ts`
- [ ] T010 [P] [US1] e2e: browser-language detection for `de`/`es`/unsupported in `frontend/apps/web-e2e/src/i18n-detect.spec.ts`

### Implementation for User Story 1

- [ ] T011 [US1] Wire browser detection into `LanguageService` init and app bootstrap (navigator.language base-matched to supported, else `en`) in `frontend/apps/web/src/app/core/i18n/language.service.ts`
- [ ] T012 [P] [US1] Extract app chrome / nav / layout + shared `jh-*` primitive strings → keys + root & `shared` catalogs (`en` authored, `de`/`es` drafted, flagged for review) under `frontend/apps/web/src/app/` shared components + `assets/i18n/`
- [ ] T013 [P] [US1] Extract **auth** strings (sign-in, register, forgot/reset, verify-email, password-policy) → `auth` scope in `frontend/apps/web/src/app/features/auth/**` + `assets/i18n/auth/`
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
- [ ] T025 [US1] Verify English fallback for missing `de`/`es` keys end-to-end (temporarily drop a key; confirm English text, never blank/raw key) — validates T006

**Checkpoint**: US1 fully functional — the app auto-localizes end-to-end with English fallback. MVP shippable.

---

## Phase 4: User Story 2 - Choose and keep my language (Priority: P2)

**Goal**: A visible switcher (reachable signed-out **and** signed-in) lets users override the language with immediate effect; the choice persists per-user (account) when signed in and locally when anonymous, following the FR-007 precedence.

**Independent Test**: Override to German on an English browser → UI switches instantly, no reload; reload → persists (anonymous); sign in elsewhere → account preference wins.

### Backend

- [ ] T026 [US2] Add nullable `PreferredLanguage` to the `User` entity in `backend/Entities/User.cs`
- [ ] T027 [US2] Create the EF migration adding `Users.PreferredLanguage` (nullable, no backfill) in `backend/Data/Migrations/`
- [ ] T028 [P] [US2] Add `PreferredLanguage` to `AuthUserDto` (+ Mapster mapping) in `backend/Dtos/Auth/AuthUserDto.cs`
- [ ] T029 [P] [US2] Create `UpdateLanguageRequest` DTO (`{ language }`) in `backend/Dtos/Account/UpdateLanguageRequest.cs`
- [ ] T030 [US2] Implement `LanguagePreferenceService` (validate against allowlist server-side; persist via `ExecuteUpdateAsync` setting `ModifiedDate`) with interface in `backend/Services/Account/`
- [ ] T031 [US2] Create thin `AccountController` with `PUT api/v{version}/account/language` (auth required; 400 on unsupported; stays signed in) in `backend/Controllers/AccountController.cs` + register service in DI

### Backend tests

- [ ] T032 [P] [US2] Integration test for `PUT /account/language` (204 success, 400 unsupported/allowlist, 401 unauthenticated) in `backend` test project

### Frontend

- [ ] T033 [P] [US2] Add `preferredLanguage` to the `AuthUser` model in `frontend/apps/web/src/app/core/models/auth.models.ts`
- [ ] T034 [US2] Extend `LanguageService` to full FR-007 precedence (account pref → `localStorage` → browser → `en`), `localStorage` persistence (`jh.lang`), and `PUT` on switch when signed in, in `frontend/apps/web/src/app/core/i18n/language.service.ts`
- [ ] T035 [US2] Update `AuthService` to apply `preferredLanguage` from `/me` on load and after login (account preference supersedes local choice — FR-007) in `frontend/apps/web/src/app/core/services/auth.service.ts`
- [ ] T036 [US2] Build the language switcher component (endonym labels "English/Deutsch/Español", current-language indicator, immediate switch, stays signed in) in `frontend/apps/web/src/app/features/settings/language/` per DESIGN.md
- [ ] T037 [US2] Surface the switcher **signed-out** (global chrome / auth screens) and **signed-in** (account menu + settings) per clarification Q2 (FR-004a), wiring placement per DESIGN.md

### Frontend tests

- [ ] T038 [P] [US2] Unit test: `LanguageService` precedence + persistence + on-switch `PUT` in `frontend/apps/web/src/app/core/i18n/language.service.spec.ts`
- [ ] T039 [P] [US2] e2e: manual override, anonymous persistence across reload, signed-in cross-session precedence in `frontend/apps/web-e2e/src/i18n-switch.spec.ts`

**Checkpoint**: US1 + US2 both work — detection *and* explicit, persisted choice.

---

## Phase 5: User Story 3 - Receive emails and notifications in my language (Priority: P3)

**Goal**: Transactional emails (subjects + bodies) are sent in the right language — the caller's effective language for pre-account flows, the recipient's stored preference for recipient-addressed content — with English fallback. In-app notification localization already shipped in US1.

**Independent Test**: German-preferring user gets German emails/notifications; a recipient-addressed email uses the *recipient's* language (not the actor's); no preference → English.

### Implementation

- [ ] T040 [US3] Add `languageInterceptor` stamping `Accept-Language: <effective-language>` (from `LanguageService`) on all `/api` calls and append it to the chain `[authInterceptor, retryInterceptor, languageInterceptor]` in `frontend/apps/web/src/app/core/interceptors/language.interceptor.ts` + `app.config.ts` (research D5)
- [ ] T041 [US3] Configure `AddRequestLocalization` (supported `en`/`de`/`es`, default `en`, Accept-Language provider) in `backend/Program.cs`
- [ ] T042 [US3] Implement `RecipientCultureResolver` (recipient `User.PreferredLanguage` → lookup by email → `en`; research D9) in `backend/Services/Localization/`
- [ ] T043 [P] [US3] Move existing email templates into `backend/EmailTemplates/en/` and update `LoadTemplateAsync` to select the `{culture}` folder with `en` fallback in `backend/Services/EmailTemplateService/EmailTemplateService.cs`
- [ ] T044 [P] [US3] Create `de`/`es` email body templates (header, footer, base-styles + each transactional template) under `backend/EmailTemplates/{de,es}/` — drafts flagged for native review
- [ ] T045 [P] [US3] Move email **subjects** + other backend-emitted user-facing copy into `.resx` resources under `backend/Resources/` (resolved via `IStringLocalizer`)
- [ ] T046 [US3] Update `*EmailService` classes to localize subjects via `IStringLocalizer` and select body culture: **request culture** for pre-account auth emails (`AuthEmailService`: verify/reset/resend), **recipient culture** via `RecipientCultureResolver` for recipient-addressed emails (welcome, password-changed, team/event/party/market) in `backend/Services/Email/*EmailService.cs`
- [ ] T047 [P] [US3] Localize any server-generated email-mirror notification copy (if produced backend-side) in `backend/Services/Notifications/`

### Backend tests

- [ ] T048 [P] [US3] xUnit: culture base-matching + allowlist + `en` fallback via `RequestLocalization`
- [ ] T049 [P] [US3] xUnit: `RecipientCultureResolver` recipient-vs-request selection (D9) + email language selection (pre-account uses caller, recipient-addressed uses recipient) in `backend` test project

**Checkpoint**: All three stories functional and independently testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T050 [P] Instantiate `specs/031-i18n-localization/checklists/ui-review.md` from `.specify/templates/ui-review-checklist-template.md` and verify German long-string tolerance (no truncation/overflow) on key screens (gate 7, SC-006)
- [ ] T051 [P] Add a lint/CI guard flagging new hardcoded user-facing strings in `frontend/apps/web/src/app/features/**` (safeguard against regressions)
- [ ] T052 Run all [quickstart.md](./quickstart.md) scenarios 1–8 end-to-end and record results
- [ ] T053 [P] Open a GitHub issue tracking the native-speaker review pass of `de`/`es` catalogs + email templates (clarification Q4 fast-follow), referencing #77
- [ ] T054 [P] If the switcher introduces a new component pattern, update DESIGN.md accordingly (report any conflict, do not self-resolve)

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
