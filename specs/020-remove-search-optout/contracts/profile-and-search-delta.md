# API Contract Delta: Remove the Player-Search Opt-Out

This feature changes existing contracts by **removing a field**; no endpoints are
added or removed. Endpoint paths, verbs, auth, and all other fields are unchanged.

## `GET /api/v1/profiles/me` — owner profile

Response `OwnerProfileDto` **loses** one property:

```diff
 {
   "handle": "string",
   "displayName": "string",
   "hometown": "string|null",
   "description": "string|null",
   "hasAvatar": true,
   "pompfen": ["Stab", "..."],
   "recentActivity": [ ... ],
   "teams": [ ... ],
   "badges": [ ... ],
   "achievements": [ ... ],
-  "appearInSearch": false
 }
```

## `PUT /api/v1/profiles/me` (owner update) — request

`UpdateProfileRequest` **loses** one property:

```diff
 {
   "displayName": "string",
   "hometown": "string|null",
   "description": "string|null",
   "pompfen": ["Stab", "..."],
-  "appearInSearch": false
 }
```

**No backward compatibility** (owner decision): the frontend and backend ship
together, so no client sends `appearInSearch`. This feature makes no guarantee about
how a stray legacy field is handled and adds no test for it.

## `GET /api/v1/profiles` (player browse/search) — behavior

- Request shape (query params `q`, `city`, `positions`, pagination): **unchanged**.
- Response `PagedResult<PlayerCardDto>`: shape **unchanged** (never carried the flag).
- **Behavioral change**: results now include every non-banned player matching the
  query — no player is withheld on a visibility preference. `totalCount` reflects the
  full matching set. Banned players remain excluded (global filter); suspended
  players are included (visible by design).

## `GET /api/v1/profiles/{handle}` (public profile, page `/u/{handle}`) — no change

`PublicProfileDto` never carried `appearInSearch`; unchanged. Still exposes no email,
account status, or account id.

## Amendments to prior contracts

- `specs/007-search/contracts/openapi.yaml` — remove `appearInSearch` from any
  profile schema and retire the opt-in language for the browse endpoint.
- `specs/007-search/spec.md` — dated amendment note retiring FR-041/FR-042 and SC-003
  (the opt-in privacy invariant).
- `specs/003-profile/spec.md` — dated amendment note for the removed profile field.
