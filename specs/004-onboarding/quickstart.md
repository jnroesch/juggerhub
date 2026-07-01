# Quickstart: First-Login Onboarding Flow

A runnable, end-to-end validation guide. It proves the feature works against the real stack. Implementation details live in `plan.md` / `data-model.md` / `tasks.md`, not here.

## Prerequisites

- Docker + `docker-compose` (the standard local stack: backend, frontend, PostgreSQL, Mailpit).
- The `004-onboarding` branch checked out.
- The `AddOnboardingCompletedAt` migration present (auto-applies on backend startup).

## Bring the stack up

```powershell
docker compose up --build
```

- Frontend: http://localhost:4200 (or the compose-mapped port)
- Backend API: http://localhost:8080/api/v1 (or mapped)
- Mailpit (verification emails): http://localhost:8025

## Scenario A — First login routes into onboarding (US1, US2)

1. Register a new account with a fresh email, password, and handle at `/register`.
2. Open Mailpit, click the verification link → the verify screen shows success.
3. Sign in at `/sign-in` with the new credentials.
   - **Expected**: you are redirected to `/onboarding` (not the dashboard), starting on the **Welcome** screen.
4. Click **Get started** → **display name** step is pre-filled with your handle, with the "Handle @… stays the same" hint.
   - Clear the field → **Continue is disabled/blocked** (display name is the only required step).
   - Enter a display name → Continue.
5. **City** → type a hometown → Continue. **Pompfen** → select a few (incl. Läufer) → Continue. **Team** → observe it is a clear placeholder; pick a sample team (this will NOT persist) → Continue. **Photo + bio** → add a small valid image + a short bio → **Finish**.
6. On **Done**, click the primary button.
   - **Expected**: you land on the dashboard.
7. Open your profile (`/profile`) and your public page (`/u/<handle>`).
   - **Expected**: display name, city, bio, selected pompfen, and the picture are all present. The **team selection is absent** (never persisted).

## Scenario B — Shown only once (US1)

1. Sign out, then sign in again with the same account.
   - **Expected**: you land on the dashboard directly; onboarding does **not** reappear.
2. Manually navigate to `/onboarding`.
   - **Expected**: you are redirected to the dashboard (already-onboarded guard).

## Scenario C — Dismiss without completing (US3)

1. Register + verify a second fresh account; sign in.
2. On the **Welcome** screen, click **I'll do this later**.
   - **Expected**: you land on the dashboard; nothing was written to the profile.
3. Sign out and back in.
   - **Expected**: onboarding does **not** reappear (dismissal is permanent).
4. Open `/profile` → you can still set every field normally.

## Scenario D — Skip individual steps (US3)

1. Register + verify a third account; sign in → onboarding.
2. Set only the display name; **Skip** city, pompfen, team, and photo+bio; Finish.
   - **Expected**: only the display name is saved; other fields keep defaults; onboarding is marked complete.

## Scenario E — Server is the authority (US1, security)

1. As a signed-in, already-onboarded user, call the API directly:
   ```powershell
   curl -i -X POST http://localhost:8080/api/v1/profiles/me/onboarding/complete --cookie "auth_token=<your-cookie>"
   ```
   - **Expected**: `204`, and the stored `OnboardingCompletedAt` is **unchanged** (idempotent).
2. Call it with no cookie:
   - **Expected**: `401` (owner-only; the client flag is never the authority).
3. Inspect `GET /api/v1/auth/me` → `onboardingCompleted` is `true` for this account and `false` for a freshly-registered one.

## Automated checks

- **Backend** (Testcontainers): `docker compose -f docker-compose.tests.yml up` (or the project's test entrypoint) runs the `Onboarding/` integration tests — owner-only + idempotent complete, flag on `/me` and `/login`, migration applies.
- **Frontend unit** (Jest): onboarding step-machine (required-name gate, skip advances without write, Back preserves values, team never persisted).
- **Frontend e2e** (Playwright): `onboarding.spec.ts` — register → verify → first login → onboarding → complete → relogin lands on dashboard; run at desktop and mobile viewports.

## Teardown

```powershell
docker compose down -v
```
