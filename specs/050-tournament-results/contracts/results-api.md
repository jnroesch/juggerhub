# Contract: Results API (050)

All routes are versioned `api/v1/…`, require sign-in (class-level `[Authorize]`, 026), and return RFC 7807 problem details on failure. Generic messages only, never internals (Principle I). JSON is camelCase and enums are names.

Legend: **EA** = admin of this event (`EventAdminGuard`) · **PA** = platform admin (`PlatformAdmin` policy) · **RL** = `[EnableRateLimiting("tugeny")]` (10/min/user).

---

## Event results: `EventResultsController` at `events/{eventId:guid}/results`

### `GET /events/{eventId}/results` → `200 TournamentResultDto`

Any signed-in user. `404` if the event does not exist. Returns an **empty result** (not 404) when nothing is recorded, so the page can still show a link or a live link.

```jsonc
{
  "source": "None" | "Manual" | "TugenyImport",
  "importedAt": "2026-09-20T18:03:11Z" | null,
  "editedSinceImport": false,
  "resultsChangedAt": "2026-09-20T18:03:11Z" | null,     // FR-007
  "tugeny": null | {                                       // FR-014/015
    "tournamentId": 200, "slug": "25-deutsche-meisterschaft",
    "name": "25. Deutsche Meisterschaft", "startDate": "2024-09-21",
    "liveUrl": "https://tugeny.org/tournaments/25-deutsche-meisterschaft/live-view",
    "tournamentUrl": "https://tugeny.org/tournaments/25-deutsche-meisterschaft/all-teams"
  },
  "placements": [                                          // ≤ 128, ordered position, sortIndex (R8)
    { "id": "…", "position": 1, "name": "Seven Sins", "teamSlug": "seven-sins" | null }
  ],
  "rankedCount": 20,
  "matchCount": 78,
  "viewer": { "canEdit": true }   // EA && Tournament && !Cancelled && StartsAt <= now
}
```

Placements do not expose `sourceName` or connection attribution to ordinary viewers. Those are for event admins (below) and platform admins.

### `GET /events/{eventId}/results/matches?skip=&take=` → `200 PagedResult<TournamentMatchDto>`

Any signed-in user. Ordered by `sortIndex`. `take` ≤ 100.

```jsonc
{ "id": "…", "stage": "Group 1" | null, "name": "QF1 1-8",
  "first":  { "name": "Rigor Mortis", "teamSlug": "rigor-mortis" | null },
  "second": { "name": "Jugger Helden Bamberg", "teamSlug": null },
  "firstScores": [5, 5], "secondScores": [2, 3],
  "winner": "First" | "Second" | "Draw" }
```

### `GET /events/{eventId}/results/editor` → `200 ResultEditorDto` · **EA**

Everything the results page needs:
- the placements, including `sourceName`, `teamId`, and connection attribution (`connectedBy` display name, or `MemberPlaceholder` for an erased account; `connectedAt`)
- `signedUpTeams`: `[{ teamId, teamSlug, teamName }]`, the teams with a confirmed (`Joined`) JuggerHub sign-up for the event (R4). These are the only teams an event admin can connect to
- the Tugeny link, and a `linkedElsewhere` flag

`403` when not an admin.

### `PUT /events/{eventId}/results/ranking` → `200 TournamentResultDto` · **EA**

Replaces the ranking (R8).

```jsonc
{ "placements": [ { "id": "…" | null, "position": 3, "name": "Guests from Kiel", "teamId": "…" | null } ] }
```

| Status | When |
|---|---|
| `400` | Validation: empty name, > 80 characters, > 128 rows, position outside 1–999, same team twice |
| `403` | Not an event admin |
| `404` | No such event |
| `409` | Not a tournament · cancelled · not started yet (FR-001) |
| `422` | A `teamId` that is neither a signed-up team nor the unchanged connection of that row's `id` (FR-004/027). The detail names the row |

Sets `Source = Manual` unless already `TugenyImport`, in which case it sets `EditedSinceImport = true`.

### `DELETE /events/{eventId}/results/ranking` → `204` · **EA**

Clears placements and matches and keeps the link (data-model state diagram). Same `403`/`404`/`409` as above.

### `PUT /events/{eventId}/results/tugeny-link` → `200 TugenyLinkDto` · **EA** · **RL**

