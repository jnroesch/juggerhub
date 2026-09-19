# Tasks: Push Notifications

**Input**: Design documents from `specs/055-push-notifications/` — GH #308

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/push-api.md](contracts/push-api.md),
[quickstart.md](quickstart.md)

**Tests**: Included. The constitution's verification gate requires them, and three of the four
preference combinations in US2 are new behaviour that would regress invisibly without one.

**Organization**: by user story. US1 (enable a device) and US2 (delivery) are both P1 but **US2
cannot be observed without US1**, since there is nothing to deliver to. US3 (the column) and US4
(turning off) are independently testable once the foundation is in place.

**Three traps, repeated here because they are easy to walk into**:

1. `NotificationService.cs:48-51` returns early when the **in-app** preference is off. A dispatch
   added after it is silently killed by an unrelated toggle. T027 is the restructure; T032 is the
   test that proves it.
2. `NotificationService.PushAsync` already means **SignalR realtime**. Nothing new may be named
   `Push…` unqualified.
3. `PushServiceClient.AutoRetryAfter` defaults to `true` with an uncapped 429 loop. T006 sets it to
   `false`; leaving it stacks retries inside our resilience handler.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US4 from spec.md
- Paths are relative to the repository root

---

## Phase 1: Setup

**Purpose**: the dependency, the configuration path across every environment, and the wiring that
Principle VII requires. Nothing here is observable yet.

- [X] T001 Add `Lib.Net.Http.WebPush` version `3.3.1` to `backend/JuggerHub.Api.csproj`, pinning the
      major per the constitution's dependency rules. Confirm the transitive set by running
      `dotnet list package --include-transitive` **before and after** the change and diffing:
      the only addition must be `Lib.Net.Http.EncryptedContentEncoding`. Reading the list once is
      misleading — `BouncyCastle.Cryptography` appears in it either way, via `MailKit → MimeKit`.
      Record the result in the PR.
- [X] T002 [P] Create `backend/Common/WebPushOptions.cs` with `SectionName = "WebPush"`,
      `ResilienceName = "WebPush"`, and `Subject`, `PublicKey`, `PrivateKey` string properties plus
      an `IsConfigured` check. Add the matching `"WebPush"` section with empty values and a
      `"Resilience": { "Outbound": { "WebPush": { … } } }` block to `backend/appsettings.json`,
      spelling out all eight resilience keys the way `Resend` and `Tugeny` already do. Leave
      `BreakerMinimumThroughput` at 5 with a comment that this integration genuinely reaches it,
      unlike email.
- [X] T003 [P] Write `scripts/New-VapidKeyPair.ps1`: generate a P-256 key pair with
      `[System.Security.Cryptography.ECDsa]::Create()`, export the public key as base64url of the
      uncompressed point (`0x04 || X || Y`) and the private key as base64url of `D`, and print the
      exact `.env` lines to paste. Header comment: the library ships no generator, the encoding is
      specific, and each environment gets its own pair so a Dev subscription is never deliverable
      from Prod.
- [X] T004 [P] Add the configuration to the local path: `WEBPUSH_SUBJECT`, `WEBPUSH_PUBLIC_KEY`,
      `WEBPUSH_PRIVATE_KEY` documented in `.env.sample` (the private key commented out, following
      the `EMAIL_RESEND_API_KEY` precedent), and the `WebPush__*` plus four
      `Resilience__Outbound__WebPush__*` variables in the backend `environment:` block of
      `docker-compose.yml` using the `${VAR:-}` form.
- [X] T005 [P] Add the deployed path: a `sensitive` + validated `webpush_private_key` variable in
      `infra/variables.tf`, passthrough in `infra/main.tf` and `infra/modules/app/variables.tf`,
      `"WebPush__PrivateKey"` in the `app-secrets` Secret and `"WebPush__Subject"` +
      `"WebPush__PublicKey"` in the `app-config` ConfigMap in `infra/modules/app/main.tf`, and
      `TF_VAR_webpush_private_key: ${{ secrets.WEBPUSH_PRIVATE_KEY }}` in
      `.github/workflows/deploy.yml`. Update the outbound-host comment in
      `infra/modules/app/network-policy.tf` to name the push services.
