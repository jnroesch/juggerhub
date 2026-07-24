# Implementation Plan: Network Resilience

**Branch**: `feat/028-network-resilience` | **Date**: 2026-07-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/028-network-resilience/spec.md`

## Summary

Replace JuggerHub's ad-hoc, per-screen failure handling with one reusable resilience layer per hop,
applied by default rather than per call site.

- **Browser → backend**: a second HTTP interceptor, registered *inside* the existing auth
  interceptor, giving every request a per-attempt time limit and retrying only safe reads with
  jittered exponential backoff. The "still working" reassurance is self-timed inside the existing
  `jh-loading` primitive, so no interceptor→UI wiring exists at all.
- **Backend → external services**: `Microsoft.Extensions.Http.Resilience` (Polly v8) via
  `AddStandardResilienceHandler()` on the typed Resend client, configured from a bound section so
  any future integration opts in with one line. The breaker's default `MinimumThroughput` of 100 is
  overridden — at our email volume it would never open.
- **Backend → database**: `EnableRetryOnFailure()` on Npgsql, which forces all **ten** existing
  `BeginTransactionAsync` call sites through `CreateExecutionStrategy()`. This is the bulk of the
  risk and the bulk of the work.
- **Real-time**: both hubs move off the default reconnect schedule, which today gives up
  permanently after ~42 seconds.

No new endpoints, no new entities, no migration.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.302) backend; TypeScript 5.9 / Angular 21.2 (zoneless)
frontend

**Primary Dependencies**: *added* — `Microsoft.Extensions.Http.Resilience` 10.x (Polly v8).
*existing and touched* — EF Core 10.0.10 + Npgsql 10.0.3, MailKit 4.17, `@microsoft/signalr` 8.x,
rxjs 7.8

**Storage**: PostgreSQL 18 — **no schema change, no migration** (FR-021a). The only database work is
provider configuration and transaction call-site restructuring.

**Testing**: xUnit integration tests (`backend/tests/JuggerHub.Api.IntegrationTests`) against a real
Postgres; Jest/Karma component + service specs frontend; Playwright e2e (`frontend/apps/web-e2e`)

**Target Platform**: Linux containers — AKS (Dev/Prod), docker-compose (local)

**Project Type**: Web application — separate `backend/` and `frontend/` roots

**Performance Goals**: No measurable added latency on the success path (SC-011). Resilience must be
pure overhead-free pass-through when nothing fails.

**Constraints**: Bounded amplification under sustained failure (SC-007, ≤2× normal call volume);
no request may hang indefinitely on either hop (SC-002); no persistent state (FR-021a); identical
behaviour across local/Dev/Prod (FR-029)

**Scale/Scope**: ~34 frontend components already using the shared loading primitive; 10 backend
transaction call sites requiring restructure; 1 outbound HTTP integration today (Resend), designed
for N

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Checked against `.specify/memory/constitution.md` **v1.3.0**, which this feature prompted:
Principle VII and Quality Gate 8 were added *from* this plan's research, so the gate below is
partly self-referential by design — VII codifies as standing practice what 028 establishes.

| Principle | Assessment |
|-----------|------------|
| **I. Security-first, never trust the client** | **PASS.** Adds no authorization surface. FR-020/FR-025 keep provider and database detail behind the existing `ExceptionHandlingMiddleware`; FR-028 forbids secrets in resilience logs, extending the sender's existing "status code only, never the body" rule. Retry classification is a client-side UX concern only — the server remains the boundary. |
| **II. Thin controllers, service-centric** | **PASS.** No controller changes. Registration follows the established `AddJuggerHubRateLimiting` extension-method pattern (`backend/Security/RateLimitPolicies.cs`) as `AddJuggerHubResilience`. Constitution admits resilience-adjacent cross-cutting concerns as core middleware/infrastructure under the "lean middleware" rule. |
| **III. Disciplined data access** | **PASS, with the feature's main risk.** No new entities, no `BaseEntity` derivations, no pagination surface. `EnableRetryOnFailure` switches EF to buffered resultsets — mandatory pagination means no unbounded query should exist, verified as a task rather than assumed. Constitution-mandated client-side `Guid.CreateVersion7()` keys already put us in EF's recommended posture for commit-failure replay (research §5). |
| **IV. Secure auth & session** | **PASS.** FR-007/FR-008 preserve the existing refresh-once/retry-once behaviour *exactly*; interceptor ordering is chosen specifically so retries cannot multiply refresh cycles. No token handling changes. |
| **V. Environment parity & containerized deployment** | **PASS with one recorded deviation.** Limits are configuration, identical in shape across environments. The SMTP-vs-Resend sender split means the local-only SMTP path gets a timeout but no retry/breaker — see Complexity Tracking. |
| **VI. Conventions & tooling** | **PASS.** Frontend keeps `.ts`/`.html`/`.css` separate (the `jh-loading` change touches `.ts` and `.html`). No new scripts; any added are `.ps1`. |
| **VII. Resilient by default, never amplifying** | **PASS — this feature is the principle's first implementation.** Bounded limits on both hops (FR-001, FR-014); transient-only retry with jittered backoff (FR-002, FR-003, FR-015); no automatic retry of browser-hop mutations (FR-004); the two meanings of `429` made explicit (FR-031 + research §4); shared infrastructure rather than per-call-site (FR-013, FR-018); configuration with safe defaults (FR-019, FR-030); breaker thresholds derived from real volume, not defaults (research §2); a mandatory stop-condition alongside retry (FR-017, FR-026); execution strategy for all ten transactions (FR-024, research §5); redacted telemetry (FR-027, FR-028). |
| **Gate 7: UI/design compliance** | **APPLIES.** FR-011b makes this UI-bearing. DESIGN.md gains a Loading/error/retry states section *before* implementation, and `checklists/ui-review.md` is instantiated from the template and verified against the diff. |
| **Gate 8: Resilience** | **APPLIES, and is the point of the feature.** Every clause maps to an FR above. The gate's teeth for *this* feature are phase 5: the constitution's migration note names the ten `BeginTransactionAsync` sites as non-conforming until restructured here. |

**Post-Phase-1 re-check**: still PASS. The design added no entities, no endpoints, and no middleware
beyond the resilience handler; the `jh-loading` decision (research §7) *removed* the global signal
service that would otherwise have been new shared state.

## Project Structure

### Documentation (this feature)

```text
specs/028-network-resilience/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output — 12 decisions
├── data-model.md        # Phase 1 output — configuration + in-memory state (no schema)
├── quickstart.md        # Phase 1 output — how to prove each story
├── contracts/
│   └── resilience-config.md   # Config keys + the opt-in contract for future integrations
├── checklists/
│   ├── requirements.md  # Spec quality (complete)
│   └── ui-review.md     # Instantiated during implementation, per FR-011b
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
backend/
├── Common/
│   └── ResilienceOptions.cs              # NEW — bound limits, per named integration
├── Security/
│   └── RateLimitPolicies.cs              # untouched; the pattern being mirrored
├── Services/
│   ├── Email/
│   │   ├── ResendEmailSender.cs          # MODIFIED — give-up logging (FR-021)
│   │   └── SmtpEmailSender.cs            # MODIFIED — explicit MailKit timeout
│   ├── Events/
│   │   ├── EventSignupService.cs         # MODIFIED — 2 transaction sites
│   │   └── EventAdminService.cs          # MODIFIED — 1
│   ├── Parties/
│   │   ├── PartyService.cs               # MODIFIED — 3
│   │   ├── PartyRosterService.cs         # MODIFIED — 1
│   │   └── PartyInvitationService.cs     # MODIFIED — 1
│   ├── Marketplace/
│   │   └── MarketRequestService.cs       # MODIFIED — 1
│   └── Teams/
│       └── TeamService.cs                # MODIFIED — 1
├── Resilience/
│   └── ResilienceExtensions.cs           # NEW — AddJuggerHubResilience()
├── Program.cs                            # MODIFIED — EnableRetryOnFailure, handler registration
└── tests/JuggerHub.Api.IntegrationTests/
    └── Resilience/                       # NEW — outbound retry/breaker + transaction replay tests

