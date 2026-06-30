# Feature Specification: Authentication & Account Access

**Feature Branch**: `002-authentication`

**Created**: 2026-06-30

**Status**: Draft

**Input**: User description: "Authentication feature — login, logout, register, password reset, email verification, and password policy. MFA is a later feature but the design must support it. Security is the paramount focus (OWASP, common advisories). Full-stack (backend, frontend, infrastructure) so the developer can run locally and create an account, log in, log out, trigger password reset, and verify email."

## Overview

This feature delivers JuggerHub's first end-user capability: members can create their own account with an email address and password, prove they own that email, sign in and out, and recover access if they forget their password. It turns the authentication *pipeline* established by the scaffold (identity store, password hashing, token validation, route guard) into real, user-facing flows.

Security is the non-negotiable priority. Every authorization and validation decision is enforced on the server; the browser is never trusted as the security boundary. The flows are designed to resist the common attacks against authentication systems — credential stuffing, account enumeration, brute force, token replay, and session fixation — and to leak nothing sensitive in responses, logs, or error messages.

Multi-factor authentication is **out of scope** for this feature, but the sign-in flow and session model are deliberately shaped so a second-factor step can be inserted later without redesigning them.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create an account and verify email ownership (Priority: P1)

A new visitor registers with their email address and a password. The system creates an unverified account and sends a verification email containing a one-time link. The visitor opens the link to confirm they own the address, which activates the account for sign-in. Until then, the account exists but cannot be used to sign in.

**Why this priority**: Account creation with proven email ownership is the entry point to the entire platform. Nothing else (sign-in, recovery) has meaning without it, and verifying email ownership before granting access is the baseline defense against fake and hijacked-address accounts.

**Independent Test**: Register a new email/password locally, observe the verification email arrive in the local mail inbox, open the link, and confirm the account becomes verified and eligible to sign in — with no ability to sign in beforehand.

**Acceptance Scenarios**:

1. **Given** a visitor on the registration screen, **When** they submit a unique email and a password meeting the policy, **Then** an unverified account is created and a verification email is sent, and they are shown a neutral "check your email" confirmation.
2. **Given** a registration request, **When** the email is already registered, **Then** the visitor sees the *same* neutral "check your email" confirmation (no indication the address already exists), and no duplicate account is created.
3. **Given** a verification email, **When** the recipient opens the verification link while it is valid, **Then** their account becomes verified and they are directed to sign in.
4. **Given** a verification link, **When** it has expired or was already used, **Then** the visitor sees a neutral message and can request a fresh verification email.
5. **Given** a password that violates the policy, **When** the visitor attempts to register, **Then** registration is rejected server-side with guidance on the unmet rules, regardless of any client-side checks.

---

### User Story 2 - Sign in and sign out (Priority: P1)

A verified member signs in with their email and password to reach the protected area of the application, optionally choosing to be remembered on the device. They can sign out, which ends the session everywhere it is held (server-side and in the browser).

**Why this priority**: Signing in and out is the core authentication value. It must honor the verification gate and the brute-force protections, and it establishes the session that every protected feature depends on.

**Independent Test**: With a verified account, sign in and reach a protected area; confirm an unverified account and wrong credentials are both refused; sign out and confirm the protected area is no longer reachable without signing in again.

**Acceptance Scenarios**:

1. **Given** a verified member, **When** they submit correct credentials, **Then** a session is established and they reach the protected area.
2. **Given** a member who has not verified their email, **When** they submit otherwise-correct credentials, **Then** sign-in is refused with a clear "verify your email" message and an option to resend the verification email — and no session is established.
3. **Given** any sign-in attempt, **When** the email is unknown or the password is wrong, **Then** the response is a single generic "invalid credentials" message that does not reveal whether the email exists.
4. **Given** repeated failed sign-in attempts for an account, **When** the configured threshold is exceeded, **Then** further attempts are refused for a lockout period, and the response does not disclose whether the account exists.
5. **Given** the sign-in screen, **When** the member selects "remember me", **Then** their session persists across browser restarts; **When** they do not, **Then** the session ends when the browser session ends.
6. **Given** a signed-in member, **When** they sign out, **Then** their session credentials are revoked server-side and cleared from the browser, and the protected area is no longer reachable without signing in again.

