# Contracts — Player Profile & Public Share Link

`openapi.yaml` documents the HTTP surface this feature adds/extends under `/api/v1`. It is a **design contract** for planning and test authoring, not a generated artifact.

## Surface

| Method | Path | Auth | Purpose |
|---|---|---|---|
| POST | `/api/v1/auth/register` | anonymous | **EXTENDED** — now also takes `handle` |
| GET | `/api/v1/auth/handle-available` | anonymous | Live handle availability/format check (UX aid) |
| GET | `/api/v1/profiles/me` | **owner (JWT)** | Owner profile (editable fields + selected pompfen + recent activity) |
| PUT | `/api/v1/profiles/me` | **owner (JWT)** | Update display name, hometown, description, pompfen set |
| PUT | `/api/v1/profiles/me/avatar` | **owner (JWT)** | Upload/replace avatar (multipart, validated) |
| GET | `/api/v1/profiles/{handle}` | anonymous | **Public** profile DTO (no email/account data) |
| GET | `/api/v1/profiles/{handle}/avatar` | anonymous | Serve avatar bytes (placeholder handled client-side) |
| GET | `/api/v1/profiles/{handle}/activity` | anonymous | Public recent activity, **paginated + capped** |

## Security invariants (assert in tests)

- `PublicProfileDto` and the `{handle}` / `{handle}/activity` responses contain **no** `email`, account-status, security, or raw account-id field (SC-002).
- Edit endpoints (`/me`, `/me/avatar`) are refused without a valid JWT and only ever act on the **authenticated subject's** profile — never a client-supplied id (SC-005).
- `handle` cannot be changed by any endpoint (immutable, SC-006). `PUT /profiles/me` ignores/-rejects any handle field.
- Duplicate `handle` on register → `409` (or 400 with a clear reason); enforced by unique index, race-safe (SC-006).
- Activity responses use `PagedResult<T>` and never return unbounded lists (SC-007).
- Avatar upload rejects wrong content-type (magic-byte sniff) or oversize with `400`, leaving any existing avatar unchanged.
