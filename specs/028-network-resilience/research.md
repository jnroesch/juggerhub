# Research: Network Resilience (028)

**Date**: 2026-07-24 | **Spec**: [spec.md](./spec.md)

All package facts below were verified against nuget.org and Microsoft Learn on 2026-07-24,
not recalled. All codebase facts were verified by reading the source.

---

## 1. Outbound HTTP resilience — package and API

**Decision**: `Microsoft.Extensions.Http.Resilience` (pin major **10**, latest 10.8.0), applied
with `AddStandardResilienceHandler()` on the typed `HttpClient`, configured from a bound
configuration section.

**Rationale**: The package is Polly v8 packaged as a `DelegatingHandler`, maintained by
Microsoft under `dotnet/extensions`. A 10.x line exists and matches the repo's .NET 10 /
`Microsoft.*` 10.0.x alignment. One call per client satisfies FR-018 (define once, opt in) and
`HttpStandardResilienceOptions` binds straight from configuration, satisfying FR-019 with no
bespoke plumbing.

**Verified defaults** — the handler chains five strategies, outermost first:

| # | Strategy | Default |
|---|----------|---------|
| 1 | Rate limiter | queue 0, permit 1 000 concurrent |
| 2 | Total timeout | 30s |
| 3 | Retry | 3 retries, exponential, jitter on, base delay 2s |
| 4 | Circuit breaker | failure ratio 10%, **min throughput 100**, sampling 30s, break 5s |
| 5 | Attempt timeout | 10s |

Retry and breaker both handle HTTP 5xx, 408, 429, plus `HttpRequestException` and
`TimeoutRejectedException`.

**Alternatives considered**: `Microsoft.Extensions.Http.Polly` — the predecessor, now superseded
and built on Polly v7. Hand-rolled `DelegatingHandler` — rejected; re-implements jitter, breaker
state and telemetry that this package already ships, and would fail FR-018's "define once" test
by inviting per-integration divergence.

---

## 2. The default circuit breaker would never open at our volume

**Decision**: Override `MinimumThroughput` down to a small number (start at **5**) and lengthen
`SamplingDuration`. Do **not** inherit the standard breaker defaults.

**Rationale**: This is the most important finding in this research. The default breaker requires
**100 requests inside a 30-second sampling window** before it will even evaluate the failure
ratio. JuggerHub sends transactional email at a rate of a handful per minute at best — the
threshold is unreachable, so with defaults the breaker is **decorative**: it would never open, and
US3 (FR-017) would silently not be delivered while appearing to be configured.

This is a general trap for low-volume outbound calls, so it is documented in the config contract
rather than left as a magic number: any future integration inherits the same reasoning.

**Alternatives considered**: Accepting the defaults — rejected, it produces a feature that passes
code review and fails in production. Removing the breaker — rejected, FR-017 requires it and it is
what bounds FR-026 amplification.

---

## 3. Retrying the email POST is deliberate, and can duplicate a message

**Decision**: Do **not** call `DisableForUnsafeHttpMethods()` on the Resend client. Retry the POST.

**Rationale**: The package offers `DisableForUnsafeHttpMethods()` (disables retry for POST, PATCH,
PUT, DELETE, CONNECT) precisely because retrying a non-idempotent write can duplicate it. For the
email send that risk is real and asymmetric: a request that times out *after* Resend accepted it
produces a **duplicate email**; not retrying produces a **lost verification email**, which is the
dead end this whole feature exists to fix. A duplicate is a mild annoyance; a loss is
unrecoverable for the person.

Note the asymmetry with the frontend hop, where FR-004 forbids retrying mutations. The rules
differ because the consequences differ — a duplicated team creation is real data corruption, a
duplicated email is not. This is stated explicitly so the two rules don't look inconsistent.

**Alternatives considered**: Idempotency keys on the provider call — Resend supports an
idempotency key header, which would remove the duplicate risk entirely. Rejected **for now** as
scope creep beyond the spec, but recorded as the clean follow-up if duplicates are ever observed.

---

## 4. 429 means two different things — don't conflate them

**Decision**: Outbound (backend→Resend), **retry** 429 honouring `Retry-After` — this is the
provider throttling us and backing off is the correct response. Inbound (browser→backend),
**never** retry 429 — that is our own fail-closed chat limiter (FR-031), and retrying it defeats
the limit.

**Rationale**: The standard handler treats 429 as retryable by default, which is right for the
outbound hop and wrong for the inbound one. Since the two hops are implemented by entirely
different code (Polly handler vs. Angular interceptor), there is no conflict — but the rule is
recorded because "429 is retryable" and "429 is not retryable" both appear in this feature and the
contradiction is only apparent.

---

## 5. EF Core retry vs. the ten existing transactions — the main risk

