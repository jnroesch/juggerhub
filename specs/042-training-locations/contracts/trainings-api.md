# Phase 1 Contract: Trainings API deltas

**Feature**: 042-training-locations | **Date**: 2026-08-04

Only the location members change. Every route, status code, guard and pagination contract is
untouched. **No new endpoint is added** — the city type-ahead the forms use is the existing
`GET /api/v1/cities/search` from feature 030.

Controllers: `backend/Controllers/TeamTrainingsController.cs`
(`api/v{version}/teams/{slug}/trainings`) and `backend/Controllers/TrainingsController.cs`
(`api/v{version}/trainings`).

**Breaking**: the free-text `location` string is removed from every training request and response.
Frontend and backend ship together; there is no compatibility window (precedent: feature 020).

---

## Shared fragments (existing, feature 030 — `backend/Dtos/Cities/CityDtos.cs`)

```jsonc
// LocationSelectionDto — WRITE. The client never sends coordinates or a resolved id.
{ "cityExternalId": "TEST:berlin", "name": "Berlin" }   // name is a hint only

// LocationDto — READ. (Member names verified against backend/Dtos/Cities/CityDtos.cs during
// implementation: the read shape uses `externalId` and `label`, NOT `cityExternalId`/`displayLabel`.)
{
  "externalId": "TEST:berlin",
  "name": "Berlin",
  "region": "Berlin",
  "countryName": "Germany",
  "countryCode": "DE",
  "label": "Berlin, Germany"
}
```

---

## 1. `POST /api/v1/teams/{slug}/trainings` — create

`CreateTrainingRequest`

| Member | Change |
|---|---|
| `location` (`string?`) | **removed** |
| `venueName` (`string?`, ≤120) | **added** — optional |
| `street` (`string?`, ≤160) | **added** — required when `locationKind == "InPerson"` |
| `postalCode` (`string?`, ≤20) | **added** — required when `locationKind == "InPerson"` |
| `location` (`LocationSelectionDto?`) | **added** — required when `locationKind == "InPerson"` |

Everything else (`isRecurring`, `name`, `description`, `locationKind`, `virtualLink`, `weekday`,
`interval`, `startTime`, `endTime`, `startDate`, `endDate`, `visibility`) is unchanged.

```jsonc
// In-person
{
  "isRecurring": true, "name": "Tuesday training", "locationKind": "InPerson",
  "venueName": "Sportpark Müngersdorf", "street": "Aachener Str. 999", "postalCode": "50933",
  "location": { "cityExternalId": "TEST:köln", "name": "Köln" },
  "virtualLink": null,
  "weekday": "Tuesday", "interval": "Weekly",
  "startTime": "19:00:00", "endTime": "21:00:00",
  "startDate": "2026-08-11", "endDate": "2026-12-15", "visibility": "TeamOnly"
}

// Virtual — no address members are read at all
{
  "isRecurring": false, "name": "Tactics call", "locationKind": "Virtual",
  "venueName": null, "street": null, "postalCode": null, "location": null,
  "virtualLink": "meet.example.com/abc",
  "startTime": "20:00:00", "endTime": "21:00:00",
  "startDate": "2026-08-11", "visibility": "TeamOnly"
}
```

**Responses**

| Status | When |
|---|---|
| `201` `CreatedTrainingDto` | unchanged shape and status (verified during implementation — the endpoint returns **Created**, not `200`) |
| `400` | in-person with a missing street or postal code — *"An in-person training needs a street and postal code."* |
| `400` | in-person with no `location` — *"An in-person training needs a city."* |
| `400` | `location.cityExternalId` not in the reference dataset — *"That city could not be found."* |
| `403` / `404` | unchanged (team admin guard) |

> The unknown-city case surfaces as `400` here because `CreateAsync` returns
> `TrainingOutcome.Invalid`, which the controller already maps to `400`. Events map the same
> condition to `422` via a different path; the *message* is what FR-005 constrains, and it is
> shared. Confirm the mapping during implementation and record it if it is changed.

---

## 2. `PATCH /api/v1/trainings/{trainingId}` — edit series

`EditSeriesRequest` — same four members replace `location: string?`, all optional.

**The address is replaced as a block.** When `location` is present, `venueName` / `street` /
`postalCode` / `location` are applied together; a member omitted from that block is stored as
`null`, not left at its previous value. When `location` is absent the address is untouched.

Field-by-field patching is explicitly **not** supported (FR-007) — it would allow a street from one
address against a city from another.

Switching `locationKind` to `"Virtual"` clears the stored address.

**Responses**: `200 SeriesEditResultDto` unchanged; the three `400` reasons above apply.

