# Phase 0 Research: Chat Push Notifications

**Feature**: 056-chat-push | **Date**: 2026-09-20 | **Spec**: [spec.md](./spec.md)

Everything below was established by reading the code on `main` at `d8b1089`, not from the issue's
description. Where the issue and the code disagree, the code is recorded as the finding.

---

## R1. Where the out-of-band dispatch lives

**Decision**: a `ChatPushBackgroundService` — a `BackgroundService` on a `PeriodicTimer`, modelled
line for line on `Services/Retention/RetentionBackgroundService.cs`: a scope per pass (never one
held between passes), a bounded per-pass timeout, failure logged and the loop continued, and
`Enabled` configuration so the integration suite can stop a timer racing its assertions.

**Rationale**: that file already solved every problem this one has, including the one that matters
most — *"Every replica runs this, so sweeps overlap. That is safe by construction and not by
locking."* The same argument is available here and is stated in R8. Reusing its shape means the
operational behaviour (startup delay, timeout, loud failure) is already understood and already
tested.

**Alternatives rejected**:

| Option | Why not |
|---|---|
| Fire-and-forget `Task.Run` from `SendAsync` | Work held in memory is lost on a rolling deploy, which is exactly when a burst of messages is most likely; and it is un-testable without sleeping. |
| An in-memory `Channel<T>` + one consumer | Same loss-on-restart problem, plus each replica would hold its own queue, so "at most once per device" becomes unknowable. |
| A durable queue (Service Bus, Redis streams) | No queue infrastructure is deployed anywhere. Redis is in `docker-compose.yml` and nothing in `infra/` deploys it — GH #219 is still open. Adding one for this is a platform decision, not a feature decision. |
| A Kubernetes `CronJob` | `RetentionBackgroundService`'s own remarks explain why not: this deletes from and reads the *application* database whose schema EF owns, so a cron pod would need the Postgres credential and would restate the query somewhere it cannot be type-checked or covered by the integration suite. Feature 038's CronJob is the exception because it runs under Umami's own role. |

---

## R2. How "considered at most once" is tracked (FR-024)

**Decision**: one new nullable column, `ChatMessages.PushConsideredAt` (`timestamptz null`), with a
**partial index** `WHERE "PushConsideredAt" IS NULL`, and a migration that **backfills every
existing row** to the migration timestamp.

The worker's discipline is: **select by the flag, then mark everything it selected — including the
rows it decides not to send for.** Skipping the mark on an ineligible message is what would let the
partial index grow without bound.

**Rationale**:

- Exact. A per-row flag cannot silently skip a message, which is the property a reach feature
  actually needs.
- The partial index is provably tiny: the only rows in it are the last few seconds' worth. Without
  the backfill it would start out covering the entire table and never shrink, because a
  pre-existing row is never selected (it fails the max-age filter) and so is never marked.
- `ChatMessages` gains a column but no behaviour: the column is `NULL` on insert, so the send path
  writes nothing extra and its transaction does not grow.
- `ExecuteUpdateAsync` bypasses the change tracker, so `ModifiedDate` must be set explicitly
  (constitution III — the defect 048 recorded). Here that is **honest rather than a white lie**: the
  row genuinely changed. Verified safe to move: `ModifiedDate` appears in no chat DTO
  (`grep ModifiedDate backend/Dtos/Chat/` is empty) and 019 ships no edit feature, so nothing
  renders it as "edited".

**Alternatives rejected**:

