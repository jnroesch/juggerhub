# API Contract: Platform Admin Area (013)

All endpoints: `[Authorize(Policy = PlatformAdminPolicy.Name)]` — server-side role
check per request (research §2). Errors use the platform's ProblemDetails shape.
Enums travel as names. Base path `api/v{version}` (v1).

Reused unchanged from feature 012 (listed for the frontend's benefit, not redefined):

- `GET  admin/access` — probe; 200 `{ isAdmin: true }` for admins, 401/403 otherwise
- `GET  admin/players/{handle}/awards` — subject's held badge+achievement awards
- `GET  admin/badges` / `admin/achievements` (catalogues) and their grant/revoke
  endpoints (`POST admin/badges/{id}/awards { subject, note? }`,
  `DELETE admin/badges/awards/{awardId}`, achievement equivalents)

## `GET admin/overview`

One round trip for the landing page.

```jsonc
// 200 AdminOverviewDto
{
  "players": 842,            // visible (non-banned) player count
  "teams": 63,
  "eventsLast30Days": 28,
  "suspended": 3,
  "newPlayers": [            // registered within 7 days, newest first, capped (5)
    { "handle": "ada-l", "displayName": "Ada Lindqvist", "hometown": "Berlin", "joinedAt": "..." }
  ],
  "recentGrants": [          // newest grants across both families, capped (5)
    { "kind": "Badge", "name": "Fair play", "subjectHandle": "jonas",
      "subjectDisplayName": "Jonas Reeh", "grantedByDisplayName": "Mira Kessler", "grantedAt": "..." }
  ]
}
```

## `GET admin/users?q=&status=&skip=&take=`

- `q` — optional; matches display name, handle, or team name (case-insensitive,
  contains).
- `status` — optional filter: `Active` | `Suspended` | `Banned` (omitted = all).
- Pagination via shared `PaginationRequest` (`skip`/`take`, hard max) →
  `PagedResult<AdminUserListItemDto>`.
- Banned players ARE included (admin surface opts out of the query filter) — the one
  place they remain findable (FR-012).

```jsonc
// 200 PagedResult<AdminUserListItemDto>
{
  "items": [
    { "handle": "mira-k", "displayName": "Mira Kessler", "status": "Active",
      "isAdmin": true, "teams": ["Berlin Bloodhounds", "Rheinfire II"],
      "badgeCount": 5, "joinedAt": "..." }
  ],
  "totalCount": 842, "skip": 0, "take": 20
}
```

## `GET admin/users/{handle}`

```jsonc
// 200 AdminUserDetailDto  |  404 unknown handle
{
  "userId": "0197...",              // the "player id" from the wireframe
  "handle": "mira-k", "displayName": "Mira Kessler",
  "hometown": "Berlin", "joinedAt": "...",
  "status": "Active", "statusChangedAt": null, "isAdmin": false,
  "teams": [ { "name": "Berlin Bloodhounds", "slug": "berlin-bloodhounds" } ],
  "pompfen": ["Laeufer", "QTip", "Kette"],  // positions played (profile pompfen)
  "lastActiveAt": "...",            // newest activity item date or null → "—"
  "recentActivity": [               // feature-003 activity items, newest first, capped
    { "title": "RSVP'd Saturday open training", "date": "..." }
  ]
}
```

(Badges/achievements for the page come from the existing
`admin/players/{handle}/awards`.)

## Account actions — `POST admin/users/{handle}/...`

All: 204 on success; 404 unknown handle; 409 on invalid transition (already in the
target state); 422 shield violation (target is a platform admin or the caller
themselves — FR-019); every success writes an `AdminActionRecord`.

| Endpoint | Effect |
|----------|--------|
| `suspend` | `Status → Suspended`, revoke all refresh tokens |
| `reinstate` | `Suspended → Active` |
| `ban` | `Status → Banned` (from Active or Suspended), revoke all refresh tokens |
| `unban` | `Banned → Active` |
| `reset-password` | Send standard reset email to the target; always 204 (no oracle about email deliverability); records `PasswordResetSent` |

No request bodies this pass (`Note` is reserved).

## Auth-flow contract changes (existing endpoints)

- `POST auth/login`: suspended account + correct password → 403 with a distinct,
  human "account suspended" ProblemDetails (parallel to the existing
  needs-verification shape); banned → the generic 401 invalid-credentials response
  (indistinguishable from wrong password).
- `POST auth/refresh`: any non-Active status → 401 (session ends).
- `POST auth/register`: unchanged contract (enumeration-neutral Accepted); banned
  email additionally never triggers the resend-verification courtesy.

## Frontend routes (UX contract; server stays the boundary)

| Route | Content |
|-------|---------|
| `/admin` | shell (shield header, sidebar Overview · Users · Catalogue, back-to-app) → overview |
| `/admin/users` | search/filter/paginated list (desktop table, mobile cards) |
| `/admin/users/:handle` | player detail: identity · account actions · awards + Assign picker |
| `/admin/catalogue` | existing 012 recognition management component, re-mounted |

Nav: lock-marked Admin item (desktop top-nav, after normal links) and "Admin panel"
account-menu row (mobile) — rendered only when the `admin/access` probe succeeds.