- [X] T006 Wire it in `backend/Program.cs`: bind `WebPushOptions`, **fail fast at startup** when the
      section is missing or malformed with no disable switch (mirroring the Redis and chat-encryption
      guards), and register the typed client
      `AddHttpClient<PushServiceClient>((http, sp) => new PushServiceClient(http) { DefaultAuthentication = …, AutoRetryAfter = false })
      .AddJuggerHubResilience(builder.Configuration, WebPushOptions.ResilienceName)`.
      **`AutoRetryAfter = false` gets its own comment** naming `PushServiceClient.cs:362-367` and the
      035 Azure-Blob precedent: the default retries a 429 without a cap, inside our handler.

---

## Phase 2: Foundational (blocking prerequisites)

**⚠ No user story can start until this phase is done.**

- [X] T007 Create `backend/Entities/PushSubscription.cs` per [data-model.md](data-model.md):
      `BaseEntity` with `UserId`, `Endpoint`, `P256dh`, `Auth`, `DeviceLabel`, `LastSuccessAt` and
      the `User` navigation. XML doc must state that **this IS owned data and belongs in
      `EraseOwnedDataAsync`**, explicitly contrasting it with `TermsAcceptance`, whose entity
      carries the opposite warning.
- [X] T008 Add the `DbSet` and configuration to `backend/Data/AppDbContext.cs` beside the
      `RefreshToken` block it copies: max lengths, **unique index on `Endpoint`**, index on
      `UserId`, index on `LastSuccessAt`, `DeleteBehavior.Cascade` to `User`. Comment the unique
      index as load-bearing: it makes a device that changes hands **move** accounts instead of
      existing twice, which is the shared-device case in FR-020.
- [X] T009 Generate the migration for the new table only
      (`dotnet ef migrations add AddPushSubscriptions`), then **read the generated file** and
      confirm it creates one table and three indexes and touches nothing else.
- [X] T010 [P] Add `Push = 2` to `NotificationChannel` in `backend/Entities/NotificationEnums.cs`
      and **delete the "Push is out of scope" sentence** from its XML doc — that was feature 011's
      deferral note and this feature is the deferral being taken up.
- [X] T011 [P] Add `bool Push` to `PreferenceChannelsDto` in
      `backend/Dtos/Notifications/NotificationPreferenceDtos.cs`, and emit it from
      `GetMatrixAsync` in `backend/Services/Notifications/NotificationPreferenceService.cs` with a
      third `Effective(category, NotificationChannel.Push)` call.
- [X] T012 [P] Mirror the channel on the client in
      `frontend/apps/web/src/app/core/models/notification-preferences.models.ts`: add `'Push'` to
      `NotificationChannelId`, `'push'` to `ChannelKey`, `push: boolean` to `PreferenceChannels`,
      and the mapping in `channelIdOf`.
- [X] T013 [P] Define the seams: `backend/Services/Notifications/Push/IPushDispatcher.cs` with
      `DispatchAsync(IReadOnlyCollection<Guid> recipientUserIds, PushContent content, CancellationToken ct)`,
      and `backend/Services/Notifications/Push/IPushSubscriptionService.cs` with register, remove and
      touch. `IPushDispatcher`'s XML doc must say it is called **by** `NotificationService` and not
      implemented inside it, because `ChatMessageService` will call it directly in #309 without
      writing a `Notification` row (019 FR-051a).
- [X] T014 Extend `backend/tests/JuggerHub.Api.IntegrationTests/JuggerHubApiFactory.cs`: add the
      `WebPush:*` and `Resilience:Outbound:WebPush:*` in-memory keys (a deliberately unreachable
      subject/endpoint host, following the `Tugeny:BaseUrl = "http://tugeny.invalid/"` precedent),
      and register a recording fake `IPushDispatcher` in `ConfigureTestServices` via
      `RemoveAll<IPushDispatcher>()` + `AddSingleton`, mirroring how `IEmailSender` is faked.

**Checkpoint**: the table exists, the third channel exists end to end as data, and tests can observe
dispatch without a network. All four stories can now proceed.

---

## Phase 3: User Story 1 — Turning on notifications for this device (Priority: P1) 🎯 MVP