frontend/apps/web/src/app/
├── core/
│   └── interceptors/
│       ├── retry.interceptor.ts          # NEW — timeout + safe-read retry
│       ├── retry.interceptor.spec.ts     # NEW
│       └── auth.interceptor.ts           # UNCHANGED — ordering does the work
├── core/services/
│   ├── chat.service.ts                   # MODIFIED — reconnect policy
│   └── notification.service.ts           # MODIFIED — reconnect policy
├── shared/ui/loading/
│   ├── loading.component.ts              # MODIFIED — self-timed reassurance line
│   ├── loading.component.html            # MODIFIED
│   └── loading.component.spec.ts         # MODIFIED
└── app.config.ts                         # MODIFIED — register retryInterceptor after auth

DESIGN.md                                 # MODIFIED — new "Loading, error & retry states" section
```

**Structure Decision**: The existing two-root web-application layout is kept unchanged. Backend
resilience registration lands in a new `backend/Resilience/` folder mirroring `backend/Security/`,
because it is infrastructure composition rather than a domain service — the same reasoning that put
rate limiting in `Security/` rather than `Services/`. Its options record lives in `backend/Common/`
alongside every other `*Options.cs`. On the frontend everything lands in the existing `core/` and
`shared/ui/` homes; no new feature folder is created, because this feature deliberately owns no
screen.

## Implementation Phasing

Ordered so each phase is independently verifiable and the riskiest work is neither first nor last.

| Phase | Story | Content | Why here |
|-------|-------|---------|----------|
| **1** | US2, US3, US5 | Package, `AddJuggerHubResilience`, Resend client opt-in, breaker tuning, give-up logging, SMTP timeout | Highest-value, lowest-risk; self-contained in one integration; delivers the generic mechanism (US5) as a by-product |
| **2** | US1 (part) | `retry.interceptor.ts` + registration + specs | Independent of phase 1; touches no existing behaviour beyond ordering |
| **3** | US1 (part) | DESIGN.md states section → `jh-loading` reassurance line → `ui-review.md` | Strictly after the DESIGN.md entry exists (FR-011b); depends on phase 2 only for context, not code |
| **4** | US1 (part) | SignalR reconnect policy on both hubs | Small, isolated, independently testable |
| **5** | US4 | `EnableRetryOnFailure` + all 10 transaction call sites + buffering check | Deliberately last: largest blast radius, touches capacity/roster invariants, and benefits from the rest being green first |
| **6** | US6 | Telemetry verification, log assertions, FR-028 review | Cuts across all phases; verifies rather than builds |

Phase 5 is the one to slow down on. It is mechanical-looking and is not: each site needs its
mutations moved inside the delegate and its replay-safety confirmed individually (research §5).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| **SMTP path gets timeout but no retry/breaker**, so resilience behaviour differs between local (Mailpit/SMTP) and Dev/Prod (Resend/HTTP) — a partial deviation from Principle V and FR-029 | `SmtpEmailSender` uses MailKit over a raw socket; an `HttpClient` `DelegatingHandler` cannot reach it. The provider split is itself constitution-sanctioned (stack table: Mailpit local, Resend Dev/Prod), so the divergence is pre-existing, and the hang-protection half of FR-014 *is* applied to both | A provider-agnostic Polly pipeline wrapped around `IEmailSender` would restore full parity, but adds a second resilience mechanism alongside the HTTP handler to serve exactly one local-development-only sender. Revisit when a genuine second non-HTTP outbound integration exists and the abstraction has two real consumers (research §9) |
| **Circuit-breaker thresholds diverge sharply from the package defaults** (`MinimumThroughput` 100 → ~5) | The default requires 100 requests per 30s window before evaluating; JuggerHub's email volume never reaches it, so the breaker would never open and FR-017 would be undelivered while appearing configured | Inheriting defaults is simpler and produces a feature that passes review and silently does nothing in production (research §2) |
