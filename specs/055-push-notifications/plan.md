# Implementation Plan: Push Notifications

**Branch**: `055-push-notifications` | **Date**: 2026-09-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/055-push-notifications/spec.md` — GH #308

## Summary

Push becomes a third notification channel for the nine existing producers. A player enables the
browser they are in, the preference matrix gains a Push column, and delivery goes out through the
Web Push protocol to whichever push service their browser uses. Chat push is #309 and depends on
the seam this feature creates.

**⚠ THE ISSUE'S CENTRAL PREMISE IS HALF WRONG, CORRECTED BY READING THE CODE.** #308 says fan-out
is centralised and that `CreateAsync`/`CreateManyAsync` "both already consult the preference
service before writing", implying push slots in beside email at one seam. They consult it for
**`NotificationChannel.InApp` only**, and they do it as an **early `return`**
(`NotificationService.cs:48-51`). Email is not sent there at all — it is sent by **eight producing
domain services** next to their in-app call, each repeating its own
`IsEnabledAsync(..., Email, ...)` check (`TeamInvitationService.cs:202`, `TeamNewsService.cs:140`,
`TeamService.cs:496`, `EventService.cs:441`, `PartyService.cs:169`, `PartyNewsService.cs:151`,
`PartyRosterService.cs:298`, `MarketRequestService.cs:277`). Consequence: a push dispatched at the
obvious place — after the row is written — **is silently switched off by the in-app toggle**, which
spec FR-015 forbids. `CreateAsync` and `CreateManyAsync` must be restructured so the two channels
are evaluated **independently** before either acts. That restructure is the load-bearing change of
this feature; everything else is ordinary.

**⚠ SECOND NAME TRAP, IN THE SAME FILE.** `NotificationService.PushAsync` already exists and means
**SignalR realtime fan-out**, not web push. The new thing is `IPushDispatcher.DispatchAsync`, and no
new member anywhere may be called `Push…` unqualified. A reviewer skimming `await PushAsync(...)`
at `NotificationService.cs:75` must not have to guess which push it is.

**⚠ THIRD TRAP, IN THE LIBRARY.** `PushServiceClient.AutoRetryAfter` defaults to **`true`** and
`MaxRetriesAfter` to **`0`**, and the retry loop only honours a cap when `MaxRetriesAfter > 0`
(`PushServiceClient.cs:362-367`). Left alone, the library retries a `429` **without a bound** and
stacks that on top of `AddJuggerHubResilience`. `AutoRetryAfter = false` is therefore mandatory,
not stylistic — the same mistake 035 recorded for the Azure Blob SDK.

**⚠ Principle VII IS ENGAGED** (first time since 050): outbound HTTPS to Google, Apple and Mozilla.
One typed client, one `AddJuggerHubResilience(configuration, "WebPush")`, one config section, and
the `429` distinction written where it is implemented.

**One new entity, one migration, one NuGet package** (`Lib.Net.Http.WebPush`, plus its single
transitive `Lib.Net.Http.EncryptedContentEncoding`). **`Lib.AspNetCore.WebPush` is deliberately NOT
taken** — its `AddPushServiceClient` returns `IServiceCollection`, not `IHttpClientBuilder`, so
resilience cannot be chained onto it; a typed client over `PushServiceClient(HttpClient)` gives the
same thing with one fewer package and the constitution's wiring.

## Technical Context

**Language/Version**: .NET 10 backend (EF Core, PostgreSQL 18), Angular 22.1.6 zoneless frontend,
nginx web tier. Service worker from feature 054.

**Primary Dependencies**: `Lib.Net.Http.WebPush` 3.3.1 (MIT, 2.1M downloads, latest release
2025-03-09; newest declared TFM `net6.0`, which .NET 10 resolves and which pulls **no**
BouncyCastle — that dependency exists only on the `net451`/`netstandard2.0` assets). No frontend
dependency: the Push API is a browser API.

**Storage**: one new table, `PushSubscriptions`, modelled on `RefreshToken`. One migration. No
change to `NotificationPreference` (the sparse model absorbs a third channel with no migration).

**Testing**: xUnit integration tests against Testcontainers Postgres with a fake dispatcher
registered through `ConfigureTestServices` (the `IEmailSender` precedent in `JuggerHubApiFactory`);
Jest for the Angular device section and the matrix column; the 054 guard spec extended for the
worker's new handlers; Playwright for the settings states that render without a real subscription.

**Target Platform**: Chrome/Edge/Firefox on desktop and Android, Safari on iOS 16.4+ **only when
installed to the Home Screen** (054). Identical config shape across local/Dev/Prod.

**Project Type**: web application — backend + frontend + the already-shipped service worker.

**Performance Goals**: no measurable added latency on the producing action; fan-out bounded by the
resilience total timeout and by explicit concurrency, never unbounded sequential awaits.

**Constraints**: a failed delivery may never fail the producing action (FR-013); no notification
text leaves the platform before a device is enabled (FR-025); the three channels are independent
(FR-015); no chat push (FR-026).

**Scale/Scope**: nine producer types, four preference categories, a handful of devices per player.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Security-First / Never Trust the Client** — PASS, with four deliberate points:
  - The endpoint, `p256dh` and `auth` values come from the browser and are **stored, never
    interpreted**. They are written against the **caller's own** user id taken from the JWT, never
    from the request body, so one account cannot subscribe on another's behalf.
  - `Endpoint` carries a **unique index**: a device that changes hands re-registers and moves to the
    new account rather than existing twice. This is what makes the shared-device case (FR-020)
    correct by construction rather than by cleanup.
  - The VAPID **private key never leaves the server** and is never logged; only the public key is
    served to the client, through an authenticated endpoint like every other surface (026).
  - `PushServiceClientException` exposes `Body` and `Headers`. Principle VII forbids bodies in
    resilience logs, so the catch logs **status code and subscription id only** — never `Body`,
    never the endpoint URL (it is a bearer-ish capability for that device).
- **II. Thin Controllers, Service-Centric Backend** — PASS. A thin `PushSubscriptionsController`
  over `IPushSubscriptionService`; dispatch behind `IPushDispatcher`; DTOs by explicit `.Select`.
- **III. Disciplined Data Access** — PASS. `PushSubscription : BaseEntity` (UUIDv7). The sweep uses
  `ExecuteDeleteAsync` and needs no `ModifiedDate` because it deletes rather than updates; the
  last-success touch **does** go through the change tracker so `AuditFieldsInterceptor` runs. No
  list endpoint is added, so pagination is not engaged (there is no device list — FR-027).
- **IV. Secure Authentication & Session Management** — PASS. No change to cookies, JWT or refresh.
  Sign-out removes the device (FR-020) by the same reasoning `AuthService.logout()` already records
  for wizard drafts and browse returns: *"what stops a shared device handing the next person"* the
  previous person's data.
- **V. Environment Parity** — PASS. Same config shape everywhere; the private key follows the
  established nine-step secret path (appsettings placeholder → `.env.sample` commented →
  compose `${VAR:-}` → `infra/variables.tf` sensitive+validated → `main.tf` → module variables →
  k8s Secret → `deploy.yml` `TF_VAR_` → GitHub Environment). Local development gets a **generated
  throwaway key pair** so the path is exercised locally, not only when deployed.
- **VI. Conventions & Tooling** — PASS. Separate `.ts`/`.html`/`.css`; the VAPID key generator is a
  **`.ps1`**, as the constitution requires (unlike 054's renderer, which needed a browser).
- **VII. Resilient by Default, Never Amplifying** — **ENGAGED, and this is the gate that matters.**
  - One typed client: `AddHttpClient<PushServiceClient>(...)` + `.AddJuggerHubResilience(configuration, "WebPush")`,
    giving the section `Resilience:Outbound:WebPush`. No hand-rolled retry, no per-client
    `HttpClient.Timeout`, no second handler.
  - **`AutoRetryAfter = false`** so the library's own uncapped 429 loop cannot stack on the pipeline.
  - **The `429` distinction, written at the call site as the constitution demands**: a `429` here is
    a *push provider throttling us* and **is** retried with backoff honouring `Retry-After` (the
    standard handler's `ShouldRetryAfterHeader` is on by default). It is not our own rate limiter,
    which is never retried against. The two cases share a status code and have opposite correct
    behaviour.
  - **`404`/`410` are rejections, not transient faults**: they are outside the standard handler's
    retry set, so they fail fast on the first attempt and the row is deleted (FR-021). They must
    never be made retriable.
  - **Breaker threshold derived from volume, not copied**: `BreakerMinimumThroughput` stays at the
    repo default of 5, which this integration genuinely reaches (one call per device per event),
    unlike email. Recorded residual below: one breaker spans all three push providers.
  - **Bounded fan-out**: deliveries run with explicit limited concurrency, never an unbounded
    sequential `foreach await` over a team's members × devices.
- **Quality Gate 7 (UI/Design compliance)** — **APPLIES.** A new device section, a new matrix
  column, and new copy in three catalogues. Instantiate `checklists/ui-review.md`. The binding cases
  are the **desktop matrix at the `md` breakpoint in German** (four columns at 768px, where the
  issue wrongly said 375px) and the **mobile card's fourth row**, plus the iOS instructions block.
- **Quality Gate 8 (Resilience)** — APPLIES, satisfied as under VII.

**Result**: No violations. Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/055-push-notifications/
├── plan.md              # This file
├── research.md          # Phase 0 decisions with alternatives
├── data-model.md        # Phase 1 — the one new entity
├── quickstart.md        # Phase 1 — how to verify, incl. the device walk
├── contracts/
│   └── push-api.md      # Phase 1 — the three endpoints + the payload shape
├── checklists/
│   ├── requirements.md  # from the spec phase
│   └── ui-review.md     # Gate 7, instantiated at implementation
└── tasks.md             # /speckit-tasks — NOT created here
```

