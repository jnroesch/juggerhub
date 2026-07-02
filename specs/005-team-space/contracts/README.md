# Contracts — Team Space & Member Handling

`openapi.yaml` documents the HTTP surface this feature adds under `/api/v1`. It is a **design contract** for planning and test authoring, not a generated artifact. Enums serialize as **names** (global `JsonStringEnumConverter`).

## Surface

| Method | Path | Auth | Purpose |
|---|---|---|---|
| POST | `/api/v1/teams` | **auth** | Create a team (creator becomes first admin) |
| GET | `/api/v1/teams/slug-available` | **auth** | Live team-slug availability/format check (UX aid) |
| GET | `/api/v1/teams/{slug}` | **member** | Team detail header (name/type/city/count/myRole) |
| DELETE | `/api/v1/teams/{slug}` | **admin** | Delete team (irreversible; cascades roster/invites/news) |
| GET | `/api/v1/teams/{slug}/public` | anonymous | **Public** info (name/type/city/member count only) |
| GET | `/api/v1/teams/{slug}/members` | **member** | Roster (paginated) |
| PATCH | `/api/v1/teams/{slug}/members/{userId}/role` | **admin** | Promote/demote (last-admin guarded) |
| DELETE | `/api/v1/teams/{slug}/members/{userId}` | **admin / self** | Remove member / leave (last-admin guarded) |
| GET | `/api/v1/teams/{slug}/activity` | **member** | Events the team played (paginated, newest-first) |
| GET | `/api/v1/teams/{slug}/news` | **member** | Read-only news feed (paginated, newest-first) |
| GET | `/api/v1/teams/{slug}/invitations` | **admin** | Pending link + targeted invites (paginated) |
| GET/POST | `/api/v1/teams/{slug}/invitations/link` | **admin** | Get / create-rotate the single active link |
| POST | `/api/v1/teams/{slug}/invitations` | **admin** | Create a targeted invite (emails the accept link) |
| DELETE | `/api/v1/teams/{slug}/invitations/{id}` | **admin** | Revoke a pending invite |
| GET | `/api/v1/teams/{slug}/invitations/user-search` | **admin** | Search users to invite (annotates member/invited) |
| GET | `/api/v1/invitations/{token}` | anonymous | Invite preview (public info + inviter + state) |
| POST | `/api/v1/invitations/{token}/accept` | **auth** | Accept & join as a member |
| POST | `/api/v1/invitations/{token}/decline` | **auth** | Decline |

## Security invariants (assert in tests)

- **Team-internal reads** (`/{slug}`, `/members`, `/news`, `/activity`, all `/invitations*`) are refused to non-members; a non-member and an unknown team both yield **404** (no membership oracle) (FR-040, SC-002).
- **Public** responses (`/{slug}/public`, `/invitations/{token}`) contain **only** name/type/city/member-count (+ inviter for the preview) — **no roster identities, no news** (FR-041/FR-042).
- Every mutation (create/invite/revoke/role/remove/delete) is authorized **server-side** by the authenticated subject's membership + role — never a client-supplied flag (FR-037, SC-002).
- The team `slug` cannot be changed by any endpoint (immutable, SC-009). Duplicate `slug` on create → **409**; race-safe via unique index.
- **Last-admin guard**: any role change / removal / self-leave that would drop the team to zero admins → **409** and no state change, atomic under concurrency (FR-017, SC-005).
- An invite that is expired (`> 7 days`), revoked, or already used never admits anyone; accept returns **409** (FR-030, SC-004). The shared link stays usable by other distinct users until expiry/revoke; a targeted invite is single-use.
- No endpoint creates a duplicate membership (unique `(TeamId, UserId)`); accepting while already a member is a no-op success (FR-026).
- Deleting a team cascades memberships/invites/news but **preserves** `Event`/participation rows (`TeamId` set null) (FR-036, SC-007).
- All list endpoints return `PagedResult<T>` — never unbounded (FR-038).
