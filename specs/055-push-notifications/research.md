# Research: Push Notifications (055)

Every decision was settled by reading this repository, the library's source on GitHub, or the
package metadata. The three owner decisions are inputs, recorded in the spec's Clarifications.

## R1 — Where the dispatch seam goes

- **Decision**: `IPushDispatcher` lives *below* `NotificationService` and is **called by** it, not
  implemented inside it. `NotificationService.CreateAsync`/`CreateManyAsync` are restructured so the
  in-app and push channels are evaluated independently before either acts.
- **Rationale**: two facts, both read rather than assumed. First, the existing channel check is an
  **early `return`** (`NotificationService.cs:48-51`), so any dispatch placed after it inherits the
  in-app preference and FR-015 breaks silently — a player who turns Team news off in-app would
  stop getting it on their phone without ever touching push. Second, 019 FR-051a says chat writes no
  notification rows; if push existed only inside the notification store, #309 would have to reverse
  that. A seam below the store satisfies both at once.
- **Alternatives considered**: dispatching inside `CreateAsync` after the row write (breaks FR-015
  and closes #309's option); dispatching from each of the eight producing domain services the way
  email is done today (eight copies of a preference check, the exact duplication 046 watched drift);
  an outbox table (real durability, but this feature's own rule is that a failed delivery must not
  fail or delay the action, so a durable queue buys reliability nobody asked for at the cost of a
  table, a worker and a backlog to reason about).

## R2 — Which library, and whether resilience can be chained

- **Decision**: `Lib.Net.Http.WebPush` 3.3.1 only, wired as a **typed client**:
  `AddHttpClient<PushServiceClient>((http, sp) => new PushServiceClient(http) { … })
  .AddJuggerHubResilience(configuration, "WebPush")`.
- **Rationale**: `PushServiceClient` is a plain class whose constructor takes an `HttpClient`
  (`PushServiceClient.cs:105`), which is exactly the shape .NET's typed-client factory wants and
  exactly how `ResendEmailSender` is already registered (`Program.cs:244-246`). The companion
  package `Lib.AspNetCore.WebPush` exists but its `AddPushServiceClient` returns
  **`IServiceCollection`**, not `IHttpClientBuilder` (`PushServiceClientServiceCollectionExtensions.cs:60`),
  and it creates its own internal named client `"Lib.AspNetCore.WebPush"` — so the constitution's
  one-chained-call requirement cannot be met through it without reaching for a private constant.
  Taking the lower-level package drops a dependency and puts resilience where Principle VII says.
  The package is MIT, 2.1M downloads, last released 2025-03-09, and on .NET 10 resolves its
  `net6.0` asset whose only dependency is `Lib.Net.Http.EncryptedContentEncoding` — **no
  BouncyCastle**, which appears only on the `net451`/`netstandard2.0` assets.
- **Alternatives considered**: `WebPush` 1.0.13 (the `web-push-libs` one) — older, bundles
  BouncyCastle on every target; hand-rolling VAPID ES256 plus RFC 8291 `aes128gcm` with BCL crypto —
  genuinely feasible now (`ECDsa`, `ECDiffieHellman`, `HKDF`, and `AesGcm` is already used by 047),
  but it means owning a crypto envelope forever whose failure mode is "silently undecryptable on one
  browser", and the issue's own judgement was that this is a bad trade. Agreed.

## R3 — The library's uncapped retry

- **Decision**: set `AutoRetryAfter = false` on the constructed client.
- **Rationale**: the default is `true` with `MaxRetriesAfter = 0`, and the loop only applies a cap
  when `MaxRetriesAfter > 0` (`PushServiceClient.cs:362-367`). Left at defaults the library retries
  a `429` **without a bound**, and that sits *inside* our resilience handler, so the two multiply.
  This is the same class of defect 035 recorded for the Azure Blob SDK (`MaxRetries = 0` there, or
  3×3 = 9 attempts), and Principle VII names stacked handlers review-rejectable.
- **Alternatives considered**: leaving the library to own `Retry-After` and skipping
  `AddJuggerHubResilience` — rejected outright: no breaker, no timeout, no jitter, and no telemetry
  under the integration's name.

## R4 — What `429`, `404` and `410` each mean here

- **Decision**: `429` **is** retried with backoff honouring `Retry-After`; `404` and `410` are
  **never** retried and delete the subscription; everything else follows the shared transient rules.
- **Rationale**: the constitution requires this distinction to be explicit wherever it is
  implemented. A `429` from a push service is a *provider throttling us*, the retriable case — and
  the standard resilience handler honours `Retry-After` by default. Our own fail-closed rate limiter
  also answers `429` and must **never** be retried against; same code, opposite behaviour, different
  direction of the call. `404`/`410` mean the subscription is gone (revoked permission, cleared site
  data, wiped device); they sit outside the standard handler's retry set, so they already fail fast,
  and `PushServiceClientException` carries `StatusCode` plus the `PushSubscription` that failed
  (`PushServiceClientException.cs:16,31`), which is everything needed to prune.
- **Alternatives considered**: treating `410` as transient (the reason unreachable subscriptions
  accumulate forever in naive implementations); logging `exception.Body` for diagnosis — rejected,
  Principle VII forbids response bodies in resilience logs.

## R5 — Once-only delivery without a new store

- **Decision**: carry the producer's `dedupeKey` (or `type:subjectId` when none is given) as both
  the notification's **`tag`** and the push message's **`Topic`**.
- **Rationale**: the existing unique index on `(recipient, dedupeKey)` already gives once-only when
  the in-app channel is on, which is the default and the common case: `CreateAsync` catches the
  duplicate and returns before dispatching. When in-app is off there is no row and no index. Rather
  than add a table or a Redis key for that narrow combination, the property is placed where the
  player experiences it: `tag` makes a second arrival **replace** the first on the device rather
  than stack, and `Topic` makes a push service replace a message it still holds undelivered. Both
  are standard parts of the platform, cost nothing, and work on every target.
- **Alternatives considered**: a `PushDelivery` dedupe table (a migration and a retention problem
  for an edge case); a Redis `SETNX` with a short TTL (Redis is present and required for the
  backplane and the rate limiter, so this would work, but it introduces direct
  `IConnectionMultiplexer` use into a domain service, which nothing outside `Security/` does today).
  Both recorded as available if the residual ever bites.

## R6 — Where the notification's words come from

- **Decision**: composed **server-side** in C# by `PushContentComposer` + `PushLocalizer`, using
  `IRecipientCultureResolver.Resolve(user)`; the frontend catalogues get only the settings copy.
- **Rationale**: GH #141's lesson is that server-assembled prose has no catalogue key to be missing,
  so the guard tests cannot see it. The usual remedy is to move the sentence to the client — and
  that is exactly what `ActivityParamsDto` does for the in-app list. It cannot be done here: the
  text is rendered by the operating system, on a device where the app is not running. So the
  sentence must be built on the server, which means it must be **localized** on the server, from the
  recipient's stored language rather than the request's (the actor's language is irrelevant to the
  recipient). `EmailLocalizer` is the established pattern for precisely this, including its
  documented reason for **positional** placeholders: word order differs across en/de/es.
- **The trap to copy from, not repeat**: `NotificationPreferenceService.cs:100-106` records that a
  bare dictionary indexer here once threw `KeyNotFoundException` and took the whole settings page
  down for one language. `PushLocalizer` uses `TryGetValue` with an English fallback per key.
- **Alternatives considered**: reusing `IEmailLocalizer` (it would work and is already injected, but
  a service named for email owning lock-screen copy is a name that stops being true; the codebase
  already has three independent in-code localizer dictionaries, so a fourth is house style);
  sending a bare "you have a new notification" and localizing in the worker (declined by the owner —
  the payload names its subject).

## R7 — Detecting the states the settings page must show

- **Decision**: six states — `unsupported`, `needs-install`, `blocked`, `off`, `enabling`, `on` —
  derived from feature detection, `Notification.permission`, and the **display mode**.
- **Rationale**: FR-002 requires each state to be distinguishable and actionable, and each has a
  different correct sentence. The iOS case is the subtle one: `PushManager` is simply **absent** in
  an iOS browser tab and present in the installed app, so the honest test is "iOS-like platform and
  not running standalone", not user-agent sniffing for a version. `blocked` matters because a site
  that has been denied **cannot re-ask** — only the browser's own settings can undo it, so the page
  must explain rather than offer a button that does nothing.
- **Alternatives considered**: a single enabled/disabled boolean (cannot distinguish "blocked" from
  "off", which is the difference between "press here" and "no button can help you"); asking for
  permission on page load (penalised by Chrome's quiet UI and forbidden by FR-004).

## R8 — The subscription row

- **Decision**: a new `PushSubscriptions` table modelled field-for-field on `RefreshToken`'s
  configuration: unique index on the credential (`Endpoint`), index on `UserId`, index on the column
  the sweep deletes by, `DeleteBehavior.Cascade` to `User`, plus a line in
  `AccountDeletionService.EraseOwnedDataAsync` and an `IRetentionSweep`.
- **Rationale**: it is the same kind of thing — a per-device credential owned by one account — and
  the existing table already carries every lesson: `AppDbContext.cs:585-606` even explains why the
  sweep needs its own index (*"this table is exactly the one that grew without bound before the
  sweep existed"*). `EraseOwnedDataAsync` has a natural home for it at the "records that exist solely
  to serve this member" group beside `Notifications` and `NotificationPreferences`
  (`AccountDeletionService.cs:288-289`). Adding the sweep is one class plus one `AddScoped` line
  (`Program.cs:263`), since `RetentionBackgroundService` runs whatever it finds.
- **The unique index on `Endpoint` is load-bearing**, not hygiene: it is what makes a device that
  changes hands move to the new account instead of existing twice, which is the shared-device case
  in FR-020.
- **Alternatives considered**: hashing the endpoint the way `RefreshToken` hashes its token — cannot
  be done, the endpoint URL must be sent back verbatim to deliver; storing keys encrypted at rest
  like 047 does for message bodies — they are per-device public key material, not content, and the
  endpoint they pair with is unencrypted anyway, so it would be ceremony.

## R9 — VAPID keys across environments

- **Decision**: `WebPush:Subject|PublicKey|PrivateKey`, the private key following the repo's
  nine-step secret path; a `.ps1` generates the pair; startup fails fast with no disable switch;
  local development gets its own throwaway pair so the path is exercised locally.
- **Rationale**: the nine-step path is what the survey of Resend and Tugeny established, and
  Principle V requires the shape to be identical everywhere. The library ships **no key generator**
  (there is no `VapidHelper` in its source tree), and the encoding is specific — public key is the
  uncompressed P-256 point, private key is `D`, both base64url — so a script beats prose telling
  someone to find a tool. Fail-fast mirrors the Redis and chat-encryption guards: a silently
  disabled channel is worse than a refused start.
- **The public key reaches the client through an authenticated endpoint**, not the build: it differs
  per environment, and 026 makes every surface signed-in anyway. Baking it into the bundle would
  make Dev and Prod need different builds, which 033 already established is the thing to avoid.
- **Alternatives considered**: one key pair shared across environments (a Dev subscription would
  then be deliverable from Prod); nginx `sub_filter` injection like 033's analytics config (that
  mechanism exists for a third-party snippet in the entry page, not for app configuration, and the
  client here is already making an authenticated call).

## R10 — Delivery concurrency and the producing action

- **Decision**: bounded parallelism inside the dispatcher; the caller wraps the dispatch in
  `try/catch` and logs; the action always succeeds.
- **Rationale**: FR-013. The existing precedent is `TeamNewsService.cs:140-162`, which fans out
  emails in a `try/catch` that logs and lets the post stand. Push multiplies that by devices per
  member, so an unbounded sequential `foreach await` over a 30-member team could hold the request
  for minutes under a slow provider. Bounded concurrency is not a hand-rolled resilience policy —
  the retry, timeout and breaker all still come from the shared pipeline.
- **Alternatives considered**: fire-and-forget on a background task (loses work with no record and
  detaches from the request's cancellation and scope); an outbox (R1).
