# Phase 1 Data Model: First-Login Onboarding Flow

This feature adds **one nullable column** to an existing table and **one field** to an existing DTO. No new entities, tables, or relationships are introduced. All other data used by the flow belongs to feature 003 and is reused unchanged.

## Entity change

### `PlayerProfile` (existing — `backend/Entities/PlayerProfile.cs`)

| Field | Type | Nullable | Notes |
|-------|------|----------|-------|
| `OnboardingCompletedAt` | `DateTime?` (`timestamptz`) | yes | **NEW.** `null` = onboarding not yet completed/dismissed; a UTC timestamp = completed at that instant. Set once (idempotent); never reset by this feature. |

- No other `PlayerProfile` fields change. Existing fields (`Handle` immutable, `DisplayName`, `Hometown`, `Description`, `Pompfen`, `Avatar`) are written by the flow **through the existing 003 endpoints**, not by any new code here.
- **Persistence config** (`AppDbContext`): a plain nullable `DateTime?` maps to a nullable `timestamp with time zone` under Npgsql with no explicit configuration required. No index (the value is read only for the current authenticated user, already reachable via the `UserId` unique relationship). Verify no fluent config is needed during implementation; add only if the default mapping is unsatisfactory.
- **Audit fields**: `ModifiedDate` is updated automatically by `AuditFieldsInterceptor` when completion is saved via a tracked update (no manual set).

### Migration

- **Name**: `AddOnboardingCompletedAt`
- **Effect**: `ALTER TABLE "PlayerProfiles" ADD COLUMN "OnboardingCompletedAt" timestamptz NULL;`
- **Data impact**: All existing rows default to `NULL` (not onboarded). Acceptable — production has no live users; any local/dev accounts will simply see onboarding on their next sign-in.
- **Application**: auto-applies on startup across local/Dev/Prod (existing behavior).

## State transition

```
                      complete flow  OR  dismiss ("I'll do this later" / leave)
   OnboardingCompletedAt = NULL  ─────────────────────────────────────────────▶  OnboardingCompletedAt = <utc now>
        (not onboarded)                POST /profiles/me/onboarding/complete           (onboarded — terminal)
                                              (idempotent: a second call is a no-op; first timestamp stands)
```

- The only transition is `NULL → timestamp`. There is no supported reverse transition in this feature (no "reset onboarding").
- `OnboardingCompleted` (the DTO boolean) is defined as `OnboardingCompletedAt != null`.

## DTO changes

### `AuthUserDto` (existing — `backend/Dtos/Auth/AuthResponses.cs`)

```
AuthUserDto(Guid Id, string Email, bool EmailConfirmed, bool OnboardingCompleted)   // + OnboardingCompleted
```

- Populated in `AuthService.LoginAsync`, `RefreshAsync`, and `GetUserAsync` (the `/auth/me` backing) via
  `user.Adapt<AuthUserDto>() with { OnboardingCompleted = await _profiles.HasCompletedOnboardingAsync(user.Id, ct) }`.
- Carries no sensitive data; only ever returned to the authenticated subject.

### Frontend model `AuthUser` (existing — `frontend/.../core/models/auth.models.ts`)

```ts
export interface AuthUser {
  id: string;
  email: string;
  emailConfirmed: boolean;
  onboardingCompleted: boolean;   // NEW — mirrors AuthUserDto
}
```

### No change to profile DTOs

`OwnerProfileDto` / `PublicProfileDto` / `UpdateProfileRequest` are **unchanged**. Onboarding state is exposed on the auth surface only, not on the profile read/write surface (it is not a public or editable profile field).

## Service surface (interface deltas)

`IProfileService` gains two methods (implementation in `ProfileService`):

| Method | Shape | Behavior |
|--------|-------|----------|
| `HasCompletedOnboardingAsync(Guid userId, CancellationToken)` | `Task<bool>` | Projected `AsNoTracking` read: `true` iff the user's profile has `OnboardingCompletedAt != null`. `false` if no profile. |
| `CompleteOnboardingAsync(Guid userId, CancellationToken)` | `Task<CompleteOnboardingStatus>` | Loads the caller's profile (tracked); if `OnboardingCompletedAt` is `null`, set to `DateTime.UtcNow` + save; else no-op. Returns `Completed` or `ProfileNotFound`. |

Where `CompleteOnboardingStatus` is a small enum `{ Completed, ProfileNotFound }` (mirrors the existing `AvatarSetStatus` pattern in `IProfileService`).

## Validation rules (reused, not new)

- **Display name** — required in the flow (client) and validated server-side by the existing `UpdateProfileRequest` (`[Required, MaxLength(50)]`).
- **Hometown / Description** — optional; existing `[MaxLength(80)]` / `[MaxLength(280)]`.
- **Pompfen** — zero or more from the fixed `Pompfe` catalog; existing replace-set semantics.
- **Avatar** — existing server-side sniff + size cap on `PUT /profiles/me/avatar`.
- **OnboardingCompletedAt** — set only by `CompleteOnboardingAsync`; never accepted from the client.