**Goal**: a player can enable the browser they are in, and the settings page tells the truth about
every state it can be in, including the iPhone install case deferred from 054.

**Independent Test**: enable in a supporting browser and confirm a row exists and the page says so
after a reload. On an iPhone in Safari, confirm the page shows install instructions and no button.

- [X] T015 [US1] Implement `backend/Services/Notifications/Push/PushSubscriptionService.cs`:
      register (idempotent on `Endpoint`, **reassigning** an endpoint held by another account to the
      caller), remove (scoped to the caller, idempotent, never revealing whether a row existed), and
      touch `LastSuccessAt` through the change tracker so `AuditFieldsInterceptor` runs.
- [X] T016 [US1] Create `backend/Dtos/Notifications/PushDtos.cs` with the register request
      (`Endpoint`, `P256dh`, `Auth`, `DeviceLabel`), the remove request (`Endpoint`) and the
      public-key response, with `[Required]` validation and an absolute-`https` check on the
      endpoint per [contracts/push-api.md](contracts/push-api.md).
- [X] T017 [US1] Create `backend/Controllers/PushSubscriptionsController.cs` — thin, authenticated:
      `GET /push/public-key`, `POST /push/subscriptions` (204), `DELETE /push/subscriptions` (204).
      The owner is **always** the caller's id from the token, never a body field. Apply the existing
      per-user rate-limit policy.
- [X] T018 [P] [US1] Write `backend/tests/JuggerHub.Api.IntegrationTests/Push/PushSubscriptionTests.cs`:
      register creates one row; registering the same endpoint twice is idempotent; registering an
      endpoint held by another account **moves** it; remove is scoped to the caller and idempotent;
      all three endpoints reject an anonymous caller; a malformed endpoint is a 400.
- [X] T019 [P] [US1] Create `frontend/apps/web/src/app/core/services/push-device.service.ts`
      exposing one signal over the six states `unsupported | needs-install | blocked | off |
      enabling | on`, plus `enable()` and `disable()`. Detect iOS by **display mode, not user-agent
      sniffing** (`PushManager` is absent in an iOS tab and present in the installed app), and derive
      a readable device label from the browser. Permission is requested **only** inside `enable()`,
      from the player's press.
- [X] T020 [P] [US1] Create
      `frontend/apps/web/src/app/features/settings/notifications/push-device-section.component.{ts,html,css}`
      rendering one sentence and the right control per state. The `blocked` state explains that only
      the browser can undo it and shows **no** button. The `needs-install` state carries the
      Add to Home Screen steps — this is 054's deferred install affordance.
- [X] T021 [US1] Add the device section to
      `frontend/apps/web/src/app/features/settings/notifications/notification-settings.component.html`
      above the matrix, and inject the service in the component's `.ts`.
- [X] T022 [US1] Add the device-section copy to **all three** catalogues at once
      (`frontend/apps/web/public/i18n/{en,de,es}.json`) under `settings.notifications.push.*`:
      one string per state, the iOS steps, and the enable/disable labels. German uses `–`, never
      `—`. `catalog-parity.spec.ts` goes red if a locale is missed.
- [X] T023 [P] [US1] Write
      `frontend/apps/web/src/app/core/services/push-device.service.spec.ts` and a spec for the
      section component: each of the six states renders its own sentence; `blocked` renders no
      button; permission is never requested without a press.

**Checkpoint**: a device can be enabled and the page is honest in every state. Nothing is delivered
yet.

---

## Phase 4: User Story 2 — Being told something happened (Priority: P1)

**Goal**: the nine producers reach enabled devices, in the recipient's language, without the in-app
toggle being able to silence them.

**Independent Test**: with a subscribed device and no tab open, trigger each producer and confirm a
notification naming its subject arrives and opens the right page.

- [ ] T024 [P] [US2] Create `backend/Services/Notifications/Push/PushLocalizer.cs` following
      `EmailLocalizer`'s shape: per-culture dictionaries, **positional** placeholders (word order
      differs across en/de/es), and **`TryGetValue` with an English fallback per key, never a bare
      indexer** — cite `NotificationPreferenceService.cs:100-106`, where the bare indexer once took
      the settings page down for a whole language.
