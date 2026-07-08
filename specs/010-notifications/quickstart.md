# Quickstart: In-App Notification System

## Run the stack

```powershell
docker compose up            # Postgres + Mailpit + backend + frontend (local)
```

Migrations auto-apply on backend startup (`ApplyMigrationsAsync`). The dev seeder runs in
Development. The frontend dev server proxies `/api` and `/hubs` (WebSocket upgrade) to the backend.

## Manual end-to-end verification (maps to acceptance scenarios)

### US1 — inbox + unread badge
1. Sign in as a seeded user. The bell shows no badge when there are nothing unread (honest empty
   state at `/alerts`).
2. Trigger a notification (see US2/US3). The bell badge appears with the unread count; `/alerts`
   lists it newest-first with icon, title, supporting line, relative time, unread marker.
3. Mark it read (row action) or "mark all read" — badge decrements/clears.
4. As a *different* user, confirm you never see the first user's notifications.

### US2 — invite with inline actions
1. As a team admin, targeted-invite another user (existing team invite flow).
2. Sign in as the invitee → `/alerts` shows a `TeamInvite` row naming the team + inviter with
   **Accept** / **Decline**.
3. Accept → you join the team; the row resolves (actions removed). Repeat a fresh invite and
   Decline → invite declined, row resolves.
4. Accept/Decline an already-expired/revoked invite → clear "no longer available"; row resolves.

### US3 — role change + team news
1. As admin, change the invitee's role → they get a `TeamRoleChanged` notification linking to
   `/t/{slug}`.
2. As admin, post team news from the team space (new **Post news** control) → every *other* member
   gets a `TeamNews` notification; you (the author) do not.
3. Non-admin attempts to post news → refused (403).

### US4 — realtime
1. Keep `/alerts` open as user B. As user A, trigger a notification for B → it appears and the badge
   increments **without a refresh**.
2. Stop the backend WS (or block `/hubs`) and navigate to `/alerts` → data still loads correctly
   over REST (graceful degradation).
3. Sign out and confirm the hub handshake is rejected (no anonymous stream).

## Automated tests

```powershell
# Backend integration tests
dotnet test backend/JuggerHub.slnx

# Frontend unit tests
cd frontend; npx nx test web
```

Key backend tests live under `tests/JuggerHub.Api.IntegrationTests/Notifications/`:
per-recipient scoping (404 for other users), unread count, mark-read/-all idempotency, each
producer creates the right notification, actor is not self-notified, non-admin news POST → 403.