### Source Code (repository root)

```text
backend/
├── Entities/
│   ├── PushSubscription.cs                  # NEW — device row, RefreshToken-shaped
│   └── NotificationEnums.cs                 # EDIT — Push = 2; delete "Push is out of scope"
├── Common/
│   └── WebPushOptions.cs                    # NEW — Subject, PublicKey, PrivateKey, ResilienceName
├── Data/
│   ├── AppDbContext.cs                      # EDIT — DbSet + indexes (Endpoint unique, UserId, LastSuccessAt)
│   └── Migrations/                          # NEW — one migration, one table
├── Dtos/Notifications/
│   ├── PushDtos.cs                          # NEW — register/remove requests, public-key response
│   └── NotificationPreferenceDtos.cs        # EDIT — PreferenceChannelsDto gains Push
├── Services/Notifications/
│   ├── NotificationService.cs               # EDIT — THE restructure: both channels evaluated independently
│   ├── NotificationPreferenceService.cs     # EDIT — matrix emits the third channel
│   └── Push/
│       ├── IPushDispatcher.cs               # NEW — the seam #309 will also call
│       ├── PushDispatcher.cs                # NEW — compose, fan out with bounded concurrency, prune on 404/410
│       ├── IPushSubscriptionService.cs      # NEW
│       ├── PushSubscriptionService.cs       # NEW — register / remove / touch
│       ├── PushLocalizer.cs                 # NEW — per-culture titles+bodies, EmailLocalizer pattern
│       └── PushContentComposer.cs           # NEW — producer payload → title, body, url, tag
├── Services/Retention/
│   └── StalePushSubscriptionSweep.cs        # NEW — IRetentionSweep, registered beside the token sweep
├── Services/Account/
│   └── AccountDeletionService.cs            # EDIT — one ExecuteDeleteAsync beside Notifications
├── Controllers/
│   └── PushSubscriptionsController.cs       # NEW — public key, register, remove
└── Program.cs                               # EDIT — options, typed client + resilience, sweep, fail-fast guard

frontend/apps/web/
├── public/sw.js                             # EDIT — push + notificationclick handlers (054 guard extended)
└── src/app/
    ├── core/
    │   ├── models/notification-preferences.models.ts   # EDIT — Push channel + 'push' key
    │   ├── services/notification-preferences.service.ts# EDIT — channelIdOf gains push
    │   ├── services/push-device.service.ts             # NEW — state machine, subscribe, unsubscribe
    │   └── services/auth.service.ts                    # EDIT — logout + clearSession drop the device
    └── features/settings/notifications/
        ├── notification-settings.component.{ts,html}   # EDIT — device section + fourth column/row
        └── push-device-section.component.{ts,html,css} # NEW — the six states incl. the iOS instructions

scripts/
└── New-VapidKeyPair.ps1                     # NEW — one-off key generation (Principle VI)
```