- [ ] T025 [P] [US2] Create `backend/Services/Notifications/Push/PushContentComposer.cs` mapping
      each of the nine `NotificationType` producer payloads to `{ title, body, url, tag }` per
      [contracts/push-api.md](contracts/push-api.md). `url` is **always app-relative**, never
      absolute and never built from user input. `tag` is the producer's dedupe key or
      `type:subjectId`.
- [ ] T026 [US2] Implement `backend/Services/Notifications/Push/PushDispatcher.cs`: load recipients'
      subscriptions in one query; fan out with **bounded concurrency** (`Parallel.ForEachAsync`,
      small degree) because a team news post is members × devices; set the push message's `Topic`
      to the content `tag` and a sensible `TimeToLive`; touch `LastSuccessAt` on success; on
      `PushServiceClientException` with `404`/`410` **delete the row and do not retry**; on any
      other failure log **status code and subscription id only — never `Body`, never the endpoint**
      (Principle VII). Comment the `429` distinction at the catch: a provider throttling us is
      retried by the shared pipeline honouring `Retry-After`, while our own limiter's `429` is never
      retried against.
- [ ] T027 [US2] **The restructure.** In `backend/Services/Notifications/NotificationService.cs`,
      change `CreateAsync` and `CreateManyAsync` so the in-app and push preferences are read
      **independently** before either acts: return early only when **both** are off; write the row
      and do the realtime `PushAsync` only when in-app is on; dispatch push last, after realtime, so
      a slow push service cannot delay the badge; and when the duplicate-key catch fires, **do not
      dispatch either** — that is where once-only comes from in the common case. In
      `CreateManyAsync` resolve the two recipient sets separately; the push set is **not** a subset
      of the in-app set. Leave a comment naming the removed early return as the reason.
- [ ] T028 [US2] Register `IPushDispatcher`/`PushDispatcher`, `IPushSubscriptionService`,
      `PushLocalizer` and `PushContentComposer` in `backend/Program.cs` beside the other
      notification services.
- [ ] T029 [P] [US2] Add the `push` and `notificationclick` handlers to
      `frontend/apps/web/public/sw.js`: show the notification from the JSON body using its `tag`;
      on click, focus an existing client at that path or open it. Keep the file's header comment
      truthful by updating it — still no `fetch` handler, no `caches`, no `importScripts`.
- [ ] T030 [US2] **Extend, do not relax**, the 054 guard in
      `frontend/apps/web/src/app/core/pwa/pwa-shell.spec.ts`: keep every existing assertion and add
      that `sw.js` now contains `addEventListener('push'` and `addEventListener('notificationclick'`.
      The no-offline assertions stay exactly as they are.
- [ ] T031 [P] [US2] Write `backend/tests/JuggerHub.Api.IntegrationTests/Push/PushDispatchTests.cs`:
      a `404` and a `410` each delete the row and cause no retry; a `5xx` is retried by the pipeline;
      a success touches `LastSuccessAt`; a recipient with no subscription causes **no outbound call
      at all** (SC-008); the response body never appears in the logs.
- [ ] T032 [US2] Write
      `backend/tests/JuggerHub.Api.IntegrationTests/Push/NotificationChannelIndependenceTests.cs` —
      **the regression guard for the whole feature**. The four-way matrix for both `CreateAsync` and
      `CreateManyAsync`: in-app on + push on (row and dispatch); in-app on + push off (row, no
      dispatch); **in-app off + push on (no row, dispatch happens)**; both off (neither). Then:
      duplicate `dedupeKey` produces one row and one dispatch; the language comes from the
      recipient, not the actor; and a dispatcher that throws leaves the producing action succeeding
      with nothing surfaced (FR-013).

**Checkpoint**: notifications arrive. US1 + US2 together are a shippable increment.

---

## Phase 5: User Story 3 — Choosing what reaches your devices (Priority: P2)

**Goal**: the matrix gains a working third column whose effect is real and account-wide.

**Independent Test**: turn a category's push off, trigger it, confirm nothing arrives while the
in-app entry still does.

