# Contracts — Authentication & Account Access

[`openapi.yaml`](./openapi.yaml) defines the `/api/v1/auth/*` surface for feature 002.

## Conventions

- **Same-origin, cookie-borne auth.** The browser only talks to the frontend origin;
  nginx proxies `/api` → backend. The access token (`jh_access`, `Path=/`) and the
  rotating refresh token (`jh_refresh`, `Path=/api/v1/auth`) are **httpOnly cookies**
  and never appear in request or response bodies.
- **Enumeration-neutral flows.** `register`, `forgot-password`, and
  `resend-verification` always return the same neutral `MessageResponse`, whether or
  not the account exists. Tests assert the responses are indistinguishable.
- **Generic errors.** Failures use the RFC7231 `ProblemDetails` envelope emitted by
  the existing exception middleware / `ProblemResponse`. No stack traces, secrets, or
  token material — ever.
- **Login outcomes.** `200` = authenticated (cookies set); `401` = generic invalid
  credentials (unknown email / wrong password / lockout, indistinguishable); `403` =
  correct password but unverified email (resend path). The unverified signal is only
  reachable with a correct password, so it is not an enumeration oracle.
- **Refresh rotation + reuse detection.** `/auth/refresh` rotates the refresh token
  (single-use). Replay of a rotated/expired token revokes the whole family and
  returns `401` with cookies cleared.

## Relationship to existing endpoints

`GET /api/v1/diagnostics/whoami` (from 001) remains the generic protected-endpoint
probe. `GET /api/v1/auth/me` is the auth-specific session probe returning the
`AuthUserDto` the frontend hydrates its state from.

## Source of truth

The contract is descriptive of the intended behavior in [`../spec.md`](../spec.md)
and [`../plan.md`](../plan.md). Where an implementation detail and this file disagree,
the spec + constitution win; update this file to match rather than drifting.
