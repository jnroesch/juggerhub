# Data Model: Platform Admin Area (013)

One migration: `AddAccountStateAndAdminActions`. See [research.md](research.md) for
the reasoning behind each choice.

## Changed: `User` (`AspNetUsers`)

| Field | Type | Notes |
|-------|------|-------|
| `Status` | `AccountStatus` (int) | NEW. `Active = 0` (default, backfills existing rows), `Suspended = 1`, `Banned = 2`. |
| `StatusChangedAt` | `timestamp?` | NEW. UTC of the last status transition; `null` for never-touched accounts. |

- Index: none needed for auth checks (row already loaded by PK/email); the admin
  users list filters on `Status` via the profile join — add index only if measured
  slow (list is paginated and small-scale).
- First use of `AspNetRoles` / `AspNetUserRoles`: role `PlatformAdmin` created by the
  startup sync; membership mirrors `AdminOptions.NormalizedEmails` (grant + revoke).

### `AccountStatus` state machine

```
Active ──suspend──▶ Suspended ──reinstate──▶ Active
Active ──ban──────▶ Banned    ──unban──────▶ Active
Suspended ──ban──▶ Banned                (allowed; unban → Active, not back to Suspended)
```

- Transitions only via `IAdminUserService`; each writes an `AdminActionRecord` in the
  same `SaveChanges` and calls `RevokeAllForUserAsync(targetId, "suspended"|"banned")`
  when entering `Suspended`/`Banned`.
- Guard (FR-019): target in `PlatformAdmin` role (or self) ⇒ refuse suspend/ban.
- Idempotence: suspending an already-suspended account (etc.) is a no-op conflict
  (409), not a second record.

### Enforcement semantics per status

| Status | Sign-in | Refresh | Public visibility | Re-registration of email |
|--------|---------|---------|-------------------|--------------------------|
| Active | ✓ | ✓ | normal | n/a (unique email) |
| Suspended | ✗ — distinct "suspended" message after correct password | ✗ | **unchanged** (spec: fully visible) | blocked (row exists) |
| Banned | ✗ — generic failure (reveals nothing) | ✗ | hidden everywhere via query filter | blocked (row retained = denylist) |

## New: `AdminActionRecord` (`AdminActionRecords`)

Append-only log of administrative account actions (FR-017). Derives from
`BaseEntity` (UUIDv7 `Id`, `CreatedDate` = when it happened, via audit interceptor).

| Field | Type | Notes |
|-------|------|-------|
| `ActorUserId` | `Guid` FK → `AspNetUsers` | acting admin; `Restrict` delete |
| `TargetUserId` | `Guid` FK → `AspNetUsers` | affected player; `Restrict` delete |
| `Action` | `AdminAccountAction` (int) | `Suspend`, `Reinstate`, `Ban`, `Unban`, `PasswordResetSent` |
| `Note` | `text?` | reserved; no UI writes it this pass |

- Index: `(TargetUserId, CreatedDate)` for a future per-player history view.
- Never updated or deleted by application code. No read UI this pass.
- Badge/achievement attribution stays on 012's `BadgeAward` / `AchievementAward`
  (`GrantedByUserId`, `Note`, `CreatedDate`) — not duplicated here.

## New enums: `Entities/AccountEnums.cs`

```csharp
public enum AccountStatus { Active = 0, Suspended = 1, Banned = 2 }
public enum AdminAccountAction { Suspend = 0, Reinstate = 1, Ban = 2, Unban = 3, PasswordResetSent = 4 }
```

(Serialized as names over the wire — global `JsonStringEnumConverter`.)

## Global query filter (ban invisibility)

```csharp
modelBuilder.Entity<PlayerProfile>()
    .HasQueryFilter(p => p.User.Status != AccountStatus.Banned);
```

- Every player-facing read that touches `PlayerProfile` (public profile, browse,
  rosters, participant lists, recognition display) drops banned players by default.
- Admin services opt out with `IgnoreQueryFilters()` — the only opt-out sites allowed
  are inside `Services/Admin` (and 012's admin award reads if they must show banned
  subjects; default: they too hide banned players except via the admin users surface).
- Suspended players are never filtered.
- Verification duty: one integration test per public surface (SC-005) — the filter is
  the mechanism, the tests are the guarantee.

## Derived (not stored)

- **Overview stats**: `count(PlayerProfiles)` (filter applies → banned excluded),
  `count(Teams)`, `count(Events where start within 30d)`,
  `count(Users where Status == Suspended)`.
- **New players this week**: newest `PlayerProfile.CreatedDate >= now-7d`.
- **Recently granted**: newest `BadgeAward` ∪ `AchievementAward` with definition
  name, subject, grantor, date (012 data).
- **Last active / recent activity**: newest event-participation activity items
  (feature 003 `EventActivityService` data); `null` renders as "—".
