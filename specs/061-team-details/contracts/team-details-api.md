# Contract: Team details

All routes are under `/api/v1`, authenticated (JWT cookie), as every team route is (feature 026).

## `PUT /teams/{slug}/details` — NEW

Replace the team's editable identity as a whole. **Admin only.**

### Request

```json
{
  "name": "Rheinfeuer",
  "type": "CityTeam",
  "location": { "cityExternalId": "2886242", "name": "Köln" },
  "description": "Founded in 2019 …\nWe train twice a week.",
  "links": [
    { "label": "Website", "url": "https://rheinfeuer.de" },
    { "label": "Instagram", "url": "instagram.com/rheinfeuer" }
  ]
}
```

| Field | Type | Notes |
|---|---|---|
| `name` | string, required | Trimmed; 2–50 |
| `type` | `"CityTeam"` \| `"Mixteam"`, required | String enum (global converter) |
| `location` | `LocationSelection` \| null | Required for `CityTeam` (resend the current city's `externalId` to keep it); null or absent for `Mixteam` |
| `description` | string \| null | Blank or null ⇒ no description; ≤ 1000 after trim |
| `links` | array (≤ 5) \| null | Null ≡ `[]`. Order is kept. `url` may omit the scheme (⇒ `https`) |

Payload guards (model binding) are deliberately looser than the rules, so that rule violations
come back **coded** (below) rather than as MVC's uncoded 400.

### Responses

| Status | When | Body |
|---|---|---|
| **200** | Saved (including "nothing changed") | `TeamDetailDto`, the updated team, links normalised |
| **400** | A rule was broken; nothing was saved | ProblemDetails + `code` (+ `link`) |
| **403** | Caller is a member but not an admin | ProblemDetails `title: "Forbidden"` |
| **404** | No such team, or the caller is not a member | ProblemDetails `title: "Team not found"` (the same body as for a non-existent team; no membership oracle) |
| 401 | Not signed in | — |

**400 body**:

```json
{
  "status": 400,
  "title": "Invalid team details",
  "detail": "Link 2: use a secure web address (https://…).",
  "code": "linkUrlInvalid",
  "link": 1
}
```

`code` ∈ `nameInvalid`, `cityRequired`, `mixteamHasCity`, `cityNotFound`, `descriptionTooLong`,
`tooManyLinks`, `linkLabelInvalid`, `linkUrlInvalid`, `linkDuplicate`. `link` (0-based) is
present only for the three `link*` codes. `detail` is English for API readers. **Clients render
`code`, never `detail`** (#179).

**Side effects on 200, inside the same transaction** (only when `name` changed):

- every delivered alert whose payload names this team (`teamSlug`) shows the new `teamName`;
  nothing becomes unread, moves or is re-sent;
- every tournament placement connected to this team shows the new name.

Nothing else is emitted: no notification, email, push or realtime event.

**Idempotent**: replaying the same body yields the same state and a 200. It is never auto-retried
by the browser (a mutation, Principle VII), but a replay would be harmless.

## `GET /teams/{slug}` — CHANGED (members only)

`TeamDetailDto` gains, **appended**:

```json
{ "description": "…" , "links": [ { "label": "Website", "url": "https://rheinfeuer.de/" } ] }
```

`description` is `null` when none, and `links` is `[]` when none. Returned by `POST /teams`
(create) too, where both are always empty.

## `GET /teams/{slug}/public` — CHANGED (every signed-in viewer)

`TeamPublicDetailDto` gains the same `description` and `links`, appended after `achievements`. No
new request is needed on the team page (SC-006).

## `PATCH /teams/{slug}` — UNCHANGED

`{ "beginnersWelcome": bool }`. It still writes only that flag. The details endpoint never
touches it (FR-007).

## Frontend shapes (`core/models/team.models.ts`)

```ts
export interface TeamLink { label: string; url: string; }
// TeamDetail and TeamPublicDetail each gain:
//   description: string | null;
//   links: TeamLink[];
export interface UpdateTeamDetails {
  name: string;
  type: TeamType;
  location: LocationSelection | null;
  description: string | null;
  links: TeamLink[];
}
export type TeamDetailsErrorCode =
  | 'nameInvalid' | 'cityRequired' | 'mixteamHasCity' | 'cityNotFound'
  | 'descriptionTooLong' | 'tooManyLinks' | 'linkLabelInvalid' | 'linkUrlInvalid' | 'linkDuplicate';
```

`TeamService.updateDetails(slug, body): Observable<TeamDetail>` → `PUT`.
