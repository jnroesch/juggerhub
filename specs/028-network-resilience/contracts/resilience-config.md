# Contract: Resilience configuration & the opt-in surface

**Date**: 2026-07-24 | **Plan**: [../plan.md](../plan.md)

This feature exposes **no HTTP endpoints** and changes no existing API contract. What it does expose
is an internal contract with two consumers: operators who tune it, and the next developer who adds
an outbound integration. FR-018 and FR-019 are both about *this* surface, so it is specified here
rather than left to be inferred from the implementation.

---

## 1. Configuration keys

Bound from `IConfiguration` under `Resilience:Outbound:<IntegrationName>`. Per the constitution,
values arrive from `.env` locally and GitHub Environments → Kubernetes ConfigMap when deployed;
none of these are secrets.

```json
{
  "Resilience": {
    "Outbound": {
      "Resend": {
        "AttemptTimeoutSeconds": 10,
        "TotalTimeoutSeconds": 30,
        "MaxRetryAttempts": 3,
        "BaseDelaySeconds": 2,
        "BreakerFailureRatio": 0.1,
        "BreakerMinimumThroughput": 5,
        "BreakerSamplingSeconds": 60,
        "BreakerDurationSeconds": 30
      }
    }
  }
}
```

**Contract guarantees**

| Guarantee | Requirement |
|-----------|-------------|
| Every key is optional; omitted keys take the documented default | FR-030 |
| Invalid values (non-positive, ratio outside `(0,1]`, total ≤ attempt) fall back to the default and log a warning at startup — they never disable a limit | FR-030 |
| The key *shape* is identical in every environment; only values differ | FR-029 |
| Adding a new integration adds a sibling section; it never edits shared behaviour | FR-018, FR-019 |

**Two repairs applied automatically** (found during implementation; both would otherwise be a
startup crash rather than a degraded limit):

- `MaxRetryAttempts: 0` is a reasonable thing to write for "attempt once, don't retry", but the
  underlying strategy validates the field as `[1, int.MaxValue]` and throws
  `OptionsValidationException` on 0 — taking the integration down silently. The shared extension
  honours the *intent* instead: it keeps the strategy at its floor and disables handling, yielding
  exactly one attempt. A **negative** value falls back to the default.
- `BreakerSamplingSeconds` is widened to at least `2 × AttemptTimeoutSeconds`, which the pipeline
  requires in order to build at all.

**Environment variable form** (compose / Kubernetes):
`Resilience__Outbound__Resend__MaxRetryAttempts=3`

---

## 2. The opt-in surface for a new integration

This is the contract US5 is measured against: a new outbound service inherits everything with **one
chained call and one config section**, and writes no resilience logic of its own.

```csharp
// Program.cs — the whole opt-in for a new HTTP integration.
builder.Services
    .AddHttpClient<IPaymentGateway, StripeGateway>()
    .AddJuggerHubResilience(builder.Configuration, "Stripe");
```

`AddJuggerHubResilience(configuration, name)` is the single shared extension
(`backend/Resilience/ResilienceExtensions.cs`). It:

1. binds `Resilience:Outbound:<name>` into a validated `ResilienceOptions`, applying safe defaults
   for anything missing or invalid;
2. calls `AddStandardResilienceHandler()` with those values;
3. names the pipeline `<name>` so telemetry attributes every retry, timeout and breaker transition
   to the right integration (FR-027).

**What a caller must not do** — each of these is a review-rejectable violation of FR-013/FR-018:

- write its own retry loop, `Task.Delay` backoff, or `try`/`catch`-and-try-again;
- set `HttpClient.Timeout` directly (the handler owns timing; a client-level timeout cuts across the
  pipeline and breaks the total-vs-attempt distinction);
- add a second resilience handler — the package explicitly warns against stacking them.

### Per-integration divergence

Anything genuinely different goes in configuration, not code. A slow provider gets a larger
`AttemptTimeoutSeconds`; a fragile one gets a lower `BreakerFailureRatio`. If an integration needs
behaviour the shared pipeline cannot express, that is a signal to extend the shared extension for
everyone — not to hand-roll one client.

### Choosing `BreakerMinimumThroughput` — read before copying

The package default is **100 calls per sampling window**. JuggerHub's outbound volume does not reach
it, so a copied default yields a breaker that can never open (research §2). Any new integration must
set this from its *actual expected call rate*: the breaker only works if the window realistically
contains at least this many calls.

---

## 3. Retry classification contract

What each hop treats as retryable. Written down because the two hops deliberately disagree about
`429`, and that looks like a bug unless it is stated.

| Condition | Browser → backend | Backend → external |
|-----------|-------------------|--------------------|
| Network failure / no response | **Retry** (GET/HEAD only) | **Retry** |
| Per-attempt timeout | **Retry** (GET/HEAD only) | **Retry** |
| 408 Request Timeout | **Retry** (GET/HEAD only) | **Retry** |
| 5xx (500, 502, 503, 504) | **Retry** (GET/HEAD only) | **Retry** |
| **429 Too Many Requests** | **Never** — this is our own fail-closed limiter; retrying defeats it (FR-031) | **Retry**, honouring `Retry-After` — this is the provider throttling us (FR-006) |
| 401 Unauthorized | **Never here** — owned by the auth interceptor's refresh-once path (FR-007) | **Never** — permanent credential failure (FR-016) |
| Other 4xx (400, 403, 404, 409, 422) | **Never** (FR-005) | **Never** (FR-016) |
| Mutating method (POST/PUT/PATCH/DELETE) | **Never**, whatever the failure (FR-004) | **Retried** for the email send — a duplicate email is strictly better than a lost verification mail (research §3) |

The last row is the one asymmetry in the feature and it is intentional. The hops differ because the
consequences differ: a duplicated team creation is data corruption; a duplicated email is a mild
annoyance, while a lost one is an account nobody can activate.

---

## 4. Frontend contract

No configuration surface (research §10) — constants in source, identical everywhere.

**Interceptor registration order is part of the contract**, not an implementation detail:

```ts
provideHttpClient(withFetch(), withInterceptors([authInterceptor, retryInterceptor]))
```

Angular chains interceptors in array order, so `retryInterceptor` sits **inside** `authInterceptor`.
That ordering is what keeps FR-007 true: a 401 passes the inner retry untouched and is handled once
by the outer refresh path, while transient faults are absorbed inside and never reach the auth
interceptor. **Reversing this order is a defect** — it lets one expired session drive several
refresh cycles.

**Error surface is unchanged** (FR-009): when attempts are exhausted, the original
`HttpErrorResponse` propagates to the caller exactly as it does today, so every existing
`failed`/retry treatment keeps working without modification.

**`jh-loading` gains one optional input** and no new required ones, so all 34 existing call sites
keep working untouched:

| Input | Type | Default | Meaning |
|-------|------|---------|---------|
| `label` | string | `'Loading…'` | *(existing)* the line of copy |
| `align` | `'left' \| 'center'` | `'left'` | *(existing)* alignment |
| `patientLabel` | string | `'Still loading…'` | **new** — replaces `label` after the threshold |

The component times this itself and is told nothing by the interceptor (research §7).