**Structure Decision**: push lives in `backend/Services/Notifications/Push/`, a sub-namespace of the
feature it serves, so the dispatcher sits *below* `NotificationService` rather than inside it. That
is the one architectural call #309 depends on: `ChatMessageService` will call `IPushDispatcher`
directly and write no `Notification` row, honouring 019 FR-051a instead of reversing it.

## Implementation Shape

### 1. The restructure of `NotificationService` (the load-bearing change)

Today (`NotificationService.cs:46-76`): check in-app → early `return` → write row → catch duplicate
→ `PushAsync` (realtime). The channel check gates *everything after it*.

After, for both `CreateAsync` and `CreateManyAsync`:

- Resolve the category once, then read **both** channel preferences (`InApp`, `Push`) before acting.
- Return early only when **neither** is on.
- When in-app is on, write the row exactly as today, including the duplicate catch. When the catch
  fires, the logical event was already notified, so **no dispatch happens either** — that is where
  FR-011's once-only property comes from for the common case.
- When in-app is off, no row exists and nothing about the in-app path runs.
- Dispatch last, after the realtime `PushAsync`, so a slow push service cannot delay the badge.
- `CreateManyAsync` resolves the two recipient sets separately (`GetEnabledRecipientsAsync` per
  channel) and dispatches to the push set, which is **not** a subset of the in-app set.

