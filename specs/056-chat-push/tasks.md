# Tasks: Chat Push Notifications

**Input**: Design documents from `specs/056-chat-push/` — GH #309

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/chat-push-api.md](contracts/chat-push-api.md),
[quickstart.md](quickstart.md)

**Tests**: Included. The constitution's verification gate requires them, and the eligibility matrix
(who does *not* get a notification) is the entire safety surface of this feature — every clause of
it fails silently and invisibly, because the symptom of a missing filter is a notification someone
was not supposed to receive, seen only by them.

**Organization**: by user story. US1 (a missed message reaches a device) and US2 (mute keeps its
promise) are both P1 and ship together — US2 is a regression guard on US1, not a later increment.
US3 (the off switch) is P2 but is **release-blocking**, see below.

---

## ⚠ Read before starting

Five findings from the code, each of which is easy to walk into and three of which fail silently.

1. **Hide does not suppress a chat push, though #309 says it does.**
   `ChatMessageService.ReturnToArchiversInboxesAsync` clears `IsHidden` for every member on every
   member-written send (048 FR-007), so the flag is already `false` when the pass runs 30 seconds
   later. **Mute is the only lever.** T040 is the load-bearing test; T041 is a *race* test and must
   archive during the delay window, not before it.
2. **The preference check is the caller's job.** `PushDispatcher.DispatchAsync` filters nothing;
   `NotificationService` reads the preference itself before calling the fan-out. Forget T049 and the
   off switch does nothing while every happy-path test still passes.
3. **Mark every message the pass selects**, including the ones it sends nothing for (T031). The
   partial index is `WHERE "PushConsideredAt" IS NULL`; a selected-but-unmarked row stays in it for
   ever and the index stops being small enough to query every ten seconds.
4. **`IChatMessageCipher.TryUnprotect` throws on a zero-length array** — it returns `false` only for
   a *corrupt* envelope. A `Member` row with a live sender and an empty `BodyCipher` is an
   attachment-only message (049), not corruption. Branch on `Length == 0` **first** (T026).
5. **One published sentence becomes false the moment this ships** (T055). `legal-catalog.spec.ts`
   compares key *sets* and cannot see a changed value, so nothing goes red if it is skipped.

### Release gate

**US1 and US2 must not be enabled in any environment where real members can receive notifications
until US3 (T044–T052) and the legal phase (T055–T057) have landed.** Shipping delivery first would
send members a notification they have no way to refuse, under a privacy policy that says the
content it carries is not in it. `ChatPush:Enabled` defaults to `true`, so this is a sequencing
requirement on the branch, not a runtime switch anyone has to remember to flip.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US3 from [spec.md](spec.md)
- Paths are relative to the repository root

---

## Phase 1: Setup

**Purpose**: the configuration section, in every environment, with safe defaults. Nothing
observable yet.

