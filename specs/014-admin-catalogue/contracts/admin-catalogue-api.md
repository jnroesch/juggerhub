# Admin Catalogue API contract (feature 014)

All routes are versioned `api/v1/...` and gated by the `PlatformAdmin` policy
(401/403 for non-admins). Errors use the existing ProblemDetails middleware. This
document lists **only** what changes or is new; unlisted 012 routes are unchanged.

## Changed — catalogue list DTO gains two fields

`BadgeDefinitionDto` and `AchievementDefinitionDto` now include:

```jsonc
{
  "id": "…", "name": "…", "description": "…",
  "appliesToPlayers": true, "appliesToTeams": false,
  "isRetired": false, "hasIcon": true,
  "grantedCount": 214,          // NEW — active awards of this type
  "createdAt": "2026-03-04T…Z"  // NEW — BaseEntity.CreatedDate
}
```

Returned by `GET /admin/badges`, `GET /admin/achievements` (paged), and by
`POST`/`PUT` on those collections (a new type has `grantedCount: 0`).

## New — reinstate (un-retire)

```
POST /api/v1/admin/badges/{definitionId}/reinstate
POST /api/v1/admin/achievements/{definitionId}/reinstate
```

- **204 No Content** — definition is now Active (`isRetired: false`); idempotent if
  already active.
- **404** — no such definition (ProblemDetails "… not found").
- No request body.

## New — remove icon

```
DELETE /api/v1/admin/badges/{definitionId}/icon
DELETE /api/v1/admin/achievements/{definitionId}/icon
```

- **204 No Content** — icon removed (`hasIcon` becomes false); idempotent if none.
- **404** — no such definition.

## Unchanged (reused) — set/replace icon

```
PUT /api/v1/admin/badges/{definitionId}/icon        (raw image bytes in body)
PUT /api/v1/admin/achievements/{definitionId}/icon
```

- Body is the raw file bytes (**not** multipart). Content type is sniffed
  server-side (PNG/JPEG/WebP); a size cap applies.
- **204** stored · **404** no such definition · **400** unsupported type or too
  large (existing icon unchanged).

## Unchanged (reused) — create / edit / retire / grant / revoke / awards read

- `POST /admin/{badges|achievements}` — create → 201 + DTO.
- `PUT /admin/{badges|achievements}/{id}` — edit (name/description/applies-to) →
  200 + DTO / 404. **Does not** change kind or retirement.
- `DELETE /admin/{badges|achievements}/{id}` — retire → 204 / 404.
- `POST /admin/{badges|achievements}/{id}/awards` — grant to one subject
  (`playerHandle` XOR `teamSlug`, optional note; achievements add context) →
  201 / 400 / 404 / 409.
- `DELETE /admin/{badges|achievements}/awards/{awardId}` — revoke → 204 / 404.
- `GET /admin/players/{handle}/awards`, `GET /admin/teams/{slug}/awards` →
  `AdminSubjectAwardsDto` (badges + achievements) / 404.

## New — admin teams area

```
GET /api/v1/admin/teams?q={query}&skip={n}&take={n}
```

- **200** `PagedResult<AdminTeamListItemDto>`:
  ```jsonc
  { "items": [ { "slug": "berlin-bison", "name": "Berlin Bison",
                 "city": "Berlin", "type": "CityTeam",
                 "memberCount": 18, "awardCount": 3 } ],
    "totalCount": 42, "skip": 0, "take": 20 }
  ```
- `q` filters by team name / city (case-insensitive, optional). Paginated
  (`PaginationRequest`, hard max take).

```
GET /api/v1/admin/teams/{slug}
```

- **200** `AdminTeamDetailDto`:
  ```jsonc
  { "teamId": "…", "slug": "berlin-bison", "name": "Berlin Bison",
    "city": "Berlin", "type": "CityTeam", "memberCount": 18,
    "createdAt": "2025-11-…Z" }
  ```
- **404** — no team with that slug.
- The team's current awards + assign/revoke use the existing
  `GET /admin/teams/{slug}/awards` and the `teamSlug` grant/revoke routes above.

## Client surface (Angular)

- `recognition-admin.service.ts` gains: `createBadge/createAchievement`,
  `updateBadge/updateAchievement`, `retireBadge/retireAchievement`,
  `reinstateBadge/reinstateAchievement`, `setBadgeIcon/setAchievementIcon`
  (raw `File` body), `removeBadgeIcon/removeAchievementIcon`.
- `admin.service.ts` gains: `searchTeams(q, skip, take)`, `getTeamDetail(slug)`.
- All are UX conveniences; the server policy is the boundary.
