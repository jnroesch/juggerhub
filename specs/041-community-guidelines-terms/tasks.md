---

description: "Task list for 041 — Terms of Use with community rules"
---

# Tasks: Terms of Use with Community Rules

**Input**: Design documents from `/specs/041-community-guidelines-terms/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: **Included, and not optional here.** The specification makes two of them
release-blocking requirements in their own right (FR-026 catalogue parity, FR-027 placeholder
sentinel), and the feature's whole value is an evidence trail — a record that survives ban and
erasure (FR-023, FR-024) is only worth having if something proves it still does.

**Organization**: Grouped by user story. Note the deliberate ordering inversion below.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

Web application (per [plan.md](./plan.md)): `backend/` (.NET API) and `frontend/apps/web/`
(Angular in an Nx monorepo). All paths below are repository-relative.

## ⚠️ Story ordering: US2 ships before US1

The priority order in spec.md is US1 (P1) → US2 (P2) → US3 (P3). **The build order is
US2 → US1 → US3**, and this is not a re-prioritisation:

- FR-016 requires the acceptance control to link to the full document. It cannot link to a page
  that does not exist.
- Research R1 makes the register form read the version from the legal catalogue. The catalogue
  entry has to exist before anything can read it.

US1 remains the P1 story and the MVP goal — it is simply gated on its own prerequisite. US2 is
independently shippable on its own (a readable document, no behaviour change), which is what
makes the inversion safe rather than a merged mega-phase.

---

## Phase 1: Setup (Baseline)

**Purpose**: Establish that the guards this feature relies on are actually running *before*
anything changes. A guard that was already broken would otherwise look like it passed.

- [X] T001 [P] Run `npm --prefix frontend test -- legal-catalog` and confirm the existing feature-036 catalogue guards pass, recording the current test count in `specs/041-community-guidelines-terms/checklists/ui-review.md` notes
- [X] T002 [P] Run `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter Auth` and confirm the Testcontainers-backed registration suite passes against a clean database

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The data shape, the configuration, and the component refactor that every story
below depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Backend foundation

- [X] T003 [P] Create `backend/Services/Terms/TermsOptions.cs` with `SectionName = "Terms"` and `CurrentVersion` defaulting to `"2026-08-03"`, following the `AdminOptions`/`RetentionOptions` shape — a missing configuration section must fall back to the default, never to empty (constitution VII: safe defaults, never "unlimited")
- [X] T004 Register the options in `backend/Program.cs` via `builder.Services.Configure<TermsOptions>(builder.Configuration.GetSection(TermsOptions.SectionName))`, placed with the other feature option registrations, and add the `Terms` section to `backend/appsettings.json`
- [X] T005 [P] Create `backend/Entities/TermsAcceptance.cs` per [data-model.md](./data-model.md) — `BaseEntity`, `UserId`, `User`, `Version`, `DisplayLanguage`; `CreatedDate` **is** the acceptance moment (no separate `AcceptedAt` column), mirroring `AdminActionRecord`. Include the XML-doc warning that this row is **not** owned data and must never be added to `AccountDeletionService.EraseOwnedDataAsync`
- [X] T006 Add `public ICollection<TermsAcceptance> TermsAcceptances { get; set; } = [];` to `backend/Entities/User.cs` — a collection, not a reference, so FR-021's future re-acceptance needs no migration
- [X] T007 Add the `DbSet<TermsAcceptance>` and the entity configuration to `backend/Data/AppDbContext.cs`: `Version` max 32 required, `DisplayLanguage` max 8 required, index on `(UserId, CreatedDate)`, unique index on `(UserId, Version)`, and **`DeleteBehavior.Restrict`** on the user FK — copy the `AdminActionRecord` block's structure and its "history must never vanish with an account row" reasoning
- [X] T008 Generate the migration with `dotnet ef migrations add AddTermsAcceptance` in `backend/`, then read the generated `Up`/`Down` to confirm it only creates `TermsAcceptances` and touches no existing table

### Frontend foundation — the 2 → N cross-link refactor

- [X] T009 [P] Extend the types in `frontend/apps/web/src/app/features/legal/legal-content.service.ts`: add `terms: LegalDocument` to `LegalContent`, and add optional `version?: string` and `lastUpdated?: string` to `LegalDocument` (research R4 — the terms document carries its own date because catalogue-level `meta.lastUpdated` is shared with the privacy policy)
- [X] T010 Replace the `siblingLink` + `siblingLabelKey` inputs on `frontend/apps/web/src/app/features/legal/legal-page.component.ts` with `siblings = input.required<readonly { link: string; labelKey: string }[]>()`, add `'terms'` to `LegalDocumentKey`, and make `lastUpdatedLine` prefer the document's own `lastUpdated` and `version` over the shared `meta`
- [X] T011 Update `frontend/apps/web/src/app/features/legal/legal-page.component.html` to loop the `siblings` list in the footer cross-link block, and to render the version alongside the last-updated date in the `data-testid="legal-meta"` caption line
- [X] T012 [P] Update `frontend/apps/web/src/app/features/legal/privacy/privacy.component.html` to pass `[siblings]` with `/terms` and `/imprint`
- [X] T013 [P] Update `frontend/apps/web/src/app/features/legal/imprint/imprint.component.html` to pass `[siblings]` with `/terms` and `/privacy`
- [X] T014 Update `frontend/apps/web/src/app/features/legal/legal-page.component.spec.ts` for the new input shape and add a case asserting a document's own `lastUpdated` wins over the catalogue-level `meta.lastUpdated`

**Checkpoint**: `npm --prefix frontend test` and `dotnet build backend` both pass. Privacy and
imprint render exactly as before with the new input shape — a pure refactor, no visible change.

---

## Phase 3: User Story 2 — Anyone can read the rules (Priority: P2, built first) 📄

**Goal**: A complete, readable, versioned Terms of Use at `/terms` in three languages, reachable
everywhere the privacy policy is, with the release guards live.

**Independent Test**: While signed out, reach `/terms` from the footer and from `/register` in
each of the three languages; confirm the German text is marked authoritative, the version and
date are visible, the table of contents anchors correctly, and a blocked catalogue fetch shows a
visible error rather than a blank document.

### The document text

- [X] T015 [US2] Write the **German** terms document into `frontend/apps/web/public/i18n/legal/de.json` as a `terms` node — `title`, `version`, `lastUpdated`, `intro`, and the eight sections in the order fixed by [contracts/catalog.md](./contracts/catalog.md). German is the authoritative text and is drafted first; the other two are translations of it, not independent drafts
- [X] T016 [P] [US2] Translate the `terms` node into `frontend/apps/web/public/i18n/legal/en.json`, keeping the key set byte-identical to `de.json` and `version` byte-identical across files
- [X] T017 [P] [US2] Translate the `terms` node into `frontend/apps/web/public/i18n/legal/es.json`, same constraints as T016
- [X] T018 [US2] Add `crossLink.toTerms` and `crossLink.toTermsLong` to all three files in `frontend/apps/web/public/i18n/legal/`, matching the existing `toPrivacy`/`toPrivacyLong` pattern
- [X] T019 [P] [US2] Add the `legal.terms` short label to the **main** catalogues `frontend/apps/web/public/i18n/{en,de,es}.json` — the footer renders on every screen and cannot wait for the lazily fetched legal catalogue

**Drafting constraints for T015–T017** (from [contracts/catalog.md](./contracts/catalog.md), all
release-relevant): `behaviour` covers every member-writable surface as *categories of conduct*,
not a feature list; `yourContent` grants a **display permission only** — the privacy policy
already says "What you write and upload is yours until you say otherwise"; `endingIt` matches
what feature 037 actually does (immediate self-service erasure, messages surviving as "A former
player", email released); `whatWeMayDo` states what may happen with **no** review timeline,
appeal procedure, report button, or moderation team, none of which exist (FR-008); `yourAccount`
carries the guardian clause with **no** age question anywhere (FR-013); `changesAndLaw` is
publish-only with no notification promised (FR-014) and gives `hello@juggerhub.com`, the address
already published in the other two documents.

### The page

- [X] T020 [P] [US2] Create `frontend/apps/web/src/app/features/legal/terms/terms.component.ts` exporting `TERMS_SECTIONS` as an explicit ordered array (`whatThisIs`, `yourAccount`, `behaviour`, `yourContent`, `whatWeMayDo`, `endingIt`, `noGuarantees`, `changesAndLaw`) and a `TermsComponent` mirroring `PrivacyComponent`
- [X] T021 [P] [US2] Create `frontend/apps/web/src/app/features/legal/terms/terms.component.html` rendering `<jh-legal-page doc="terms" [sectionOrder]="sections" [showToc]="true" [siblings]="…" />` with `/privacy` and `/imprint` as siblings
- [X] T022 [US2] Add the lazy `terms` route to `frontend/apps/web/src/app/app.routes.ts` inside the existing legal-routes comment block — **no guard**, outside the shell, no backend call (the third documented exception to feature 026; see [contracts/routes.md](./contracts/routes.md) RC-1)
- [X] T023 [US2] Add the `/terms` anchor **first** in `frontend/apps/web/src/app/shared/ui/legal-links/legal-links.component.html`, styled identically to the existing two. This single edit satisfies FR-010 across all 11 placements — do **not** edit the off-shell screens individually
- [X] T024 [US2] Update `frontend/apps/web/src/app/shared/ui/legal-links/legal-links.component.spec.ts` to assert all three links render in both the `footer` and `inline` variants

### Guards for User Story 2

- [X] T025 [US2] Run `npm --prefix frontend test -- legal-catalog` and confirm the existing generic guards now cover the `terms` node. Then **verify each guard bites**: delete a paragraph from `terms` in `de.json` (G1 must fail), and place `__TODO__` in a terms value (G2 must fail). Restore both. **Never** fix a G1 failure by changing the global `useFallbackTranslation` — that breaks feature 031 app-wide
- [X] T026 [US2] Add `backend/tests/JuggerHub.Api.IntegrationTests/Terms/TermsVersionParityTests.cs` asserting (a) `terms.version` is byte-identical across `en.json`/`de.json`/`es.json` and (b) it equals `TermsOptions.CurrentVersion`. Resolve the catalogue directory with the upward repo-walk from `AppContext.BaseDirectory` used by `Email/TemplateParityTests.cs`, **throwing** if not found — a guard that silently skips is worse than none. G1 compares keys; values are supposed to differ between translations, so this is the only thing checking the one leaf that must not

**Checkpoint**: `/terms` renders in all three languages, is linked from every screen, and the
three release guards are live. **This is independently shippable** — no behaviour has changed.

---

## Phase 4: User Story 1 — Agreeing before getting an account (Priority: P1) 🎯 MVP

**Goal**: Registration requires an active, server-validated acceptance and records durable
evidence of it.

**Independent Test**: Register through the form without ticking the box — refused. Send a
registration request that omits acceptance entirely — refused server-side, with no account left
behind. Tick and register — the account exists and exactly one acceptance record names the
version, the moment, and the display language.

### Backend

- [X] T027 [US1] Add `AcceptsTerms` (bool), `TermsVersion` (string), and `TermsLanguage` (string) to `RegisterRequest` in `backend/Dtos/Auth/AuthRequests.cs`. **Attributes go on the constructor parameters, not the generated properties** — MVC reads parameter-level metadata for positional records and throws otherwise; the file's existing header comment says so
- [X] T028 [US1] Add `TermsNotAccepted` and `TermsVersionMismatch` to `RegisterStatus` in `backend/Services/Auth/AuthResults.cs`, with matching `RegisterResult` factory methods
- [X] T029 [US1] In `backend/Services/Auth/AuthService.cs`, inject `IOptions<TermsOptions>` and validate terms **at the very top of `RegisterAsync`** — before the password check, before `ResolveHandleForRegistrationAsync`, and before `FindByEmailAsync`. Refuse when `AcceptsTerms` is not true, when `TermsVersion` differs from the configured current version, and when `TermsLanguage` is outside the supported allowlist. See [contracts/register-api.md](./contracts/register-api.md) for why the ordering matters: these refusals must never entangle with the method's deliberately enumeration-neutral `Accepted()` response
- [X] T030 [US1] In the same method, attach the acceptance to the user graph before `_userManager.CreateAsync` — `user.TermsAcceptances.Add(new TermsAcceptance { Version = options.CurrentVersion, DisplayLanguage = request.TermsLanguage })` — so it persists in the **same `SaveChanges`** as the account and profile. Record the **server's** version constant, never the client-submitted string
- [X] T031 [US1] Add the two new `switch` arms to `Register` in `backend/Controllers/AuthController.cs`: `TermsNotAccepted` → `400` "Terms not accepted", `TermsVersionMismatch` → `409` "Terms have changed" with a detail telling the reader to reload. Log the outcome only — never the submitted values (Principle I)

### Frontend

- [X] T032 [P] [US1] Add the three fields to the register payload type in `frontend/apps/web/src/app/core/models/auth.models.ts`
- [X] T033 [P] [US1] Add the acceptance-control strings to `frontend/apps/web/public/i18n/{en,de,es}.json` under `auth.register` — the label segments either side of the link, the blocked-submit reason, the "terms have changed" message, and the catalogue-load-failure message
- [X] T034 [US1] In `frontend/apps/web/src/app/features/auth/register/register.component.ts`, add an `acceptsTerms` control defaulting to `false`, load the legal catalogue via the existing `LegalContentService` to read `terms.version`, extend `canSubmit` to require acceptance **and** a successfully loaded catalogue, and send `{ acceptsTerms, termsVersion, termsLanguage }` on submit with the Transloco active language. Surface a `409` as the specific "terms have been updated" message, not the generic error
- [X] T035 [US1] In `frontend/apps/web/src/app/features/auth/register/register.component.html`, add the unticked checkbox inside a single `<label>` following the existing `sign-in-remember` pattern, with the `/terms` link **underlined** (in-prose link per DESIGN.md) and separately tab-reachable. Compose the label from translated segments around a real `routerLink` — **no `[innerHTML]`**
- [X] T036 [US1] Extend `frontend/apps/web/src/app/features/auth/register/register.component.spec.ts`: the box is unticked on first render, submit is blocked while unticked, submit is blocked when the catalogue failed to load, and the payload carries all three fields on success

### Tests for User Story 1

- [X] T037 [P] [US1] Add `backend/tests/JuggerHub.Api.IntegrationTests/Terms/TermsAcceptanceRegistrationTests.cs` covering acceptance omitted and `acceptsTerms: false` — both `400`, and assert **no** user, profile, or acceptance row exists afterwards (FR-018, FR-019, SC-002). Call the endpoint directly; the form is not the boundary
- [X] T038 [P] [US1] Add a test that a valid registration creates exactly one acceptance row carrying the server's version, a `CreatedDate` at registration time, and the submitted display language (FR-020, SC-001)
- [X] T039 [P] [US1] Add tests for a stale `termsVersion` → `409` with no account created, and an unsupported `termsLanguage` → `400` with no account created
- [X] T040 [US1] Add a test that a registration failing **after** valid acceptance (duplicate handle) leaves the `TermsAcceptances` count unchanged (FR-022) — this is what proves the row rides the account's own `SaveChanges`

**Checkpoint**: The gate is closed. No account can be created without recorded acceptance,
including by a caller that never rendered the form.

---

## Phase 5: User Story 3 — Producing what an account agreed to (Priority: P3)

**Goal**: For any account, the operator can state which version of the rules it is bound by and
when — and that record outlives every state the account can reach.

**Independent Test**: Retrieve the acceptance for an account through the admin user detail; then
suspend it, ban it, and finally erase it, confirming the record still names the original version
and timestamp at each step.

- [X] T041 [P] [US3] Add `TermsAcceptanceDto(string Version, DateTime AcceptedAt, string DisplayLanguage)` to `backend/Dtos/Admin/AdminUserDtos.cs`, projecting `AcceptedAt` from `CreatedDate` so the DTO reads as evidence. No user identifier crosses the boundary — the caller already knows which account it asked about
- [X] T042 [US3] Extend the `AdminUserDetailDto` projection in `backend/Services/Admin/AdminUserService.cs` (`GetDetailAsync`) with the account's acceptances, newest first, via an explicit `.Select` on the existing `AsNoTracking` query — no separate round-trip, no object mapper (Principle II/III)
- [X] T043 [P] [US3] Add `backend/tests/JuggerHub.Api.IntegrationTests/Terms/TermsAcceptanceSurvivalTests.cs` asserting the record is present and unchanged after `SuspendAsync` and after `BanAsync` (FR-023)
- [X] T044 [US3] ⚠ Add the erasure-survival test to the same file: run the feature-037 self-service deletion, then assert the acceptance row **still exists** with its original version and timestamp, and that the `User` row it points at has `Status = Deleted` with neutralised identifying columns (FR-024). **This is the test that fails if someone adds `TermsAcceptances` to `AccountDeletionService.EraseOwnedDataAsync`** — see [data-model.md](./data-model.md)
- [X] T045 [US3] Add a test that re-registering with the released email after erasure creates a **new** account and a **second** acceptance row, leaving the original untouched

**Checkpoint**: The chain from rule → record → action is verifiable end to end.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T046 Read `backend/Services/Account/AccountDeletionService.cs` and confirm `EraseOwnedDataAsync` was **not** modified by this feature. The method is a list of `ExecuteDeleteAsync` calls over every `UserId`-keyed table and this feature added one such table that must stay off it
- [X] T047 [P] Work through [`checklists/ui-review.md`](./checklists/ui-review.md) against the diff, recording `file:line` for any failure. Note the two intentional overrides already documented there: CHK030 (`container-sm`) beats CHK013, and CHK031 (`2xl` section rhythm) beats CHK014, for document pages
- [ ] T048 [P] Run [quickstart.md](./quickstart.md) scenarios 8–10 manually — reading the document, the acceptance control, and the cross-document consistency read (FR-006, FR-008, FR-009, SC-007)
- [X] T049 Run the full verification set: `dotnet build backend`, `dotnet test backend/tests/JuggerHub.Api.IntegrationTests`, `npm --prefix frontend test`, `npm --prefix frontend run lint`, and confirm the migration applies cleanly to a fresh database
- [X] T050 Update `specs/041-community-guidelines-terms/checklists/requirements.md` notes with anything the build revealed, and record any spec drift in the PR description

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **US2 (Phase 3)**: Depends on Foundational. **Blocks US1** (see the ordering note at the top)
- **US1 (Phase 4)**: Depends on Foundational **and US2** — needs the document to link to and the catalogue version to read
- **US3 (Phase 5)**: Depends on Foundational and US1 — there is nothing to retrieve until acceptance records exist
- **Polish (Phase 6)**: Depends on all desired stories

### User Story Dependencies

- **US2 (P2)**: The only story that is genuinely standalone. Ships a readable document with zero behaviour change
- **US1 (P1)**: Hard dependency on US2 (FR-016 + research R1). Independently *testable* once US2 lands
- **US3 (P3)**: Depends on US1 producing records. Its survival tests are the feature's real regression net

### Within Each Story

- Backend entity/config before services; services before controllers
- Catalogue text before the component that renders it
- The German text before its translations (T015 before T016/T017)
- The `siblings` refactor before the terms page (Phase 2 before Phase 3)

### Parallel Opportunities

- T001, T002 — both baseline checks
- T003, T005, T009 — different files, no shared dependency
- T012, T013 — the two sibling page updates
- T016, T017 — the two translations, once German exists
- T019, T020, T021 — main catalogue, component TS, component HTML
- T032, T033 — client model and strings
- T037, T038, T039 — independent test files/cases
- T041, T043 — DTO and survival tests
- T047, T048 — UI review and manual quickstart

---

## Parallel Example: User Story 2

```bash
# After the German authoritative text (T015) lands, both translations run together:
Task: "Translate the terms node into frontend/apps/web/public/i18n/legal/en.json"
Task: "Translate the terms node into frontend/apps/web/public/i18n/legal/es.json"

