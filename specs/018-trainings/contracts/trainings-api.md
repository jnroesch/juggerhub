# Phase 1 API Contract: Trainings

REST, `api/v{version:apiVersion}` (v1.0), JWT bearer in httpOnly cookie (all endpoints
`[Authorize]` — a signed-in user is always required, including outsiders on public sessions). Controllers
are thin; every authorization decision is server-side via `TrainingGuard`. Lists use the shared
`PaginationRequest` (`skip`/`take`) → `PagedResult<T>`. Outcomes map from `TrainingOutcome`:
`Ok`→200/201, `NotFound`→404, `Forbidden`→403, `NotTeamAdmin`→403, `Invalid`→400, `Conflict`→409.

Team-only resources are **404** (not 403) to non-members/outsiders, mirroring teams/parties (no
existence leak).

## `TrainingsController` — `api/v1/teams/{slug}/trainings`

### `GET /api/v1/teams/{slug}/trainings/sessions`
Trainings-tab dated list. **Auth**: team member (admin or member). Non-member → 404.
Query: `PaginationRequest`, `window=upcoming|all` (default `upcoming`).
`200` → `PagedResult<TrainingSessionRowDto>`:
```jsonc
{
  "items": [{
    "sessionId": "…", "trainingId": "…", "name": "Tuesday Training",
    "isOneOff": false, "sessionDate": "2025-11-19", "startTime": "19:00", "endTime": "21:00",
    "locationKind": "InPerson", "location": "Sportpark Müngersdorf, Köln", "virtualLink": null,
    "visibility": "TeamOnly", "status": "Scheduled",
    "goingCount": 6, "maybeCount": 2, "cantCount": 1,
    "myAnswer": "Going" | "Maybe" | "Cant" | null,
    "detached": false
  }],
  "totalCount": 12, "skip": 0, "take": 20
}
```

### `GET /api/v1/teams/{slug}/trainings/series`
Active-series overview (admin panel on the tab). **Auth**: team **admin** → else 404/403.
`200` → `PagedResult<TrainingSeriesSummaryDto>`: `{ trainingId, name, weekday, interval, startTime,
endTime, endDate, visibility, upcomingCount, nextSessionDate }`.

### `POST /api/v1/teams/{slug}/trainings`
Create a series or one-off. **Auth**: team **admin** (`NotTeamAdmin`→403). Body `CreateTrainingDto`:
```jsonc
{
  "isRecurring": true,
  "name": "Tuesday Training",
  "description": "Regular team training — drills then scrims.",
  "locationKind": "InPerson", "location": "Sportpark Müngersdorf, Köln", "virtualLink": null,
  "weekday": "Tuesday",              // required iff isRecurring
  "interval": "Weekly",             // required iff isRecurring
  "startTime": "19:00", "endTime": "21:00",
  "startDate": "2025-09-16",
  "endDate": "2025-12-16",           // required iff isRecurring
  "visibility": "TeamOnly"
}
```
`201` → `CreatedTrainingDto { trainingId, sessionCount, firstSessionId }`. `400 Invalid` on bad
schedule (end before start, end-time ≤ start-time, zero-session expansion, missing series fields).
Side effect: fan-out `TrainingScheduled` to team members.

### `GET /api/v1/teams/{slug}/trainings/public`
Outsider-facing list of the team's **public** upcoming sessions (public team page / shared entry).
**Auth**: any signed-in user. `200` → `PagedResult<TrainingSessionRowDto>` filtered to effectively-public,
upcoming, non-skipped sessions; `myAnswer` reflects the caller's guest response if any.

## `TrainingSessionsController` — `api/v1/trainings/sessions/{sessionId}` (+ `api/v1/me/trainings`)

### `GET /api/v1/trainings/sessions/{sessionId}`
Session page. **Auth**: team member, **or** any signed-in user when the session is effectively public.
Team-only + outsider → 404.
`200` → `TrainingSessionDetailDto`:
```jsonc
{
  "sessionId": "…", "trainingId": "…", "teamSlug": "rheinfeuer", "teamName": "Rheinfeuer",
  "name": "Tuesday Training", "description": "…", "isOneOff": false,
  "sessionDate": "2025-11-19", "startTime": "19:00", "endTime": "21:00",
  "locationKind": "InPerson", "location": "Sportpark Müngersdorf, Köln", "virtualLink": null,
  "seriesLabel": "weekly", "visibility": "TeamOnly", "status": "Scheduled",
  "isPast": false, "isDetached": false,
  "viewerIsAdmin": false, "viewerIsGuest": false, "myAnswer": "Maybe" | null,
  "whosComing": {
    "going": { "count": 6, "people": [{ "handle": "ada-k", "displayName": "Ada K.", "position": "Q-Tip", "isGuest": false, "isYou": true }] },
    "maybe": { "count": 2, "people": [ … ] },
    "cant":  { "count": 1, "people": [ … ] }
  }
}
```
`whosComing.people` are the top few per group (avatars); full lists via the attendance endpoint.

