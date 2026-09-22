# Implementation Plan: Chat Push Notifications

**Branch**: `056-chat-push` | **Date**: 2026-09-20 | **Spec**: [spec.md](./spec.md)

**Input**: GH #309. Depends on #307 (054, merged) and #308 (055, merged, PR #329).

## Summary

A chat message reaches nobody who is not on the site. Feature 055 placed `IPushDispatcher` **below**
`NotificationService` precisely so chat could use it without writing a `Notification` row; this is
that seam's first caller, so 019's "no Alerts rows" decision is honoured rather than reversed.

The mechanism is **delay-and-recheck**: a background pass picks up member-written messages that have
been unconsidered for 30 seconds, works out per recipient whether they still have not read it and
still want to hear about it, and hands one `PushContent` per conversation to the existing
dispatcher. Nothing is awaited on `SendAsync`.

**Three owner decisions shape it** (spec Clarifications): a **30-second** quiet delay; the payload
carries a **preview of the message**, not just the sender's name; and **Chat becomes an entry in the
notification preferences matrix**, Push-only.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Angular 22 zoneless (frontend)

**Primary Dependencies**: **none added.** `Lib.Net.Http.WebPush` 3.3.1, the `WebPush` typed client
and its `AddJuggerHubResilience` pipeline all come from 055 unchanged.

**Storage**: PostgreSQL via EF Core. **One nullable column + one partial index + one backfill.**

**Testing**: xUnit + Testcontainers (backend integration), Jest (frontend unit), Playwright (e2e)

**Target Platform**: AKS, multi-replica. Browser push on Chromium / Firefox / installed iOS PWA.

**Project Type**: web application — `backend/` + `frontend/apps/web/`

**Performance Goals**: `SendAsync` gains **zero** work (FR-002/SC-002). One pass costs one indexed
select, one update, and one dispatch per affected conversation.

**Constraints**: no new outbound integration; no change to the service worker contract; no new
endpoint; worst-case felt latency = quiet delay + poll interval.

**Scale/Scope**: 1 column, 1 migration, 1 enum member, 1 background service, 1 composer, 1 extracted
helper, 1 changed DTO field, 1 narrowed controller guard, 1 matrix row, ~10 push strings × 3
cultures, **1 privacy-policy sentence × 3 locales**.

---

## The five things most likely to go wrong

Read these before writing code. Each is a finding from the codebase, not a guess.

### 1. ⚠ Hide does **not** suppress a chat push, and the issue says it does

#309 treats mute and hide as equivalent levers because the nav badge excludes
`IsMuted || IsHidden`. On this path they are not equivalent:
`ChatMessageService.ReturnToArchiversInboxesAsync` clears `IsHidden` for **every** member on
**every** member-written send (048 FR-007), so 30 seconds later the flag is already `false`.

**Mute is the only lever that suppresses.** FR-011 bites only when a member archives *during* the
quiet delay. Build it that way, test FR-010 as the load-bearing case, and test FR-011 as a race —
do not write a test that asserts hide suppresses generally, because it will pass for the wrong
reason or fail for the right one. (research R4)

### 2. ⚠ The preference check is the **caller's** job

`PushDispatcher.DispatchAsync` filters nothing. `NotificationService` reads the preference itself
and only then calls `IPushFanOut`. A chat worker that calls the dispatcher directly and forgets to
call `GetEnabledRecipientsAsync(..., Chat, Push, ct)` ships an off switch that does nothing, and
every test of the happy path still passes. (research R4, R7)

### 3. ⚠ Mark **every** message the pass selects, including the ones it sends nothing for

The partial index covers `WHERE "PushConsideredAt" IS NULL`. A selected-but-unmarked message stays
in that index for ever, and the index stops being small — which is the only reason it is affordable
to query every ten seconds. The mark is unconditional, after selection, before or after dispatch.
(data-model D1)

### 4. ⚠ `TryUnprotect` throws on an **empty** array, and an empty body is a real message

`IChatMessageCipher.TryUnprotect` returns `false` rather than throwing for a *corrupt* envelope —
but it throws `ArgumentException` for a **zero-length** one, by design. A `Member`-kind row with a
live sender and an empty `BodyCipher` is an attachment-only message (049), not corruption. **Branch
on `Length == 0` first**, then call the cipher. Getting this backwards turns every photo-only
message into a logged exception. (research R6)

