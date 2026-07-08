# Research: Authentication & Account Access

Phase 0 output. The constitution and the 001 scaffold fix the stack and the
primitives; this resolves the **how** of the security-sensitive integration points.
Each item is a concrete decision with rationale and the alternatives rejected.
No `NEEDS CLARIFICATION` remain (the three highest-impact decisions were settled
with the user before the spec: password policy as-is, hard verify-before-login,
rotating-refresh + remember-me).

---

## 1. Sign-in path — reveal "unverified" only after a correct password

**Decision**: Do **not** use `SignInManager.PasswordSignInAsync` for the primary
check. Instead call `SignInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)`
and then explicitly gate on `user.EmailConfirmed`:

1. Look up the user by normalized email. If none → return the generic
   invalid-credentials failure (still do a dummy password hash to even out timing).
2. `CheckPasswordSignInAsync` validates the password **and** drives Identity
   lockout (`AccessFailedCount`/`LockoutEnd`). On `Lockout` or `Failed` → generic
   invalid-credentials failure.
3. On password success, **then** check `EmailConfirmed`. If false → return the
   distinct "verify your email" result (with resend path). If true → issue tokens.

**Critical config**: `IdentityOptions.SignIn.RequireConfirmedEmail` MUST stay
**`false`**. `CheckPasswordSignInAsync` runs the same `PreSignInCheck` →
`CanSignInAsync` as `PasswordSignInAsync`, so with `RequireConfirmedEmail = true` it
would *also* short-circuit to `NotAllowed` for unconfirmed accounts **before** the
password is checked — reintroducing the enumeration oracle. Leaving it `false` makes
`CanSignInAsync` return true (so only lockout can block pre-password), and the
verify-before-login gate is enforced by the explicit `EmailConfirmed` check in step 3.
(The earlier plan wording "flip `RequireConfirmedEmail = true`" was corrected to this
during implementation — see the security rationale below.)

**Rationale**: `PasswordSignInAsync` runs `CanSignInAsync` (which enforces
`RequireConfirmedEmail`) **before** the password is checked, so it returns
`NotAllowed` for any unconfirmed account regardless of password — an enumeration
oracle (an attacker learns the address exists and is unverified without knowing the
password). Splitting the checks means the "unverified" signal is only ever exposed
to a caller who already proved knowledge of the password (i.e. the legitimate user),
satisfying spec US2/AS2 and FR-007 while preserving FR-008/FR-025 enumeration
neutrality for everyone else. It also keeps lockout (FR-009) intact via
`lockoutOnFailure: true`.

**MFA seam**: the second factor slots in exactly between steps 2 and 3's success and
token issuance — see §7.

**Alternatives considered**:
- *`PasswordSignInAsync` + map `NotAllowed`→verify* — rejected; leaks unverified
  status pre-password (enumeration).
- *Block at `CanSignInAsync` and return generic for unverified too* — rejected; then
  a legitimately-registered user gets no actionable "verify your email" path.

---

## 2. Rotating, single-use refresh tokens with family reuse-detection

**Decision**: Add a persisted `RefreshToken : BaseEntity`. The raw token is a
256-bit cryptographically-random value (`RandomNumberGenerator.GetBytes(32)`,
base64url) sent only in the `jh_refresh` httpOnly cookie; the database stores only
its **SHA-256 hash** (`TokenHash`), never the raw value. Each token belongs to a
**family** (`FamilyId`, shared across a login's rotation chain) and records
`ReplacedByTokenId`, `ExpiresAt`, `IsPersistent` (remember-me), `RevokedAt`, and
`UserId`.

`/auth/refresh` flow: hash the presented cookie value, look it up.
- Not found / expired / already revoked → **treat the whole family as compromised**:
  revoke every token in the family and return 401 (force re-login). This is the
  reuse-detection response (FR-015).
- Found, active, not expired → mint a new access JWT, create a new `RefreshToken` in
  the **same family** with `ReplacedByTokenId` back-link, mark the old one
  `RevokedAt = now`, set the new refresh cookie. Single-use rotation (FR-014).

Sliding lifetime is bounded by an absolute family cap (e.g. refresh max 14 days
persistent / 1 day session) so a family can't be renewed forever.

SHA-256 (not argon2) is correct here because the token is already 256 bits of
full-entropy randomness — there is nothing to brute-force, so a fast hash that
prevents "DB read → usable token" is the right tool; argon2 is reserved for
low-entropy human passwords.

**Rationale**: Rotating + reuse-detection is the OWASP-recommended pattern for
long-lived sessions: a stolen refresh token is single-use, and the moment either the
attacker or the victim replays a rotated token the entire family is invalidated,
bounding the damage. Storing only a hash means a database compromise does not yield
usable session credentials (FR-024, A02:2021 Cryptographic Failures).

**Alternatives considered**:
- *Stateless/self-contained refresh JWT* — rejected; can't be revoked server-side
  (needed for logout, password reset, reuse detection).
- *Store raw token* — rejected; DB read would hand over live sessions.
- *No rotation (long-lived refresh)* — rejected; a single theft = indefinite access.

---

## 3. Cookies — two httpOnly cookies, path-scoped refresh, remember-me persistence

**Decision**: Keep the existing `jh_access` cookie (access JWT, ~15 min,
`SameSite=Strict`, `HttpOnly`, `Secure` env-driven, `Path=/`). Add `jh_refresh`
carrying the raw refresh token with the same flags **except `Path=/api/v1/auth`**
(so the long-lived secret is only ever sent to the auth/refresh endpoints, never on
every API call). Remember-me drives persistence:
- **Remember me on** → both cookies set with an `Expires` (persistent); refresh
  family absolute cap ~14 days.
- **Remember me off** → cookies set **without** `Expires` (session cookies, cleared
  on browser close); refresh family cap ~1 day.

Server-side `RefreshToken.ExpiresAt`/`IsPersistent` remain the real boundary; the
cookie's persistence is only the browser-side convenience (never the security
boundary).

