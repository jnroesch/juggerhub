# Contract: Access Control & Profile Visibility

Feature 026. Defines the authorization posture per endpoint (before → after) and the
public-profile visibility decision. Server-side is the boundary; the frontend guard
is UX only.

## Global posture

- **New default**: a `FallbackPolicy` requires an authenticated user (JwtBearer /
  cookie). Any endpoint without an explicit `[AllowAnonymous]` now returns
  **401 Unauthorized** (generic ProblemDetails) to anonymous callers.
- **Unauthorized shape**: unchanged — the existing `OnChallenge` ProblemDetails
  (`401`, title "Unauthorized", detail "Authentication is required to access this
  resource.").

## Endpoint authorization matrix

### Teams (`/api/v1/teams`) — controller already `[Authorize]`

| Endpoint | Before | After |
|---|---|---|
| `GET /` (browse) | anonymous | **auth required** (remove `[AllowAnonymous]`) |
| `GET /{slug}/public` | anonymous | **auth required**; keeps optional-auth shaping for the viewer relation, but the caller must now be authenticated |
| all other team endpoints | auth | auth (unchanged) |

### Events (`/api/v1/events`) — controller already `[Authorize]`

| Endpoint | Before | After |
|---|---|---|
| `GET /` (browse) | anonymous | **auth required** |
| `GET /{id}` (detail) | anonymous | **auth required** (optional-auth shaping retained for members/admins) |
| `GET /{id}/participants` | anonymous | **auth required** |
| `GET /{id}/news` | anonymous | **auth required** |
| `GET /{id}/contacts` | anonymous | **auth required** |
| all other event endpoints | auth | auth (unchanged) |

### Profiles (`/api/v1/profiles`) — add controller-level `[Authorize]`

| Endpoint | Before | After |
|---|---|---|
| `GET /` (player browse/search) | anonymous | **auth required** |
| `GET /me*` (owner) | auth | auth (unchanged) |
| `GET /{handle}` | anonymous | **anonymous, visibility-gated** (see below) |
| `GET /{handle}/avatar` | anonymous | **anonymous, visibility-gated** |
| `GET /{handle}/activity` | anonymous | **anonymous, visibility-gated** |

### Stays anonymous (allowlist — must keep explicit `[AllowAnonymous]`)

Auth flows; Health; RecognitionIcons (`/badges/{id}/icon`, `/achievements/{id}/icon`
— icon bytes only); invite previews (`InvitationsController`,
`EventInvitationsController`, `PartyInvitationsController`, `MarketController`
preview reads); and the three visibility-gated profile reads above.

## Visibility decision (public-profile reads)

Applied inside `IProfileService` for `GET /{handle}`, `/{handle}/avatar`,
`/{handle}/activity`. The controller passes `Guid? viewerUserId` from
`GetOptionalUserId()`.

| Profile exists? | `IsPublic` | Caller authenticated? | Result |
|---|---|---|---|
| no | — | — | `404` "Profile not found" |
| yes | `true` | no (anonymous) | `200` public content |
| yes | `true` | yes | `200` public content |
| yes | `false` | no (anonymous) | `404` **identical** to "not found" (no oracle) |
| yes | `false` | yes | `200` public content (authed can view any) |
| banned owner | any | any | `404` (global ban filter; never reaches the gate) |

- Avatar/activity mirror the profile result: gated the same way; `404`/`NotFound`
  identical to a missing handle.
- **No existence oracle**: the `IsPublic=false` + anonymous branch reuses the exact
  missing-handle response path — same status/title/detail/timing.

## Owner visibility mutation

- `GET /api/v1/profiles/me` → `OwnerProfileDto` now includes `isPublic`.
- `PUT /api/v1/profiles/me` → `UpdateProfileRequest` now accepts `isPublic`;
  persisted for the authenticated subject only. No new endpoint.
- A visibility change takes effect on the next anonymous request to the handle
  (FR-015) — no caching layer to invalidate.

## Frontend route contract

| Route | Before | After |
|---|---|---|
| `t/:slug` | no guard | `canActivate: [authGuard]` |
| `events/:id` | no guard | `canActivate: [authGuard]` |
| `browse/teams` | no guard | `canActivate: [authGuard]` |
| `browse/events` | no guard | `canActivate: [authGuard]` |
| `browse/players` | no guard | `canActivate: [authGuard]` |
| `u/:handle` | no guard | **no guard** (stays anonymous); renders not-found when the API returns 404 for a private/missing profile |

Owner settings gain a "Make my profile public" toggle bound to `isPublic`.