---

### User Story 3 - Recover a forgotten password (Priority: P1)

A member who has forgotten their password requests a reset by entering their email. If an account exists, they receive an email with a one-time, time-limited reset link. Following it lets them set a new password, after which existing sessions are invalidated and they are notified of the change.

**Why this priority**: Self-service recovery is essential to a usable authentication system and a major support-cost driver if missing. It is also a high-value attack target, so it must resist enumeration and token replay and must invalidate sessions on success.

**Independent Test**: Request a reset for a known account locally, open the reset link from the local mail inbox, set a new password, confirm the old password no longer works and the new one does, and confirm any prior session is invalidated.

**Acceptance Scenarios**:

1. **Given** the forgot-password screen, **When** a visitor submits any email address, **Then** they see the *same* neutral "if an account exists, a reset link has been sent" confirmation whether or not the address is registered.
2. **Given** a valid reset link, **When** the member sets a new password meeting the policy, **Then** the password is updated, all existing sessions for that account are invalidated, and a password-change notification email is sent.
3. **Given** a reset link, **When** it has expired or was already used, **Then** the reset is refused with a neutral message and the option to request a new link.
4. **Given** a completed password reset, **When** the member next signs in, **Then** only the new password works and the previous password is rejected.

---

### User Story 4 - Stay signed in without re-entering credentials (Priority: P2)

While a member is active, their short-lived access credential is renewed silently in the background so they are not interrupted, without weakening security. Renewal credentials are single-use and rotated on each renewal; if a renewal credential is ever replayed, the whole session lineage is invalidated and the member must sign in again.

**Why this priority**: Short-lived access credentials are a security best practice but produce a poor experience without silent renewal. Rotation with replay detection is what makes long-lived sessions safe; it depends on sign-in (US2) existing first.

**Independent Test**: Sign in, let the short-lived access credential expire, perform a protected action, and confirm it succeeds via silent renewal with no visible re-login; then simulate replay of a already-rotated renewal credential and confirm the session is forcibly ended.

**Acceptance Scenarios**:

1. **Given** a signed-in member whose access credential has expired, **When** they perform a protected action, **Then** the session is renewed silently and the action succeeds without a visible sign-in prompt.
2. **Given** a renewal, **When** it succeeds, **Then** the previous renewal credential is invalidated and replaced (single-use rotation).
3. **Given** a renewal credential that has already been rotated, **When** it is presented again, **Then** the entire session lineage is invalidated and the member is required to sign in again.
4. **Given** a member who has been idle beyond the session lifetime, **When** they return, **Then** they are routed to sign in rather than silently kept active.

---

### User Story 5 - Understand password requirements while typing (Priority: P3)

While registering or setting a new password, the member sees the password rules and live feedback on which rules are met, with the submit action enabled only once all rules are satisfied. The same rules are enforced authoritatively on the server.

**Why this priority**: Live policy feedback materially improves completion rates and reduces failed submissions, but it is a usability enhancement layered on the security-critical flows; the server-side enforcement (in US1/US3) is what actually protects the system.

**Independent Test**: Open the registration screen, retrieve the published policy, and confirm the rules render and update live as the password is typed, with submit gated until all are satisfied; confirm the server still rejects a non-compliant password if the client checks are bypassed.

**Acceptance Scenarios**:

1. **Given** the registration or reset screen, **When** it loads, **Then** the current password rules are retrieved from the server and displayed.
2. **Given** a member typing a password, **When** each rule becomes satisfied, **Then** the corresponding indicator updates live and the submit action is enabled only when all rules pass.
3. **Given** the published policy, **When** an administrator changes the policy on the server, **Then** the displayed rules reflect the change without a client release.