**Rationale**: Minimizes exposure of the highest-value secret (refresh token) by
path-scoping it; satisfies FR-010 remember-me and FR-012 (script-inaccessible
storage). Reuses the existing `AuthCookieDefaults` pattern.

**Alternatives considered**:
- *Single cookie for both* — rejected; refresh token would ride every request,
  widening exposure.
- *`localStorage` for tokens* — forbidden by the constitution; XSS-exfiltratable.

---

## 4. Email-verification & password-reset tokens (Identity data-protection providers)

**Decision**: Use Identity's built-in token providers via `UserManager`:
`GenerateEmailConfirmationTokenAsync` / `ConfirmEmailAsync` and
`GeneratePasswordResetTokenAsync` / `ResetPasswordAsync`. These are signed,
tamper-evident, time-limited, and single-use (consumption rotates the user's
`SecurityStamp`, invalidating the token).

Lifespans: the default data-protection token lifespan is 1 day (good for email
verification, FR-002/FR-003). Password-reset links must be shorter (FR-018), so
register a **dedicated short-lived reset provider** and point
`IdentityOptions.Tokens.PasswordResetTokenProvider` at it with a ~1-hour lifespan
(the default provider's lifespan is global, so a separate provider is the clean way
to give reset its own clock).

**Links**: emails link to the SPA, not the API:
`{FrontendBaseUrl}/verify-email?userId={id}&token={urlEncodedToken}` and
`{FrontendBaseUrl}/reset-password?userId={id}&token={urlEncodedToken}`. The token is
`Base64Url`-encoded for transport (Identity tokens contain `+`/`/`); the SPA reads
the query params and POSTs them to `/auth/verify-email` / `/auth/reset-password`.
`userId` is the UUIDv7 (safe to expose; unguessable enough per the constitution).

**Rationale**: Reuses Identity's vetted, framework-maintained token machinery
(no hand-rolled crypto); single-use + expiry + tamper-evidence come for free and
satisfy FR-024/SC-008. Linking to the SPA keeps the user in one consistent UI and
lets the frontend render success/expiry/resend states.

**Alternatives considered**:
- *Custom signed tokens / JWTs as links* — rejected; reinvents what Identity
  provides and is easier to get wrong.
- *Link directly to an API GET that redirects* — rejected; a GET that mutates state
  is unsafe (prefetch/scanner can consume the token) and bypasses the SPA's UX.

---

## 5. Email sending — `IEmailSender` abstraction (MailKit/Mailpit local, Resend deployed)

**Decision**: Introduce `IEmailSender { Task SendAsync(string to, string subject, string htmlBody, CancellationToken) }`
with two implementations selected by `Email:Provider`:
- **`SmtpEmailSender`** (`Email:Provider=Smtp`, local) — **MailKit** `SmtpClient` to
  Mailpit (`Email:SmtpHost=mailpit`, `Email:SmtpPort=1025`, no auth/TLS locally).
- **`ResendEmailSender`** (`Email:Provider=Resend`, Dev/Prod) — typed `HttpClient`
  POSTing to the Resend REST API with `Email:Resend:ApiKey`.