### 5. ⚠ One published sentence becomes false the moment this ships

`public/i18n/legal/{en,de,es}.json` line 183 each end with a claim that message content is not in a
notification — "Nothing you wrote is in it" / "Inhalte, die du geschrieben hast, sind nicht darin." /
"No incluye nada de lo que hayas escrito." German is the authoritative document.
**`legal-catalog.spec.ts` compares key sets and cannot see a changed value**, so nothing will fail
if this is forgotten. FR-029 is part of the change, not a follow-up. The Spanish needs a wider edit
than the other two — it also enumerates "which team, event or training it concerns". (research R10)

---

## Constitution Check

| Principle / Gate | Verdict |
|---|---|
| **I. Security-first, never trust the client** | **Engaged.** Recipients are resolved server-side through `ChatGuard`, never from client input. `PUT …/Chat/Email` is refused with `400` rather than stored as a meaningless row. No message content in any log (FR-021c) — stricter than the platform's norm and deliberate. The preview is content the recipient is already entitled to read. |
| **II. Thin controllers, service-centric** | Passes. One controller line added (a guard). All logic in `ChatPushBackgroundService` / `ChatPushComposer` behind interfaces; DTOs from explicit `.Select` projections. |
| **III. Disciplined data access** | **Engaged.** New column on `BaseEntity`-derived `ChatMessage`; `AsNoTracking` + projections for every read; the batch is bounded (no unpaged `ToListAsync`); `ExecuteUpdateAsync` sets `ModifiedDate` in the same statement. |
| **IV. Auth & sessions** | Untouched. |
| **V. Environment parity** | Passes. One new `ChatPush` config section, same shape everywhere, safe defaults, **no new secret**. |
| **VI. Conventions** | Passes. Frontend keeps `.html`/`.css`/`.ts` separate; no new scripts. |
| **VII. Resilient, never amplifying** | **Not engaged as a new integration — and reaching for `AddJuggerHubResilience` here is review-rejectable.** No outbound call is added: the existing `WebPush` typed client already carries timeout, jittered retry, `Retry-After` handling and breaker; wrapping the seam again stacks handlers. What it *does* require and what is built: a bounded per-pass timeout, a bounded batch size, **no retry of a failed dispatch** (the message is marked considered either way — retrying would amplify an incident to deliver a convenience), and nothing sensitive in logs. (research R9) |
| **Gate 7 — UI/Design** | **ENGAGED** → `checklists/ui-review.md`. New matrix row with two unavailable cells, new server-owned copy, three locales. |
| **Gate 8 — Resilience** | Reviewed above; no network call added. |

**No violations. Complexity Tracking section omitted.**

---

## Project Structure

### Documentation (this feature)

```text
specs/056-chat-push/
├── plan.md               # this file
├── spec.md
├── research.md           # Phase 0 — the decisions and what was rejected
├── data-model.md         # Phase 1 — one column, one enum member
├── quickstart.md         # Phase 1 — how to see it work
├── contracts/
│   └── chat-push-api.md  # Phase 1 — the one changed DTO, the internal seam, config
├── checklists/
│   ├── requirements.md
│   └── ui-review.md      # Gate 7
└── tasks.md              # /speckit-tasks — not created here
```

### Source code

