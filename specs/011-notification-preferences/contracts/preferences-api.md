# Contract: Notification Preferences API

Base path: `/api/v1/notification-preferences`. All endpoints require authentication (JWT-in-cookie).
The user is always the authenticated subject — never a parameter. No cross-user access.

## REST

### GET `/api/v1/notification-preferences`
Return the caller's effective preference matrix (defaults applied for unset cells).

- 200 → `NotificationPreferenceMatrixDto`:
  ```jsonc
  {
    "categories": [
      {
        "category": "InvitesAndRoster",
        "label": "Invites & roster changes",
        "description": "Team invites, people joining or leaving",
        "channels": { "inApp": true, "email": true }
      },
      {
        "category": "TeamNews",
        "label": "Team news",
        "description": "News posted to your teams",
        "channels": { "inApp": true, "email": true }
      }
    ],
    "alwaysOn": [
      { "label": "Security & sign-in", "description": "Verification, password, and login security" }
    ]
  }
  ```
- 401 if unauthenticated.

### PUT `/api/v1/notification-preferences/{category}/{channel}`
Upsert one cell (auto-save on toggle). Idempotent.

- Route: `category` ∈ {`InvitesAndRoster`, `TeamNews`}; `channel` ∈ {`InApp`, `Email`} (by name).
- Body: `{ "enabled": true }`.
- 204 on success.
- 400 if `category`/`channel` is unknown.
- 401 if unauthenticated.

> There is intentionally no endpoint to toggle the always-on Security & sign-in group — it is not
> a preference. A PUT naming an unknown category returns 400.

## Enforcement (behavioral contract, not an endpoint)

- Before an **in-app** notification is created for a recipient, `NotificationService` checks
  `IsEnabled(recipient, categoryOf(type), InApp)`; if off, no notification row is created and the
  unread count does not change.
- Before a **category email** is sent, the producer checks `IsEnabled(recipient, category, Email)`;
  if off, no email is sent.
- A preference-lookup failure defaults to **deliver** and never fails the originating action.
- Security/sign-in email ignores preferences entirely.

## Authorization invariants (tested)

- GET/PUT act only on the authenticated user's rows; there is no way to read or write another user's.
- Anonymous → 401 on all endpoints.
- In-app off for a category ⇒ that category's next event creates 0 notifications for the user.
- Email off for a category ⇒ that category's next event sends 0 emails for the user.
- Email on for team-role-change / team-news ⇒ a well-formed base-template email is sent.
- Toggling the invite category's Email off ⇒ no invite email; In-app still governed separately.