Register the **existing** `EmailTemplateService` in DI (currently unregistered) as
`IEmailTemplateService`. A thin `AuthEmailService` composes the two: render the HTML
(verification / password-reset / password-change-notification / welcome templates)
+ build the SPA link, then hand off to `IEmailSender`. Bind a new `EmailOptions`
(`Provider`, `FromAddress`, `SmtpHost`, `SmtpPort`, `Resend:ApiKey`, `FrontendBaseUrl`).

**Rationale**: The scaffold generates email HTML but never sends it; this closes the
gap (FR-030) while honoring the constitution's "Mailpit local / Resend deployed,
selected by config" rule with zero code differences across environments. MailKit is
the maintained, recommended SMTP client (`System.Net.Mail.SmtpClient` is flagged
"don't use for new development"). Resend over `HttpClient` avoids adding an SDK
dependency. Email send failures are logged and, for the enumeration-neutral flows,
do **not** change the generic client response.

**Alternatives considered**:
- *`System.Net.Mail.SmtpClient`* — rejected; deprecated guidance.
- *Resend official SDK* — viable, but a typed `HttpClient` keeps dependencies minimal.
- *Send synchronously inside the request and surface failures* — rejected for the
  neutral flows (would leak existence via error/timing); failures are logged instead.

---

## 6. Enumeration neutrality & timing

**Decision**: `register`, `forgot-password`, and `resend-verification` always return
the **same** generic success (HTTP 200 with a neutral message), whether or not the
account exists or is already verified (FR-005/FR-017/FR-025). To flatten timing:
on the "user exists" path do the real work (token gen + email send); on the "absent"
path perform comparable throwaway work (e.g. a dummy password-hash / token-gen cost)
and skip the actual send. Perfect constant-time is not claimed — the goal is to
remove the *obvious* large differences a scanner keys on. For registering an existing
address, optionally send the real owner an "account already exists / did you mean to
reset?" email instead of creating a duplicate (anti-enumeration + helpful).

**Rationale**: Directly satisfies the enumeration-protection FRs and SC-003. Login
failures are likewise collapsed to one generic message (§1). This is the standard
OWASP guidance for auth response uniformity (A07:2021 Identification & Authentication
Failures).

**Alternatives considered**:
- *Return 404/409 when the email is unknown/taken* — rejected; textbook enumeration.

---

## 7. MFA-readiness (no MFA delivered)

**Decision**: Model sign-in's outcome as a small result type rather than a bare
bool/token: `AuthOutcome { Succeeded, RequiresEmailVerification, PendingTwoFactor, Failed }`.
Today the flow goes password-OK → email-confirmed → **issue tokens**. The code path
is structured so a future MFA feature inserts a single branch after the email-confirmed
check: if `user.TwoFactorEnabled`, return `PendingTwoFactor` (with a short-lived,
narrowly-scoped interim token) instead of full tokens, and add a `/auth/2fa` endpoint
to complete it. `TwoFactorEnabled` (native Identity column) is retained and never
removed. No second factor, enrollment, or recovery codes are built now (FR-026/FR-033).

**Rationale**: Satisfies SC-010 — a second factor can be added without changing the
register/logout/recovery/refresh contracts. Keeping it a result type now avoids a
later breaking reshape of `IAuthService`.

**Alternatives considered**:
- *Boolean success now, refactor later* — rejected; guarantees a later breaking change
  to the sign-in contract.
- *Build MFA now* — out of scope per the user.

---

## 8. Session invalidation on password change/reset

**Decision**: On password reset (and any future change), `ResetPasswordAsync` rotates
the `SecurityStamp`; additionally **revoke all of the user's refresh-token families**
(`ExecuteUpdateAsync` setting `RevokedAt` + `ModifiedDate` explicitly, since the
interceptor is bypassed). Outstanding `jh_access` JWTs remain technically valid until
their ≤15-minute expiry — an accepted residual given the short lifetime — but no
session can be **renewed** (refresh revoked), so access is lost within the access-token
window. A change-notification email is sent (FR-020).

**Rationale**: Meets FR-016/SC-005 (pre-existing sessions invalidated) with a bounded,
clearly-documented residual rather than the heavier machinery of validating the
security stamp on every request. If stricter immediacy is later required, add a
security-stamp validation step to JWT validation; noted, not built.

**Alternatives considered**:
- *Per-request `SecurityStamp` validation (revoke access instantly)* — deferred; more
  per-request cost than warranted at 15-minute access lifetime.

---

## 9. CSRF posture with cookie-borne auth

