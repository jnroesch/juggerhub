# Quickstart: Notification Preferences

## Run the stack

```powershell
docker compose up   # Postgres + Mailpit + backend + frontend
```

Migrations auto-apply on startup. Mailpit captures outbound email locally (open its web UI to read
role-change / team-news / invite emails).

## Manual end-to-end verification (maps to acceptance scenarios)

### US1 — the matrix, defaults, auto-save, enforcement
1. Sign in, open **Notification settings** (`/settings/notifications`, also linked from the avatar
   menu / account page). Every togglable channel is on by default; Security & sign-in shows as
   always-on with no toggle.
2. Turn **Team news → In-app** off. Reload — it's still off (auto-saved, no save button).
3. As an admin of a team the user is on, post team news → confirm **no in-app notification** appears
   for the user (unread badge unchanged), while other categories still notify.
4. Turn **Team news → Email** off; post news again → confirm **no team-news email** in Mailpit.
   Turn it back on, post → the email arrives.
5. As a second user, confirm your settings are independent.

### US2 — email for previously in-app-only categories
1. With **Invites & roster → Email** on, have an admin change the user's role → a **role-change
   email** arrives in Mailpit, linking to the team.
2. With **Team news → Email** on, post news → a **team-news email** arrives, linking to the team.
3. Turn the respective Email toggle off and repeat → no email; the in-app notification (if In-app on)
   still appears.
4. Confirm the emails use the shared base header/footer.

### US3 — layouts
1. On a desktop width, settings render as a **category × channel matrix**.
2. On a narrow width, the same categories render as **stacked cards** with In-app / Email chips; both
   auto-save and reflect the same state.
3. Simulate a save failure (offline) → an honest "couldn't save" state, no silent loss.

### Always-on safety
- Trigger a password reset → the email is always sent regardless of any preference; there is no
  toggle that can suppress it.

## Automated tests

```powershell
dotnet test backend/JuggerHub.slnx      # includes Notifications/PreferenceTests
cd frontend; npx nx test web
```

Backend preference tests cover: per-user scoping (no cross-user read/write, anon → 401), defaults
(no row = on), In-app off suppresses the notification, Email off suppresses the category email,
Email on sends role-change/news email, security email exempt, and fail-safe delivery on lookup error.