Every one of those bullets is a test: the pair (in-app on/off × push on/off) is four cases and all
four must be asserted, because three of them are new behaviour.

### 2. Once-only, without a new store

`dedupeKey` protects the in-app row through a unique index. When in-app is off there is no row and
no index to lean on. Rather than add a dedupe table or a Redis key, the once-only property is moved
to where the player actually experiences it:

- the notification is shown with a **`tag`** equal to the dedupe key (or `type:subjectId` when the
  producer passes none), so a second arrival **replaces** the first on the device instead of
  stacking — this is what `tag` is for;
- the push message carries the same value as its **`Topic`**, so a push service that still holds an
  undelivered message replaces it rather than queueing a second.

Both are free, standard, and work on every platform. Recorded in research R5 with the alternatives.

### 3. Composing what the player reads

- `PushContentComposer` turns a producer payload record (`TeamInvitePayload`, `TeamNewsPayload`, …,
  all already carrying the names and slugs needed) into `{ title, body, url, tag }`. Nothing else
  travels: FR-009, and it is what keeps a news post's body off a lock screen.
- `PushLocalizer` holds per-culture strings with **positional** placeholders, copying
  `EmailLocalizer`'s documented reason (word order differs across en/de/es). Culture comes from
  `IRecipientCultureResolver.Resolve(user)` — the recipient's stored language, not the request's.
  **`TryGetValue` with an English fallback, never a bare indexer**: the bare indexer is exactly what
  took the settings page down for a whole language before feature 039 fixed it
  (`NotificationPreferenceService.cs:100-106` records the incident).
- The strings live in **C#**, not the frontend catalogues, because the server composes the sentence.
  This is the one place where GH #141's rule points the other way: prose the server assembles has no
  key to be missing, so it must be localized where it is built. The frontend catalogues get only the
  settings copy.

### 4. Delivery, pruning and resilience

`PushDispatcher.DispatchAsync(recipientIds, content, ct)`:

- loads the recipients' subscriptions in one query;
- fans out with **bounded concurrency** (`Parallel.ForEachAsync`, small degree), never an unbounded
  sequential loop — a team news post is members × devices;