- [ ] T033 [US3] Add the fourth column to
      `frontend/apps/web/src/app/features/settings/notifications/notification-settings.component.html`:
      `md:grid-cols-[1fr_5rem_5rem]` becomes `md:grid-cols-[1fr_5rem_5rem_5rem]` on **both** the
      header row and the list item, plus a Push toggle block copying the Email one, with
      `data-testid="toggle-{category}-push"` and the same `role="switch"` and `aria-label` shape.
      Below `md` this renders as a fourth labelled row in the card, not a fourth column.
- [ ] T034 [P] [US3] Add `settings.notifications.push` as the column heading to all three
      catalogues in `frontend/apps/web/public/i18n/{en,de,es}.json`.
- [ ] T035 [P] [US3] Extend
      `frontend/apps/web/src/app/features/settings/notifications/notification-settings.component.spec.ts`:
      three toggles render per category; pressing the push toggle calls `setCell` with the `Push`
      channel; the always-on group gains no push toggle (FR-017).
- [ ] T036 [P] [US3] Extend the preference integration tests in
      `backend/tests/JuggerHub.Api.IntegrationTests/` to cover `PUT /notification-preferences/{category}/Push`
      round-tripping through the existing endpoint, and the matrix returning `push` for every
      category with the unset default of `true`.

**Checkpoint**: the column works and is account-wide.

---

## Phase 6: User Story 4 — Turning it off, and devices that stop working (Priority: P2)

**Goal**: off means off, sign-out means off on that device, and nothing undeliverable accumulates.

**Independent Test**: turn off, sign out, and let a dead subscription be pruned; confirm delivery
stops in each case while other devices continue.

- [ ] T037 [US4] Add the off path to
      `frontend/apps/web/src/app/features/settings/notifications/push-device-section.component.{ts,html}`:
      the `on` state offers turning this device off, which unsubscribes in the browser **and** calls
      `DELETE /push/subscriptions`.
- [ ] T038 [US4] Hook sign-out in
      `frontend/apps/web/src/app/core/services/auth.service.ts`: in `logout()`, unsubscribe locally
      and best-effort `DELETE` **before** the logout POST, beside `drafts.clearAll()` and
      `browseReturns.clear()`, extending that method's existing comment about what stops a shared
      device handing the next person the previous person's data. In `clearSession()` only the local
      unsubscribe is possible — there is no session left to call with — so note that the row is
      pruned later by its next `404`/`410` or by the sweep. A failure here must never block sign-out.
- [ ] T039 [P] [US4] Create `backend/Services/Retention/StalePushSubscriptionSweep.cs` implementing
      `IRetentionSweep` with `Name => "stale-push-subscriptions"`, deleting by `LastSuccessAt` older
      than a configured grace period via `ExecuteDeleteAsync`; add the grace period to
      `backend/Common/RetentionOptions.cs` and `backend/appsettings.json`; register it with one
      `AddScoped<IRetentionSweep, …>` line in `backend/Program.cs` beside the refresh-token sweep.
- [ ] T040 [P] [US4] Add one `ExecuteDeleteAsync` for `PushSubscriptions` to
      `EraseOwnedDataAsync` in `backend/Services/Account/AccountDeletionService.cs`, in the
      "records that exist solely to serve this member" group beside `Notifications` and
      `NotificationPreferences`.
- [ ] T041 [P] [US4] Write
      `backend/tests/JuggerHub.Api.IntegrationTests/Push/PushSubscriptionLifecycleTests.cs`: removal
      stops delivery to that device and leaves the player's other device receiving; the sweep deletes
      only rows past the grace period; **account deletion leaves no subscription row** (SC-009); the
      hosted sweep is registered, mirroring `RefreshTokenRetentionTests`.
- [ ] T042 [P] [US4] Extend
      `frontend/apps/web/src/app/core/services/auth.service.spec.ts`: `logout()` attempts the
      unsubscribe and the DELETE before the logout call, and still completes when the unsubscribe
      throws.

**Checkpoint**: all four stories work independently.

---

## Phase 7: Polish and cross-cutting

- [ ] T043 Add the disclosure to `frontend/apps/web/public/i18n/legal/{de,en,es}.json`, German
      authoritative: the delivery address stored per enabled device goes in the storage section
      beside 054's worker paragraph, and the push service goes in as a recipient. **It must NOT be
      appended to the processors list** — that section promises every provider works "under a
      contract that only lets them handle data on our instructions", and the push service is chosen
      by the player's browser and is under no contract with us. Describe the kind of recipient and
      the fact that the browser determines it. Run
      `npx nx test web --testPathPatterns="legal-catalog|catalog-parity"`.
