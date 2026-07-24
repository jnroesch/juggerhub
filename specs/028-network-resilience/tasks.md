---

description: "Task list for 028 — Network Resilience"
---

# Tasks: Network Resilience

**Input**: Design documents from `/specs/028-network-resilience/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/resilience-config.md](./contracts/resilience-config.md)

**Tests**: **INCLUDED.** Resilience is invisible on the happy path — a green build proves only that
nothing broke, which was already true before this feature. Every behaviour here is only observable
by inducing a fault, so tests are the deliverable, not an optional extra. This matches the repo's
existing integration-test culture and [quickstart.md](./quickstart.md).

**Organization**: Grouped by user story. Phases are numbered in spec priority order; see
[Implementation Strategy](#implementation-strategy) for the **risk-based execution order** the plan
recommends, which differs.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1–US6 from spec.md
- Exact file paths included in every task

## Path Conventions

Web application, two roots: `backend/` (.NET 10) and `frontend/apps/web/src/app/` (Angular 21).
Backend tests in `backend/tests/JuggerHub.Api.IntegrationTests/`; frontend specs sit beside their
source; e2e in `frontend/apps/web-e2e/src/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Bring in the dependency and the configuration surface.

- [X] T001 Add `<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="10.8.0" />` to `backend/JuggerHub.Api.csproj`, keeping the alphabetical ordering of the existing `ItemGroup` and pinning the major per the constitution's dependency rule
- [X] T002 [P] Add the `Resilience:Outbound:Resend` block with documented defaults to `backend/appsettings.json`, matching the shape in `specs/028-network-resilience/contracts/resilience-config.md` §1
- [X] T003 [P] Add `Resilience__Outbound__Resend__*` sample entries to `.env.sample` and pass them through the `backend` service environment in `docker-compose.yml`, so local runs exercise the same configuration path as deployed (constitution Principle V)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared, opt-in resilience mechanism that FR-018 requires — defined once, inherited
by every outbound integration.

**⚠️ Scope of the block**: this phase blocks **US2, US3 and US5** only. **US1, US4 and US6 do not
depend on it** and may start immediately in parallel — the browser hop, the database hop and
observability share no code with the outbound HTTP pipeline.

- [X] T004 Create `ResilienceOptions` record in `backend/Common/ResilienceOptions.cs` with the eight fields and defaults from `specs/028-network-resilience/data-model.md` §1, following the existing `*Options.cs` convention in that folder
- [X] T005 Add options validation to `backend/Common/ResilienceOptions.cs`: all values positive, `BreakerFailureRatio` within `(0,1]`, `TotalTimeoutSeconds > AttemptTimeoutSeconds`; invalid or missing values fall back to the built-in default and log a startup warning — never to "unlimited" or a disabled limit (FR-030)
- [X] T006 Create `backend/Resilience/ResilienceExtensions.cs` exposing `AddJuggerHubResilience(this IHttpClientBuilder, IConfiguration, string name)` that binds `Resilience:Outbound:<name>`, calls `AddStandardResilienceHandler()` with those values, and names the pipeline `<name>` so telemetry attributes events to the right integration (FR-018, FR-019, FR-027); mirror the extension-method style of `backend/Security/RateLimitPolicies.cs`
- [X] T007 Add `backend/tests/JuggerHub.Api.IntegrationTests/Resilience/ResilienceOptionsTests.cs` covering FR-030: absent section → defaults; each invalid value → that field's default with a warning; assert no configuration produces an unbounded limit

**Checkpoint**: a typed client can opt in with one chained call. US2/US3/US5 unblocked.

---

## Phase 3: User Story 1 — A brief hiccup doesn't break the page (Priority: P1) 🎯 MVP

**Goal**: The browser hop gets a bounded time limit and silent retry of safe reads, plus a quiet
reassurance line when loading drags. Kills the infinite-spinner bug.

**Independent Test**: Pause the backend and confirm screens resolve to an actionable error rather
than spinning forever; restart it mid-browse and confirm reads recover with no error card and no
click.

### Tests for User Story 1

> Write these first and confirm they fail.

