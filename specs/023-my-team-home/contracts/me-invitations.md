# Contract: List my pending invitations

The single new endpoint. Accept/decline are **not** new — they reuse the existing invitee token flow, documented at the bottom for reference.

## `GET /api/v1/profiles/me/invitations`

List the authenticated caller's **usable** (pending + unexpired) **targeted** team invitations, newest-first, paginated.

**Auth**: Required (JWT-in-cookie, `JwtBearer` scheme). Acts only on the authenticated subject — never a client-supplied id.

**Query parameters** (shared `PaginationRequest`):

| Param | Type | Default | Notes |
|---|---|---|---|
| `skip` | int | 0 | Negative normalized to 0 |
| `take` | int | 20 | ≤ 0 or > 100 normalized to 20 |

**200 OK** — `PagedResult<MyInvitationDto>`:

```json
{
  "items": [
    {
      "token": "3p9c...urlsafe",
      "teamName": "Rot Team Hamburg",
      "teamSlug": "rot-team-hamburg",
      "teamType": "CityTeam",
      "city": "Hamburg",
      "memberCount": 12,
      "inviterDisplayName": "Lena K.",
      "createdDate": "2026-07-18T09:12:00Z",
      "expiresDate": "2026-07-25T09:12:00Z"
    }
  ],
  "totalCount": 1,
  "skip": 0,
  "take": 20
}
```

**Empty result** — `200 OK` with `items: []`, `totalCount: 0` (a player with no usable invites; the UI omits the section, FR-014).

**401 Unauthorized** — no/invalid session.

### Server rules (constitution I; FR-008, FR-009)

- Filter: `Kind == Targeted AND Status == Pending AND ExpiresDate > utcNow AND TargetUserId == callerId`.
- Never returns invites addressed to another user, link invites, or non-usable invites.
- `AsNoTracking`, projected to `MyInvitationDto` in `.Select(...)`; ordered `CreatedDate` descending.
- `MemberCount` from `Team.Memberships.Count`; `inviterDisplayName` from the inviter's profile display name (fallback `"A teammate"`).

### Behavior notes

- The endpoint returns each invite's opaque `token`. This is safe: the result set is scoped to the caller, and the caller already holds that token from the invitation email. No token belonging to another user is ever returned.
- No side effects (pure read). It does **not** mark invites seen/read.

## Reused (existing, unchanged) — accept / decline

The frontend acts on a listed invite with its `token`:

- **`POST /api/v1/invitations/{token}/accept`** → `200 { "teamSlug": "..." }`
  - Outcomes (from `AcceptAsync`): `Joined` / `AlreadyMember` → `200` with slug; `NotUsable` → `409` ("Invite unavailable"); not found → `404`.
  - On `200`, the client refreshes memberships and navigates to `/t/{teamSlug}` (FR-017, FR-018).
- **`POST /api/v1/invitations/{token}/decline`** → `204`
  - Consumes a pending targeted invite; the client removes the row.
  - Not found → `404`.

For any non-2xx (e.g. an invite that went stale between load and action), the client shows a friendly message and drops the stale row from the list (FR-015) — never a raw error.
