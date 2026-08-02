---

description: "Task list for 039 — Transactional Email Templates & Notification Preference Gating"
---

# Tasks: Transactional Email Templates & Notification Preference Gating

**Input**: Design documents from `/specs/039-transactional-email-templates/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. The spec requires automated coverage explicitly (FR-024 – FR-027, FR-026a), so test tasks are first-class here rather than optional.

**Organization**: Grouped by user story so each can be implemented, tested, and demoed independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)
- Exact file paths are included in every task

## Path Conventions

Web application: `backend/` (ASP.NET Core) and `frontend/apps/web/src/app/` (Angular + Nx), per [plan.md](./plan.md) Structure Decision.

---

## ⚠️ Ordering constraint that is NOT a preference

**Phase 2 (T004–T006) MUST land before Phase 3.** The four services HTML-encode their values today; `ReplaceVariables` does a raw `string.Replace`. Migrating them onto templates before the template layer encodes by default would *remove* their escaping and open an injection hole. A commit landing between the two ships that hole. See [research.md](./research.md) D1.

---

## Phase 1: Setup

**Purpose**: Establish a green baseline and a home for the new tests

- [X] T001 Run `dotnet test` in `backend/` and `npx nx test web` in `frontend/` to confirm a green baseline before any change
- [X] T002 [P] Create the test directory `backend/tests/JuggerHub.Api.IntegrationTests/Email/` for this feature's template and gating coverage

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The escaping guarantee, the localizer capability, and the enum members that multiple stories build on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Escaping (blocks US1 — see ordering constraint above)

- [X] T003 [P] Add an encoding regression test asserting a team named `<b>Ravens</b>` arrives escaped in an email body, in `backend/tests/JuggerHub.Api.IntegrationTests/Email/EmailEncodingTests.cs` (must FAIL before T004)
- [X] T004 Add the `RawHtml(string Value)` marker record and make `ReplaceVariables` HTML-encode every substituted value unless it is a `RawHtml`, in `backend/Services/EmailTemplateService/EmailTemplateService.cs`
- [X] T005 Wrap the only two variables that legitimately carry markup — `PLAN_FEATURES` in `GenerateSubscriptionWelcomeEmailAsync` and `STATUS_STYLE` in `GenerateUnusualLoginNotificationEmailAsync` — in `RawHtml`, in `backend/Services/EmailTemplateService/EmailTemplateService.cs` (do **not** wrap `ACTOR_LINE`, `AUTHOR_LINE`, or any `*_URL`; see [research.md](./research.md) D1)
- [X] T006 Add a test asserting subject lines are **not** escaped — a team named `Ravens & Co` must read literally in the subject — in `backend/tests/JuggerHub.Api.IntegrationTests/Email/EmailEncodingTests.cs` (FR-010)

### Localizer capability (blocks US1)

- [X] T007 Add a `string Get(string key, string culture, params object[] args)` overload applying `string.Format` with the invariant culture to `IEmailLocalizer` and `EmailLocalizer`, in `backend/Services/Email/EmailLocalizer.cs` (positional `{0}`/`{1}` placeholders — word order differs across en/de/es)

### Notification enums (blocks US2 cancellation gating and US3)

- [X] T008 Append `EventCancelled = 8` to `NotificationType` and `Events = 3` to `NotificationCategory` in `backend/Entities/NotificationEnums.cs`
- [X] T009 Add the `NotificationType.EventCancelled => NotificationCategory.Events` case to `NotificationCategories.For` in `backend/Entities/NotificationEnums.cs` — **the switch ends in `_ => NotificationCategory.TeamNews`, so omitting this case compiles, passes existing tests, and silently files cancellations under the user's Team news toggle**
- [X] T010 [P] Add a test asserting `NotificationCategories.For(NotificationType.EventCancelled) == NotificationCategory.Events`, in `backend/tests/JuggerHub.Api.IntegrationTests/Notifications/PreferenceTests.cs`

**Checkpoint**: The template layer is safe by default, subjects are provably exempt, and the new enum members exist and are correctly mapped. User story work can begin.

---

## Phase 3: User Story 1 — Every transactional email looks and reads like JuggerHub (Priority: P1) 🎯 MVP

**Goal**: The four emails carry the shared header, footer, address block, and footer reason, and render in the recipient's language.

**Independent Test**: Trigger each of the four against a recipient whose stored language is German; each arrives with shared chrome and German subject/chrome, and none ends in a bare `— JuggerHub`.

### Tests for User Story 1

- [X] T011 [P] [US1] Add a test asserting a non-auth email (party request) carries the shared header, address block, and footer reason, in `backend/tests/JuggerHub.Api.IntegrationTests/Email/EmailChromeTests.cs` (FR-001, FR-002, FR-024)
- [X] T012 [P] [US1] Add a placeholder-parity test asserting the `en`, `de`, and `es` variants of each of the four templates contain an identical `{{PLACEHOLDER}}` set, in `backend/tests/JuggerHub.Api.IntegrationTests/Email/TemplateParityTests.cs` — **fallback is per-file, not per-placeholder, so a `de` template missing `{{PARTY_URL}}` would ship a German email with no call-to-action and no error** (FR-026a)
- [X] T013 [P] [US1] Extend `No_email_leaves_a_placeholder_unrendered` to cover all four new emails, in `backend/tests/JuggerHub.Api.IntegrationTests/Auth/EmailLinkTests.cs` (FR-026)
- [X] T014 [P] [US1] Add a test asserting a recipient with stored language `de` receives a German subject and chrome for a party-news email, in `backend/tests/JuggerHub.Api.IntegrationTests/Account/EmailLanguageTests.cs` (FR-009, SC-002)

### Templates for User Story 1

Each task writes all three language variants. Use only classes already in `base-styles.html` — no new CSS (FR-004). Contracts: [contracts/email-templates.md](./contracts/email-templates.md) EC-3 – EC-7.

- [X] T015 [P] [US1] Author `event-cancelled.html` in `backend/EmailTemplates/en/`, `backend/EmailTemplates/de/`, and `backend/EmailTemplates/es/` per contract EC-3
- [X] T016 [P] [US1] Author `party-request.html` in `backend/EmailTemplates/{en,de,es}/` per contract EC-4 (one template serves both the initial request and the nudge)
- [X] T017 [P] [US1] Author `party-news.html` in `backend/EmailTemplates/{en,de,es}/` per contract EC-5, reusing the excerpt card markup from `backend/EmailTemplates/en/team-news.html`
- [X] T018 [P] [US1] Author `market-invite.html` in `backend/EmailTemplates/{en,de,es}/` per contract EC-6, preserving today's "nothing happens until you accept" reassurance

### Localizer copy for User Story 1

- [X] T019 [US1] Add the twelve `subject.*` / `title.*` / `footer.*` keys for the four emails, each with `en`, `de`, and `es` values, in `backend/Services/Email/EmailLocalizer.cs` per contract EC-8

### Template service methods for User Story 1

- [X] T020 [US1] Declare `GenerateEventCancelledEmailAsync`, `GeneratePartyRequestEmailAsync`, `GeneratePartyNewsEmailAsync`, and `GenerateMarketInviteEmailAsync` — each taking a `culture` — in `backend/Services/EmailTemplateService/IEmailTemplateService.cs`
- [X] T021 [US1] Implement the four methods, each supplying its `EMAIL_TITLE` and `FOOTER_REASON` from `IEmailLocalizer`, in `backend/Services/EmailTemplateService/EmailTemplateService.cs` (depends on T019, T020)

### Migrate the four services for User Story 1

- [X] T022 [P] [US1] Replace the inline HTML in `SendCancellationEmailAsync` with the template call and a localized subject, removing the now-redundant `HtmlEncode` calls, in `backend/Services/Email/EventEmailService.cs` (depends on T021)
- [X] T023 [US1] Replace the inline HTML in `SendPartyRequestEmailAsync` and `SendPartyNewsEmailAsync` with template calls and localized subjects, removing the `HtmlEncode` calls and the stale "finalized with dedicated templates in their stories" class comment, in `backend/Services/Email/PartyEmailService.cs` (depends on T021)
- [X] T024 [P] [US1] Replace the inline HTML in `SendMarketInviteEmailAsync` with the template call and a localized subject, removing the `HtmlEncode` calls, in `backend/Services/Email/MarketEmailService.cs` (depends on T021)

### Thread recipient culture for User Story 1

- [X] T025 [P] [US1] Add `PreferredLanguage` to the team-member projection in `PostRequestAsync` and pass the resolved culture to the email call, in `backend/Services/Parties/PartyService.cs`
- [X] T026 [P] [US1] Add `PreferredLanguage` to the crew projection in `NotifyCrewAsync`, pass the resolved culture, and truncate the news body to a 140-character excerpt matching `TeamNewsService.Excerpt`, in `backend/Services/Parties/PartyNewsService.cs` (FR-005)
- [X] T027 [P] [US1] Add `PreferredLanguage` to the participant and team-admin projections in `NotifyCancellationAsync` and pass the resolved culture, in `backend/Services/Events/EventService.cs`
- [X] T028 [P] [US1] Add `PreferredLanguage` to the target projection in `DeliverInviteAsync` and pass the resolved culture, in `backend/Services/Marketplace/MarketRequestService.cs`
- [X] T029 [P] [US1] Add `PreferredLanguage` to the target projection on the nudge path and pass the resolved culture, in `backend/Services/Parties/PartyRosterService.cs`

**Checkpoint**: All four emails are branded, localized, and safe. US1 is independently demoable via Mailpit.

---

## Phase 4: User Story 2 — Notification preferences actually govern these emails (Priority: P2)

**Goal**: A disabled Email channel stops these emails; the footer's "Manage notifications" link tells the truth.

**Independent Test**: Disable Email for a category, trigger the corresponding email, confirm no mail arrives while the in-app notification still does.

**Note**: The In-app channel needs **no work** — `NotificationService.CreateAsync`/`CreateManyAsync` already filter on it centrally for every producer ([research.md](./research.md) D2). Only the Email side is ungoverned.

### Tests for User Story 2

- [X] T030 [P] [US2] Add a test asserting a user with Email disabled for `InvitesAndRoster` receives no market-invite email but still receives the in-app notification, in `backend/tests/JuggerHub.Api.IntegrationTests/Marketplace/MarketplaceTests.cs` (FR-012, FR-016, FR-027)
- [X] T031 [P] [US2] Add a test asserting a user with Email disabled for `TeamNews` receives no party-news email, in `backend/tests/JuggerHub.Api.IntegrationTests/Parties/PartyTests.cs`
- [X] T032 [P] [US2] Add a test asserting a user with **no stored preference** still receives a party-request email, in `backend/tests/JuggerHub.Api.IntegrationTests/Parties/PartyTests.cs` (FR-014)
- [X] T033 [P] [US2] Add a test asserting the party-request **nudge** path is gated, not only the initial fan-out, in `backend/tests/JuggerHub.Api.IntegrationTests/Parties/PartyTests.cs`

### Implementation for User Story 2

Each gate is one batched `GetEnabledRecipientsAsync(..., NotificationChannel.Email)` call before the send loop, wrapped so a preference failure never fails the originating action — mirroring `TeamNewsService`. Contract: [contracts/notification-contracts.md](./contracts/notification-contracts.md) NC-3.

- [X] T034 [US2] Inject `INotificationPreferenceService` and gate the party-request email fan-out on `InvitesAndRoster` + `Email`, in `backend/Services/Parties/PartyService.cs`
- [X] T035 [US2] Inject `INotificationPreferenceService` and gate the **nudge** email on `InvitesAndRoster` + `Email`, in `backend/Services/Parties/PartyRosterService.cs` — this is the second party-request call site and is the one most easily missed
- [X] T036 [US2] Inject `INotificationPreferenceService` and gate the party-news email fan-out on `TeamNews` + `Email`, in `backend/Services/Parties/PartyNewsService.cs`
- [X] T037 [US2] Inject `INotificationPreferenceService` and gate the market-invite email on `InvitesAndRoster` + `Email`, in `backend/Services/Marketplace/MarketRequestService.cs` (single recipient — still use the batch call with one id)
- [X] T038 [US2] Inject `INotificationPreferenceService` and gate the cancellation email fan-out on `Events` + `Email`, in `backend/Services/Events/EventService.cs` (depends on T008)

**Checkpoint**: All four emails honour the Email toggle; in-app delivery is unaffected.

---

## Phase 5: User Story 3 — An event cancellation is a first-class notification (Priority: P3)

**Goal**: Cancellation appears in the notification list and is governed by its own "Events" settings row.

**Independent Test**: Cancel an event with sign-ups; each participant's notification list gains a cancellation entry linking to the event, and an "Events" row appears in notification settings with working toggles.

### Tests for User Story 3

- [X] T039 [P] [US3] Add a test asserting cancelling an event creates one `EventCancelled` notification per recipient — individuals plus admins of signed-up teams — de-duplicated by user, in `backend/tests/JuggerHub.Api.IntegrationTests/Events/EventTests.cs` (FR-017)
- [X] T040 [P] [US3] Add a test asserting the preference matrix includes an `Events` category with both channels defaulting to enabled, and that it is returned for `de` and `es` request languages without error, in `backend/tests/JuggerHub.Api.IntegrationTests/Notifications/PreferenceTests.cs` (FR-019, FR-020, FR-021)
- [X] T041 [P] [US3] Add a test asserting a user with Email disabled for `Events` receives no cancellation email but still sees the in-app notification, in `backend/tests/JuggerHub.Api.IntegrationTests/Events/EventTests.cs`

### Backend implementation for User Story 3

- [X] T042 [US3] Add `UserId` to the participant and team-admin projections, switch de-duplication from email address to `UserId`, and fan out `EventCancelled` via `CreateManyAsync` with dedupe key prefix `event-cancelled:{eventId}` alongside the existing email loop, in `backend/Services/Events/EventService.cs` (payload per [data-model.md](./data-model.md): `eventId`, `eventName`)
- [X] T043 [US3] Add `NotificationCategory.Events` to `CategoryOrder` in `backend/Services/Notifications/NotificationPreferenceService.cs` — **a category absent from this list never renders in settings even when its copy exists**
- [X] T044 [US3] Add the `Events` label and description to **all three** language dictionaries in `CategoryCopy`, in `backend/Services/Notifications/NotificationPreferenceService.cs` — **`copy[category]` is an indexer, so a missing `de` or `es` entry throws `KeyNotFoundException` and breaks the whole settings page for those users rather than falling back** (copy in [contracts/notification-contracts.md](./contracts/notification-contracts.md) NC-2)

### Frontend implementation for User Story 3

- [X] T045 [P] [US3] Add `'EventCancelled'` to the `NotificationType` union, add the `EventCancelledPayload` interface (`eventId`, `eventName`) to the `NotificationPayload` union, and add the `isEventCancelled` narrowing helper, in `frontend/apps/web/src/app/core/models/notification.models.ts`
- [X] T046 [P] [US3] Add `'Events'` to the `NotificationCategoryId` union in `frontend/apps/web/src/app/core/models/notification-preferences.models.ts` — this is the only preferences-screen change needed, since rows render from the server-supplied list
- [X] T047 [US3] Extend the `link`, `title`, and `supporting` computeds to handle `EventCancelled` (link `/events/{eventId}`), in `frontend/apps/web/src/app/features/alerts/notification-row/notification-row.component.ts` — **all three must be extended; the icon degrades safely via the template's `@default` arm but an unhandled type falls through to `alerts.row.fallbackTitle` with an empty supporting line** (depends on T045)
- [X] T048 [P] [US3] Add `alerts.row.eventCancelledTitle` (with an `{event}` param) and `alerts.row.eventCancelledSupporting` to `frontend/apps/web/src/app/assets/i18n/en.json`, `de.json`, and `es.json`
- [X] T049 [P] [US3] Add a unit test asserting the row renders a title, supporting line, and `/events/{id}` link for an `EventCancelled` notification, in `frontend/apps/web/src/app/features/alerts/notification-row/notification-row.component.spec.ts`

**Checkpoint**: Cancellation is a real notification with its own governed category, end to end.

---

## Phase 6: User Story 4 — Legal links reachable from every email (Priority: P3)

**Goal**: Privacy policy and imprint are one click away from any transactional email.

**Independent Test**: Trigger any templated email and follow both footer links in each language.

### Tests for User Story 4

- [X] T050 [P] [US4] Extend the footer assertions to require privacy and imprint links built from the configured frontend host, and assert they appear on a **non-auth** email, in `backend/tests/JuggerHub.Api.IntegrationTests/Auth/EmailLinkTests.cs` (FR-022, FR-023, FR-024)

### Implementation for User Story 4

- [X] T051 [US4] Add `PRIVACY_URL` (`{base}/privacy`) and `IMPRINT_URL` (`{base}/imprint`) to `AddSharedUrls` in `backend/Services/EmailTemplateService/EmailTemplateService.cs` per contract EC-2
- [X] T052 [P] [US4] Add localized privacy and imprint links to the `.footer-links` column in `backend/EmailTemplates/en/footer.html`, `backend/EmailTemplates/de/footer.html`, and `backend/EmailTemplates/es/footer.html`, reusing the existing `.footer-links a` styling with no new CSS (depends on T051)

**Checkpoint**: Every templated email — not just the four — carries the legal links.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T053 Copy `.specify/templates/ui-review-checklist-template.md` to `specs/039-transactional-email-templates/checklists/ui-review.md` and verify each item against the diff (Constitution Gate 7; DESIGN.md wins on conflict — this review should confirm the *absence* of new visual patterns)
- [X] T054 Run the full backend suite (`dotnet test` in `backend/`) and the frontend suite (`npx nx test web`, `npx nx lint web`, `npx nx build web` in `frontend/`)
- [ ] T055 **BLOCKED — needs a human.** Walk the manual validation scenarios A–F in [quickstart.md](./quickstart.md), rendering all four emails in all three languages via Mailpit at http://localhost:8025. `docker compose up` requires a local `.env` supplying `JWT_SIGNING_KEY` and friends, which was not present and must not be fabricated. **Substituted** with `Email/TemplateRenderMatrixTests.cs`, which renders all 4 × 3 combinations through the real `EmailTemplateService` and asserts shared chrome, legal links, escaping, accented-character survival, and zero unresolved placeholders — covering scenarios A, B and E programmatically. What remains genuinely manual is the **visual** judgement in a real mail client (layout, the excerpt card, button rendering across clients), which no assertion substitutes for.
- [X] T056 [P] Update GitHub issue #109 with the outcome, noting that its step 4 (delete `EmailLegalFooter.cs`) was a no-op because that file exists in no branch, and that preference gating plus the `Events` category were added beyond the original scope

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **US1 (Phase 3)**: Depends on Foundational (T004–T007 specifically)
- **US2 (Phase 4)**: Depends on Foundational (T008 for the `Events` category used by T038). Independent of US1 in principle, but shares four producer files with it — see conflict note below
- **US3 (Phase 5)**: Depends on Foundational (T008, T009)
- **US4 (Phase 6)**: Depends on Foundational only for build order; functionally independent of all stories
- **Polish (Phase 7)**: Depends on all desired stories

### Cross-story file conflicts (matters for parallel staffing)

These files are touched by more than one story. Sequence them or expect merge conflicts:

| File | Stories |
|---|---|
| `backend/Services/Events/EventService.cs` | US1 (T027), US2 (T038), US3 (T042) |
| `backend/Services/Parties/PartyService.cs` | US1 (T025), US2 (T034) |
| `backend/Services/Parties/PartyNewsService.cs` | US1 (T026), US2 (T036) |
| `backend/Services/Marketplace/MarketRequestService.cs` | US1 (T028), US2 (T037) |
| `backend/Services/Parties/PartyRosterService.cs` | US1 (T029), US2 (T035) |
| `backend/Services/EmailTemplateService/EmailTemplateService.cs` | Foundational (T004, T005), US1 (T021), US4 (T051) |
| `backend/tests/.../Auth/EmailLinkTests.cs` | US1 (T013), US4 (T050) |

**Recommendation**: run US1 → US2 → US3 sequentially in one stream and US4 in parallel — US4 touches only the footer files and one method the others do not modify.

### Within Each User Story

- Tests are written first and must FAIL before implementation
- Templates and localizer copy (T015–T019) before the service methods that consume them (T020–T021)
- Service methods before the callers that use them (T022–T024)
- Backend before frontend in US3 (payload shape drives the client type)

---

## Parallel Opportunities

### Phase 2 (Foundational)

```bash
Task: "T003 encoding regression test in .../Email/EmailEncodingTests.cs"
Task: "T010 category mapping test in .../Notifications/PreferenceTests.cs"
```

### Phase 3 (US1) — the four templates are fully independent

```bash
Task: "T015 event-cancelled.html in EmailTemplates/{en,de,es}/"
Task: "T016 party-request.html in EmailTemplates/{en,de,es}/"
Task: "T017 party-news.html in EmailTemplates/{en,de,es}/"
Task: "T018 market-invite.html in EmailTemplates/{en,de,es}/"
```

Then, after T021, the culture-threading tasks touch five different files:

```bash
Task: "T025 PartyService.cs"      Task: "T026 PartyNewsService.cs"
Task: "T027 EventService.cs"      Task: "T028 MarketRequestService.cs"
Task: "T029 PartyRosterService.cs"
```

### Phase 5 (US3) — frontend model, i18n, and spec files are independent

```bash
Task: "T045 notification.models.ts"
Task: "T046 notification-preferences.models.ts"
Task: "T048 i18n en/de/es.json"
```

---

## Implementation Strategy

### MVP (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational (**mandatory** — the ordering constraint lives here)
2. Phase 3 US1
3. **STOP and VALIDATE**: all four emails branded and localized in Mailpit
4. Demoable: the visible defect in #109 is fixed

### Incremental Delivery

1. Setup + Foundational → template layer is safe, enums exist
2. **+ US1** → four emails branded and localized (MVP)
3. **+ US2** → the footer's "Manage notifications" link becomes truthful
4. **+ US3** → cancellation is a real notification with its own category
5. **+ US4** → legal links on every email

US1 without US2 ships a "Manage notifications" link whose toggles do nothing for these four messages. That is acceptable only as an intermediate commit, not as a deployed increment — ship US1 and US2 together.

---

## Notes

- **[P]** = different files, no dependencies on incomplete tasks
- No database migration is required at any point — preferences are sparse and both enums are append-only
- Commit after each task or logical group; keep Phase 2 and Phase 3 in the same PR so no commit ships the escaping gap
- Verify each test fails before implementing the behaviour it covers
