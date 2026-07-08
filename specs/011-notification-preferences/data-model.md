# Data Model: Notification Preferences

## Entity: `NotificationPreference`

Derives from `BaseEntity` (UUIDv7 id, audit timestamps). **Sparse**: a row exists only for a
(user, category, channel) cell the user has explicitly set. Absence of a row means the opt-out
default — **enabled** (on). This keeps the model extensible: new categories/channels introduce new
possible cells without migrating anyone; a user with no row is simply notified everywhere.

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | UUIDv7 (BaseEntity). |
| `UserId` | `Guid` (FK → `AspNetUsers`, cascade) | Owner. All reads/writes scoped to this. Required. |
| `Category` | `NotificationCategory` (enum → int) | The user-facing toggle group. |
| `Channel` | `NotificationChannel` (enum → int) | `InApp` or `Email`. |
| `Enabled` | `bool` | The set value for this cell. |

### Indexes

- Unique `(UserId, Category, Channel)` — one cell per user; upsert target; also the per-user read.

### Lifecycle

- User 1—* NotificationPreference (cascade delete with the account).
- No FK to producers/teams — categories are a fixed reference enum, not data.

## Enums (added to `NotificationEnums.cs`)

```text
NotificationCategory : int   // user-facing toggle groups (togglable only)
  InvitesAndRoster = 0        // TeamInvite + TeamRoleChanged
  TeamNews         = 1        // TeamNews

NotificationChannel : int
  InApp = 0
  Email = 1
```

Serialized by name (global `JsonStringEnumConverter`).

### Type → Category map (helper, not stored)

A static `NotificationCategories.For(NotificationType)`:

| NotificationType (feature 010) | Category |
|--------------------------------|----------|
| `TeamInvite` | `InvitesAndRoster` |
| `TeamRoleChanged` | `InvitesAndRoster` |
| `TeamNews` | `TeamNews` |

New producers map their type here without schema change.

## Effective resolution

`IsEnabledAsync(userId, category, channel)`:
1. Look up the (userId, category, channel) row.
2. If present → its `Enabled`. If absent → **true** (default on).
3. On any lookup error → **true** (fail-safe: deliver, never drop), logged.

`GetEnabledRecipientsAsync(userIds, category, channel)` (news fan-out): one query fetching the
*disabled* cells among the candidates for that (category, channel), then return `userIds` minus the
disabled set — so defaults (no row) stay included.

## DTOs (client-facing)

- `NotificationPreferenceMatrixDto`:
  ```jsonc
  {
    "categories": [
      { "category": "InvitesAndRoster", "label": "Invites & roster changes",
        "description": "Team invites, people joining or leaving",
        "channels": { "inApp": true, "email": true } },
      { "category": "TeamNews", "label": "Team news",
        "description": "News posted to your teams",
        "channels": { "inApp": true, "email": false } }
    ],
    "alwaysOn": [
      { "label": "Security & sign-in", "description": "Verification, password, and login security" }
    ]
  }
  ```
  Labels/descriptions are server-owned so the two form factors and any future client share one source.
- `SetPreferenceRequest`: `{ "enabled": bool }` (the category + channel are route parameters).

## Migration

`AddNotificationPreferences` — creates `NotificationPreferences` with the unique
`(UserId, Category, Channel)` index and the cascade FK to the user. No backfill (absence = default).
