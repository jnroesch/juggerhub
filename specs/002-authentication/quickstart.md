# Quickstart — Authentication & Account Access

Runnable end-to-end validation for feature 002. Proves a developer can register,
verify (via the local Mailpit inbox), sign in, sign out, and reset a password — all
through the containerized stack, with the security boundary enforced server-side.

> **Docker-only.** Everything runs in containers (no `ng serve`, no host runtime).
> Implementation lives in `tasks.md`; this is a *validation* guide.

## Prerequisites

- Docker + Docker Compose.
- A local `.env` (copy from `.env.sample`). Ensure `JWT_SIGNING_KEY` is set
  (≥ 32 bytes) and the `Email__*` keys resolve to Mailpit (defaults already do).

## Bring up the stack

```powershell
Copy-Item .env.sample .env   # first time only, then review values
docker compose up --build
```

- App: <http://localhost:3000>
- API docs (Development): Scalar UI from the backend
- **Mailpit inbox** (captures all local email): <http://localhost:8025>

## Happy-path journey (manual)

1. **Register** — open the app, go to **Register**, enter a new email + a password
   that satisfies the live policy indicator (8+ chars, upper/lower/digit/symbol,
   3 unique). Submit → you land on a neutral **"check your email"** screen.
2. **Verify** — open <http://localhost:8025>, open the verification email, click the
   link (→ `/verify-email?...`). The app confirms the address and routes you to
   **sign in**. *Try signing in before this step → it is refused with a "verify your
   email" message and a resend option.*
3. **Sign in** — enter the same credentials, optionally tick **Remember me**. You
   reach the protected area. Confirm a wrong password gives a single generic error,
   and that 5 wrong attempts lock the account for the lockout window.
4. **Session continuity** — stay active past the 15-minute access-token lifetime and
   perform a protected action; it succeeds via silent refresh (no visible re-login).
5. **Sign out** — use sign out; the protected area is no longer reachable and the
   auth cookies are cleared.
6. **Forgot / reset password** — from sign-in, use **Forgot password**, enter your
   email (neutral confirmation shown). Open the reset email in Mailpit, follow the
   link (→ `/reset-password?...`), set a new password. Confirm the **old password no
   longer works**, the new one does, and any prior session was invalidated.

## Security spot-checks (manual)

- **No token in storage**: with DevTools open, confirm `localStorage`/`sessionStorage`
  hold no tokens; `jh_access` / `jh_refresh` are httpOnly cookies (not script-readable).
- **Enumeration neutrality**: register an *existing* email and a *new* email — the
  responses are identical. Same for forgot-password with a known vs unknown address.
- **Generic errors**: trigger a failure and confirm the response is a generic
  `ProblemDetails` (no stack trace, internal text, secret, or token).

## Automated validation

```powershell
# Backend integration tests (xUnit + Testcontainers Postgres + test email sender)
docker compose -f docker-compose.yml -f docker-compose.test.yml run --rm backend-test

# Frontend unit tests (Jest: AuthService + interceptor)
docker compose -f docker-compose.yml -f docker-compose.test.yml run --rm frontend-test

# Frontend e2e (Playwright, desktop + mobile) — drives the full
# register→verify→login→logout→reset journey, reading links from the Mailpit API
docker compose -f docker-compose.yml -f docker-compose.test.yml run --rm playwright
```

**Expected**: backend auth suite green (register, verify-before-login gate,
login/logout, refresh rotation + reuse-detection, forgot/reset, enumeration-neutral
responses, lockout); frontend unit + e2e green across desktop and mobile viewports.

## Success = spec Success Criteria

This quickstart exercises SC-001 (full local cycle), SC-002 (verify gate),
SC-003 (enumeration neutrality), SC-004 (lockout), SC-005 (reset invalidates
sessions), SC-006 (silent refresh + reuse detection), SC-007 (no leaks),
SC-008 (single-use links), and SC-009 (responsive auth screens).
