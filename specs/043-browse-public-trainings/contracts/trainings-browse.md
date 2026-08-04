# Contract: Public trainings browse

**Feature**: 043 | **Date**: 2026-08-04

One new endpoint. Nothing existing changes shape — this feature adds no breaking change, unlike 042
which removed `location` from six DTOs.

---

## `GET /api/v1/trainings`

Lists training sessions teams have opened to everyone, across all teams. One item per dated session.

**Placement**: a root `[HttpGet]` on the existing `TrainingsController`
(`[Route("api/v{version:apiVersion}/trainings")]`), matching how teams, events, and players expose
browse on their own controllers (research R6). It cannot shadow the controller's existing
`{trainingId:guid}` or `sessions/...` routes — both carry a constrained or literal segment and rank
higher.

**Auth**: inherits the controller's class-level
`[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`. Signed-in only
(FR-007, feature 026). **No `[AllowAnonymous]`** — and note that adding one would need the
feature-021/026 OpenAPI-allowlist treatment, which is a reason not to.

### Query parameters

Bound `[FromQuery]` into `TrainingBrowseQuery` + the shared `PaginationRequest`. Every parameter is
optional; unknown or unparseable values fall back to the default rather than erroring.

| Name | Type | Default | Meaning |
|------|------|---------|---------|
| `q` | string | — | Substring of the training name, accent- and case-insensitive. Shorter than the configured minimum is treated as absent |
| `hidePast` | bool | `true` | Exclude sessions dated before today (UTC, day-granular) |
| `from` | date (`YYYY-MM-DD`) | — | `sessionDate >= from` |
| `to` | date (`YYYY-MM-DD`) | — | `sessionDate <= to` |
| `city` | string | — | Effective city name, accent-insensitive exact match |
| `country` | string | — | Effective city's ISO code or country name |
| `sort` | `SessionDateAsc` \| `Proximity` | `SessionDateAsc` | Serialized by name (global `JsonStringEnumConverter`) |
| `skip` | int | `0` | Negative is normalised to 0 |
| `take` | int | `20` | Clamped to the shared maximum of 100 |

**Proximity normalisation is a client concern, not a server one** (research R1, refined during
implementation). The endpoint applies **no** implicit date window: `sort=Proximity` with no `to`
returns the full future. The browse page sets `to = today + 14 days` in its own filter state when
the viewer switches to nearest-first, so the bound travels as an ordinary `to` parameter, renders
through the existing chip, and clears through the existing chip removal. The envelope therefore
stays exactly `PagedResult<T>` — there is no `appliedFrom`/`appliedTo`.

### Responses

**`200 OK`** — `PagedResult<TrainingCardDto>` plus the echoed range:

```jsonc
{
  "items": [
    {
      "sessionId": "0198f3c2-...",
      "trainingId": "0198f3b1-...",
      "name": "Dienstagstraining",
      "teamSlug": "hamburg-hammers",
      "teamName": "Hamburg Hammers",
      "isOneOff": false,
      "sessionDate": "2026-08-11",
      "startTime": "19:00:00",
      "endTime": "21:00:00",
      "locationKind": "InPerson",
      "location": {
        "externalId": "...", "name": "Hamburg", "region": "Hamburg",
        "countryName": "Germany", "countryCode": "DE",
        "label": "Hamburg, Germany"
      },
      "locationLabel": "Hamburg"
    }
  ],
  "totalCount": 37,
  "skip": 0,
  "take": 20
}
```

Notes on the payload:

- `locationLabel` is **composed server-side** as `"City, Country"` — the same form the events browse
  card produces — so a training and an event at the same address are byte-identical (FR-009 /
  SC-003). It falls back to the venue name and then the legacy free-text location for a training
  with no canonical city. ⚠ It is **not** the city-only label the dashboard agenda uses; see
  research R4's correction note.
- A **virtual** session has `locationKind: "Virtual"`, `location: null`, and `locationLabel: ""`.
  The client renders the "Online" wording from the kind — the backend deliberately does **not**
  return the string `"Online"` for a training, unlike `EventCardDto` (042's divergence).
- A **pre-042** training has `location: null` but a non-empty `locationLabel` from the legacy
  free-text field.
- No RSVP counts, no visibility flag, no status, no street or postal code — see data-model §3.

**`401 Unauthorized`** — no or invalid token.

**`409 Conflict`** — `sort=Proximity` from a caller with no home city (FR-021). RFC 7807 problem
details, mirroring `TeamsController.cs:101-104`:

```json
{
  "title": "No home city",
  "detail": "Set your home city to sort trainings by distance.",
  "status": 409
}
```

The refusal is deliberate: silently returning a differently-ordered list would misrepresent the
result. The frontend does not offer the option without a home city, so this is reachable only by a
hand-made request — it is a correctness boundary, not a UX path.

### Ordering guarantees

| `sort` | Order | Tiebreaker |
|--------|-------|------------|
| `SessionDateAsc` | Soonest session date first | `sessionId` |
| `Proximity` | Distance from the caller's home city to the session's city, ascending; then date | `sessionId` |

Under `Proximity`, sessions with no city — every virtual session, and every pre-042 training — are
**excluded from both the items and `totalCount`** (FR-022/FR-023). The count therefore matches what
the view can produce; it is not the unfiltered total.

### What the endpoint never returns

- A session whose effective visibility is `TeamOnly` — for any caller, including a member of the
  owning team (FR-004). Membership is not consulted at all.
- A `Cancelled` or `Skipped` session, under any filter or sort (FR-005).
- Any field of the owning series' address on a session that carries its own (SC-004).

---

## Frontend contract

`SearchService.browseTrainings(params)` → `Observable<PagedResult<TrainingCard>>`, alongside the
three existing methods, with a `toTrainingParams` builder using the same `put()` helper so absent
filters are omitted rather than sent empty.

⚠ `toTrainingParams` **must send `city`**. The existing `toEventParams`/`toTeamParams` send only
`country` even though their backends accept `city` — trainings is the first surface to use it
(research R8). Copying an existing builder verbatim would silently drop the city filter.

---

## Unchanged contracts

Stated explicitly because they are the ones a reader might expect to move:

- `GET /api/v1/trainings/sessions/{id}` — the destination page. Unchanged; it already admits any
  signed-in caller to a public session and records them as a guest.
- `GET /api/v1/events`, `/teams`, `/profiles` — untouched (FR-030, verified by SC-010).
- `TrainingSessionRowDto`, `AgendaSessionDto`, `TrainingSessionDetailDto` — untouched. The browse
  card is a **new** DTO rather than a reuse, because the existing row carries RSVP counts and a
  visibility flag that have no place on a discovery card.