- [X] T001 Create `backend/Common/ChatPushOptions.cs` with `SectionName = "ChatPush"` and the seven
      properties from [contracts/chat-push-api.md](contracts/chat-push-api.md#configuration):
      `Enabled` (true), `QuietDelaySeconds` (30), `PollIntervalSeconds` (10),
      `MaxMessageAgeMinutes` (60), `MaxMessagesPerPass` (500), `PassTimeoutMinutes` (5),
      `PreviewLength` (120). Follow `backend/Common/RetentionOptions.cs` for shape and for the
      habit of explaining *why* a number is what it is in XML docs — `QuietDelaySeconds` is an
      owner decision (spec Clarifications) and `MaxMessagesPerPass` exists because Principle VII
      forbids unbounded work, not as a tuning knob.
- [X] T002 Add the `ChatPush` section to `backend/appsettings.json` with the defaults above, placed
      next to the existing `Retention` section (line 35).
- [X] T003 [P] Add `"ChatPush": { "QuietDelaySeconds": 5, "PollIntervalSeconds": 2 }` to
      `backend/appsettings.Development.json` so local verification does not require a 30-second
      wait per attempt.
- [X] T004 [P] Set `ChatPush:Enabled` to `false` in
      `backend/tests/JuggerHub.Api.IntegrationTests/JuggerHubApiFactory.cs`, mirroring how
      `Retention:Enabled` is handled there. **Load-bearing**: every scanner test drives one
      deterministic pass directly, and a live timer would race those assertions non-reproducibly.
- [X] T005 Register the options in `backend/Program.cs` beside the `RetentionOptions` line (269):
      `builder.Services.Configure<ChatPushOptions>(builder.Configuration.GetSection(ChatPushOptions.SectionName));`

**Checkpoint**: `dotnet build` passes; the app starts; nothing behaves differently.

---

## Phase 2: Foundational (blocking prerequisites)

**⚠ CRITICAL**: no user story work can begin until this phase is complete.

### The column

- [X] T006 Add `public DateTime? PushConsideredAt { get; set; }` to
      `backend/Entities/ChatMessage.cs`, with the XML doc from
      [data-model.md](data-model.md#d1-chatmessagepushconsideredat-new-column). The doc must say
      three things: that it records the question having been **asked, never the answer**; that the
      worker MUST mark every message it selects; and why `ModifiedDate` legitimately moves on a
      message nobody edited.
- [X] T007 Configure the **partial** index in `backend/Data/AppDbContext.cs` beside the other
      `ChatMessage` index configuration:
      `HasIndex(m => m.CreatedDate).HasFilter("\"PushConsideredAt\" IS NULL")` named
      `IX_ChatMessages_PushConsideredAt_Pending`. A full index would cover every message ever sent
      to serve a query that only ever wants the newest handful.
- [X] T008 Generate the migration `AddChatMessagePushConsideredAt` and **hand-add the backfill** to
      the generated `Up`: `migrationBuilder.Sql("UPDATE \"ChatMessages\" SET \"PushConsideredAt\" = now() WHERE \"PushConsideredAt\" IS NULL;")`,
      after the column is added and **before** the index is created. Comment it: without it every
      pre-existing row sits in the partial index for ever (it fails the max-age filter, so it is
      never selected and never marked), and the first pass after deployment would consider the
      whole message history.
- [X] T009 Apply the migration locally (`dotnet ef database update` against the local stack) and verify by inspection: the index exists, `indpred` is
      non-null (`\d+ "ChatMessages"` in psql, or query `pg_index`), and
      `SELECT count(*) FROM "ChatMessages" WHERE "PushConsideredAt" IS NULL` returns `0`.

### The category

- [X] T010 [P] Add `Chat = 4` to `NotificationCategory` in `backend/Entities/NotificationEnums.cs`,
      **appended, never inserted** — the values are stored as integers and renumbering would
      silently re-point every existing preference row. The XML doc states that this category has no
      `NotificationType` mapped to it and never will (019 FR-051), and that it governs Push only.
- [X] T011 [P] Add `NotificationCategories.Supports(category, channel)` to the same file, written
      permissively (`category != Chat || channel == Push`) so a future category defaults to the
      visible behaviour rather than to a silently missing toggle. Doc it as the one home for the
      rule, naming its three readers.
- [X] T012 [P] Add `NotificationCategoryTests.Chat_has_no_producer_type` to
      `backend/tests/JuggerHub.Api.IntegrationTests/Notifications/` asserting that no
      `NotificationType` maps to `NotificationCategory.Chat` through `NotificationCategories.For`.
      This pins FR-004 at the type level: the day someone adds a `ChatMessage` notification type,
      this test says no before the Alerts inbox does.

### The shared name

- [X] T013 Create `backend/Services/Chat/ChatDisplayName.cs` as an `internal static` class holding
      `For(...)` and `InquiryAdminLabel(...)`, moved **verbatim** from
      `ChatConversationService.DisplayName` (line ~1244). Add an optional
      `ChatNameFallbacks` record parameter defaulting to today's English literals
      (`"Group"`, `"Team chat"`, `"Party chat"`, `"Chat"`). Document that it is called, never
      copied — the 043 `LocationLabelFor` precedent — because the notification and the inbox must
      agree about what a conversation is called.
- [X] T014 Repoint `ChatConversationService`'s two call sites (lines ~503 and ~703) at
      `ChatDisplayName.For`, passing no fallbacks so the default set applies, and delete the private
      originals. **Behaviour must not change.**
- [X] T015 Run the existing chat suite **unedited** —
      `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~Chat"` —
      and confirm it is green. That is the proof the extraction changed nothing; do not adjust a
      test to make it pass.

**Checkpoint**: the schema, the category and the naming helper exist. Nothing delivers yet.

---

## Phase 3: User Story 1 — A message you missed reaches your phone (P1) 🎯 MVP

**Goal**: a member-written message that has gone unread for the quiet delay produces one
notification per conversation on each of the recipient's enabled devices, saying who wrote and what.

**Independent test**: [quickstart.md](quickstart.md) scenario 1 — two browsers, one message, wait,
a notification arrives naming the sender and showing the text, and the Alerts inbox stays empty.

### The strings

- [X] T016 [US1] Add the chat keys to
      `backend/Services/Notifications/Push/PushLocalizer.cs` in en/de/es: `chat.sentMessage`
      ("sent you a message" — the no-preview fallback), `chat.sentAttachment` ("sent an
      attachment"), `chat.groupBody` (`"{0}: {1}"` — sender, text), and the three conversation-name
      fallbacks (`chat.name.group`, `chat.name.team`, `chat.name.party`). Placeholders stay
      **positional** (word order differs across the three languages) and the lookup stays
      `TryGetValue` + English fallback — **never a bare indexer**, which 039 recorded taking the
      settings page down for one language.

### The composer

- [X] T017 [US1] Create `backend/Services/Chat/Push/IChatPushComposer.cs` — a pure seam taking the
      per-conversation naming inputs, the message (id, sender name, `BodyCipher`, whether it has
      attachments), the recipient's culture and whether the recipient is the inquiry requester, and
      returning a `PushContent`. No `AppDbContext`, no `IPushDispatcher`: it must be unit-testable
      without a database.
- [X] T018 [US1] Implement `backend/Services/Chat/Push/ChatPushComposer.cs` per
      [research.md R6](research.md#r6-composing-the-text): `Direct` → title is the sender, body is
      the text; every other kind → title is `ChatDisplayName.For(...)` with localized fallbacks,
      body is `"{sender}: {text}"`.
- [X] T019 [US1] In the same file, implement the URL as `/chat/{conversationId}` — app-relative,
      built from a `Guid`, verified against `frontend/apps/web/src/app/app.routes.ts` where the open
      conversation is a child of the `chat` shell — and the tag as `chat:{conversationId}`,
      **keyed on the conversation, not the message**, which is what makes several messages collapse
      into one notification across passes as well as within one.
- [X] T020 [US1] Implement truncation to `ChatPushOptions.PreviewLength` (120) with an ellipsis,
      cutting on a character boundary. Messages run to 2000 characters
      (`ChatConstants.MaxMessageLength`) and no lock screen shows that; sending it whole reproduces
      a long private message outside the platform for no benefit (FR-020).
- [X] T021 [US1] Implement the three degraded bodies in order: **`BodyCipher.Length == 0` first**
      → `chat.sentAttachment` (trap 4 — asking the cipher about an empty array throws
      `ArgumentException` by design); then `TryUnprotect` returning `false` → `chat.sentMessage`,
      naming sender and conversation with no preview (FR-021a, mirroring how 047 already shows a
      placeholder for one message rather than failing a conversation); then a missing sender profile
      → `Common.MemberPlaceholder.For(culture)`.
- [X] T022 [P] [US1] Write `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatPushComposerTests.cs`
      covering every branch of T018–T021: direct vs group title, the `"{sender}: {text}"` join,
      truncation at the bound, unreadable body, attachment-only, missing profile, the URL shape, the
      tag shape, and that a German recipient gets German fallbacks while the sender's language is
      irrelevant (FR-006).

### The pass

- [X] T023 [US1] Create `backend/Services/Chat/Push/IChatPushScanner.cs` exposing
      `Task<int> RunOnceAsync(CancellationToken ct)` returning the number of messages considered.
      **Separate from the background service on purpose**: every test drives one deterministic pass,
      the same reason `IRetentionSweep` is separable from `RetentionBackgroundService`.
- [X] T024 [US1] In `backend/Services/Chat/Push/ChatPushScanner.cs`, implement the **selection**:
      `ChatMessages` where `PushConsideredAt == null`, `Kind == Member`, `CreatedDate <= now - QuietDelaySeconds`,
      ordered by `Id`, `Take(MaxMessagesPerPass)`, `AsNoTracking()`, projected to only what is
      needed. **No lower age bound in the query** — the max-age rule is a dispatch filter (T031),
      not a selection filter, because a message that is never selected is never marked.
- [X] T025 [US1] Implement the **claim**: one `ExecuteUpdateAsync` over the selected ids filtered by
      `PushConsideredAt == null`, setting `PushConsideredAt` and `ModifiedDate` in the same
      statement (constitution III — `ExecuteUpdateAsync` bypasses the audit interceptor, the defect
      048 recorded). **If it affects zero rows, another replica owns this batch: return.**
- [X] T026 [US1] Implement **grouping**: group the claimed messages by conversation and take the
      **newest** per conversation as the one to notify about. Safe because `LastReadMessageId` is a
      monotonic UUIDv7 cursor, so "read the newest but not an older one" cannot occur. Comment that
      this is what keeps FR-022 true without a second dispatch per message.
- [X] T027 [US1] Implement **recipient resolution** via
      `ChatGuard.ResolveParticipantUserIdsAsync(conversationId, ct)` — never a second membership
      query. It already handles all six conversation kinds *and* the archived-snapshot case where
      the roster it would otherwise ask has been deleted.
- [X] T028 [US1] Implement the **base filters**: drop the sender (FR-008); drop anyone whose
      `ConversationParticipant.LastReadMessageId` is at or past the message id (FR-009); resolve
      join cut-offs in batch through `ChatGuard.ResolveJoinCutoffsAsync` and drop anyone who joined
      after the message (FR-014).
- [X] T029 [US1] Implement the **kind exclusions**: skip a message whose conversation `State` is
      `Archived` (FR-017) and skip `IsDeleted` messages (FR-016) — both still get marked. System
      lines never reach here because T024 filters `Kind == Member`, which is FR-015 satisfied by
      selection rather than by a branch; say so in a comment so a future widening of the selection
      does not quietly repeal it.
- [X] T030 [US1] Implement **composition and dispatch**: group the surviving recipients by
      `SupportedLanguages.ResolveOrDefault(u.PreferredLanguage)` (the **recipient's** language, the
      shape `PushFanOut` already uses), compose once per language, and call
      `IPushDispatcher.DispatchAsync` — **never `IPushFanOut`**, which takes a `NotificationType`
      and the in-app row's payload JSON, neither of which chat has. Wrap the whole per-conversation
      block in `try/catch` so one conversation's failure cannot end the pass (FR-003).
- [X] T031 [US1] Implement the **max-age rule**: a message older than `MaxMessageAgeMinutes` is
      marked considered and **not dispatched** (FR-025). Comment that the marking is unconditional
      and why — trap 3, the index invariant.
- [X] T032 [US1] Register the scanner in `backend/Program.cs` near line 273:
      `builder.Services.AddScoped<IChatPushScanner, ChatPushScanner>();` and the composer as scoped
      alongside it. `IPushDispatcher` is already a singleton
      (`WebPushServiceCollectionExtensions.cs:84`), so it is injectable directly.

### The loop

- [X] T033 [US1] Create `backend/Services/Hosted/ChatPushBackgroundService.cs` modelled on
      `backend/Services/Retention/RetentionBackgroundService.cs`: honour `Enabled`, a `PeriodicTimer`
      at `PollIntervalSeconds`, **a scope per pass** (never one held between passes — it would keep
      a pooled connection idle), a per-pass timeout from `PassTimeoutMinutes` linked to the stopping
      token (Principle VII: nothing waits forever, background loops included), a failure logged and
      the loop continued, and a clean exit on `OperationCanceledException` during shutdown.
- [X] T034 [US1] **Add no retry anywhere** in `backend/Services/Hosted/ChatPushBackgroundService.cs`
      or `backend/Services/Chat/Push/ChatPushScanner.cs`. A failed dispatch is dropped; the message
      stays marked. Record in the background service's remarks that this is deliberate: retrying
      would amplify an incident on the hottest table in the product to deliver a convenience, and
      the in-app equivalent is still waiting for the member either way (FR-003, Principle VII).
      Equally, add no `AddJuggerHubResilience` — 055's `WebPush` typed client already carries the
      pipeline and wrapping the seam again stacks handlers, which is review-rejectable.
- [X] T035 [US1] In `backend/Services/Hosted/ChatPushBackgroundService.cs` and
      `backend/Services/Chat/Push/ChatPushScanner.cs`, log counts and ids only — **never message
      text, a sender name or a conversation name**, at any level (FR-021c). This is stricter than
      the rest of the platform's logging and the comment should say so, next to the
      `PushDispatcher` precedent (`backend/Services/Notifications/Push/PushDispatcher.cs:110-117`)
      of logging a status code and a subscription id and explicitly not the body or the endpoint.
- [X] T036 [US1] Register it in `backend/Program.cs` beside the retention host (line 274):
      `builder.Services.AddHostedService<ChatPushBackgroundService>();`

### Tests

- [X] T037 [P] [US1] In `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatPushScannerTests.cs`: an unread direct message older than the delay produces
      exactly one dispatch to the recipient, with the sender as title and the text as body; a
      message *younger* than the delay produces none and is **not** marked.
- [X] T038 [P] [US1] In `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatPushScannerTests.cs`: running the pass twice over the same message dispatches
      **once** (FR-024); four messages in one conversation produce **one** dispatch tagged
      `chat:{conversationId}` (FR-022); messages in two conversations produce two dispatches with
      different tags (FR-023).
- [X] T039 [P] [US1] In `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatPushScannerTests.cs`: **no `Notification` row exists** in the database after
      any of it (FR-004 / 019 FR-051 — the decision this whole design protects); a system line, a
      deleted message and a message in an archived conversation each produce nothing and are each
      marked; a message older than `MaxMessageAgeMinutes` is marked and not dispatched; and a
      dispatcher that throws leaves the message, the read state and the conversation untouched
      (FR-003). Reuse the existing
      `backend/tests/JuggerHub.Api.IntegrationTests/Push/FakePushDispatcher.cs` — do not write a
      second fake.

**Checkpoint**: quickstart scenarios 1, 2, 3 and 6 pass. This is the MVP.

---

## Phase 4: User Story 2 — Muting a conversation keeps its promise (P1)

**Goal**: the levers members already rely on keep working. 048 made mute the named stand-in for
leaving a team chat, so this is a regression guard, not a later increment — it ships with US1.

**Independent test**: [quickstart.md](quickstart.md) scenario 4 — mute, post, wait, nothing
arrives; unmute, post, one arrives.

- [X] T040 [US2] Add the **mute** clause to the eligibility filter in
      `backend/Services/Chat/Push/ChatPushScanner.cs`: drop any recipient whose
      `ConversationParticipant.IsMuted` is true (FR-010). Comment that this is the **only** lever
      that suppresses a chat push, and why — see T041.
- [X] T041 [US2] Add the **hide** clause (`IsHidden`) in the same predicate, with the comment that
      makes it comprehensible: a member-written message already clears `IsHidden` for everyone
      through `ChatMessageService.ReturnToArchiversInboxesAsync` (048 FR-007), so this matches only
      when a member archives the conversation **during** the quiet delay — which is exactly when
      they have just said they do not want to hear about it (FR-011). Without this comment the
      clause reads as dead code and will eventually be deleted.
- [X] T042 [US2] Add the **left a group** clause (`LeftDate == null`) — FR-012 — and the **block**
      check for `Direct` conversations via `ChatGuard.IsBlockedBetweenAsync(senderId, recipientId)`,
      which is stored directionally and enforced symmetrically (FR-013). Only `Direct` needs it;
      say so rather than calling it for every kind.
- [X] T043 [P] [US2] In `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatPushEligibilityTests.cs`: one test per clause — muted, left, blocked — each
      asserting zero dispatches while an unmuted control conversation in the same pass still
      produces one. Plus **the race test for FR-011**: send, set `IsHidden` while the message is
      still inside the quiet window, run the pass, assert nothing. Do **not** write a test asserting
      that hide suppresses generally; it does not, and such a test would pass for the wrong reason.

**Checkpoint**: quickstart scenarios 4 and 4b pass.

---

## Phase 5: User Story 3 — Turning chat notifications off altogether (P2, release-blocking)

**Goal**: Chat sits in the notification preferences matrix like every other entry, with a Push
toggle that works and In-app / E-mail visibly not offered.

**Independent test**: [quickstart.md](quickstart.md) scenario 5 — the row renders, the toggle
stores, turning it off silences chat while a team invitation still arrives, and `PUT …/Chat/Email`
is refused.

### The teeth

- [X] T044 [US3] **Add the preference filter to the scanner** (trap 2):
      `INotificationPreferenceService.GetEnabledRecipientsAsync(recipients, NotificationCategory.Chat, NotificationChannel.Push, ct)`
      — the batch form, applied after the other filters and before composition, in
      `backend/Services/Chat/Push/ChatPushScanner.cs`. Comment that `PushDispatcher` filters nothing
      and that `NotificationService` reads the preference itself before calling its fan-out, so this
      call is what makes the toggle real.

### The server surface

- [X] T045 [US3] Add `IReadOnlyList<NotificationChannel> AvailableChannels` to
      `PreferenceCategoryDto` in `backend/Dtos/Notifications/NotificationPreferenceDtos.cs`.
      Additive. Document why `Channels` is **not** narrowed for Chat: `false` is indistinguishable
      from a member's own choice to switch something off, and making the record nullable pushes a
      three-state decision onto four categories that use it correctly today.
- [X] T046 [US3] In `backend/Services/Notifications/NotificationPreferenceService.cs`, append
      `NotificationCategory.Chat` to `CategoryOrder` (last, after `Events`) and populate
      `AvailableChannels` for every category from `NotificationCategories.Supports`.
- [X] T047 [US3] Add the Chat label and description to `CategoryCopy` in **all three** cultures in
      the same file. The description is where the empty cells are explained, because it is the one
      place a member is looking at them — English along the lines of *"New messages in your
      conversations. Chat has its own inbox and badge, so there's nothing to send in-app or by
      e-mail."* German is the one to get right first. **No parity guard covers this dictionary**;
      its three entries are checked by eye, and the per-category `TryGetValue` fallback (added by
      039 after a bare indexer took the page down) means a missing culture degrades rather than
      throws.
- [X] T048 [US3] Add the unavailable-cell guard to
      `backend/Controllers/NotificationPreferencesController.cs`, beside the existing
      `Enum.IsDefined` check: refuse with `400` and
      *"That notification category isn't delivered on that channel."* when
      `!NotificationCategories.Supports(category, channel)`. Principle I — without it a client can
      store a `(Chat, Email)` row that means nothing and that something could later read back as
      though it meant something.
- [X] T049 [P] [US3] In `backend/tests/JuggerHub.Api.IntegrationTests/Notifications/NotificationPreferenceTests.cs`: `PUT …/Chat/Push` → `204` and the row is
      stored; `PUT …/Chat/InApp` and `PUT …/Chat/Email` → `400` **and no row is written**; the
      matrix response contains a `Chat` category last with `availableChannels == ["Push"]` and its
      `channels.inApp` / `channels.email` still present.
- [X] T050 [P] [US3] In `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatPushEligibilityTests.cs`: a recipient with `(Chat, Push)` set to `false`
      receives nothing while an unset recipient in the same pass receives one (FR-027); a recipient
      who has turned **Team news** off still receives chat notifications and vice versa (FR-028a).

### The interface

- [X] T051 [US3] Add `availableChannels` to the preference types in
      `frontend/apps/web/src/app/core/services/notification-preference.service.ts`.
- [X] T052 [US3] Render the unavailable cells in
      `frontend/apps/web/src/app/features/settings/notifications/notification-settings.component.html`.
      Each of the three cell blocks becomes conditional on the channel being available; the
      unavailable form is **not a button, has no `role="switch"` and is not focusable** (CHK041),
      shows an em dash with an `sr-only` explanation (CHK042 — a lone "—" is announced as nothing),
      and keeps its mobile row label so the card still reads as three rows (CHK043).
- [X] T053 [P] [US3] Add the unavailable-cell string to
      `frontend/apps/web/public/i18n/{en,de,es}.json` — **all three in one change**, or
      `catalog-parity.spec.ts` goes red.
- [X] T054 [P] [US3] Extend
      `frontend/apps/web/src/app/features/settings/notifications/notification-settings.component.spec.ts`:
      a category with `availableChannels: ['Push']` renders one switch and two non-interactive
      cells, and `toggle()` is never called for an unavailable channel.

**Checkpoint**: quickstart scenario 5 passes, including the `curl`.

---

## Phase 6: The privacy policy (FR-029, release-blocking)

**No automated guard covers this phase.** `legal-catalog.spec.ts` compares key *sets*, so a stale
value passes silently — and the German version is the legally authoritative one.

- [X] T055 Rewrite the push-service paragraph in
      `frontend/apps/web/public/i18n/legal/de.json` (line 183). The sentence *"Inhalte, die du
      geschrieben hast, sind nicht darin."* must go, replaced by text saying that what a
      notification says can include something another member wrote to you. Write it **German
      first** — it is the authoritative document and the other two are informational.
      Keep it generic: describe the **category of data**, never the feature, and do **not** replace
      it with a new claim about what the product does *not* do, which is the shape of sentence being
      corrected here for the second time.
- [X] T056 Mirror the change in `frontend/apps/web/public/i18n/legal/en.json` (line 183) — drop
      *"Nothing you wrote is in it."* — and in `frontend/apps/web/public/i18n/legal/es.json`, which
      needs a **wider** edit than the other two: it additionally enumerates *"a qué equipo, evento o
      entrenamiento se refiere"*, and a chat message is none of those. All three in one commit.
- [X] T057 Run `npx jest --testPathPatterns "legal-catalog"` (green, as expected — it cannot see
      this change) and then verify by eye per [quickstart.md](quickstart.md#8-the-privacy-policy-fr-029-sc-012):
      open `/privacy` in all three languages and read the paragraph.

---

## Phase 7: Amending 019

- [X] T058 [P] Add an "Amended by feature 056" callout near the top of `specs/019-chat/spec.md`, in
      the shape 022, 046 and 048 each used.
- [X] T059 [P] Mark **FR-051a superseded in part** in `specs/019-chat/spec.md`, with the breakdown
      from [research.md R11](research.md#r11-amending-019): "no new notification type" **stands**;
      "no chat rows in the Alerts inbox" (FR-051) **stands, untouched**; "no new
      notification-preference category" is **superseded**. Marking FR-051a superseded without that
      breakdown would read as a reversal of the Alerts-row decision — the opposite of what happened,
      and the thing #309 itself argues hardest against.
- [X] T060 [P] Add a pointer to this feature in `specs/019-chat/contracts/chat-api.md`, as 046 did.

---

## Phase 8: Polish, verification and delivery

- [X] T061 Run the full backend suite: `dotnet test backend/tests/JuggerHub.Api.IntegrationTests`.
      Green, including the chat tests untouched since T015.
- [X] T062 [P] Run the frontend suite, build, lint and typecheck from `frontend/`. Jest 30 takes
      `--testPathPatterns` (plural); the old singular flag is silently ignored and runs everything.
- [X] T063 Work through [checklists/ui-review.md](checklists/ui-review.md) against the diff,
      recording `file:line` for anything that fails. CHK044 (the five-row matrix at `md` in German)
      and CHK040 (unavailable cells must not read as switched-off switches) are the binding ones.
      CHK037 is a **pre-existing** failure on that page (`animate-pulse` skeletons, GH #337) and is
      recorded, not fixed.
- [ ] T064 Owner browser walk per [quickstart.md](quickstart.md#7-the-device-walk-owner-required):
      Android installed + iPhone Added-to-Home-Screen, phone locked, with the app fully closed; plus
      Settings → Notifications at **375px and `md`, in German**. Screenshots to the PR. Read the
      output rather than assuming it passed.
- [X] T065 Confirm the release gate before opening the PR: T044–T054 and T055–T057 are all done, so
      no environment can deliver a chat notification that a member cannot refuse, under a policy
      that says its content is not in it.
- [X] T066 Open the PR with `Closes #309`, the residuals from [plan.md](plan.md#residuals-recorded-rather-than-solved)
      restated in the description, and the device-walk screenshots. Note the two follow-ups worth
      filing: a "hide previews" control, and naming a party chat after its party (pre-existing, 046).

---

## Dependencies

```text
Phase 1 (Setup)
   └─► Phase 2 (Foundational) ── column ─┬─► Phase 3 (US1) ──┬─► Phase 4 (US2)
                               category ─┤                   └─► Phase 5 (US3)
                               name ─────┘                            │
                                                                      ▼
                                      Phase 6 (legal) ──► Phase 7 (019) ──► Phase 8
```

- **Phase 2 blocks everything.** US1 needs the column to know what it has considered, US3 needs the
  category to exist, and the composer needs the extracted name helper.
- **US2 and US3 both extend the same predicate** in `ChatPushScanner.cs` (T040–T042, T044), so they
  are sequential with each other even though their tests and their other files are independent.
- **Phases 6 and 7 depend on nothing in code** and can be written at any point — but Phase 6 must
  **land** before US1 is enabled anywhere real (release gate).
- US2 and US3 are not optional increments: US2 is a regression guard on an existing promise, US3 is
  release-blocking. The only genuinely shippable-alone slice is US1 **in a local environment**.

## Parallel opportunities

| Where | Tasks | Why they are safe together |
|---|---|---|
| Setup | T003, T004 | different config files |
| Foundational | T010, T011, T012 alongside T013 | the enum and the name helper are unrelated files |
| US1 | T022 while T023–T032 proceed | the composer is pure and finished at T021 |
| US1 tests | T037, T038, T039 | separate test files, one shared fake |
| US3 | T049, T050 backend ‖ T051–T054 frontend | different stacks, contract fixed at T045 |
| Legal / 019 | T055–T057 ‖ T058–T060 | documents, no code |
| Verification | T061 ‖ T062 | different stacks |

## Implementation strategy

**MVP** is Phase 1 + Phase 2 + Phase 3 (US1) — T001–T039. At that point a missed message reaches a
phone, locally. It is not shippable: it has no off switch and the privacy policy contradicts it.

**Shippable increment** is Phases 1–7, T001–T060. Every P1 behaviour, the control that makes it
refusable, and the document that describes it honestly.

Work in small commits per checkpoint, as CLAUDE.md requires. The natural commit boundaries are the
checkpoints: setup, foundation, composer, pass, loop, suppression, preference server, preference UI,
legal, amendment.