```jsonc
// request
{ "address": "https://tugeny.org/tournaments/25-deutsche-meisterschaft/all-teams" }  // or a bare slug
// response
{ "tournamentId": 200, "slug": "…", "name": "…", "startDate": "2024-09-21", "linkedElsewhere": false }
```

| Status | When |
|---|---|
| `400` | Not a tugeny.org tournament address or slug (R6) |
| `404` | Tugeny has no such tournament (its body was `null`) |
| `409` | Cancelled or not a tournament. Linking **before** the start is allowed, since that is when the live link is useful |
| `503` | Tugeny unreachable or returning something unusable (FR-018). Problem detail "Tugeny can't be reached right now — try again in a few minutes." |

### `DELETE /events/{eventId}/results/tugeny-link` → `204` · **EA**

Never deletes results (FR-014).

### `GET /events/{eventId}/results/tugeny-import` → `200 TugenyImportPreviewDto` · **EA** · **RL**

Stateless draft (R7). Nothing is saved.

```jsonc
{ "tournamentName": "25. Deutsche Meisterschaft",
  "placements": [ { "position": 1, "name": "Seven Sins", "tugenyTeamId": 40 } ],
  "matchCount": 78,
  "signedUpTeams": [ { "teamId": "…", "teamSlug": "…", "teamName": "…" } ],
  "replacesExisting": true }
```

| Status | When |
|---|---|
| `409` | No link · cancelled · not started (FR-001) |
| `422` | `"notFinalized"`: Tugeny serves no ranking yet (FR-016). Problem `type` suffix `not-finalized`, so the client can point to paste and hand entry |
| `503` | As above |

### `POST /events/{eventId}/results/tugeny-import` → `200 TournamentResultDto` · **EA** · **RL**

Re-fetches and commits (R7).

```jsonc
{ "connections": [ { "tugenyTeamId": 40, "teamId": "…" } ] }   // each an explicit admin choice; may be empty
```

Status codes are the same as the preview, plus `422` when a connection names a team without a confirmed sign-up, or when two Tugeny teams map to one JuggerHub team (FR-005).

---

## Team history: on `TeamsController`

### `GET /teams/{slug}/placements?skip=&take=` → `200 PagedResult<TeamPlacementDto>`

Any signed-in user, the same audience as `GET /teams/{slug}/public`. `404` for an unknown slug. **Connected placements only** (FR-022, FR-026). Newest tournament first (`Event.StartsAt` desc, then `EventId`).

```jsonc
{ "eventId": "…", "eventName": "25. Deutsche Meisterschaft", "date": "2024-09-21",
  "position": 1, "rankedCount": 20 }
```

---

## Platform admin: `AdminResultsController` at `admin/results` · **PA**

### `GET /admin/results/placements?connected=false&q=&skip=&take=` → `200 PagedResult<AdminPlacementDto>`

`connected` defaults to `false` (the work queue). `q` searches `sourceName` and `name` (`ILike` + `Unaccent`, following the `AdminTeamService` pattern). Newest tournament first, then position.

```jsonc
{ "id": "…", "eventId": "…", "eventName": "…", "eventDate": "2024-09-21",
  "position": 3, "rankedCount": 20, "sourceName": "Ecplise", "name": "Eclipse",
  "team": null | { "slug": "eclipse", "name": "Eclipse" },
  "connectedBy": "Jan" | null, "connectedAt": "…" | null,
  "fromTugeny": true }
```

### `PUT /admin/results/placements/{id}/team` → `200 AdminPlacementDto`

```jsonc
{ "teamSlug": "eclipse" }
```

`404` for an unknown placement or team. `409` when that team is already placed in the same ranking (FR-005). It sets `TeamId`, `Name = Team.Name`, `ConnectedByUserId = caller`, `ConnectedAt = now`. **It touches exactly one row** (FR-025).

### `DELETE /admin/results/placements/{id}/team` → `200 AdminPlacementDto`

It sets `TeamId`, `ConnectedByUserId` and `ConnectedAt` to null, and `Name = SourceName`. It is idempotent.

**Team picker**: reuses the existing `GET /admin/teams?q=&skip=&take=` (`AdminTeamService.SearchAsync`). No new search endpoint.
