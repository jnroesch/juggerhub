# Quickstart Validation: Platform Admin Area (013)

Runnable scenarios proving the feature end-to-end. Contracts:
[contracts/admin-api.md](contracts/admin-api.md) · data:
[data-model.md](data-model.md).

## Prerequisites

```powershell
# stack up (Postgres, Mailpit, backend, frontend)
docker compose up -d
# .env must contain the admin sync source, e.g.:
#   ADMIN_EMAILS=admin@test.de
```

Backend tests / build:

```powershell
dotnet test backend/tests/JuggerHub.Api.IntegrationTests   # includes new Admin/* suites
dotnet build backend
```

Frontend:

```powershell
npx nx test web
npx nx e2e web-e2e   # includes admin-area.spec.ts
npx nx build web
```

## Scenario 1 — role sync + fail closed (US1)

1. Register `admin@test.de` and a second account `player@test.de` (verify both via
   Mailpit at `http://localhost:8025`).
2. Restart the backend (sync runs at startup). Log shows the grant for
   `admin@test.de`.
3. `GET /api/v1/admin/access` as admin → 200; as player → 403; anonymous → 401.
4. Remove `ADMIN_EMAILS`, restart → log warns "no platform administrators"; admin now
   gets 403 (mirror semantics + fail closed).

## Scenario 2 — gated entry + overview (US2)

1. Sign in as admin (desktop viewport): lock-marked **Admin** item after the normal
   nav links → opens `/admin`: shield header, sidebar, four stats, "New players this
   week", "Recently granted", search box.
2. Mobile viewport: no fifth tab; account menu shows the "Admin panel" row.
3. Sign in as player: no admin entry anywhere; `/admin` typed directly → redirected
   home (and the API refuses regardless).
4. Overview numbers match seeded data; typing in the search box lands on
   `/admin/users?q=...` with the query applied.

## Scenario 3 — find & open (US3)

1. `/admin/users`: search `mira` (name), `@ada` (handle), and a team name — each
   returns the right rows with teams, status, admin marker, badge count.
2. Filter Suspended / Banned; combine with search; page forward/back — "Showing X–Y
   of Z" stays truthful.
3. Open a row → `/admin/users/{handle}`.

## Scenario 4 — account help (US4)

1. On `player@test.de`'s detail: **Suspend** (confirm) → status chip flips; as the
   player: sign-in now refused with a clear "suspended" message, but their public
   profile `/u/{handle}` is still visible to others (spec: suspend blocks login
   only). **Reinstate** → sign-in works.
2. **Send reset link** → Mailpit shows the standard reset email; admin UI only
   confirms "sent".
3. **Ban** (confirm) → as others: `/u/{handle}` 404s, player gone from browse, team
   roster, event participants; sign-in refused generically; registering a new account
   with that email gets the neutral response and creates nothing. **Unban** → all of
   it returns intact (memberships, badges, content).
4. Suspend/ban attempted against an admin account or yourself → refused with the
   "remove from admin configuration first" explanation (API: 422).
5. Integration tests assert an `AdminActionRecord` row per action (actor, target,
   action, timestamp).

## Scenario 5 — Assign picker (US5)

1. On a player's detail: awards listed with grantor + date; **Assign** opens the
   picker (badges / achievements tabs; already-held marked "Given" and disabled).
2. Grant one with a note → appears on the detail and in the overview's "Recently
   granted" with attribution; the note is on the award (012 log semantics).
3. Revoke it (confirm) → gone. Double-grant attempt (API) → refused.
4. `/admin/catalogue` still serves the full 012 management surface.

## Expected regression suites

- `Recognition/*` integration tests pass unchanged after the authorization swap
  (SC-008) — only test *setup* may change (role instead of allowlist).
- Full backend suite + `nx test web` + e2e green before merge.