| Option | Why not |
|---|---|
| A single watermark row (`Id > lastSeen`, exploiting UUIDv7 ordering) | One row of state and no schema change to the hot table — attractive, and **wrong**. Ids are generated app-side in `BaseEntity`'s field initialiser, so with several replicas an id can commit *behind* the watermark under clock skew or a long transaction, and that message is then **silently never notified about**. A silent miss in a reach feature is the one failure mode that must not exist. |
| `ConversationParticipant.LastPushedMessageId` | Participant rows are **created lazily** for Team/Party conversations and carry no authority (`ConversationParticipant`'s own remarks). A team member who has never opened the team chat has no row, so there is nowhere to record the mark — and creating one per member per message would put a write amplification of `members × messages` on the hottest path in the product. |
| A separate outbox table written during `SendAsync` | Self-cleaning and keeps `ChatMessages` clean, but it puts an `INSERT` per message on the hottest write path and adds an entity whose entire content is a foreign key. The column achieves the same with strictly less. |

---

## R3. One notification per conversation, not per message (FR-022, FR-023)

**Decision**: each pass groups the messages it claimed **by conversation** and dispatches **once per
conversation**, about the **newest** message the recipient has not read. The collapse key is
`chat:{conversationId}`, which becomes both the notification `tag` and the push `Topic`.

**Rationale**: `PushDispatcher` already turns a tag into both, and its own comment states the
contract — *"the tag makes a second arrival replace the first ON THE DEVICE; the topic makes a push
service replace a message it still holds UNDELIVERED"*. Keying it on the conversation makes FR-022
true across passes as well as within one: four messages that cross the quiet-delay line in two
different passes still leave one notification on the device.

Using the *newest* unread message is safe because the read marker is monotonic
(`LastReadMessageId` is a UUIDv7 keyset cursor), so "read the newest but not an older one" cannot
occur.

---

## R4. Deciding who gets one

**Decision**: reuse what exists; add no second oracle for membership.

| Rule | Source, already in the codebase |
|---|---|
| Who is in the conversation | `ChatGuard.ResolveParticipantUserIdsAsync` — server-side, handles all six kinds *and* the archived-snapshot case |
| Not the sender (FR-008) | filter the resolved list |
| Not already read (FR-009) | `ConversationParticipant.LastReadMessageId` compared with the message id, the same keyset predicate the unread badge uses |
| Muted / hidden (FR-010, FR-011) | `ConversationParticipant.IsMuted` / `IsHidden` |
| Left a group (FR-012) | `LeftDate == null` |
| Blocked (FR-013) | `ChatGuard.IsBlockedBetweenAsync` — directional storage, symmetric enforcement |
| Joined after the message (FR-014) | `ChatGuard.ResolveJoinCutoffsAsync` — the batch form, already used by the badge |
| Wants chat push at all (FR-027) | `INotificationPreferenceService.GetEnabledRecipientsAsync(..., Chat, Push)` |

**The preference check is the caller's job, and that is established, not invented.**
`NotificationService.CreateAsync` reads the preference itself and only then calls `IPushFanOut`;
`PushDispatcher.DispatchAsync` filters nothing. A chat worker calling the dispatcher directly
therefore **must** filter first, or FR-027 does nothing.

### The finding that corrects the issue: hide cannot suppress a chat push

#309 says *"`ChatMessageService.cs:310` and the nav badge both exclude `IsMuted || IsHidden`"* and
treats the two as equivalent levers. They are not, on this path.
`ChatMessageService.ReturnToArchiversInboxesAsync` clears `IsHidden` for **every** member on
**every** member-written send (feature 048, FR-007). By the time the worker evaluates the message,
one quiet delay later, the flag is already `false`.

So **mute is the only lever that suppresses a chat push**, and FR-011 only bites in one narrow
case: the member archives the conversation *during* the quiet delay — which is precisely the moment
they have just said they do not want to hear about it. That is correct behaviour, and the spec
states it that way rather than implying a suppression that does not happen. It also means
**FR-010's test is the load-bearing one** and FR-011's is a race test, not a mirror of it.

---

## R5. What the notification calls the conversation

**Decision**: extract `ChatConversationService.DisplayName` and its `InquiryAdminLabel` helper into
an `internal static` `Services/Chat/ChatDisplayName.cs` and **call it, never copy it**. The inbox's
two existing call sites move to the extracted version unchanged.

**Rationale**: this is the 043 precedent verbatim — `LocationLabelFor` was made `internal static`
so that "a training and an event at the same address read byte-identically" was structural rather
than a convention two files had to keep. The same applies here to SC-010 and to the spec's
"conversation naming follows the app" assumption: if the composer copies the switch, the two drift
the first time a conversation kind is added. It is also the 046 test for whether to extract — the
switch encodes a *rule* (what a conversation is called, and that an inquiry is called different
things by its two sides), not presentation.

**The per-viewer part is real and cheap.** `DisplayName` is not one name per conversation:

- `Direct` → the *other* participant. For a notification the recipient's "other" is always the
  sender, because a DM has exactly two members and the sender is not the recipient.
- `TeamInquiry` / `EventInquiry` → the requester sees the team or event; an admin sees
  `"{requester} · {team}"`. So the composer projects the naming inputs **once per conversation**
  and evaluates the switch **once per recipient**, passing `isRequester: recipientId ==
  RequesterUserId`. No extra query.

**The English-fallback wart, and what is done about it.** The switch's fallbacks are C# literals:
`"Group"`, `"Team chat"`, `"Party chat"`, `"Chat"`. `Group` and `Team` never reach theirs in
practice (a group's name is required, a live team chat has a team). **`Party` always does** — a live
party conversation stores no name and `DisplayName` has no party-name input, so every party chat is
literally called "Party chat" in English for every member today. Feature 046 already recorded this
as minor drift.

It is acceptable in the inbox and it is not acceptable on a German lock screen, so the extracted
helper takes an optional `ChatNameFallbacks` record defaulting to today's English set. The inbox
passes nothing and stays byte-identical; the composer passes localized values. Ten lines, and it
keeps the browser walk in German honest.

---

## R6. Composing the text

**Decision**: a new `IChatPushComposer` in `Services/Chat/Push/`, **not** an extension of
`PushContentComposer`.

**Rationale**: `PushContentComposer.Compose(NotificationType, payloadJson, culture, tag)` is typed
to a `NotificationType` and to the in-app row's stored JSON payload. Chat has neither — FR-004 is
the whole point — so making chat fit it would mean inventing a fake type and a fake payload. Both
produce the same `PushContent` record and both hand it to the same `IPushDispatcher`; the seam is
the record, which is exactly what 055's layering intended.

**Shape** (FR-019):

| Kind | Title | Body |
|---|---|---|
| `Direct` | sender's display name | the message text |
| every other kind | the conversation's name (R5) | `"{sender}: {text}"` |

- **Truncation (FR-020)**: 120 characters, then `…`. Cut on a character boundary, never mid-escape.
  Messages are up to `ChatConstants.MaxMessageLength` (2000) and no lock screen shows that; sending
  it whole would reproduce a long message outside the platform for no benefit.
- **Unreadable body (FR-021a)**: `IChatMessageCipher.TryUnprotect` *returns false, never throws* —
  its XML doc says so explicitly. So the fallback is a branch, not a `catch`: name the sender and
  the conversation, no preview. This mirrors what 047 already does in the app (a placeholder for
  one message, never a failed conversation).
- **Attachment-only (FR-021b)**: a zero-length `BodyCipher` on a `Member`-kind row with a live
  sender is a real message made of files (`ChatMessage`'s own remarks call this "the one that looks
  wrong"). Never ask the cipher to decrypt it — `TryUnprotect` throws `ArgumentException` on an
  empty array by design. Branch on length first and say "sent an attachment".
- **Missing sender profile**: `Common.MemberPlaceholder.For(culture)`, the same stand-in the
  conversation itself uses.
- **URL (FR-005)**: `/chat/{conversationId}` — verified against `app.routes.ts`, where the open
  conversation is a child of the `chat` shell and the id is in the URL by design (019 FR-046). It
  is app-relative and built from a `Guid`, so the service worker's "must start with `/`" refusal
  can never fire on it.

---

## R7. The Chat preference entry

**Decision**: `NotificationCategory.Chat = 4`. **No migration** — `NotificationPreference` is
sparse and 055 already proved a new member costs nothing (*"a third channel adds possible cells
without touching a single stored row"*). The same is true of a fifth category.

**A category with no `NotificationType` is new, and the seam holds.** `NotificationCategories.For`
maps *types* to categories; `Chat` is simply never produced by it, which is correct — chat writes
no notification rows (FR-004). The existing test that every `NotificationType` has a case
(feature 039's guard against the silent `default` arm) is unaffected, because it walks types, not
categories.

**Channel availability becomes a domain rule with one home**: a new
`NotificationCategories.Supports(category, channel)` beside `For`, returning `false` for
`(Chat, InApp)` and `(Chat, Email)` and `true` otherwise. Three readers:

1. `NotificationPreferenceService.GetMatrixAsync` — so the DTO can say which cells exist.
2. `NotificationPreferencesController.Set` — **400 on an unavailable cell**, beside the existing
   `Enum.IsDefined` guard. Without this a client can write a `(Chat, Email)` row that means
   nothing, which is precisely the never-trust-the-client rule (Principle I).
3. The Chat category's own description copy.

`PreferenceCategoryDto` gains `IReadOnlyList<NotificationChannel> AvailableChannels` — additive, so
every existing consumer keeps working.

---

## R8. More than one replica (FR-024, SC-007)

**Decision**: claim by `ExecuteUpdateAsync` before dispatching, and rely on the collapse tag for the
residual race.

Each pass: select candidate ids → `ExecuteUpdateAsync(SetProperty(PushConsideredAt, now))` filtered
by `... && PushConsideredAt == null` → **if that affected zero rows, another replica owns this
batch: skip the pass entirely.** Otherwise dispatch.

**Why that is enough, stated honestly.** The claim is not atomic with the select, so two replicas
can interleave and both dispatch for the same conversation. The consequence is **one extra outbound
HTTPS call per affected device, and nothing visible to the member** — the tag replaces the first
notification with the identical second. This is the same argument `RetentionBackgroundService`
makes for itself (*"safe by construction and not by locking"*), with "deleting by age is
idempotent" replaced by "dispatching under a collapse tag is idempotent on the device".

`SELECT … FOR UPDATE SKIP LOCKED` would make it exact, and was rejected: EF Core cannot express it
without raw SQL, it needs a user-initiated transaction (which Principle VII requires be wrapped in
the execution strategy), and it buys the removal of a duplicate that is already invisible.

---

## R9. Principle VII

**Not engaged as a new integration, and reaching for `AddJuggerHubResilience` here is
review-rejectable.** This feature adds **no outbound call of its own**: it hands a `PushContent` to
055's `IPushDispatcher`, which already runs through the named `WebPush` typed client with the shared
timeout, jittered retry, `Retry-After` handling and breaker. Wrapping the seam again would stack
handlers, which Principle VII names explicitly.

What Principle VII *does* require here, and what the plan must therefore contain:

- **The background pass is bounded.** A per-pass timeout linked to the stopping token, exactly as
  `RetentionBackgroundService` does — "nothing waits forever" applies to a background loop as much
  as to a request.
- **The batch is bounded.** A maximum number of messages per pass, in configuration with a safe
  default. An unbounded `ToListAsync` over a backlog is the same defect in a different costume.
- **No retry is added anywhere.** A failed dispatch is dropped, not re-queued: the message is
  marked considered whether or not delivery succeeded. Retrying would amplify an incident on the
  hottest table in the product to deliver a convenience, and the in-app equivalent is still waiting
  for the member either way (FR-003).
- **Nothing sensitive is logged (FR-021c).** `PushDispatcher` already logs a status code and a
  subscription id and explicitly never the body or the endpoint. The chat worker logs counts and
  ids only — never a message, a sender name or a conversation name. This is stricter than the rest
  of the platform's logging and it is deliberate.

---

## R10. The privacy policy sentence that is about to become false

**This is the most consequential thing in the diff, and it is one sentence.**

`frontend/apps/web/public/i18n/legal/{en,de,es}.json`, line 183 of each, is the paragraph feature
055 added about the push service. It ends:

- **en** — "They receive your device's delivery address and the text of the notification, which is
  what appears on your lock screen: what happened and what it concerns. **Nothing you wrote is in
  it.**"
- **de** — "… worum es geht und wen oder was es betrifft. **Inhalte, die du geschrieben hast, sind
  nicht darin.**"
- **es** — "… qué ha pasado y **a qué equipo, evento o entrenamiento se refiere**. **No incluye nada
  de lo que hayas escrito.**"

The moment a preview ships, all three are false in a published, legally binding document — and the
German is the authoritative one. **FR-029 is therefore not a documentation chore that can trail the
code; it is part of the change.**

Note the Spanish needs a **wider** edit than the other two: it additionally enumerates "which team,
event or training it concerns", and a chat message is none of those.

The replacement is written to the standing rule that legal text describes **a category of data, not
a feature** — "what a notification says can include something another member wrote to you", never
"chat previews". It also must not be replaced with a new statement of what the product does *not*
do; that is exactly the shape of sentence that is being corrected here, for the second time.

`legal-catalog.spec.ts` compares key **sets**, so editing one locale alone does not fail — the
guard cannot see a changed *value*. All three are edited in one commit, German first.

---

## R11. Amending 019

**Decision**: the callout shape 022, 046 and 048 each used — a "Amended by feature 056" note at the
top of `specs/019-chat/spec.md`, FR-051a marked superseded **in part**, and a pointer added to
`specs/019-chat/contracts/chat-api.md`.

**What is superseded and what is not.** FR-051a reads: *"Chat MUST NOT introduce a new notification
type or notification-preference category; the existing Alerts spine and its preference matrix
(features 010/011) MUST be left unchanged by this feature."*

- "no new notification type" — **stands**, restated as FR-004.
- "no chat rows in the Alerts inbox" (FR-051) — **stands, untouched**.
- "no new notification-preference category" — **superseded** by the owner's decision (FR-027).

Saying "FR-051a is superseded" without that breakdown would read as a reversal of the Alerts-row
decision, which is the opposite of what happened and the thing #309 itself argues hardest against.

---

## R12. Where the strings live

**Decision**: `PushLocalizer` gains the chat keys, in C#, per culture, with the English fallback via
`TryGetValue` — **never a bare indexer** (039 recorded that taking the settings page down for one
language).

This is the same inversion of GH #141 that 055 already recorded and justified: the sentence is
rendered by the operating system on a device where the app is not running, so it cannot be built
from parameters on the client. Placeholders stay **positional**, for `EmailLocalizer`'s reason —
word order differs across en/de/es.

New keys: the `"{sender}: {text}"` join, "sent you a message" (the no-preview fallback), "sent an
attachment", and the three conversation-name fallbacks from R5.

**The Chat category's label and description** are a different mechanism and live in
`NotificationPreferenceService.CategoryCopy` with the other four — server-owned so the desktop
matrix and the mobile stack share one source. The description is where the "in-app and e-mail are
not offered for chat" explanation belongs, because that is the one place a member is looking at the
empty cells.

---

## Open residuals carried into the plan

1. **A party chat is called "Party chat"** even in German unless R5's fallback localization lands;
   with it, the *name* is localized but still generic, because a live party conversation stores no
   name and the projection has no party-name input. Naming a party chat after its party is a
   pre-existing gap (046), not this feature's.
2. **Duplicate outbound calls are possible** under the R8 race. Invisible to the member, bounded by
   the batch size, and not worth `SKIP LOCKED`.
3. **The quiet delay is felt twice.** Worst-case latency is the delay plus one poll interval, so the
   poll interval has to be short enough that the delay is not doubled by the wait to be noticed.
4. **No presence suppression.** A member with the app open on a desktop and the conversation closed
   is notified. Deliberate (spec assumption), and unavoidable while `userVisibleOnly` stands.
5. **A notification survives the message being read on another device**, once it has been shown.
   Clearing it would need the app to be running. Out of scope.
6. **Sender names are not localized and cannot be.** They are people's names.
