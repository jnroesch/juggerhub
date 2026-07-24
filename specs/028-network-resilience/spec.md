# Feature Specification: Network Resilience

**Feature Branch**: `feat/028-network-resilience`

**Created**: 2026-07-24

**Status**: Draft

**Input**: User description: "A generic resilience system for JuggerHub covering two hops: (1) browser → backend HTTP, and (2) backend → external services (Resend email today, more providers later)."

## Clarifications

### Session 2026-07-24

- Q: When an email cannot be delivered after all immediate attempts, how durable must delivery be? → A: Immediate in-process retry only. The failure is recorded in operational logs; **no** stored delivery record and no background dispatcher. Durable, restart-surviving delivery is deferred to its own follow-up feature. This feature therefore introduces no persistent data at all.
- Q: While a retry is in progress, is anything shown to the person? → A: Silent for a short initial period, then a quiet inline note inside the existing loading treatment ("Still loading…"). No noise in the common case, honest when a retry is actually taking time. Because DESIGN.md currently has no loading/error/retry guidance, this feature must add that entry and run the UI review checklist.

## Why this exists

An audit of the current code found no general retry or timeout policy anywhere in
the product. What exists today is a handful of targeted mechanisms:

- a single sign-in interceptor that retries **once** after refreshing an expired session
  (401 only);
- automatic reconnect on the two real-time sockets (chat, notifications), which re-seed
  their state after a reconnect;
- a one-shot page reload when a stale app bundle can't load a lazy chunk;
- per-screen "try again" buttons that the person has to press themselves.

Everything else is a single attempt. The consequences are user-visible:

- A request that never comes back leaves a spinner turning **forever** — there is no
  client-side time limit at all.
- A one-second blip during a deploy turns a working page into an error card, even for a
  plain read that could simply have been asked again.
- A single failure at the email provider **permanently loses** that email. Someone who
  registers and never receives their verification mail has no path forward except to
  guess and try again.
- A database restart during a rolling deploy surfaces as a generic failure.

Each of these has been patched locally, or not at all, and every new outbound provider
would repeat the same gap. This feature replaces the ad-hoc patchwork with **one
reusable resilience layer per hop, applied by default**, so a transient fault becomes a
brief pause rather than an error or lost work — while a genuine failure still fails
fast, visibly, and honestly.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A brief hiccup doesn't break the page (Priority: P1)

Someone is browsing teams, opening an event, or loading their home dashboard when the
connection wobbles for a moment — a deploy is rolling, a pod is restarting, or their
phone switches from wifi to mobile data. Instead of an error card, the app quietly asks
again and the content appears. If it truly can't load, they get an honest, actionable
message with a way to retry — never an endless spinner.

**Why this priority**: This is the most common failure the community actually
experiences, and today it always degrades into either an error state or an infinite
spinner. It affects every screen, and it is the one improvement people will notice.

**Independent Test**: Fully testable by making the backend briefly unavailable (or
slow) while loading any read-only screen and confirming the content still arrives; and
by making the backend hang and confirming the screen resolves to an actionable error
within a bounded time instead of spinning indefinitely.

**Acceptance Scenarios**:

1. **Given** a read request fails once with a transient network or server fault,
   **When** the person is on any screen that loads data, **Then** the app retries
   automatically and the content appears without the person pressing anything.
2. **Given** a request that never receives a response, **When** the configured time
   limit elapses, **Then** the request is abandoned and the screen shows an actionable
   error with a retry, rather than remaining in a loading state.
3. **Given** all automatic attempts have been exhausted, **When** the last one fails,
   **Then** the existing error-and-retry treatment for that screen is shown, and the
   person can retry manually.
4. **Given** an action that changes data (creating a team, sending a message, RSVPing),
   **When** it fails for any reason, **Then** it is **never** retried automatically and
   the person is told what happened, so no action is ever silently performed twice.