- [X] T008 [P] [US1] Spec in `frontend/apps/web/src/app/core/interceptors/retry.interceptor.spec.ts`: a `GET` failing with 503 then succeeding resolves without error, and attempts are bounded at the configured maximum (FR-002)
- [X] T009 [P] [US1] Spec in the same file: `POST`, `PUT`, `PATCH` and `DELETE` are issued exactly once on any failure — the single most important assertion in this story (FR-004)
- [X] T010 [P] [US1] Spec in the same file: 400, 403, 404, 409, 422 and **429** are never retried; 429 specifically because it is our own fail-closed limiter (FR-005, FR-031)
- [X] T011 [P] [US1] Spec in the same file: a request that never responds is abandoned at the per-attempt timeout and surfaces an error, and the original `HttpErrorResponse` reaches the caller unchanged after attempts are exhausted (FR-001, FR-009)
- [X] T012 [P] [US1] Spec in the same file: a 401 passes the retry layer untouched and triggers **exactly one** `/auth/refresh` — the regression guard for interceptor ordering (FR-007, FR-008)
- [X] T013 [P] [US1] Spec in `frontend/apps/web/src/app/shared/ui/loading/loading.component.spec.ts`: the default label shows immediately, the patient label replaces it only after the threshold, and the existing `role="status"`, alignment and custom-label behaviours are unchanged

### Implementation for User Story 1

- [X] T014 [US1] Create `frontend/apps/web/src/app/core/interceptors/retry.interceptor.ts`: per-attempt `timeout()` inside `retry({ count, delay })` with exponential backoff plus jitter, gated to `GET`/`HEAD`, the retryable-status list from `data-model.md` §2, and the existing `SKIP_REFRESH` URL exclusions; unsubscription cancels in-flight retries for free (FR-010)
- [X] T015 [US1] Register it in `frontend/apps/web/src/app/app.config.ts` as `withInterceptors([authInterceptor, retryInterceptor])` — **order is part of the contract**, retry must be inner; add a comment stating why, since reversing it silently reintroduces multiplied refresh cycles
- [X] T016 [US1] Add a **Loading, error & retry states** section to `DESIGN.md` covering the muted `body-sm` loading line, the patient-copy threshold, error-with-retry versus empty-state, and the voice rules — **this task must complete before T017** (FR-011b)
- [X] T017 [US1] Add the self-timed `patientLabel` input to `frontend/apps/web/src/app/shared/ui/loading/loading.component.ts` and `.html`: the component starts its own timer on init and swaps its label after the threshold, telling the interceptor nothing (research §7); default `'Still loading…'`, no layout shift, all 34 existing call sites keep working untouched
- [X] T018 [P] [US1] Replace `.withAutomaticReconnect()` in `frontend/apps/web/src/app/core/services/chat.service.ts` with a custom retry policy that backs off and retries indefinitely up to a capped interval, so the socket no longer dies permanently after ~42s (FR-012)
- [X] T019 [P] [US1] Apply the same reconnect policy in `frontend/apps/web/src/app/core/services/notification.service.ts`; confirm the existing `onreconnected` re-seed still runs on a late reconnect
- [X] T020 [US1] Copy `.specify/templates/ui-review-checklist-template.md` to `specs/028-network-resilience/checklists/ui-review.md` and verify every item against the diff; DESIGN.md wins on conflict (constitution Gate 7)
- [X] T021 [US1] Add an e2e spec in `frontend/apps/web-e2e/src/` that loads a data screen under a stalled backend route and asserts a bounded, actionable failure rather than an indefinite loading state

**Checkpoint**: US1 fully functional and independently demonstrable.

---

## Phase 4: User Story 2 — A transactional email is not lost to a provider blip (Priority: P1)

**Goal**: A transient provider failure no longer permanently loses a verification or invite email;
a permanent failure is loudly recorded instead of silently swallowed.

**Independent Test**: Stub the provider to fail transiently and confirm the email is still
delivered; stub a permanent rejection and confirm one attempt plus a complete log entry.

**Depends on**: Phase 2.

### Tests for User Story 2

- [X] T022 [P] [US2] Integration test in `backend/tests/JuggerHub.Api.IntegrationTests/Resilience/OutboundEmailResilienceTests.cs`: a provider returning 500 then 200 results in exactly one delivered email and no user-visible failure (FR-015)
- [X] T023 [P] [US2] Test in the same file: 401 and 422 (permanent) are attempted exactly once and never retried (FR-016)
- [X] T024 [P] [US2] Test in the same file: a provider that never responds is abandoned at the attempt timeout rather than holding the operation open (FR-014)
- [X] T025 [P] [US2] Test in the same file asserting the give-up log **contains** recipient, message kind and provider status code, and **does not contain** the response body, the API key, or any credential (FR-021, FR-028)

