# Contracts — First-Login Onboarding Flow

`openapi.yaml` documents the **one new endpoint** and the **one DTO delta** this feature adds under `/api/v1`. It is a **design contract** for planning and test authoring, not a generated artifact. All profile *persistence* reuses feature 003's endpoints unchanged (see `specs/003-profile/contracts/`).

## New / changed surface

| Method | Path | Auth | Purpose |
|---|---|---|---|
| POST | `/api/v1/profiles/me/onboarding/complete` | **owner (JWT)** | **NEW** — mark the signed-in user's onboarding complete. Idempotent. `204` / `404` (no profile) / `401`. |
| — | `AuthUserDto` | — | **EXTENDED** — gains `onboardingCompleted: boolean`. Returned by `POST /auth/login`, `GET /auth/me`, `POST /auth/refresh`. |

## Reused (unchanged) surface the flow calls

| Method | Path | Auth | Used for |
|---|---|---|---|
| PUT | `/api/v1/profiles/me` | owner (JWT) | Save display name, hometown (city), description (bio), pompfen set |
| PUT | `/api/v1/profiles/me/avatar` | owner (JWT) | Upload the profile picture |
| POST | `/api/v1/auth/login` | anonymous → sets cookie | Returns `AuthUserDto` incl. `onboardingCompleted` — drives the first-login redirect |
| GET | `/api/v1/auth/me` | owner (JWT) | Session hydrate — carries `onboardingCompleted` |

## Security invariants (assert in tests)

- `POST /me/onboarding/complete` requires a valid JWT (`401` otherwise) and acts **only** on the authenticated subject's profile — never a client-supplied id (SC-006).
- The endpoint is **idempotent**: a second call after completion returns success and does **not** move the original timestamp (edge case: "marking complete more than once").
- `onboardingCompleted` is derived server-side from `OnboardingCompletedAt != null`; the client value is never trusted as the authority for gating (SC-008). It is returned only to the authenticated subject (not an enumeration oracle).
- The onboarding flow has **no** endpoint that mutates the handle; `PUT /profiles/me` continues to ignore/reject any handle field (FR-023).
- The team step calls **no** endpoint (persists nothing — FR-021).