- [ ] T044 Instantiate `specs/055-push-notifications/checklists/ui-review.md` from
      `.specify/templates/ui-review-checklist-template.md` and verify each item against the diff.
      The binding cases are the **desktop matrix at the `md` breakpoint in German with four
      columns** and the **mobile card's fourth row**, plus the device section in each of its six
      states. DESIGN.md wins on any conflict.
- [ ] T045 Full verification per [quickstart.md](quickstart.md) §7:
      `dotnet test backend/tests/JuggerHub.Api.IntegrationTests`, then
      `cd frontend; npx nx lint web; npx nx test web; npx nx build web --configuration=production`,
      then `docker compose -f docker-compose.yml -f docker-compose.test.yml run --rm --build playwright`.
      Confirm the migration applies cleanly against a fresh database and that
      `git diff --stat main -- infra` shows only the secret passthrough.
- [ ] T046 Manual end-to-end and failure behaviour per [quickstart.md](quickstart.md) §4 and §5,
      including the step that would regress silently: **in-app off, push on, notification still
      arrives**. Confirm a failing push service leaves the producing action succeeding and logs no
      response body.
- [ ] T047 Device walk per [quickstart.md](quickstart.md) §6 on Android and iPhone, screenshots to
      the PR: lock-screen delivery, tap-through to the right page, and the iPhone install
      instructions shown in Safari before installation.
- [ ] T048 Commit in logical groups with `#308` in each message, open the PR with `Closes #308`, the
      device screenshots, the recorded residuals from [plan.md](plan.md), and a note on #309 that its
      `IPushDispatcher` seam now exists and that chat must call it **without** writing a
      `Notification` row.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** → **Foundational (Phase 2)** → everything else.
- Within Setup: T001 first (the package), then T002–T005 in parallel, then T006 (needs both).
- Within Foundational: T007 → T008 → T009 (the table); T010–T013 are parallel with each other and
  with the table work; T014 needs T013.
- **US1 (Phase 3)** needs Foundational only.
- **US2 (Phase 4)** needs Foundational, and **needs US1 to be observable** — there is nothing to
  deliver to without a subscription. The code can be written in parallel; the verification cannot.
- **US3 (Phase 5)** needs Foundational for its backend half (already done in T010–T012) and US2 for
  its effect to be visible.
- **US4 (Phase 6)** needs US1 (a device to turn off) and US2 (delivery to stop).
- **Phase 7** needs everything, except T043 (the policy), which can start any time.

### Within US1

T015 → T016 → T017 → T018 (backend chain); T019 ∥ T020 → T021 → T022; T023 after T019/T020.

### Within US2

T024 ∥ T025 → T026 → T027 → T028; T029 → T030; T031 after T026; T032 after T027.

### Parallel opportunities

- Setup: **T002, T003, T004, T005** together after T001.
- Foundational: **T010, T011, T012, T013** together, alongside the T007→T009 table chain.
- US1: the backend chain (T015–T018) and the frontend chain (T019–T023) are independent.
- US2: **T024 and T025** together; the worker work (T029, T030) is independent of the backend.
- US4: **T039, T040, T041, T042** are four different files.

---

## Implementation Strategy

### MVP (US1 + US2)

1. Phase 1 and Phase 2 in full.
2. US1: a device can be enabled and every state is honest.
3. US2: the nine producers reach it.
4. **Stop and validate** the four-way preference matrix (T032) before going further. If only one
   test in this feature is trusted, it is that one.

### Incremental delivery

- Add US3: players can narrow what reaches their phone.
- Add US4: off, sign-out and pruning.
- Phase 7: disclosure, UI review, verification, device walk, PR.

### Notes

- Principle VII is engaged. Any hand-rolled retry, per-client `HttpClient.Timeout`, or second
  resilience handler added in this diff is review-rejectable.
- Gate 7 is engaged. Screenshots are mandatory, at 375px and desktop, in German.
- No task touches `backend/Services/Chat/` — chat push is #309.
