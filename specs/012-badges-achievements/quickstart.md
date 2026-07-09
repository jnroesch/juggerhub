# Quickstart & Validation: Badges & Achievements

End-to-end validation for feature 012. Proves the manual-award MVP works: an admin defines and grants a badge/achievement, it shows on the profile and team page, duplicates are prevented, revoke hides it, and non-admins are refused server-side.

See [contracts/openapi.yaml](./contracts/openapi.yaml) for the API surface and [data-model.md](./data-model.md) for entities. This is a run/validation guide — no implementation code here.

## Prerequisites

- Stack up: `docker compose up -d --build` (database, backend, frontend, Mailpit).
- Backend auto-applies the `AddBadgesAndAchievements` migration on startup.
- At least two accounts: one **admin** (its email listed in the admin allowlist) and one **non-admin**. Plus a player profile handle and a team slug to grant to.

## Configure the temporary admin allowlist

The admin gate is a config allowlist (interim — real role is #21). Set the admin email(s) via env, consistent with other config/secrets:

```
# .env (local) — docker-compose maps ADMIN_EMAILS → the backend's Admin__Emails
ADMIN_EMAILS=admin@test.de
```

> Local development default is `admin@test.de` (already in `.env.sample`). Register an
> account with this email to act as the platform admin locally. Multiple admins can be
> comma-separated. Deployed environments set their own value in their GitHub Environment.

`.NET` reads `Admin__Emails` → `Admin:Emails` (`AdminOptions`). Deployed environments set this in the GitHub Environment, never committed. Restart the backend after changing.

## Admin UI walkthrough (local)

In Development the backend seeds a fixed catalogue (badges: Beta tester, Fair play, Founding club, Trainer; achievements: Champion, 50 trainings) plus a couple of sample grants, so the display and picker have content immediately.

1. Register/sign in as `admin@test.de`. The account menu (avatar, top-right) shows an **Admin panel** entry — it is hidden for non-admins.
2. Open **/admin**. Choose **Player** or **Team**, enter a `@handle` / team slug, and **Load**.
3. Their current badges & achievements show with **Revoke**. Click **Assign** → pick from the Badges/Achievements catalogue (already-held items are marked **Given**), add an optional note, and **Grant**.
4. Open the player's profile (`/u/<handle>`) or the team page (`/t/<slug>`) — the grant appears; a revoke removes it.

> The admin UI is **grant-from-a-fixed-catalogue** (per the Admin wireframe: "admins pick, not create"). Creating/editing badge *types* is available on the backend API (`POST /admin/badges`, etc.) but has no v1 UI by design.

## Scenario 1 — Admin defines and grants a badge (US1, SC-001)

1. Sign in as the admin. Create a badge:
   - `POST /api/v1/admin/badges` with `{ name: "Beta Tester", description: "Was here early", appliesToPlayers: true, appliesToTeams: true }` → **201**.
   - Upload art: `PUT /api/v1/admin/badges/{id}/icon` with a small PNG → **204**.
2. Grant to a player: `POST /api/v1/admin/badges/{id}/awards` with `{ playerHandle: "<handle>" }` → **201**.
3. Grant to a team: same endpoint with `{ teamSlug: "<slug>" }` → **201**.

**Expected**: both grants succeed; each response is an `Award` with `source: Manual`, `status: Active`.

## Scenario 2 — Display on profile and team page (US2, SC-003)

1. Open the player profile at `/u/<handle>` (public) and as the owner.
2. Open the team page at `/t/<slug>`.

**Expected**: the "Beta Tester" badge renders in the badges area (replacing the old "Coming soon" stub) with icon, name, description, earned date. Achievements render in their own group. A subject with none shows the empty state, not a blank/broken area.

## Scenario 3 — Duplicate prevention (SC-004)

- Re-grant the same badge to the same player: `POST /api/v1/admin/badges/{id}/awards` `{ playerHandle }` again → **409 Conflict** (generic problem body).

**Expected**: no second active award; the profile still shows exactly one.

## Scenario 4 — Subject-type mismatch (FR-005)

1. Create a players-only badge: `{ ..., appliesToPlayers: true, appliesToTeams: false }`.
2. Try to grant it to a team (`{ teamSlug }`) → **400** (generic problem, no stack trace).

## Scenario 5 — Achievement with context

- Create an achievement: `POST /api/v1/admin/achievements` `{ name: "Champion", description: "...", appliesToTeams: true, appliesToPlayers: false }`.
- Grant with context: `POST /api/v1/admin/achievements/{id}/awards` `{ teamSlug, contextYear: 2026, contextLabel: "National Championship" }` → **201**.

**Expected**: the team page shows the achievement with its year/label context.

## Scenario 6 — Revoke (SC-005)

- `DELETE /api/v1/admin/badges/awards/{awardId}` with `{ reason: "granted in error" }` → **204**.

**Expected**: the badge disappears from the public profile/team page immediately; the row is retained as `Revoked` (auditable), and the same badge can be granted again afterward (a new active award).

## Scenario 7 — Server-side authorization (US1 #5, SC-002, SC-006) 🔒

Run these as the **non-admin** account (and unauthenticated), calling the API **directly** (not via the UI):

- `POST /api/v1/admin/badges` → **403** (authenticated non-admin) / **401** (anonymous).
- `POST /api/v1/admin/badges/{id}/awards` → **403 / 401**.
- `DELETE /api/v1/admin/badges/awards/{awardId}` → **403 / 401**.

**Expected**: every define/grant/revoke path is refused server-side regardless of the UI. This is the core security check — the config gate is the boundary, not any client guard.

## Automated test coverage (what to assert)

- **Backend integration (xUnit + Testcontainers)**: create/edit/retire definition; grant to player and team; duplicate → 409; subject-type mismatch → 400; retire keeps existing awards; revoke hides + retains; **non-admin and anonymous → 403/401 on every admin route**; public profile/team payload includes active-only awards.
- **Frontend Jest**: badge/achievement display components render list + empty state; admin forms validate applicability (at least one subject type).
- **Playwright (desktop + mobile)**: admin grants a badge → it appears on the profile and team page; revoked badge no longer shows.

## Out of scope to validate (v1)

Automatic/criteria-based awarding (US3) is deferred — there is nothing to evaluate. A real system-admin role (#21) is not part of this feature; only the config gate is validated here.