```text
backend/
├── Common/
│   └── ChatPushOptions.cs                     # NEW — the ChatPush config section
├── Entities/
│   ├── ChatMessage.cs                         # +1 column (D1)
│   └── NotificationEnums.cs                   # +Chat = 4, +Supports() (D2, D3)
├── Data/
│   ├── AppDbContext.cs                        # +partial index configuration
│   └── Migrations/…_AddChatMessagePushConsideredAt.cs   # NEW — column, index, backfill
├── Dtos/Notifications/
│   └── NotificationPreferenceDtos.cs          # +AvailableChannels
├── Controllers/
│   └── NotificationPreferencesController.cs   # +unavailable-cell guard → 400
├── Services/
│   ├── Chat/
│   │   ├── ChatDisplayName.cs                 # NEW — extracted from ChatConversationService (R5)
│   │   ├── ChatConversationService.cs         # calls the extracted helper; no behaviour change
│   │   └── Push/
│   │       ├── IChatPushComposer.cs           # NEW
│   │       ├── ChatPushComposer.cs            # NEW — title/body/url/tag (R6)
│   │       ├── IChatPushScanner.cs            # NEW — one pass, callable from a test
│   │       └── ChatPushScanner.cs             # NEW — select, claim, decide, dispatch, mark
│   ├── Notifications/
│   │   ├── NotificationPreferenceService.cs   # +Chat copy, +AvailableChannels
│   │   └── Push/PushLocalizer.cs              # +chat strings × 3 cultures (R12)
│   └── Hosted/
│       └── ChatPushBackgroundService.cs       # NEW — PeriodicTimer loop (R1)
└── tests/JuggerHub.Api.IntegrationTests/
    └── Chat/ChatPush*.cs                      # NEW

frontend/apps/web/
├── src/app/core/services/notification-preference.service.ts   # +availableChannels
├── src/app/features/settings/notifications/
│   ├── notification-settings.component.html   # unavailable cells
│   └── notification-settings.component.spec.ts
├── public/i18n/{en,de,es}.json                # matrix copy for unavailable cells
└── public/i18n/legal/{en,de,es}.json          # ⚠ FR-029 — the sentence (R10)
```

**Structure Decision**: the existing `backend/` + `frontend/apps/web/` split. The worker goes in a
new `Services/Hosted/` folder rather than beside the retention one, because it is not a retention
sweep and registering it as an `IRetentionSweep` would make it run daily.

---

## Implementation phases

Each phase is independently verifiable and ends in a commit.

### Phase 1 — The preference entry (US3, and it comes first on purpose)

`NotificationCategory.Chat`, `NotificationCategories.Supports`, the category copy in three
languages, `AvailableChannels` on the DTO, the controller's `400`, and the matrix row.

**First, not last.** The off switch must exist before anything can be delivered, so nobody in any
environment receives a notification they had no way to refuse. It is also independently
demonstrable: the row renders, the toggle stores, the unavailable cells are refused — all before a
single notification exists.

### Phase 2 — The column

Entity property, the partial index in `AppDbContext`, the migration **with its backfill**. Verified
by applying it and checking the index is partial and the table has no `NULL`s left.

### Phase 3 — Naming, extracted

Move `DisplayName` + `InquiryAdminLabel` to `ChatDisplayName` with the optional fallbacks record;
repoint `ChatConversationService`'s two call sites. **Behaviour must not change** — the existing
chat inbox tests are the proof, and they are run before and after with no edits.

### Phase 4 — The composer

`ChatPushComposer` + the `PushLocalizer` strings. Pure and fully unit-testable: no database, no
network. Every branch from R6 gets a test — direct vs group titles, truncation, unreadable body,
attachment-only, missing profile, and the URL shape.

### Phase 5 — The scan

`ChatPushScanner.RunOnceAsync` — select, claim, resolve recipients, filter, compose per language,
dispatch, mark. **Exposed as its own interface so every integration test drives one deterministic
pass instead of waiting on a timer**, which is the same reason `IRetentionSweep` is separable from
its background service.

### Phase 6 — The loop

`ChatPushBackgroundService` + `ChatPushOptions` + the config in `appsettings*.json`, `.env.sample`,
compose and the test factory (with `Enabled = false` in tests).

### Phase 7 — The privacy policy (FR-029)

All three locales in one commit, German drafted first. **No guard will catch this if it is skipped.**

### Phase 8 — Amending 019

The callout, FR-051a marked superseded *in part* with the breakdown from R11, and the pointer in
`contracts/chat-api.md`.

### Phase 9 — Verification

Backend suite, frontend suite, build, lint, typecheck; the Gate 7 checklist against the diff; and
the owner's browser walk — **375px and desktop, in German**, per the standing rule.

---

## Testing strategy

