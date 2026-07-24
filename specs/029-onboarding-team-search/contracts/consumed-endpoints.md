# Contracts: Onboarding Team Search

**Feature**: `specs/029-onboarding-team-search/` | **Date**: 2026-07-24

**This feature adds no API surface.** No new endpoint, no changed request or response shape, no new
DTO, no OpenAPI change. What follows documents the two existing endpoints the step consumes, so the
contract this feature *depends on* is written down and can be checked if either ever changes.

---

## 1. Team search — `GET /api/v1/teams`

Owner: feature 007 (browse/search). Client: `SearchService.browseTeams()`.

**Requests this step makes** (only these two shapes):

```http
# On entering the step — the beginner-friendly opening list
GET /api/v1/teams?activeOnly=true&beginnersWelcome=true&sort=NameAsc&take=20

# After a typing pause — all teams matching the query
GET /api/v1/teams?q=berlin&activeOnly=true&sort=NameAsc&take=20
```

`beginnersWelcome` is present **only** when `q` is absent. That single difference is what makes
spec FR-002 and FR-003 both true.

**Response** (`PagedResult<TeamCard>`, unchanged):

```jsonc
{
  "items": [
    {
      "slug": "berlin-jugger",
      "name": "Berlin Jugger",
      "city": "Berlin",          // nullable
      "playerCount": 24,
      "beginnersWelcome": true,
      "logoInitial": "B"
    }
  ],
  "totalCount": 1,
  "skip": 0,
  "take": 20
}
```

**Depended-on behaviour**:
- Requires an authenticated session (feature 026); the step is behind the onboarding guard, so this
  always holds.
- Server-side visibility filtering is the boundary — the step renders whatever it is given and
  filters nothing itself.
- Being a `GET`, it inherits the retry interceptor's per-attempt time limit and jittered retries.

**Not used here**: `city`, paging beyond the first page, and any sort other than `NameAsc`.

---

## 2. Join request — `POST /api/v1/teams/{slug}/join-requests`

Owner: feature 009 (public team page). Client: `TeamService.requestToJoin(slug)`.

```http
POST /api/v1/teams/berlin-jugger/join-requests
Content-Type: application/json

{}
```

**Responses the step handles**:

| Status | Meaning | Step's behaviour |
|---|---|---|
| `204 No Content` | Request created, or one was already pending (idempotent) | Mark the slug asked; show the pending confirmation |
| `409 Conflict` | The caller is already a member of that team | "You're already on that team." No retry. |
| `401 Unauthorized` | Session gone | Handled by `authInterceptor` (refresh, then sign-in) — not this step's concern |
| anything else | — | "We couldn't send that request just now." No retry. |

**Depended-on behaviour** (verified in `backend/tests/JuggerHub.Api.IntegrationTests/Teams/JoinRequestTests.cs`):
- Creates a **pending** request, not a membership. A team admin approves or declines it. The step's
  confirmation copy depends on this and would become a lie if it ever changed.
- Idempotent while a request is pending, so a repeat press cannot produce two queue entries.
- Being a `POST`, it is time-limited but **never** auto-retried (constitution VII).

---

## UI contract (what this feature actually changes)

The only contract this feature alters is the onboarding step's own surface. Test ids, since the
existing specs and any future e2e depend on them:

| Test id | Element |
|---|---|
| `onboarding-team-search` | The search input — **now enabled** (was `disabled`) |
| `onboarding-team-row` | A result row (repeated; carries the slug) |
| `onboarding-team-ask` | The ask-to-join action, present only when a team is selected and not yet asked |
| `onboarding-team-loading` | The `jh-loading` line |
| `onboarding-team-error` | Search failure block, containing the retry action |
| `onboarding-team-empty` | No-results / empty message |
| `onboarding-team-confirmation` | The pending-request confirmation |
| `onboarding-team-request-error` | The quiet join-request failure line |
| `onboarding-continue`, `onboarding-skip` | **Unchanged**, and must remain enabled in every state |

**Removed**: the hardcoded `onboarding-team-Team A` / `onboarding-team-Team B` ids that the sample
rows generated. Nothing referenced them.
