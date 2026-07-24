# Quickstart: Validating Network Resilience (028)

**Date**: 2026-07-24 | **Plan**: [plan.md](./plan.md)

How to prove each user story actually works. Resilience is unusual in that **the happy path proves
nothing** — you have to break something on purpose. Every scenario below is therefore
"induce a fault, observe the recovery".

## Prerequisites

```powershell
docker compose up -d          # backend, frontend, postgres, redis, mailpit
```

- Backend `http://localhost:8080`, frontend `http://localhost:4200`, Mailpit UI `http://localhost:8025`
- A registered, verified account to sign in with
- Config keys and their defaults: [contracts/resilience-config.md](./contracts/resilience-config.md)

---

## US1 — A brief hiccup doesn't break the page

### 1a. A transient read recovers silently

```powershell
# Load any list screen (browse teams), then interrupt the backend mid-browse:
docker compose restart backend
```

**Expect**: reads in flight during the restart complete once the backend is back. No error card, no
manual retry. **Fails if** you see the browse error state for a restart of a few seconds.

### 1b. A hung request ends in a bounded failure — the "infinite spinner" bug

This is the regression this feature exists to kill, so verify it deliberately:

```powershell
docker compose pause backend    # accepts the connection, never answers
```

**Expect**: the screen resolves to an actionable error within the per-attempt timeout × attempts —
not an endless spinner. `docker compose unpause backend` afterwards.

### 1c. Mutations are never retried

With the backend paused, attempt an action that changes data (RSVP, send a message, create a team).
**Expect**: exactly **one** request in the browser network panel, failing once. **Fails if** you see
repeated attempts — FR-004 is violated and duplicate side effects become possible.

### 1d. Session refresh still behaves (FR-007)

Sign in, wait for the access token to expire (15 min per `Jwt:AccessTokenLifetimeMinutes`), then
load a screen. **Expect**: exactly one `/auth/refresh`, then the original request succeeds. **Fails
if** the network panel shows several refresh cycles — that means the interceptor order is reversed.

### 1e. The reassurance line appears, but only when slow

Throttle to "Slow 3G" in devtools and load a data screen.

**Expect**: nothing unusual for the first ~2s, then the existing loading line changes to the patient
copy — no banner, no toast, no layout shift. On a fast load it must never appear. Confirm it is
announced by a screen reader (the element carries `role="status"`).

---

## US2 — A transactional email survives a provider blip

Local uses Mailpit over SMTP, which does not exercise the HTTP resilience path. To test the real
path, point at Resend with a deliberately broken configuration, or run the integration tests, which
stub the provider:

```powershell
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~Resilience"
```

**Expect**:

- a provider returning 500 then 200 results in **one** delivered email, not a failure;
- a provider returning 401/422 (permanent) is **not** retried — one attempt only;
- a provider that never answers is abandoned at the attempt timeout, not held open.

### Give-up logging (FR-021, FR-028)

Force a persistent failure and read the backend logs.

**Expect**: an error-level entry naming the recipient, the message kind, and the provider status.
**Fails if** the response body, the API key, or any credential appears — FR-028 is a hard rule, and
the existing sender's "status code only, never the body" precedent is what it extends.

---

## US3 — A provider outage doesn't take the app down

Hold the provider in a failing state and send enough emails to exceed
`BreakerMinimumThroughput` within `BreakerSamplingSeconds`.

**Expect**: after the threshold, further sends fail **immediately** without a network call; after
`BreakerDurationSeconds` a single trial call is made; a healthy provider resumes normal service.

**The trap this checks for**: with the package default of `MinimumThroughput: 100` the breaker never
opens at our volume, so this scenario would silently pass by never tripping. Confirm the breaker
*does* trip — an untripped breaker here is a failure, not a pass (research §2).

Meanwhile, confirm unrelated pages stay responsive throughout.

---

## US4 — The database restarting is invisible

```powershell
docker compose restart postgres
```

with reads and writes in flight.

**Expect**: requests wait and succeed. **Then verify the transactional paths specifically** — these
are the ten restructured call sites and the highest-risk change in the feature:

- event sign-up at capacity (`EventSignupService`)
- party formation and roster changes (`PartyService`, `PartyRosterService`)
- marketplace request accept (`MarketRequestService`)
- team deletion (`TeamService`)
- event admin changes (`EventAdminService`)

```powershell
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~Parties|FullyQualifiedName~Events|FullyQualifiedName~Marketplace"
```

**Expect**: green, and specifically **no** `InvalidOperationException: The configured execution
strategy ... does not support user-initiated transactions`. That exception means a call site was
missed — it is the single most likely defect in this feature.

**Also verify no double-application**: run the concurrent sign-up / capacity race tests. A replayed
delegate that mutates state outside the block would show up as an over-capacity roster or a
duplicated signup.

---

## US5 — The next provider costs nothing extra

Add a throwaway typed client:

```csharp
builder.Services.AddHttpClient<IScratchClient, ScratchClient>()
    .AddJuggerHubResilience(builder.Configuration, "Scratch");
```

**Expect**: it times out, retries, and breaks identically to the email client with **no** resilience
code of its own, and adding `Resilience:Outbound:Scratch` to configuration changes its behaviour
without touching shared code. Remove the scratch client afterwards — it is a validation device, not
a deliverable.

---

## US6 — Operators can see what resilience is doing

Induce a retry, a timeout, and a breaker transition. **Expect** each to be visible in logs/metrics
with the pipeline name identifying the integration, and the breaker transition at a severity that
stands out. Re-read the output once more against FR-028 before signing off.

---

## Full verification before hand-off

```powershell
dotnet build backend/JuggerHub.slnx
dotnet test backend/tests/JuggerHub.Api.IntegrationTests
npm --prefix frontend run test
npm --prefix frontend run lint
npm --prefix frontend run build
npx --prefix frontend playwright test        # e2e
```

Plus the UI review checklist required by FR-011b: instantiate
`.specify/templates/ui-review-checklist-template.md` into
`specs/028-network-resilience/checklists/ui-review.md` and verify each item against the diff.
DESIGN.md wins on any conflict.

**Do not report this feature as verified on a green build alone.** Every scenario above needs a
fault induced; a passing test suite proves the code compiles and the happy path works, which is
precisely what was already true before this feature existed.