### Edge Cases

- **Registering an existing address**: Returns the identical neutral confirmation as a new registration (no enumeration); no duplicate account is created. The legitimate address owner may be informed via email that an account already exists.
- **Sign-in with correct password but unverified email**: Refused with a "verify your email" path (resend available). This status is revealed only when the password is otherwise correct, so it is not an enumeration vector for someone who does not know the password.
- **Account locked from repeated failures**: Further attempts are refused for the lockout window with a generic message that does not confirm the account exists; lockout is enforced server-side regardless of client state.
- **Reset or verification requested for an unknown address**: Returns the same neutral confirmation as for a known address; no email is sent to non-existent accounts and timing differences are minimized.
- **Reset/verification token expired, already used, or tampered**: Refused with a neutral message and a path to request a fresh link; tokens are time-limited and single-use.
- **Password reset or change while other sessions are active**: All existing sessions for the account are invalidated, forcing re-authentication everywhere.
- **Renewal-credential replay**: Detected and treated as compromise — the session lineage is revoked and re-authentication is forced.
- **Concurrent sessions on multiple devices**: Each device holds its own independent session; signing out (or resetting the password) behaves predictably per the rules above.
- **Any backend error in an auth flow**: Returns a generic, well-formed message with no stack trace, internal detail, secret, or token material leaked.
- **Small/mobile viewport**: All auth screens remain usable — no clipped content, no unintended horizontal scrolling, primary actions reachable.

## Requirements *(mandatory)*

### Functional Requirements

#### Registration & email verification

- **FR-001**: The system MUST let a visitor register an account with an email address and a password, creating the account in an unverified state.
- **FR-002**: The system MUST send a verification email containing a one-time, time-limited link when an account is registered.
- **FR-003**: The system MUST verify the account when a valid verification link is followed, and MUST reject expired, already-used, or tampered links with a neutral message that offers a fresh link.
- **FR-004**: The system MUST allow a visitor to request that the verification email be resent, returning a neutral confirmation regardless of whether the address is registered or already verified.
- **FR-005**: The system MUST NOT reveal, in registration or resend responses, whether an email address is already registered (enumeration protection), and MUST NOT create duplicate accounts for the same address.

#### Sign-in, sign-out & lockout

- **FR-006**: The system MUST authenticate a member by email and password and establish a session only when the credentials are correct AND the account's email is verified.
- **FR-007**: The system MUST refuse sign-in for an unverified account even with correct credentials, returning a "verify your email" outcome with a resend path, and MUST NOT establish a session.
- **FR-008**: The system MUST return a single generic failure for unknown-email and wrong-password cases that does not disclose which one occurred.
- **FR-009**: The system MUST lock an account out of sign-in after a configured number of consecutive failures for a configured period, enforced server-side, and MUST NOT disclose account existence through the lockout response.
- **FR-010**: The system MUST offer a "remember me" choice at sign-in that determines whether the session persists beyond the browser session or ends with it.
- **FR-011**: The system MUST let a signed-in member sign out, revoking the session's renewal credential server-side and clearing session credentials from the browser so the protected area is no longer reachable without signing in again.

#### Session, renewal & rotation

- **FR-012**: The system MUST issue a short-lived access credential for authenticated requests and MUST store all session credentials only where client-side scripts cannot read them (never in script-accessible browser storage).
- **FR-013**: The system MUST provide silent renewal of the short-lived access credential using a longer-lived renewal credential, so active members are not interrupted by re-authentication.
- **FR-014**: The system MUST treat renewal credentials as single-use and rotate them on every renewal (issue a new one, invalidate the old).
- **FR-015**: The system MUST detect reuse of an already-rotated renewal credential and respond by invalidating the entire associated session lineage and forcing re-authentication.
- **FR-016**: The system MUST invalidate all of an account's active sessions when its password is changed or reset.

