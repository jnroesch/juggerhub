# Data Model: Authenticated-Only Access with Opt-In Public Profiles

Feature 026 adds one field. No new entities.

## Entity change — `PlayerProfile`

`backend/Entities/PlayerProfile.cs` (`: BaseEntity`, 1:1 with `User`).

| Field | Type | Null? | Default | Notes |
|---|---|---|---|---|
| `IsPublic` | `bool` | no | `false` | Owner-controlled anonymous visibility. `true` = the profile is viewable anonymously at `/u/{handle}`; `false` = anonymous callers get the same 404 as a missing handle. Authenticated callers can view regardless. |

**Rules**
- Default `false` (private) for new profiles (set at registration alongside the
  existing fields).
- Immutable-by-others: changed only by the owner via `UpdateAsync` acting on the
  authenticated subject (never a client-supplied id).
- Independent of ban state: the global query filter
  (`AppDbContext.cs` — `p.User.Status != AccountStatus.Banned`) still removes banned
  owners from every player-facing read, so `IsPublic` can never re-expose a banned
  account (FR-019). No filter change needed.
- No effect on the owner's own authenticated views (`/me`, `/me/teams`, etc.).

## Migration — `AddProfileVisibility`

- Adds `IsPublic boolean NOT NULL DEFAULT false` to `PlayerProfiles`.
- The column default backfills every existing row to `false`, satisfying FR-017
  ("existing profiles set to private on rollout") without a data script.
- Reversible: `Down` drops the column.
- Follows the constitution's EF conventions; `CreatedDate`/`ModifiedDate` untouched
  (schema-only change).

## DTO deltas

`backend/Dtos/Profile/ProfileDtos.cs`:

- **`OwnerProfileDto`** — add `bool IsPublic` so the owner UI can render the toggle
  state. (Owner DTO only; never leaks to the public projection.)
- **`UpdateProfileRequest`** — add `bool IsPublic` so the owner can set it through
  the existing update. Positional record; no validation attribute needed (bool).
- **`PublicProfileDto`** — **unchanged**. The public projection must not expose the
  flag or any account/security data (FR-013); visibility is enforced *before* this
  DTO is produced, not carried on it.

## Service contract deltas

`backend/Services/Profile/IProfileService.cs` — three reads become viewer-aware:

| Method | Change | Gate |
|---|---|---|
| `GetPublicAsync(handle, viewerUserId, ct)` | add `Guid? viewerUserId` | return the DTO iff `IsPublic \|\| viewerUserId is not null`, else `null` |
| `GetProfileIdAsync(handle, viewerUserId, ct)` | add `Guid? viewerUserId` | same gate (drives `{handle}/activity`) |
| `GetAvatarAsync(handle, viewerUserId, ct)` | add `Guid? viewerUserId` | same gate (drives `{handle}/avatar`) |

`UpdateAsync` persists `IsPublic` from the request. `GetOwnerAsync` includes it in
the owner DTO.

The `null` return maps to the existing 404 / `NotFound()` branches in
`ProfilesController`, preserving the no-existence-oracle guarantee (FR-011).