### Implementation for User Story 2

- [X] T026 [US2] Chain `.AddJuggerHubResilience(builder.Configuration, "Resend")` onto the existing `AddHttpClient<IEmailSender, ResendEmailSender>()` registration in `backend/Program.cs` (currently bare at line ~198); deliberately do **not** call `DisableForUnsafeHttpMethods()` — the email POST is retried on purpose (research §3)
- [X] T027 [US2] Add explicit give-up logging to `backend/Services/Email/ResendEmailSender.cs` at error level, naming recipient and message kind alongside the status code, extending — not replacing — its existing "status only, never the body" rule (FR-021)
- [X] T028 [US2] Set an explicit connect/send timeout on the MailKit client in `backend/Services/Email/SmtpEmailSender.cs` so the local sender cannot hang either; add a comment recording that retry and breaker deliberately do not apply to this local-only path (plan.md Complexity Tracking)
- [X] T029 [US2] Verify the enumeration-neutral flows (registration, forgotten-password) are unchanged when a send ultimately fails — the action's own outcome and response must not shift (FR-022, constitution Principle I)

**Checkpoint**: US1 and US2 both work independently.

---

## Phase 5: User Story 3 — A provider outage doesn't take the app down (Priority: P2)

**Goal**: Sustained provider failure stops being retried against, fails fast, and recovers on its
own — so the retries added in US2 cannot become the outage.

**Independent Test**: Hold the provider failing, confirm calls stop being attempted after the
threshold, then restore it and confirm normal service resumes without intervention.

**Depends on**: Phase 2. Best validated after US2.

### Tests for User Story 3

- [X] T030 [P] [US3] Test in `backend/tests/JuggerHub.Api.IntegrationTests/Resilience/CircuitBreakerTests.cs`: with the tuned threshold, sustained failures open the breaker and subsequent calls fail **without** contacting the provider (FR-017)
- [X] T031 [P] [US3] Test in the same file: after the break duration a single trial call is permitted, success closes the breaker, failure re-opens it (FR-017)
- [X] T032 [P] [US3] Test in the same file: total provider calls under sustained failure stay within the bounded factor rather than growing with retries (FR-026, SC-007)

### Implementation for User Story 3

- [X] T033 [US3] Set the breaker values in `backend/Common/ResilienceOptions.cs` defaults and `backend/appsettings.json`: `BreakerMinimumThroughput` ≈ **5**, not the package default of 100 — at JuggerHub's email volume the default window never fills and the breaker would never open (research §2). Add a comment carrying that reasoning so it is not "corrected" back later
- [X] T034 [US3] Confirm an open breaker does not block unrelated requests — user-facing actions that would use email must not hang on it, and the rest of the app stays responsive (US3 scenario 3)

**Checkpoint**: retry now has a stop condition, satisfying constitution VII's "retry without a stop condition is a hazard".

---

## Phase 6: User Story 4 — The database restarting is invisible (Priority: P2)

**Goal**: Transient database faults are retried instead of surfacing as errors.

**Independent Test**: Restart Postgres with traffic in flight and confirm requests complete.

**⚠️ Highest-risk phase in the feature.** Enabling retry makes every existing
`BeginTransactionAsync` throw until restructured. The ten call sites all guard capacity/roster
invariants, so a careless wrap can corrupt data rather than merely fail. These tasks are **not**
mechanical — read research §5 before starting.

**Depends on**: nothing in this feature. Independent of Phases 2–5.

### Tests for User Story 4

- [X] T035 [P] [US4] Test in `backend/tests/JuggerHub.Api.IntegrationTests/Resilience/DatabaseResilienceTests.cs`: a transient connection failure is retried and the request succeeds (FR-023)
- [X] T036 [P] [US4] Test in the same file: once the retry window is exhausted the request fails with the standard generic error and leaks no internal detail (FR-025, constitution Principle I)

### Implementation for User Story 4