#### Password recovery & policy

- **FR-017**: The system MUST let a member request a password reset by email and MUST send a one-time, time-limited reset link only when the address belongs to an existing account, while returning an identical neutral confirmation in all cases (enumeration protection).
- **FR-018**: The system MUST let a member set a new password via a valid reset link, MUST reject expired/used/tampered reset links with a neutral message and a path to request a new one, and MUST enforce the password policy on the new password.
- **FR-019**: The system MUST publish the active password policy so the frontend can display the rules and validate them live, and MUST enforce the same policy authoritatively on the server for every password-setting action (register, reset).
- **FR-020**: The system MUST notify the account owner by email when their password is changed or reset.

#### Security & data protection (cross-cutting)

- **FR-021**: The system MUST hash stored passwords with the platform's approved password-hashing mechanism and MUST never store, log, or return passwords in clear text.
- **FR-022**: The system MUST NOT expose stack traces, internal exception detail, secrets, or token material to the client; all auth errors MUST use the platform's generic, well-formed error format.
- **FR-023**: The system MUST enforce every authentication and authorization decision server-side; client-side checks exist only for user experience and are never the security boundary.
- **FR-024**: All verification, reset, and renewal credentials MUST be time-limited; verification and reset credentials MUST be single-use; and stored renewal credentials MUST NOT be recoverable in usable form if the data store is read (i.e. not stored in plain form).
- **FR-025**: The system MUST minimize observable differences (responses and timing) between "account exists" and "account does not exist" across registration, verification resend, and password-reset requests.

#### Multi-factor readiness (design constraint, not delivered here)

- **FR-026**: The sign-in flow and session-issuance model MUST be structured so a future second-factor (MFA) step can be inserted after primary credential verification without redesigning sign-in, session issuance, or renewal. No second factor is implemented in this feature, and a per-account second-factor capability flag MUST be retained on the account record for later use.

#### Frontend experience

- **FR-027**: The system MUST provide screens for registration, sign-in (with remember-me), forgot-password, reset-password, email-verification handling, and the post-registration "check your email" / resend and "please verify" states, styled from the shared design system and enforcing no security decisions itself.
- **FR-028**: The frontend session handling MUST attach session credentials to API calls, attempt a silent renewal on an unauthorized response (except for the auth and renewal calls themselves), and otherwise route the member to sign-in; it MUST reflect real authentication state rather than a placeholder.
- **FR-029**: All auth screens MUST be usable on both desktop and mobile viewports — no clipped content, no unintended horizontal scrolling, primary actions reachable — and MUST present clear loading, success, and error states.

#### Infrastructure & local runnability

- **FR-030**: The system MUST send transactional auth emails (verification, password reset, password-change notification) through a real email-sending path: captured by the local mail tool in local development and delivered via the configured provider in deployed environments, selected by configuration.
- **FR-031**: A developer MUST be able to run the full stack locally with the standard single-command startup and complete the entire register → verify (via the local mail inbox) → sign-in → sign-out → forgot/reset-password cycle end-to-end.
- **FR-032**: All configuration and secrets for auth and email MUST come from external configuration (committed sample for local use; real values never committed), never hard-coded.

#### Out of scope (explicitly deferred)

- **FR-033**: The following MUST NOT be implemented in this feature and are deferred: multi-factor / two-factor authentication, social / OAuth / SSO sign-in, breached-password screening (tracked separately), CI/CD and cloud infrastructure provisioning, and any roles/permissions beyond basic authenticated identity.

### Key Entities *(include if feature involves data)*