5. **Given** a request that fails because it was rejected (not found, not allowed,
   invalid input, rate limited), **When** the failure is received, **Then** the app
   fails immediately without retrying, so bad requests are not amplified.
6. **Given** the person's session has expired, **When** a request returns unauthorized,
   **Then** the existing refresh-and-retry behaviour runs exactly as it does today and
   the new retry layer does not add attempts on top of it.
7. **Given** a retry succeeds quickly, **When** the content loads, **Then** nothing extra
   was shown — the retry was invisible and the screen looked like a normal load.
8. **Given** a retry is still in progress past the visibility threshold, **When** the
   person is waiting, **Then** the existing loading treatment gains a quiet note telling
   them it's still working, without a banner, overlay, or layout shift.

---

### User Story 2 - A transactional email is not lost to a provider blip (Priority: P1)

Someone registers, is invited to a team, or is contacted about an event. The email
provider is briefly unavailable. The email still arrives, because the system tries again
rather than dropping it after one failure. If it genuinely cannot be delivered, that
fact is recorded and visible to operators rather than disappearing silently.

**Why this priority**: A lost verification email is a dead end — the person cannot use
their account and has no way to recover it themselves. This is the highest-consequence
failure in the product, and it currently happens on a single provider error.

**Independent Test**: Fully testable by making the email provider return a transient
failure for the first attempts and confirming the email is still delivered, and by
making it fail persistently and confirming the outcome is recorded and surfaced rather
than swallowed.

**Acceptance Scenarios**:

1. **Given** the email provider returns a transient failure, **When** an email is sent,
   **Then** the system tries again with increasing delays and the email is delivered
   without anyone intervening.
2. **Given** the email provider is unreachable or hangs, **When** the configured time
   limit elapses, **Then** the attempt is abandoned rather than holding the operation
   open indefinitely.
3. **Given** the provider rejects the message for a permanent reason (invalid address,
   rejected credentials), **When** the response is received, **Then** the system does
   **not** retry and records the failure.
4. **Given** every attempt has failed, **When** the send is finally given up on, **Then**
   the failure is recorded with enough detail for an operator to see which message to
   which recipient failed and why — never silently swallowed.
5. **Given** an email send fails, **When** it was part of a user-facing action such as
   registration, **Then** the action's own outcome is unchanged and no internal detail
   about the provider reaches the person.

---

### User Story 3 - A provider outage doesn't take the app down with it (Priority: P2)

The email provider is having a sustained outage. Rather than every request piling up
against a dead service — making the app slow for everyone and hammering a service that
is already struggling — the system notices, stops trying for a while, fails fast, and
then probes carefully to see whether it has recovered.

**Why this priority**: Without this, the retry behaviour added in Story 2 makes an
outage *worse*: more attempts against a failing service, more threads held open, slower
responses for unrelated users. Retry without a stop condition is a hazard, so this
follows Story 2 closely.

**Independent Test**: Fully testable by holding the provider in a failing state and
confirming that, after a threshold, calls stop being attempted and fail immediately;
then restoring the provider and confirming normal operation resumes automatically.

**Acceptance Scenarios**:

1. **Given** an outbound service fails repeatedly beyond a configured threshold, **When**
   the next call is made, **Then** it fails immediately without contacting the service.
2. **Given** the system has stopped calling a failing service, **When** a recovery
   interval passes, **Then** it makes a limited trial call and resumes normal operation
   if that call succeeds.
3. **Given** an outbound service is in a failing state, **When** users perform actions
   that would use it, **Then** those actions are not left waiting on it, and the rest of
   the app keeps working normally.
4. **Given** an outage across the whole system, **When** many retries are in flight,
   **Then** the total number of attempts stays bounded rather than multiplying the
   normal load.

---

### User Story 4 - The database restarting is invisible (Priority: P2)

A deploy rolls, or the database restarts. Requests in flight during those seconds
currently surface as failures. Instead, they briefly wait and succeed.