**Decision**: Enable `EnableRetryOnFailure()` on the Npgsql provider, and wrap **all ten**
existing `BeginTransactionAsync` call sites in `Database.CreateExecutionStrategy().ExecuteAsync(...)`.

**Rationale**: With a retrying execution strategy configured, a user-initiated transaction throws

> `InvalidOperationException: The configured execution strategy '...RetryingExecutionStrategy' does not support user-initiated transactions. Use the execution strategy returned by 'DbContext.Database.CreateExecutionStrategy()' to execute all the operations in the transaction as a retriable unit.`

**Correction, verified during implementation** — the documentation implies this fires when the
transaction is opened. It does not. Measured on EF 10 + Npgsql 10:

| Action inside a manual transaction | Result |
|---|---|
| `BeginTransactionAsync()` outside an execution strategy | **succeeds**, no exception |
| `ExecuteSqlRawAsync` / raw SQL | **succeeds**, no exception |
| the first `SaveChangesAsync()` | **throws** |

The practical consequences are worth stating plainly. First, it *is* still a hard failure on the
first request through any of these paths — all ten sites call `SaveChangesAsync`, so all ten
genuinely had to be restructured. Second, and less comfortably: a future call site that opens a
transaction and uses only `ExecuteUpdateAsync` / `ExecuteDeleteAsync` would sail past this guard
while silently losing retry-as-a-unit semantics. The guard is not a complete safety net, so the
review rule is "grep for `BeginTransactionAsync`", not "wait for the exception". This is pinned by
a test in `DatabaseResilienceTests`.

The ten call sites found by inspection:

| File | Lines |
|------|-------|
| `backend/Services/Teams/TeamService.cs` | 352 |
| `backend/Services/Parties/PartyService.cs` | 315, 371, 407 |
| `backend/Services/Parties/PartyRosterService.cs` | 101 |
| `backend/Services/Parties/PartyInvitationService.cs` | 324 |
| `backend/Services/Marketplace/MarketRequestService.cs` | 315 |
| `backend/Services/Events/EventSignupService.cs` | 97, 225 |
| `backend/Services/Events/EventAdminService.cs` | 63 |

Every one of these covers a capacity/roster invariant (row locks via
`ExecuteSqlInterpolatedAsync`, occupancy counts, unique-violation races), so they are exactly the
paths where a partial replay would be most damaging. Two consequences:

1. **All state mutation must move inside the delegate.** The strategy re-invokes the delegate
   wholesale on a transient fault. Anything mutated or `Add`ed *before* the block would be applied
   twice. Each site must be reviewed individually, not mechanically wrapped.

   **Sharper than expected, found during implementation**: a rollback does **not** unwind EF's
   change tracker. An entity `Add`ed by a failed attempt stays in `Added` state, so a replay that
   constructs a second entity would insert *both* on the next `SaveChangesAsync`. Moving
   construction inside the delegate is necessary but **not sufficient**. Every wrapped site
   therefore begins with `_db.ChangeTracker.Clear()` — the in-scope equivalent of EF's "use a fresh
   context per attempt" guidance, which we cannot follow literally because the context is
   scoped by DI. This in turn forces any tracked entity that gets mutated to be **loaded inside**
   the delegate too (it would otherwise be detached by the clear and its writes lost) — which is
   why `PartyService.WithdrawAsync`, `PartyService.DisbandAsync` and
   `PartyInvitationService.AcceptAsync` had their loads moved in, not just their transactions
   wrapped.
2. **Retried operations must be replay-safe.** Reviewed and satisfied: these blocks take their
   row lock and re-read occupancy *inside* the transaction, so a replay recomputes from current
   state rather than reusing a stale read.

**The commit-failure idempotency problem does not apply to us.** EF documents that a connection
drop *during commit* leaves the outcome unknown, and that the standard mitigation is to avoid
store-generated keys so a replay collides instead of silently inserting a duplicate. The
constitution (Principle III) already mandates client-generated `Guid.CreateVersion7()` primary
keys on every entity — so JuggerHub is already in EF's recommended "Option 1" posture by
construction. Worth stating plainly: a decision made for B-tree locality happens to close this
correctness hole for free.

**Second-order effect**: `EnableRetryOnFailure` makes EF **buffer resultsets** rather than stream
them. The constitution already mandates pagination on every list endpoint, so no unbounded query
should exist to be buffered — but this is the reason the tasks include a check rather than an
assumption.

**Alternatives considered**: Wrapping DbContext calls in Polly — rejected, it would double-retry
on top of Npgsql's own strategy and cannot participate in EF's transaction semantics. Leaving the
database untouched — rejected, FR-023 requires it and a database restart on every deploy is the
single most predictable transient fault we have.

---

## 6. Frontend — one interceptor, ordered *inside* the auth interceptor

