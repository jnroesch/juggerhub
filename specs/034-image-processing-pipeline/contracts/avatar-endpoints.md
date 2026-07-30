# Contract: Avatar HTTP endpoints (shape unchanged; behavior refined)

Feature: `034-image-processing-pipeline`. These endpoints already exist; this feature does **not** change their routes, auth, request shape, or status codes (FR-014). What changes is what gets **stored/served** and the set of rejection reasons.

## `PUT /api/v1/profiles/me/avatar`

- **Auth**: `[Authorize]` (JWT bearer) — unchanged.
- **Body**: multipart `IFormFile file` — unchanged.
- **Framework cap**: `[RequestSizeLimit(8 MB)]` — unchanged (the generous input cap, FR-012).
- **Responses** (unchanged codes):
  | Code | When |
  |---|---|
  | `204 No Content` | Processed & stored |
  | `404 Not Found` | Caller has no profile (`ProfileNotFound`) |
  | `400 Bad Request` (ProblemDetails, `detail` = reason) | Any processing rejection |

- **Rejection reasons carried in `detail`** — now distinct (FR-003), e.g.:
  - "No image was provided." (empty)
  - "Use a PNG, JPEG, or WebP image." (unsupported type)
  - "Image is too large (max 8 MB)." (input too large)
  - "Image resolution is too large." (pixel-guard) — *new*
  - "That image could not be read." (corrupt/unreadable) — *new*
  - "Processed image exceeds the size limit." (output ceiling) — *new*

- **Behavioral change**: the stored avatar is now a normalized **WebP** regardless of upload format; the original bytes are discarded (FR-015). On any rejection, an existing avatar is left unchanged (FR-009).

## `GET /api/v1/profiles/{handle}/avatar`

- **Auth**: `[AllowAnonymous]` with the existing visibility/ban gate — unchanged.
- **Response**: `200` with the stored image, or `404`.
- **Change**: `Content-Type` of a newly-uploaded avatar is `image/webp` (was the source type). No route/gate change.

## Test drift (existing suite)

`ProfileTests.Avatar_upload_accepts_a_valid_png` currently asserts the served `Content-Type` is `image/png`. After this feature it MUST assert `image/webp` (the upload is re-encoded). This is the one existing assertion that changes.

> **Out of scope (noted, not done here)**: caching headers (`Cache-Control`/`ETag`) on the GET response — tracked with the storage work (#97), not this feature.