**Why this priority**: This happens on every deploy and is entirely recoverable, but it
is narrower in blast radius than Stories 1–3 and its correctness constraints need care
around multi-step operations.

**Independent Test**: Fully testable by restarting the database while requests are in
flight and confirming they complete successfully rather than returning errors.

**Acceptance Scenarios**:

1. **Given** the database is briefly unavailable or refusing connections, **When** a
   request needs it, **Then** the request waits and retries within a bounded window and
   succeeds once the database is back.
2. **Given** a fault occurs partway through a multi-step operation, **When** it is
   retried, **Then** the operation either completes exactly once or fails cleanly —
   never applies a change twice or leaves data half-written.
3. **Given** the database is genuinely down beyond the retry window, **When** the window
   is exhausted, **Then** the request fails with the standard generic error and no
   internal detail reaches the client.

---

### User Story 5 - Adding the next provider costs nothing extra (Priority: P2)

A developer adds a second outbound service — payments, a maps lookup, a push provider.
They get the standard timeout, retry, and stop-when-failing behaviour by opting in, not
by re-deriving and re-implementing it, and not by copying the email code.

**Why this priority**: This is the "generic system" part of the request and the reason
this is a feature rather than four patches. The behaviour must be defined once so the
next integration inherits it, but it delivers no user-visible value on its own.

**Independent Test**: Fully testable by adding a throwaway outbound client that opts
into the standard behaviour, then confirming it times out, retries, and stops calling a
failing service identically to the email client — with no resilience logic of its own.

**Acceptance Scenarios**:

1. **Given** a new outbound service integration, **When** it is registered using the
   standard approach, **Then** it inherits the shared timeout, retry, and
   stop-when-failing behaviour with no bespoke code.
2. **Given** an integration with different needs (slower service, stricter limit),
   **When** it is registered, **Then** its limits can be adjusted through configuration
   without rewriting the shared behaviour.
3. **Given** the shared behaviour is changed once, **When** the change is deployed,
   **Then** every integration that opted in picks it up.

---

### User Story 6 - Operators can see what resilience is doing (Priority: P3)

When something is slow or failing, an operator can tell from logs and metrics alone how
often requests are being retried, how often they time out, and whether the system has
stopped calling a downstream service — without reproducing the problem.

**Why this priority**: Essential for operating the system and for tuning the limits
chosen here, but it does not change what any user experiences.

**Independent Test**: Fully testable by inducing retries, timeouts, and a
stop-when-failing state and confirming each is visible in logs/metrics with the affected
operation identified.

**Acceptance Scenarios**:

1. **Given** a request is retried, **When** it succeeds or is finally given up on,
   **Then** the attempt count and outcome are recorded.
2. **Given** a timeout occurs, **When** it is recorded, **Then** the record identifies
   which operation timed out and after how long.
3. **Given** the system stops calling a failing service or resumes, **When** the state
   changes, **Then** the change is recorded at a severity that makes it noticeable.
4. **Given** any of these records, **When** they are written, **Then** they contain no
   secrets, credentials, tokens, or personal message content.

---

### Edge Cases

- **Retries stacking on the session refresh** — a request that fails as unauthorized
  already triggers a refresh and one retry. The new layer must not multiply that into
  several refresh cycles, and the two paths must have a single, defined interaction.
- **Retry storms during an incident** — if every client retries every request while the
  backend is struggling, the retries become the outage. Total attempts must stay bounded
  under sustained failure, on both hops.
- **Rate-limit responses** — the chat rate limiter deliberately fails closed and returns
  a rejection. Retrying that would make the limiting worse, so rejections must be
  excluded from retry, and any wait-and-retry hint from the server must be honoured
  rather than ignored.
- **The person navigates away mid-retry** — an abandoned screen must not keep retrying
  in the background, and its eventual result must not overwrite newer content.
- **A request that timed out may still have been processed** — the client cannot know
  whether a request that never answered was applied. This is exactly why actions that
  change data are never retried automatically.