- [X] T037 [US4] Enable `options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())` in the `AddDbContext<AppDbContext>` registration in `backend/Program.cs` (line ~49). Expect every task below to be **required** immediately after — the app will throw on transactional paths until they are done
- [X] T038 [P] [US4] Wrap the transaction in `backend/Services/Teams/TeamService.cs` (line ~352) in `Database.CreateExecutionStrategy().ExecuteAsync(...)`, moving all state mutation inside the delegate
- [X] T039 [US4] Wrap all three transactions in `backend/Services/Parties/PartyService.cs` (lines ~315, ~371, ~407) — one task because they share a file
- [X] T040 [P] [US4] Wrap the transaction in `backend/Services/Parties/PartyRosterService.cs` (line ~101)
- [X] T041 [P] [US4] Wrap the transaction in `backend/Services/Parties/PartyInvitationService.cs` (line ~324)
- [X] T042 [P] [US4] Wrap the transaction in `backend/Services/Marketplace/MarketRequestService.cs` (line ~315)
- [X] T043 [US4] Wrap both transactions in `backend/Services/Events/EventSignupService.cs` (lines ~97, ~225) — one task, shared file; note the row lock and occupancy re-read must stay inside the delegate so a replay recomputes from current state
- [X] T044 [P] [US4] Wrap the transaction in `backend/Services/Events/EventAdminService.cs` (line ~63)
- [X] T045 [US4] Review all ten sites together: confirm nothing is `Add`ed, mutated or staged **before** the delegate, and that each block is replay-safe. This is a deliberate second pass — a site can be individually correct-looking and still double-apply (FR-024)
- [X] T046 [US4] Verify the buffered-resultset consequence of `EnableRetryOnFailure` is harmless: confirm no unpaginated list query exists that would now be buffered into memory (constitution Principle III mandates pagination — verify, don't assume)
- [X] T047 [US4] Run the existing capacity and concurrency suites (`Parties`, `Events`, `Marketplace`) and confirm green, with **no** `InvalidOperationException: The configured execution strategy ... does not support user-initiated transactions` anywhere — that exception means a call site was missed

**Checkpoint**: deploys and database restarts stop producing user-visible errors.

---

## Phase 7: User Story 5 — The next provider costs nothing extra (Priority: P2)

**Goal**: Prove the mechanism is genuinely generic, not Resend-shaped.

**Independent Test**: A throwaway client inherits everything with one chained call and no
resilience code of its own.

**Depends on**: Phase 2.

- [X] T048 [US5] Temporarily register a scratch typed client in `backend/Program.cs` using only `.AddJuggerHubResilience(builder.Configuration, "Scratch")`, confirm it times out, retries and breaks identically to the email client, confirm a `Resilience:Outbound:Scratch` section changes its behaviour without touching shared code, then **remove it** — it is a validation device, not a deliverable
- [X] T049 [P] [US5] Reconcile `specs/028-network-resilience/contracts/resilience-config.md` against the shipped implementation so the opt-in contract, the key names and the "what a caller must not do" list describe reality rather than intent

**Checkpoint**: US5's independent test passes; FR-018 demonstrated rather than asserted.

---

## Phase 8: User Story 6 — Operators can see what resilience is doing (Priority: P3)

**Goal**: Retries, timeouts and breaker transitions are diagnosable from telemetry alone.

**Independent Test**: Induce each event class and find it in the logs/metrics with the affected
operation named.

**Depends on**: the paths it observes (Phases 3–6).

- [X] T050 [P] [US6] Verify the resilience package's built-in telemetry is emitted and that each event carries the pipeline name identifying the integration; add configuration if it is not on by default (FR-027)
- [X] T051 [P] [US6] Confirm breaker state transitions log at a severity that stands out, not at debug (FR-027)
- [X] T052 [US6] Final redaction review across every path added in this feature — no secrets, credentials, tokens, personal message content, response bodies or full request bodies in any resilience record (FR-028, constitution Principle I and Gate 8)

---

## Phase 9: Polish & Cross-Cutting Concerns

- [X] T053 Run the full `specs/028-network-resilience/quickstart.md` validation — every scenario induces a real fault; a green build alone does not satisfy this task
- [X] T054 Run the complete verification suite: `dotnet build backend/JuggerHub.slnx`, `dotnet test backend/tests/JuggerHub.Api.IntegrationTests`, `npm --prefix frontend run test`, `npm --prefix frontend run lint`, `npm --prefix frontend run build`, and the Playwright e2e run
- [X] T055 Self-check the diff against constitution **Quality Gate 8** and Principle VII, since this feature is that principle's first implementation and its reference case
- [X] T056 [P] Open a GitHub issue for the deferred **durable email delivery** follow-up (persist-before-send, background dispatcher, multi-replica claiming), referencing spec.md's Out of Scope section and the Q1 clarification

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: depends on Setup; blocks **US2, US3, US5 only**
- **US1 (Phase 3)**, **US4 (Phase 6)**: depend on nothing in this feature — can start immediately, in parallel with Phase 1/2
- **US2 (Phase 4)**, **US3 (Phase 5)**, **US5 (Phase 7)**: depend on Phase 2
- **US6 (Phase 8)**: observes Phases 3–6; do last
- **Polish (Phase 9)**: everything

### Story Dependencies

- **US1 (P1)**: independent. Internally ordered: T016 (DESIGN.md) **must** precede T017 (FR-011b)
- **US2 (P1)**: needs Phase 2
- **US3 (P2)**: needs Phase 2; only meaningfully testable once US2's retry exists
- **US4 (P2)**: fully independent — different hop, different code, no shared files
- **US5 (P2)**: needs Phase 2; validates it
- **US6 (P3)**: needs the paths it observes

### Parallel Opportunities

- T002, T003 in Setup
- All six US1 test tasks (T008–T013) — different assertions, two files
- T018 and T019 (chat vs. notification service — different files)
- All four US2 test tasks (T022–T025)
- **US4's call-site tasks T038, T040, T041, T042, T044** — five different files, genuinely parallel. T039 and T043 are *not* split further because each covers multiple transactions in one file
- Whole stories: US1 and US4 can proceed alongside the Phase 2 → US2 → US3 chain with no file contention

## Parallel Example: User Story 1 tests

```bash
Task: "GET retried on 503, bounded — retry.interceptor.spec.ts"
Task: "Mutations issued exactly once — retry.interceptor.spec.ts"
Task: "4xx and 429 never retried — retry.interceptor.spec.ts"
Task: "Timeout surfaces bounded error — retry.interceptor.spec.ts"
Task: "401 → exactly one refresh — retry.interceptor.spec.ts"
Task: "Patient label after threshold only — loading.component.spec.ts"
```

## Parallel Example: User Story 4 call sites

```bash
Task: "Execution strategy — TeamService.cs"
Task: "Execution strategy — PartyRosterService.cs"
Task: "Execution strategy — PartyInvitationService.cs"
Task: "Execution strategy — MarketRequestService.cs"
Task: "Execution strategy — EventAdminService.cs"
```

---

## Implementation Strategy

### Recommended execution order (risk-based — differs from phase numbering)

Phases are numbered by spec priority, as the template requires. **plan.md sequences the work
differently**, and that ordering is the one to follow:

```text
Phase 1 → Phase 2 → US2 → US3 → US5   (backend outbound: highest value, lowest risk, self-contained)
                  → US1                (frontend: independent, can run alongside)
                  → US4                (database: LAST — largest blast radius)
                  → US6 → Polish
```

Stories are independent, so this is a recommendation rather than a dependency — but US4 last is
deliberate. It touches ten transactional paths guarding capacity and roster invariants, and it
benefits enormously from everything else already being green, so a failure is unambiguously
attributable.

### MVP scope

**US1 alone is a shippable MVP.** It needs no backend change at all, and it fixes the failure people
actually hit — the infinite spinner and the error card on a one-second blip. Stop after Phase 3,
validate, ship.

**US2 is the strongest second increment**: it closes the highest-consequence failure in the product,
a permanently lost verification email.

### Incremental delivery

1. Phase 1 + 2 → mechanism exists
2. + US1 → **MVP**, browser hop resilient, demo-able
3. + US2 → emails survive blips
4. + US3 → retry can't amplify an outage
5. + US5 → mechanism proven generic
6. + US4 → deploys and database restarts invisible
7. + US6 + Polish → diagnosable, verified, gate-checked

---

## Notes

- **T009 is the one test not to skip.** "Mutations are never retried" is the assertion standing
  between this feature and duplicated user actions.
- **T016 before T017** is a hard ordering, not a preference — FR-011b makes writing the DESIGN.md
  entry a precondition for building the treatment.
- **T033's value is deliberately unlike the library default**, and the comment explaining why is
  part of the task. Without it, a future reader "fixes" it back to 100 and the breaker silently
  stops working.
- No entity, migration, or endpoint tasks appear anywhere in this list — that is correct, not an
  omission (FR-021a).
- Commit per task or logical group; stop at any checkpoint to validate a story independently.