**Decision**: Primary control = `SameSite=Strict` on both auth cookies + the
same-origin nginx `/api` proxy (the browser only ever talks to the frontend origin),
so cross-site requests don't carry the cookies. This is the same posture 001 chose.
Note (defense-in-depth, **not** built here): if the same-origin assumption is ever
relaxed (e.g. a separate API origin needing `SameSite=None`), add a double-submit
CSRF token on state-changing auth POSTs. Documented as an assumption + future option.

**Rationale**: `SameSite=Strict` + first-party same-origin already defeats classic
CSRF for these flows; adding token machinery now would be unjustified complexity
against the current architecture (constitution: justify added complexity).

**Alternatives considered**:
- *Anti-forgery tokens now* — rejected as premature given strict same-site same-origin.

---

## 10. Frontend single-flight refresh interceptor

**Decision**: Upgrade `authInterceptor` so a 401 (on a non-auth request) triggers a
**single** call to `/auth/refresh`; concurrent 401s share that one in-flight refresh
(a shared `Observable`/promise) and then retry once. Skip refresh entirely for the
auth endpoints themselves (`/auth/login`, `/auth/register`, `/auth/refresh`,
`/auth/forgot-password`, `/auth/reset-password`, `/auth/verify-email`,
`/auth/resend-verification`). If refresh fails → `AuthService.signOut()` (clear
client state) + route to `/sign-in`. `AuthService` tracks real state via signals
(`currentUser`, `isAuthenticated`) hydrated from `/auth/me`.

**Rationale**: Realizes the scaffold's documented interceptor TODO and FR-028 while
avoiding a refresh stampede (N concurrent 401s must not fire N refreshes, which would
trip reuse-detection and log the user out). The server stays the security boundary;
this is UX continuity only.

**Alternatives considered**:
- *Refresh per failed request* — rejected; concurrent refreshes would rotate the token
  multiple times and trigger false reuse-detection.
- *Proactive timer-based refresh* — viable later; reactive-on-401 is simpler and
  sufficient now (can be added without contract change).

---

## 11. Testing strategy

**Decision**: Backend — extend the existing xUnit + `WebApplicationFactory` +
`Testcontainers.PostgreSql` project with an auth test suite and a **test
`IEmailSender`** (captures `(to, subject, html)` in-memory) so tests can extract the
verification/reset token from the rendered link and complete the flow without SMTP.
Cover: register (+ neutral response for existing email), verify-before-login gate,
login success/failure/lockout, refresh rotation + reuse-detection (replay → family
revoked), forgot/reset (+ old-password rejected, sessions revoked), and
enumeration-neutral bodies. Frontend — Jest for `AuthService` + interceptor
(single-flight refresh); Playwright e2e (desktop + mobile projects) drives the full
register→verify→login→logout→reset journey against the dockerized stack, reading the
verification/reset links from the **Mailpit REST API** (`GET :8025/api/v1/messages`).

**Rationale**: Reuses the 001 harness and the Docker-only mandate; the test sender
keeps backend tests hermetic while the Mailpit-API-driven e2e proves the *real* SMTP
send path end-to-end (SC-001).

**Alternatives considered**:
- *Mock `UserManager`/`SignInManager` in unit tests* — kept minimal; integration
  against real Identity + Postgres catches the wiring that matters here.

---

## 12. `.env.sample` / config reconciliation

**Decision**: Align `.env.sample` and `appsettings*.json` with what the backend
actually reads. The sample currently mixes stale placeholders
(`Jwt__Secret`, `Jwt__AccessTokenMinutes`, `cursor-template` issuer/audience) with
the real `JWT_*` / `Jwt__*` vars the code binds. Remove the stale ones, document the
`Email__*` keys (`Provider`, `FromAddress`, `SmtpHost`, `SmtpPort`,
`Resend__ApiKey`, `FrontendBaseUrl`) with safe local defaults, and add an `Email`
section to `appsettings.json`. docker-compose already injects
`Email__SmtpHost/Port/Provider`; add `Email__FromAddress` and a frontend base URL.

**Rationale**: Principle V/secret-management — local config must match reality so a
fresh clone runs first try (SC-001); stale placeholders are a footgun.

**Alternatives considered**:
- *Leave the sample as-is* — rejected; misleads and breaks zero-step startup.

---

## Resolved unknowns

All Technical Context items are resolved; **no `NEEDS CLARIFICATION` remain**. MFA,
OAuth/SSO, and HIBP breach-screening (GitHub issue #11) are intentionally out of scope
and tracked separately.