- **Account (member identity)**: A platform member who can authenticate. Holds the email address, an email-verified indicator, the hashed password, brute-force/lockout state, and a retained second-factor capability flag (unused this feature). Derives from the platform's common record base.
- **Session / Renewal Credential**: A persisted record representing one long-lived session on one device — its owner, expiry, persistence choice ("remember me"), rotation lineage (which credential replaced which), and revocation state. Stored so the raw credential is not recoverable from the data store. Enables rotation, replay detection, and bulk invalidation on password change.
- **Verification & Reset Tokens (transient)**: Time-limited, single-use, tamper-evident links proving control of an email address or authorizing a password reset. Not durable entities — they are issued, consumed once, and expire; they are never stored in a form that lets them be reissued.
- **Password Policy (read model)**: The published set of password rules the frontend renders and the server enforces; a configuration-driven view, not a per-user record.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can complete the full register → verify (via the local mail inbox) → sign-in → sign-out → forgot/reset-password cycle locally, using only the provided configuration sample and the single start command, in under 15 minutes.
- **SC-002**: 100% of sign-in attempts on unverified accounts are refused (no session established), and 100% of attempts with correct credentials on verified accounts succeed.
- **SC-003**: Registration, verification-resend, and password-reset requests return responses that are indistinguishable between existing and non-existing accounts — verified by identical response bodies/status and timing differences within a small tolerance.
- **SC-004**: After the configured number of consecutive failed sign-ins, further attempts are refused for the lockout window 100% of the time, without the response disclosing whether the account exists.
- **SC-005**: A password reset invalidates 100% of the account's pre-existing sessions, the previous password is rejected on next sign-in, and a change-notification email is delivered.
- **SC-006**: After an access credential expires, a member's next protected action succeeds via silent renewal with no visible re-login in 100% of normal cases; a replayed renewal credential forces re-authentication 100% of the time.
- **SC-007**: No client-facing response or log entry in any tested auth scenario contains a password, a stack trace, an internal exception message, a secret, or reusable token material.
- **SC-008**: Verification and reset links are accepted only while valid and only once — 100% of expired, already-used, or tampered links are refused with a neutral, recoverable message.
- **SC-009**: All auth screens render and remain fully usable across a representative desktop viewport and a representative mobile viewport (e.g. ~1280px and ~375px wide) with no clipped content or unintended horizontal scrolling.
- **SC-010**: A second-factor step can later be added to sign-in without changing the registration, sign-out, recovery, or renewal contracts — demonstrated by the sign-in/session design accommodating a documented "pending second factor" state.

## Assumptions

- **Stack and security model are fixed by the constitution**: Password hashing (argon2), session-credential storage (script-inaccessible cookies), the password policy values, account lockout thresholds, the email approach (local mail tool + deployed provider), DTO/error conventions, and scripting/containerization rules come from `.specify/memory/constitution.md` and are treated as given. This feature *uses* them; it does not re-decide them.
- **Builds on the scaffold**: The identity store, password hasher, access-credential issuance/validation, route guard, and request interceptor established by feature 001 already exist; this feature turns them into real flows and fills the gaps (recovery/rotation records, email sending, screens).
- **Password policy as-is**: The constitution's current password rules are used unchanged. Breached-password screening is a deliberate later addition (tracked in the backlog), not part of this feature.
- **Verification gate is hard**: A registered-but-unverified member cannot sign in until they verify; this is an intentional security choice over lower-friction soft gating.
- **Rotating renewal with replay detection**: Sessions use short-lived access credentials plus rotating, single-use renewal credentials with reuse detection; "remember me" selects persistent vs. session-only persistence.
- **Same-origin browser/API**: The browser app and API are served same-origin locally (via the existing proxy), so session cookies remain first-party; cross-site request forgery is mitigated primarily by strict same-site cookies, with stronger anti-forgery available as defense-in-depth if the same-origin assumption ever changes.
- **MFA is genuinely deferred**: Only readiness (flow shape + retained capability flag) is in scope; no second factor, recovery codes, or authenticator enrollment is built here.
- **Responsive by default**: Every auth screen is built and validated for desktop and mobile from the start, following the design system.
- **Local email capture**: Transactional emails are captured locally by the existing local mail tool and viewed in its inbox UI; the deployed provider is configured via environment/config without code changes.