- **Repeated failure of a real-time socket** — the chat and notification sockets
  currently give up after a short series of attempts, leaving a permanently dead
  connection until the page is reloaded. What happens after that series must be defined,
  since live updates silently stopping is worse than a visible error.
- **The device goes offline entirely** — retrying against no network is pointless noise;
  the system needs a defined behaviour rather than burning through attempts.
- **The first request of a session** — resilience must not delay the initial sign-in or
  session check so much that starting the app feels slow.
- **A slow but working service** — a service answering just under the time limit must
  not be treated as failing, and the limit must not be so tight that legitimate slow
  operations are cut off.
- **Clock and configuration drift across environments** — limits are configuration, so a
  misconfigured environment must fall back to safe defaults rather than, say, retrying
  without bound.

## Requirements *(mandatory)*

### Functional Requirements

#### Browser → backend

- **FR-001**: Every request the app makes to the backend MUST have a bounded time limit,
  after which it is abandoned and reported as a failure. No request may remain
  outstanding indefinitely.
- **FR-002**: Requests that only read data MUST be retried automatically when they fail
  for a transient reason (no response, connection lost, or a server-side "temporarily
  unavailable" response), up to a bounded number of attempts.
- **FR-003**: Delays between attempts MUST increase and MUST include a randomised
  component, so that many clients failing at once do not retry in lockstep.
- **FR-004**: Requests that create, change, or delete data MUST NOT be retried
  automatically under any circumstance.
- **FR-005**: Responses indicating the request itself was rejected — not found, not
  permitted, invalid, conflicting, or rate limited — MUST NOT be retried.
- **FR-006**: Where the server indicates how long to wait before retrying, that
  instruction MUST be honoured in preference to the default delay, and MUST still be
  bounded by the overall attempt limit.
- **FR-007**: The existing expired-session behaviour (refresh once, retry once, otherwise
  sign out) MUST be preserved exactly, and the retry layer MUST NOT add further attempts
  to requests already being retried for that reason.
- **FR-008**: Sign-in, registration, session-refresh, and other authentication requests
  MUST retain their current single-attempt behaviour, since the caller handles their
  failures deliberately.
- **FR-009**: When automatic attempts are exhausted, the failure MUST surface to the
  calling screen unchanged, so existing error-and-retry treatments continue to work
  without modification.
- **FR-010**: Retrying MUST stop immediately when the request is no longer needed —
  for example, when the person navigates away or a newer request supersedes it.
- **FR-011**: A retry MUST be invisible for a short initial period, so that the common case
  adds no visual noise. If loading is still in progress after that threshold, the app MUST
  show a quiet note within the **existing** loading treatment — never a new overlay, banner,
  or toast, and never a layout shift.
- **FR-011a**: That note MUST follow the established voice — addressed to "you", sentence
  case, no emoji — and MUST NOT rely on colour alone to convey its meaning.
- **FR-011b**: Because DESIGN.md has no loading, error, or retry state guidance today, this
  feature MUST add that guidance to DESIGN.md before the treatment is built, and MUST run
  the UI review checklist against the result (constitution Quality Gate 7).
- **FR-012**: The real-time chat and notification connections MUST continue attempting to
  reconnect rather than giving up permanently, and MUST re-seed their state on every
  successful reconnect as they do today. When live updates are not available, the app
  MUST remain correct through ordinary requests.
- **FR-013**: Resilience behaviour MUST NOT be configured per call site. Screens and
  services MUST get it by default, without opting in individually.

#### Backend → external services

- **FR-014**: Every outbound call to an external service MUST have a bounded time limit,
  both per attempt and across all attempts together.
- **FR-015**: Outbound calls that fail for a transient reason MUST be retried
  automatically with increasing, randomised delays, up to a bounded number of attempts.
- **FR-016**: Outbound calls that fail for a permanent reason — rejected credentials,
  invalid input, a permanently refused recipient — MUST NOT be retried.
- **FR-017**: When an external service fails repeatedly beyond a threshold, the system
  MUST stop calling it for a defined interval and fail those calls immediately, then
  probe with a limited trial call before resuming normal operation.
- **FR-018**: The resilience behaviour MUST be defined once and reusable, so a new
  outbound integration inherits it by opting in rather than by implementing its own.
- **FR-019**: Limits (time, attempts, thresholds, intervals) MUST be adjustable per
  integration through configuration, without changing the shared behaviour.
- **FR-020**: The final outcome of an outbound call MUST NOT leak provider detail,
  credentials, or internal errors to any user-facing response, per the never-leak rule.
- **FR-021**: When an email cannot be delivered after all immediate attempts, the system
  MUST record the failure in operational logs with the recipient, the kind of message, and
  the reason, at a severity that makes it noticeable. It MUST NOT store the message for
  later delivery and MUST NOT attempt delivery again outside the originating operation —
  durable delivery is deliberately deferred to a follow-up feature (see Out of Scope).
- **FR-021a**: This feature MUST NOT introduce any persistent data. All resilience state
  (attempt counts, service health) is in-memory and per-process, and is expected to reset on
  restart.
- **FR-022**: A failed outbound call MUST NOT change the outcome of the user-facing
  action it accompanies where that action is already designed to be neutral (for
  example, registration and forgotten-password flows deliberately reveal nothing).

#### Database

- **FR-023**: Requests MUST survive a brief database interruption — a restart, a
  connection reset, or a refused connection during a deploy — by waiting and retrying
  within a bounded window.
- **FR-024**: Operations spanning multiple steps MUST remain correct under retry: each
  MUST either complete once in full or fail cleanly, never partially apply or apply
  twice.
- **FR-025**: When the retry window is exhausted, the request MUST fail with the standard
  generic error, with no internal detail reaching the client.

#### Cross-cutting

- **FR-026**: Under sustained failure, the total number of attempts MUST stay bounded and
  MUST NOT multiply the normal request volume beyond a defined factor.
- **FR-027**: Every retry, timeout, and stop-calling state change MUST be recorded, with
  the affected operation identified and the outcome stated.
- **FR-028**: Recorded resilience events MUST NOT contain secrets, credentials, tokens,
  personal message content, or full request bodies.
- **FR-029**: Behaviour MUST be identical across local, Dev, and Prod, differing only in
  configured values — per the environment-parity principle.
- **FR-030**: Missing or invalid resilience configuration MUST fall back to safe built-in
  defaults rather than disabling limits or retrying without bound.
- **FR-031**: The existing fail-closed rate limiter MUST keep failing closed, and its
  rejections MUST be excluded from every retry path on both hops.

### Key Entities

**None of these are stored data.** Per FR-021a this feature persists nothing; the terms
below exist so the rest of the specification has consistent vocabulary.

- **Resilience profile** — a named set of limits (time limit, attempt count, delay
  growth, failure threshold, recovery interval) that an integration or request class is
  governed by. Configuration.
- **Attempt outcome** — what is reported about a single try: which operation, which
  attempt number, how long it took, and whether it succeeded, was retried, or was given
  up on. Telemetry.
- **Service health state** — whether a given external service is being called normally,
  is being skipped after repeated failures, or is being probed for recovery. In-memory
  and per-process; resets on restart.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: During a routine deployment, someone using the app sees **zero** error
  screens caused by the deployment itself while reading content.
- **SC-002**: **100%** of requests resolve to either content or an actionable error
  within a bounded time — no screen can be left loading indefinitely.
- **SC-003**: A read interrupted by a transient fault succeeds without the person taking
  any action in at least **95%** of cases where the interruption lasts under five
  seconds.
- **SC-004**: **Zero** actions that change data are performed twice as a result of
  automatic retries.
- **SC-005**: At least **99%** of transactional emails are delivered despite a transient
  provider failure that would previously have lost them outright.
- **SC-006**: **100%** of emails that ultimately fail appear in operational logs naming
  the recipient and the reason; none fail silently. (Recovering such an email still
  requires the person to request it again — durable delivery is out of scope here.)
- **SC-007**: During a total outage of an external service, the number of calls made to
  it stays within **twice** the normal call volume rather than growing with retries.
- **SC-008**: A database restart during a deployment produces **zero** user-visible
  errors for requests already in flight.
- **SC-009**: Adding a new outbound integration requires **no** new resilience logic —
  the standard behaviour is inherited in a single registration step.
- **SC-010**: An operator can determine the count of retries, timeouts, and
  stop-calling events for any time window from telemetry alone, without reproducing the
  problem.
- **SC-011**: The added resilience introduces **no measurable delay** to requests that
  succeed on the first attempt.

## Assumptions

- **Never retrying data-changing actions** is taken as settled, per the feature request.
  Making such actions safely repeatable (so they could be retried) is explicitly out of
  scope; if it is wanted later it is its own feature.
- **Which failures count as transient** follows industry-standard practice: no response,
  connection failures, timeouts, and the standard "temporarily unavailable" and
  "gateway" server responses. Everything else is treated as permanent.
- **Concrete limit values** (seconds, attempt counts, thresholds) are treated as
  configuration to be set during planning, not fixed by this specification. They must be
  tunable per environment without behavioural difference.
- **The two real-time sockets already re-seed correctly** on reconnect, and live updates
  are an enhancement rather than the source of truth. This feature changes how long they
  keep trying, not that invariant.
- **The rate limiter's fail-closed behaviour is intentional** and stays as it is. This
  feature only ensures its rejections are never retried against.
- **Inbound resilience is out of scope** — this feature does not change how the backend
  responds under its own load (queueing, shedding, or advertising wait times beyond what
  already exists).
- **Offline-first behaviour is out of scope** — no request queueing for later replay, no
  local write buffer. Losing the network means a clear failure, not deferred work.
- **DESIGN.md currently has no guidance for loading, error, or retry states.** This gap
  was found while writing this specification and is now **closed by this feature**
  (FR-011b) rather than worked around: the loading/retry state entry is added to DESIGN.md
  first, then built against, then checked with the UI review checklist.
- **Durable email delivery is knowingly deferred.** In-process retry closes the
  short-blip case, which is the common one. An outage longer than the retry window, or a
  restart mid-retry, still loses the message — it will now be logged loudly instead of
  silently, and recovery is the person requesting it again. Accepted as a follow-up.
- **The constitution has no resilience section.** If the behaviour defined here becomes a
  standing engineering rule, it warrants a constitution amendment; that is a planning
  decision, not a spec one.
- **Existing per-screen error-and-retry treatments stay.** This feature reduces how often
  people see them; it does not replace them.

## Dependencies

- The existing session-refresh interceptor, whose unauthorized behaviour must be
  preserved and coordinated with.
- The existing real-time chat and notification connections and their reconnect and
  re-seed behaviour.
- The existing fail-closed rate limiter and its shared counters.
- The email provider integration, which is the only outbound service today and the first
  consumer of the shared outbound behaviour.
- The existing generic error handling that keeps internal detail away from clients.

## Out of Scope

- **Durable email delivery** — storing a message before sending, retrying it after a
  restart, or dispatching it from a background worker. Deferred to its own feature
  (**GitHub #70**); the multi-replica deployment means such a dispatcher needs
  coordination to avoid sending the same message twice, which is a design problem of its
  own.
- Any new persistent data or schema change (FR-021a).
- Making data-changing actions safely repeatable so they could be retried.
- Offline support, request queueing for later replay, or local write buffering.
- Inbound load management (request shedding, queueing, backpressure).
- Any screen change beyond the quiet inline note in FR-011 and the DESIGN.md entry that
  governs it.
- Replacing or adding external service providers.