---

## 3. `PATCH /api/v1/trainings/sessions/{sessionId}` — edit one session

`EditSessionRequest` — same four members replace `location: string?`, all optional.

Semantics (FR-006 – FR-009):

- Any single-session edit detaches the session and **freezes** the inherited address into its own
  override columns, as it already does for time, kind and link.
- Supplying the address block relocates the session; street, postal code and city are all required
  together.
- If the effective kind ends up `"Virtual"`, the stored address is cleared.
- A relocated session keeps its address when the series is edited afterwards.

**Responses**: `200 TrainingSessionDetailDto` unchanged; the three `400` reasons above apply.

---

## 4. Read responses

### `GET /api/v1/teams/{slug}/trainings/sessions` → `TrainingSessionRowDto[]`
### `GET /api/v1/me/trainings` → `AgendaSessionDto[]`

| Member | Change |
|---|---|
| `location` (`string?`) | **removed** |
| `locationLabel` (`string`) | **added** — server-computed, city → venue → legacy text |

`locationKind` and `virtualLink` are unchanged. `locationLabel` is `""` only when a legacy row has
no city, no venue and no legacy text; the client renders "Online" from `locationKind` for virtual
sessions, exactly as today.

```jsonc
{
  "sessionId": "01923...", "trainingId": "01923...", "name": "Tuesday training",
  "isOneOff": false, "sessionDate": "2026-08-11",
  "startTime": "19:00:00", "endTime": "21:00:00",
  "locationKind": "InPerson",
  "locationLabel": "Köln",
  "virtualLink": null,
  "visibility": "TeamOnly", "status": "Scheduled",
  "goingCount": 4, "maybeCount": 1, "cantCount": 0,
  "myAnswer": "Going", "detached": false
}
```

### `GET /api/v1/trainings/sessions/{sessionId}` → `TrainingSessionDetailDto`

| Member | Change |
|---|---|
| `location` (`string?`) | **removed** |
| `venueName` (`string?`) | **added** |
| `street` (`string?`) | **added** |
| `postalCode` (`string?`) | **added** |
| `location` (`LocationDto?`) | **added** — the resolved city, `null` when virtual |
| `locationLabel` (`string`) | **added** |

The structured members are what the edit forms prefill from (`jh-city-picker`'s `[initial]` input
takes a `LocationDto`). List rows deliberately omit `street` / `postalCode` — no list renders them,
and event list rows don't carry them either.

```jsonc
{
  "sessionId": "01923...", "locationKind": "InPerson",
  "venueName": "Sportpark Müngersdorf",
  "street": "Aachener Str. 999",
  "postalCode": "50933",
  "location": {
    "externalId": "TEST:köln", "name": "Köln", "region": "Nordrhein-Westfalen",
    "countryName": "Germany", "countryCode": "DE", "label": "Köln, Germany"
  },
  "virtualLink": null,
  "locationLabel": "Köln"
}
```

---

## 5. Unchanged surfaces

`PUT .../visibility`, `PUT .../response`, `POST .../skip`, `POST .../cancel`,
`GET .../attendance`, `DELETE .../guests/{userId}` — no location members, no change.

`GET /api/v1/cities/search` — reused as-is. This feature adds a consumer, not a variant: minimum
query length, ranking, same-name disambiguation and the `503` unavailable state all come from
features 030/032.

---

## 6. Contract test checklist

Add to `backend/tests/JuggerHub.Api.IntegrationTests/Trainings/TrainingApiTests.cs`. Seeded test
cities (`TEST:berlin`, `TEST:köln`, `TEST:hamburg`, …) are already available — see
`TrainingTestSupport.cs:40`.

- [ ] Create in-person with street + postal + city → `200`; detail returns all four structured members
- [ ] Create in-person missing street → `400`
- [ ] Create in-person missing postal code → `400`
- [ ] Create in-person with no `location` → `400`
- [ ] Create in-person with an unresolvable `cityExternalId` → `400`, message names the city
- [ ] Create virtual with an address supplied → `200`, address stored as `null` (FR-003)
- [ ] Series edit switching in-person → virtual clears the stored address
- [ ] Session relocation: only that session's `locationLabel` changes; siblings unchanged
- [ ] **Venue-leak guard**: series has a venue name, session relocated to an address with none → the session's `venueName` is `null`, not the series' (FR-007 / research R1)
- [ ] Relocated session survives a later series address change (FR-008)
- [ ] Clearing the override returns the session to the series address (FR-009)
- [ ] Session edited to virtual has all four address overrides `null` (FR-003)
- [ ] `locationLabel` for a training equals the label for an event at the same address (SC-003)