**Decision**: Add `retryInterceptor` registered **after** `authInterceptor`:
`withInterceptors([authInterceptor, retryInterceptor])`. Per-attempt `timeout()`, then
`retry({ count, delay })` with exponential backoff plus jitter, gated to safe methods.

**Rationale**: Angular chains interceptors in array order, so the second is *inner*. That ordering
is what makes FR-007 hold:

- A **401** propagates past the inner retry untouched (401 is not transient, so retry ignores it)
  and is handled by the outer auth interceptor exactly as today — refresh once, retry once.
- A **transient 5xx or network drop** is absorbed by the inner retry and never reaches the auth
  interceptor at all.

The reverse order would put retry outside the refresh, so one expired session could trigger
several refresh cycles — precisely the compounding the spec's edge case warns about.

Retry gate: method in `GET`/`HEAD` (FR-002, FR-004), status in {0/network, 502, 503, 504, 408} or
a timeout (FR-005 excludes 4xx and 429), and URL not in the existing auth skip list (FR-008).
`timeout()` is applied per attempt inside the retry so the attempt limit bounds total time
(FR-001, FR-014's client-side analogue). Unsubscription cancels everything for free, which is
FR-010 with no extra code.

**Alternatives considered**: A wrapper service over `HttpClient` — rejected, it is opt-in per call
site and fails FR-013. `HttpContextToken` opt-outs — considered and kept in reserve; not needed,
since method plus status plus the existing skip list already classify every current call
correctly, and an unused extension point is complexity we would have to justify.

---

## 7. The retry indicator needs no interceptor→UI plumbing at all

**Decision**: Put the delay timer **inside the existing `jh-loading` component**. After a
threshold it swaps its own label to a "still working" line. The interceptor tells the UI nothing.

**Rationale**: The obvious design — a global "a retry is in flight" signal that components
subscribe to — is both more code and *less* correct: it is global, so a slow background poll would
make every loading line on the page announce itself.

`jh-loading` (feature 024) is already the single standardized loading treatment, is used in **34**
component templates, already carries `role="status"`, and is rendered *only while something is
loading*. So it already knows the one fact that matters — "this has been loading for a while" —
and can say so with zero coupling. It is also honest in a way the coupled version isn't: a slow
first attempt and a silent retry look identical to the person waiting, and both deserve the same
reassurance.

`role="status"` means the change is announced to assistive tech, satisfying FR-011a's
"never colour alone" requirement through a mechanism that is already there.

**Alternatives considered**: A global signal service (rejected above). A new banner/toast component
— rejected outright by FR-011. Doing nothing — rejected by the Q2 decision.

---

## 8. DESIGN.md must gain a states section first (FR-011b)

**Decision**: Add a **Loading, error & retry states** section to DESIGN.md *before* building the
treatment, then instantiate `specs/028-network-resilience/checklists/ui-review.md` from the
template and verify the diff against it.

**Rationale**: Confirmed by inspection — DESIGN.md's twelve sections (Overview, Voice & content,
Colors, Typography, Layout, Elevation, Shape, Motion & states, Components, Iconography, Do's and
don'ts) contain **no** guidance for loading, error, empty-vs-error, or retry. "Motion & states"
covers hover/press/focus only. Meanwhile `jh-loading` and `jh-empty-state` already exist as
primitives, so the code is ahead of the design document.

Content is therefore *documenting an existing convention plus one addition*, not inventing a style:
the muted `body-sm` text line, the threshold before the reassurance line, the copy voice ("you",
sentence case, no emoji), and when error-with-retry is used instead of empty-state.

**Alternatives considered**: Building first and back-filling DESIGN.md — rejected; that is exactly
the drift Quality Gate 7 exists to prevent, and the spec makes the ordering a MUST.

---

## 9. The SMTP path cannot use the HTTP handler — accepted divergence

**Decision**: The standard resilience handler applies to `ResendEmailSender` (and every future
HTTP integration). `SmtpEmailSender` gets an explicit MailKit **timeout** only — no retry, no
breaker.

**Rationale**: `SmtpEmailSender` uses MailKit over a raw socket, not `HttpClient`, so a
`DelegatingHandler` cannot reach it. It is selected only when `Email:Provider=Smtp`, which per the
constitution's own stack table means **local development against Mailpit** — a container on the
same compose network, where transient network faults are not a real failure mode.

This is a real, if narrow, tension with FR-029 and constitution Principle V (environments differ in
configuration, never behaviour). It is recorded in the plan's Complexity Tracking rather than
papered over. The mitigating facts: the provider split is itself constitution-sanctioned and
pre-existing, and the *timeout* half of FR-014 — the part that protects against a hang — is applied
to both senders, so neither can block indefinitely.

**Alternatives considered**: A provider-agnostic Polly pipeline wrapped around `IEmailSender`
itself — genuinely more parity-correct and would cover non-HTTP integrations later. Rejected for
now as more machinery than one local-only sender justifies; revisit when a second non-HTTP outbound
service actually appears, at which point it has a real second consumer.

---

## 10. Frontend limits are build-time constants

**Decision**: Frontend timeout/attempt values are constants in the app source. No runtime or
per-environment override mechanism is added.

**Rationale**: The Angular app ships as a static bundle served by nginx with no runtime config
injection, and none exists today. FR-029 requires *identical behaviour* differing only in
configured values; with a single bundle the values are identical everywhere by construction, which
satisfies the principle trivially. Inventing a config-injection mechanism to make them tunable
would be new architecture for no current need.

**Alternatives considered**: Runtime config endpoint or nginx-templated `env.js` — rejected as
unjustified complexity; recorded here so the question isn't re-opened without a reason.

---

## 11. SignalR reconnect (FR-012)

**Decision**: Replace the parameterless `withAutomaticReconnect()` on both hubs with a custom
retry policy that backs off and **keeps trying indefinitely**, capped at a maximum interval.

**Rationale**: The parameterless default retries at 0s, 2s, 10s, 30s and then **stops permanently**.
After roughly 42 seconds of disruption — routine for a rolling deploy — the socket is dead until the
page is reloaded, and because live updates are silent enhancements the person gets *no signal* that
they have stopped arriving. That is the spec's "silently stopping is worse than a visible error"
edge case, and it is a live defect today rather than a hypothetical.

Both `chat.service.ts` and `notification.service.ts` already re-seed on `onreconnected`, so a
late reconnect is correct, not just live — the existing invariant is what makes indefinite retry
safe.

**Alternatives considered**: A longer fixed array — rejected, it moves the cliff rather than
removing it. Reload-the-page prompt — rejected as heavier than the problem and outside FR-011's
"no new overlay" constraint.

---

## 12. Observability (FR-027, FR-028)

**Decision**: Backend leans on the resilience package's built-in telemetry (it emits standard
metrics and logs for attempts, timeouts and breaker transitions) plus explicit structured logging at
the email give-up point (FR-021). Frontend logs nothing new.

