# Contracts: Project Scaffold (Walking Skeleton)

## Files

- [`openapi.yaml`](./openapi.yaml) — the API contract for this feature.

## Surface in scope

Only what the walking skeleton needs to prove the stack and the security boundary:

| Method | Path | Auth | Proves |
|--------|------|------|--------|
| `GET` | `/api/v1/health` | Public | Frontend → API → PostgreSQL round trip; graceful DB-down handling (FR-002, FR-003, FR-004) |
| `GET` | `/api/v1/diagnostics/whoami` | Cookie (JWT in httpOnly cookie) | The `[Authorize]` pipeline is enforced server-side; returns `401` without a valid cookie (FR-005, FR-006) |

## Conventions established (consumed by all future features)

- **Versioning**: every route is under `/api/v{n}` (URL-segment versioning); v1 now.
- **Auth transport**: JWT only in a secure `httpOnly` cookie (`jh_access`); never
  in `Authorization`-header-from-localStorage. Same-origin via the frontend `/api`
  proxy.
- **Errors**: non-success responses use an RFC7231 `ProblemDetails` body with a
  generic `detail` — no stack traces or secrets.
- **Paging**: `PaginationRequest` (input) and `PagedResult<T>` (output) are the
  required shapes for any future list endpoint; documented here, not yet used by an
  endpoint.

## Out of scope (defined in later features)

Register / login / refresh / forgot-password endpoints, and all Teams,
Tournaments, and Forum endpoints. They will extend this contract under `/api/v1`.