- on success, touches `LastSuccessAt` (change-tracked, so audit fields run);
- on `PushServiceClientException` with `404`/`410`, deletes that row and does **not** retry;
- on any other failure, logs status code + subscription id and moves on;
- is wrapped by the caller in `try/catch` so the producing action always succeeds — the same shape
  `TeamNewsService.cs:140-162` already uses for its email fan-out.

### 5. Enabling a device, and its six states

`PushDeviceService` (frontend) exposes one signal with exactly the states FR-002 names:
`unsupported`, `needs-install` (iOS Safari, not in standalone display mode), `blocked`
(`Notification.permission === 'denied'`), `off`, `enabling`, `on`. The enable path is
permission request → `registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey })`
→ `POST` the subscription. `userVisibleOnly` is not optional: browsers require every push to show
something, which is also why no presence suppression is attempted (FR-012).

The iOS detection is **display-mode**, not user-agent sniffing: on iOS, `PushManager` is absent in a
browser tab and present in the installed app, so the honest test is "iOS-like platform **and** not
`display-mode: standalone`". This is where 054's deferred install affordance lands.

### 6. The service worker gains two handlers

`public/sw.js` adds `push` (show the notification from the JSON body, using `tag`) and
`notificationclick` (focus an existing client or open the url). 054's guard spec is **extended, not
relaxed**: it keeps asserting no `fetch` handler, no `caches`, no `importScripts`, and now also
asserts the two new handlers exist. Nothing about the no-offline promise changes.

### 7. Keys and configuration

`scripts/New-VapidKeyPair.ps1` generates a P-256 pair with BCL crypto and prints the two base64url
values plus the exact `.env` lines. The library ships no generator, and the format is specific
(public = uncompressed point, private = `D`), so a script beats a comment telling someone to find a
tool. Startup **fails fast** when the section is missing or malformed, mirroring the Redis and chat-
encryption guards in `Program.cs` — with no disable switch.

### 8. The privacy policy needs care, not an append

FR-024's text cannot simply join the processors list. That section says every provider works *"under
a contract that only lets them handle data on our instructions"*, and the push service is chosen by
the player's browser, not by us, and operates under no contract with us. Appending would make an
existing sentence false. The disclosure therefore describes the *kind* of recipient and the fact
that the browser determines it, and the storage section gains the delivery address beside 054's
worker paragraph. German authoritative, all three locales, `legal-catalog.spec.ts` enforces parity.

## Deviations and residuals (recorded)

- **One breaker spans all three push providers.** A single named client means a sustained outage at
  one provider can open the breaker for the others. Per-provider clients would fix it and cost three
  config sections and a routing rule for a failure mode we have never seen. Revisit if Dev or Prod
  ever shows it; the dispatcher already groups by endpoint host, so the split is cheap later.
- **Once-only is a presentation property when in-app is off.** With in-app on — the default — the
  existing unique index makes it a storage property. The `tag`/`Topic` pair covers the rest; a
  delivery could still be *sent* twice in a re-processing scenario while only one notification is
  *shown*. A dedupe store was considered and declined as weight for a narrow case.
- **`Lib.Net.Http.WebPush`'s newest declared TFM is `net6.0`.** It resolves and runs on .NET 10 and
  has no native dependencies, but it is not actively tracking new runtimes. The blast radius is
  contained by `IPushDispatcher`: replacing the library means rewriting one class.
- **No managed device list** (spec FR-027, owner decision). A player who loses a phone relies on
  signing out, on the 404/410 prune, or on the stale sweep.
- **Sign-out asymmetry.** A deliberate sign-out unsubscribes in the browser and tells the server. A
  session that dies on a failed refresh can only unsubscribe locally, since there is no longer a
  session to call with; the row is then pruned on its next 404/410 or by the sweep.
- **No presence suppression.** A player reading on a laptop still gets a buzz on their phone
  (FR-012). Browsers require every push to be visible, and the delay-and-recheck mechanism #309
  discusses is that issue's to design.
- **Email fan-out stays uncultured.** `TeamEmailService` hardcodes English subjects today. Push is
  built per-recipient-language from the start, so the two channels will differ until #77 lands. Not
  fixed here; naming it so it is not read as an inconsistency introduced by this feature.