| What | Where | Why it is the guard |
|---|---|---|
| **The eligibility matrix** — read / unread × muted / not × sender / recipient × joined-before / after × blocked / not × preference on / off | `ChatPushEligibilityTests` | This is the feature. Every FR-008..FR-014 and FR-027 is a row in it, and a single missing filter is a real leak |
| A pass twice over the same message dispatches **once** | `ChatPushScannerTests` | FR-024 |
| Four messages in one conversation → **one** dispatch, tagged `chat:{id}` | `ChatPushScannerTests` | FR-022 |
| Two conversations → two dispatches, different tags | `ChatPushScannerTests` | FR-023 |
| A message older than max age is **marked and not dispatched** | `ChatPushScannerTests` | FR-025 *and* the index invariant (D1) |
| System line, deleted message, archived conversation → nothing | `ChatPushScannerTests` | FR-015, FR-016, FR-017 |
| **No `Notification` row exists after any of it** | `ChatPushScannerTests` | FR-004 / 019 FR-051 — the decision this whole design protects |
| A failing dispatcher leaves the send, the message and the read state untouched | `ChatPushScannerTests` | FR-003 |
| Composer branches (R6) | `ChatPushComposerTests` | FR-019, FR-020, FR-021a, FR-021b |
| `PUT …/Chat/Email` → `400`; `PUT …/Chat/Push` → `204` | `NotificationPreferenceTests` | Principle I, contract |
| Chat category present, `availableChannels == ["Push"]` | `NotificationPreferenceTests` | contract |
| Existing chat inbox tests, unedited, after the Phase 3 extraction | existing suite | proves the extraction changed nothing |
| Matrix renders unavailable cells, and they are not switches | frontend spec | Gate 7 |

`FakePushDispatcher` already exists at
`backend/tests/JuggerHub.Api.IntegrationTests/Push/FakePushDispatcher.cs` — reuse it rather than
writing a second one.

---

## Residuals, recorded rather than solved

1. **A party chat is still generically named.** R5 localizes the fallback, so a German member sees a
   German word instead of "Party chat" — but a live party conversation stores no name and the
   projection has no party-name input, so it cannot be named after its party. Pre-existing (046).
2. **Duplicate outbound calls are possible** when two replicas interleave a claim. Invisible to the
   member (the tag collapses them), bounded by the batch size, and not worth `SELECT … FOR UPDATE
   SKIP LOCKED` (research R8).
3. **Felt latency is delay + poll interval**, so 30 seconds is really 30–40.
4. **No presence suppression.** A member with the app open and the conversation closed is notified.
   Deliberate, and unavoidable while `userVisibleOnly` stands.
5. **A shown notification is not withdrawn** when the message is read elsewhere. Clearing it needs
   the app to be running.
6. **The preview cannot be switched off separately** from chat notifications. Recorded in the spec
   as out of scope, not rejected — a plausible next ask.
7. **`ModifiedDate` moves on a message nobody edited.** Accurate (the row did change) and invisible
   (no chat DTO carries it, 019 has no edit), but it is the kind of thing that looks wrong in a
   diff, so the column's XML doc says why.

## Found during implementation

- **The block clause is defence in depth, not the first line.** `ChatMessageService.SendAsync`
  already refuses a send between blocked players with `403`, so no message from a blocked sender
  can exist for the pass to consider. The clause in `EligibleAsync` closes exactly one gap: a block
  created *after* the send and before the pass ran — the same shape as archiving during the delay.
  Both halves are asserted in `ChatPushEligibilityTests` so the clause is not mistaken for the only
  thing standing between a blocked player and somebody's lock screen.
- **019's own FR-051a guard caught the category, as designed.**
  `ChatDoesNotTouchAlertsTests.Chat_adds_no_notification_type_or_category` failed the moment
  `NotificationCategory.Chat` appeared — its doc comment had said adding one should be "a
  deliberate, reviewed change to feature 010/011's contract, not a side effect of a chat PR", and
  that is what it forced. It is now split in two: the notification-type half unchanged, and a new
  test asserting the category exists, has no producer, and is refused on In-app and E-mail.
- **`ChatGuard` needed a third join-cutoff shape.** The existing batch helper resolves one user
  across many conversations; the pass needs one conversation across many users.
  `ResolveJoinCutoffsForMembersAsync` was added beside its siblings rather than written out at the
  call site, so the rule about what a join cutoff *is* keeps one home instead of acquiring a third
  copy — a thirty-player team chat would otherwise have cost thirty round trips per message.
- **`dotnet ef migrations add --no-build` silently produced an empty migration**, because it read
  the previous build's assembly and found no model difference. Caught by reading the generated file
  rather than by any test — an empty migration would have shipped a snapshot claiming a column the
  database never got. Always let the tool build.
