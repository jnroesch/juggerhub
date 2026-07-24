# Data Model: Network Resilience (028)

**Date**: 2026-07-24 | **Plan**: [plan.md](./plan.md)

## No persistent data

**This feature introduces no entities, no columns, and no migration** (FR-021a). Nothing derives
from `BaseEntity`; `AppDbContext` is unchanged apart from provider configuration.

That is a deliberate consequence of the Q1 clarification: durable email delivery — the one part of
this feature that *would* have needed stored state — was scoped out. If it returns as a follow-up,
it brings a `Delivery record` entity with it; nothing here should be shaped in anticipation of that.

The model below is therefore **configuration and in-memory runtime state**, documented so the plan
and tasks share vocabulary.

---

## 1. Resilience profile *(configuration)*

A named set of limits governing one outbound integration. Bound from `IConfiguration`, so it is
tunable per environment without a rebuild (FR-019) and identical in shape everywhere (FR-029).

| Field | Type | Default | Governs |
|-------|------|---------|---------|
| `AttemptTimeoutSeconds` | int | 10 | Time limit for a single try (FR-014) |
| `TotalTimeoutSeconds` | int | 30 | Time limit across all tries together (FR-014) |
| `MaxRetryAttempts` | int | 3 | Bounded attempts (FR-015) |
| `BaseDelaySeconds` | double | 2 | First backoff step; grows exponentially with jitter (FR-015) |
| `BreakerFailureRatio` | double | 0.1 | Failure share that trips the breaker (FR-017) |
| `BreakerMinimumThroughput` | int | **5** | Calls needed in the window before the ratio is evaluated — **deliberately far below the package default of 100**, which our volume never reaches (research §2) |
| `BreakerSamplingSeconds` | int | 60 | Window over which the ratio is measured (FR-017) |
| `BreakerDurationSeconds` | int | 30 | How long calls are skipped once tripped (FR-017) |

**Validation rules**

- Every value must be positive; `BreakerFailureRatio` must be in `(0, 1]`.
- `TotalTimeoutSeconds` must exceed `AttemptTimeoutSeconds`, or a single attempt could outlive the
  total budget.
- Missing or invalid configuration falls back to these built-in defaults — never to "unlimited"
  (FR-030). This is the safe-default rule and is a test, not a comment.

**Lifecycle**: resolved once at startup per named integration. Not user-editable, not exposed by
any endpoint.

---

## 2. Frontend request policy *(build-time constants)*

The browser hop has no configuration mechanism and gains none (research §10) — the app is a static
bundle, so these are constants in source and identical in every environment by construction.

| Field | Value | Governs |
|-------|-------|---------|
| Per-attempt timeout | 15s | FR-001 — nothing may hang forever |
| Max retry attempts | 2 (3 tries total) | FR-002 |
| Base backoff | 300ms, exponential, jittered | FR-003 |
| Retry-eligible methods | `GET`, `HEAD` | FR-002, FR-004 |
| Retry-eligible failures | network error / status 0, 408, 502, 503, 504, per-attempt timeout | FR-002; excludes all other 4xx and 429 (FR-005, FR-031) |
| Never-retry URL list | the existing `SKIP_REFRESH` auth paths | FR-008 |
| Reassurance threshold | 2s of continuous loading | FR-011 |

**Note on the 429 exclusion**: this is our own fail-closed chat limiter and retrying it defeats the
limit (FR-031). The *outbound* hop retries 429 deliberately, because there it means the provider is
throttling us. Same status code, opposite correct behaviour — see research §4.

---

## 3. Service health state *(in-memory, per-process)*

Whether an outbound integration is being called normally, skipped after repeated failures, or
probed for recovery. Owned entirely by the resilience pipeline.

**States and transitions**

| State | Meaning | Transition out |
|-------|---------|----------------|
| `Closed` | Normal — calls go through | → `Open` when failure ratio ≥ threshold over the sampling window, with at least `BreakerMinimumThroughput` calls observed |
| `Open` | Calls fail immediately without contacting the service (FR-017) | → `HalfOpen` after `BreakerDurationSeconds` |
| `HalfOpen` | A single trial call is permitted | → `Closed` on success, → `Open` on failure |

**Explicitly per-process and non-durable.** The deployment runs more than one replica, so each pod
holds its own breaker state and they will disagree during an outage. This is accepted, not
overlooked: a breaker is a local load-shedding device, and per-pod state fails safe — the worst case
is that a recovering service is probed once per pod rather than once globally. Sharing it through
Redis (as the rate limiter must, because *its* correctness depends on a global count) would be
real added complexity for no correctness gain.

Resets on restart, per FR-021a.

---

## 4. Attempt outcome *(telemetry, not stored)*

What is reported about a single try. Emitted by the resilience pipeline's built-in telemetry plus
the explicit give-up log (FR-021, FR-027).

| Field | Purpose |
|-------|---------|
| Operation / pipeline name | Which integration (FR-027) |
| Attempt number, total attempts | How hard we tried |
| Duration | Timeout diagnosis (FR-027) |
| Outcome | succeeded / retried / timed out / given up |
| Breaker transition | State change, logged at a noticeable severity (FR-027) |

**Redaction rules (FR-028)** — permitted: **masked** recipient address (`p***@example.com`),
message kind, provider status code, attempt counts, durations. **Forbidden**: full email addresses,
response bodies, credentials, tokens, personal message content, full request bodies. This extends
the existing rule in `ResendEmailSender`, which already logs failures *by status code only, never
the body*, on the grounds that the body may carry detail.

Masking serves two rules at once, both raised by CodeQL against the first implementation of this
feature: it keeps personal data out of log storage (`cs/exposure-of-sensitive-information`), and
stripping control characters stops a hostile address forging log lines (`cs/log-forging`) — the
address is user-supplied, so a CR/LF payload could otherwise make the audit trail lie.