# The page scaffolding is independent of the catalogue work:
Task: "Create frontend/apps/web/src/app/features/legal/terms/terms.component.ts"
Task: "Create frontend/apps/web/src/app/features/legal/terms/terms.component.html"
Task: "Add the legal.terms label to frontend/apps/web/public/i18n/{en,de,es}.json"
```

## Parallel Example: User Story 1

```bash
# Backend tests, once T027–T031 land:
Task: "Registration refused without acceptance in backend/tests/.../Terms/TermsAcceptanceRegistrationTests.cs"
Task: "Valid registration records the evidence row"
Task: "Stale version 409 and unsupported language 400"
```

---

## Implementation Strategy

### Prerequisite first (US2)

1. Phase 1 Setup — confirm the guards already run
2. Phase 2 Foundational — data shape, options, and the 2 → N refactor
3. Phase 3 US2 — the document
4. **STOP and VALIDATE**: `/terms` reads correctly in three languages; guards bite when broken
5. Shippable on its own — a published document with no behaviour change

### MVP (US1)

6. Phase 4 US1 — the acceptance gate
7. **STOP and VALIDATE**: registration cannot succeed without a recorded acceptance, proven
   against the endpoint rather than the form
8. This is the feature's point: the enforcement powers from 013 now rest on an agreement

### Completing the chain (US3)

9. Phase 5 US3 — retrieval plus the survival tests
10. Phase 6 Polish — UI review, manual scenarios, full verification

---

## Notes

- `[P]` tasks touch different files and have no incomplete dependencies
- Commit after each task or logical group; keep phases in separate commits so US2 can ship alone
- **Scope discipline**: this feature reserves rights the product cannot exercise through any
  interface. FR-005 reserves content removal; FR-008 forbids the text from describing tooling
  that does not exist. **Do not build admin content removal, a report button, a moderation
  queue, or a re-acceptance interstitial** — all four are recorded in spec.md's Out of Scope,
  and adding one silently expands the feature
- **Do not** weaken a guard to make a build pass. A G1 failure means German text is missing; a G3
  failure means a record would name a version whose text nobody saw