**Rationale**: The package's telemetry already reports pipeline events with the pipeline name, so
FR-027 needs configuration and verification rather than new instrumentation. The one thing it cannot
know is the *business* outcome — "this verification email to this person is now permanently lost" —
which is exactly what FR-021 requires and what the existing `ResendEmailSender` catch block will
carry.

FR-028 constrains what is logged: the existing sender already logs Resend failures by **status code
only, never the body**, on the explicit grounds that the body may carry detail. That established
precedent extends unchanged — recipient address and message kind are permitted (needed to act on
the failure), body and credentials are not.

**Alternatives considered**: Custom metric emission — rejected as duplicating what the package
provides. Frontend telemetry — rejected; no client telemetry pipeline exists today and adding one is
a separate feature.

---

## 13. The health probe must opt OUT of connection resiliency

**Decision**: `HealthService` caps its database probe at a 3-second budget via a linked
`CancellationTokenSource`, deliberately bypassing the retry the rest of the application now has.

**Rationale**: Found by running the quickstart against a genuinely stopped database rather than
trusting the test suite. With `EnableRetryOnFailure` on, `CanConnectAsync` retries the connection
for roughly **30 seconds** before giving up — so `/api/v1/health` stopped answering promptly and
simply hung.

That is not a cosmetic problem. The Kubernetes **liveness** probe polls this endpoint on a short
timeout (`infra/modules/app/main.tf`), so a database blip would fail the probe repeatedly and get
the perfectly healthy API pod **restarted** — converting a recoverable database hiccup into an
application outage. The feature meant to reduce downtime would have introduced a new way to cause
it.

Measured before and after, with the database stopped:

| | Response time | Body |
|---|---|---|
| Before | >30s (client gave up) | — |
| After | ~3.2s | `{"status":"unhealthy","database":"unreachable"}` |

The general rule this establishes: **a health probe reports status, it does not survive outages.**
Retry belongs on the paths doing real work, not on the one whose entire job is to answer quickly
and truthfully. Every other database path keeps the retrying strategy.

**Alternatives considered**: Raising the liveness-probe timeout in Terraform — rejected as treating
the symptom; it would also slow genuine failure detection. A separate non-retrying `DbContext` for
the probe — correct but far heavier than a linked token for one call site.

**Verified end to end** (not only by unit test): database stopped → prompt graceful `unhealthy` →
database restarted → application recovered on its own, container `RestartCount=0` and start time
unchanged. That last detail is the real US4 acceptance: the app rode out a full database outage
without being restarted.