### `PUT /api/v1/trainings/sessions/{sessionId}/response`
Set/change my RSVP (upsert). **Auth**: team member, or outsider on an effectively-public session
(recorded `IsGuest=true`). Body `{ "answer": "Going" | "Maybe" | "Cant" }`.
`200` → updated counts `{ goingCount, maybeCount, cantCount, myAnswer }`.
`409 Conflict` if the session is `Cancelled`/`Skipped` or in the past; `404` if team-only + outsider.

### `PATCH /api/v1/trainings/sessions/{sessionId}`
Edit a single session — **detaches** it. **Auth**: team **admin**. Body (all optional):
`{ sessionDate?, startTime?, endTime?, locationKind?, location?, virtualLink? }`. Sets the matching
overrides + `Detached=true`. `200` → `TrainingSessionDetailDto`. `409` if past/cancelled.

### `POST /api/v1/trainings/sessions/{sessionId}/skip`
Skip this date (soft-remove, no notification). **Auth**: team **admin**. `204`. `409` if past/cancelled.

### `POST /api/v1/trainings/sessions/{sessionId}/cancel`
Cancel (stays visible marked-off, notify responders, block responses). **Auth**: team **admin**. `200` →
row DTO with `status:"Cancelled"`. Side effect: fan-out `TrainingUpdated` (kind `cancelled`) to
responders. `409` if past/already cancelled.

### `PUT /api/v1/trainings/sessions/{sessionId}/visibility`
Per-session visibility (sets `VisibilityOverride`). **Auth**: team **admin**. Body
`{ "visibility": "Public" | "TeamOnly" }`. `200`.

### `GET /api/v1/trainings/sessions/{sessionId}/attendance`
Full attendance incl. guests. **Auth**: team **admin**. Query `PaginationRequest`, optional
`group=going|maybe|cant`. `200` → `PagedResult<AttendanceEntryDto>`:
`{ handle, displayName, position, isGuest, isYou, isTeamAdmin, answer }`. Guests carry `isGuest:true`.

### `DELETE /api/v1/trainings/sessions/{sessionId}/guests/{userId}`
Remove a guest's response (never affects team membership). **Auth**: team **admin**. `204`. `400` if the
target is a team member (only guests are removable here). `404` if no such guest response.

### `PATCH /api/v1/trainings/{trainingId}` — *(on `TrainingsController` or a series sub-route)*
Edit the whole series. **Auth**: team **admin**. Body (all optional):
`{ name?, description?, startTime?, endTime?, locationKind?, location?, virtualLink?, weekday?,
interval?, endDate?, visibility? }`. In-place fields update the template + upcoming non-detached
sessions; `weekday`/`interval`/`endDate` changes trigger regeneration (see data-model §Reconciliation).
`200` → `{ trainingId, addedSessions, removedSessions, keptSessions }`. Side effect: `TrainingUpdated`
(kind `seriesEdit`) to responders of surviving upcoming sessions. `400` if a change yields zero future
sessions.

### `PUT /api/v1/trainings/{trainingId}/visibility`
Whole-series visibility (sets `Training.Visibility`; sessions without an override follow). **Auth**: team
**admin**. Body `{ "visibility": "Public" | "TeamOnly" }`. `200`. Returns a shareable link hint for the
UI (`/trainings/sessions/{id}` is the public entry per session).

### `GET /api/v1/me/trainings`
Dashboard "Your trainings" agenda. **Auth**: the signed-in caller. Returns the caller's next upcoming
sessions across **all** teams they belong to **plus** public sessions where they have a guest response,
chronological. Query `PaginationRequest` (bounded; default small take). `200` →
`PagedResult<AgendaSessionDto>`: `TrainingSessionRowDto` + `{ teamSlug, teamName, isPublicGuest }`.

## Authorization matrix (summary)

| Action | Team admin | Team member | Outsider (public session) | Outsider (team-only) |
|---|---|---|---|---|
| List tab sessions / series | ✓ | ✓ (no series panel) | — | 404 |
| Create training | ✓ | ✗ (403) | ✗ | ✗ |
| View session | ✓ | ✓ | ✓ | 404 |
| RSVP (Going/Maybe/Can't) | ✓ | ✓ | ✓ (as guest) | 404 |
| Who's coming (top few) | ✓ | ✓ | ✓ | 404 |
| Edit series / session, skip, cancel, visibility | ✓ | ✗ (403) | ✗ | ✗ |
| Full attendance / remove guest | ✓ | ✗ (403) | ✗ | ✗ |
| Dashboard agenda (`me/trainings`) | own | own | own (public guest rows) | — |
