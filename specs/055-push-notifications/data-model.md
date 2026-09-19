# Data Model: Push Notifications (055)

**One new table, one migration.** The preference model absorbs the third channel with no schema
change at all.

## New entity: `PushSubscription`

One row is one browser on one machine that a player has enabled. Modelled on `RefreshToken`, which
is the same kind of thing — a per-device credential owned by exactly one account.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | From `BaseEntity`, UUIDv7 (Principle III) |
| `CreatedDate` / `ModifiedDate` | `DateTime` | From `BaseEntity`, set by `AuditFieldsInterceptor` |
| `UserId` | `Guid` | Owner. FK to `User`, `DeleteBehavior.Cascade` |
| `Endpoint` | `string`, max 512, required | The push service URL the browser issued. **Unique index.** Stored verbatim — unlike `RefreshToken.TokenHash` it cannot be hashed, because it must be sent back to deliver |
| `P256dh` | `string`, max 128, required | The device's public key, base64url, as the browser produced it |
| `Auth` | `string`, max 64, required | The device's auth secret, base64url |
| `DeviceLabel` | `string`, max 64, nullable | A recognisable label derived from the browser, for the settings page only. Never parsed |
| `LastSuccessAt` | `DateTime`, nullable | Touched on every successful delivery. **Indexed** — the stale sweep deletes by it |
| `User` | navigation | |

### Indexes

- `Endpoint` — **unique**. Load-bearing, not hygiene: a device that changes hands re-registers and
  **moves** to the new account rather than existing twice. This is what makes the shared-device case
  (spec FR-020) correct by construction.
- `UserId` — every dispatch loads a recipient's devices by it.
- `LastSuccessAt` — serves the retention sweep. `AppDbContext.cs:585-606` records why this index is
  not optional: the refresh-token table *"is exactly the one that grew without bound before the
  sweep existed"*.

### Lifecycle

| Event | Effect |
|---|---|
| Player enables this browser | Row inserted, or an existing row for that endpoint reassigned to the caller |
| Successful delivery | `LastSuccessAt` touched through the change tracker, so audit fields run |
| Push service answers `404` or `410` | Row deleted, no retry (FR-021) |
| Player turns this device off | Row deleted |
| Player signs out | Row deleted, best-effort, plus a local browser unsubscribe |
| Unreachable beyond the retention window | Deleted by `StalePushSubscriptionSweep` (FR-022) |
| Account deleted | Deleted by `EraseOwnedDataAsync`, beside `Notifications` and `NotificationPreferences` (FR-023) |

**This table IS owned data and belongs in `EraseOwnedDataAsync`** — the opposite of
`TermsAcceptance`, whose entity carries a warning saying it must never be added there. The
distinction is that a device subscription exists solely to serve the member and has no audit
purpose that survives them.

## Changed: `NotificationChannel`

```
InApp = 0,
Email = 1,
Push  = 2,   // new
```

**No migration.** `NotificationPreference` is sparse — a row exists only for a cell the player
explicitly set, and a missing row means on. A third channel therefore adds possible cells for every
account without touching a single stored row. The unique index on `(UserId, Category, Channel)`
(`AppDbContext.cs:577`) already covers the new value.

The enum's XML doc currently ends *"Push is out of scope"*. That sentence is deleted here; it was
feature 011's deferral note, and this feature is the deferral being taken up.

## Changed: `PreferenceChannelsDto`

`(bool InApp, bool Email)` gains `bool Push`. Its mirror in
`frontend/apps/web/src/app/core/models/notification-preferences.models.ts` gains the same field, the
`ChannelKey` union gains `'push'`, and `channelIdOf` gains its mapping. No endpoint changes: the
existing `PUT /notification-preferences/{category}/{channel}` binds the channel from the route as an
enum (`NotificationPreferencesController.cs:42-44`), so a new member is routable the day it exists.

## Not added

- No device list, no device naming, no per-device category preference (spec FR-027).
- No delivery log, no dedupe table (research R5 — `tag` and `Topic` carry once-only).
- No outbox (research R1).
