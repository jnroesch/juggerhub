# Contract: Team member removal, leaving, and accepting invitations

**Feature**: 064 · No new route. Two existing routes change behaviour.

## `DELETE /api/v1/teams/{slug}/members/{targetUserId}` — unchanged responses, new side effects

Responses are exactly as today (FR-021):

| Status | When |
|--------|------|
| 204 | removed (an admin removing someone) or left (a member removing themselves) |
| 403 | the caller is not an admin and the target is not themselves |
| 404 `Member not found` | the target is not on the team (already left / removed meanwhile) |
| 404 `Team not found` | no such team, or the caller is not a member |
| 409 `Last admin` | the target is the team's only admin |

After a **204**, and only then, once the removal has committed:

| Caller vs target | Sent |
|------------------|------|
| admin removes someone else | `TeamMemberRemoved` → target; `TeamMemberDeparted{removed:true}` → every other current admin |
| a member removes themselves (leave) | `TeamMemberDeparted{removed:false}` → every current admin |

Nothing is sent on any non-204 answer. A failed delivery never changes the 204.

## `POST /api/v1/invitations/{token}/accept` — now rate-limited

New: policy `team-invite-accept`, **10 per player per clock hour** (fixed window, across all teams,
shared-link and addressed invitations alike).

| Status | When |
|--------|------|
| 200 `{ teamSlug }` | joined, or already a member (unchanged) |
| 404 / 409 | unknown / unusable invitation (unchanged) |
| **429** | the player has accepted ten team invitations in the current hour. **Our own limit: never retried**, on either hop. The client maps the status (never the body) to `teams.inviteLimited`. |

A refused accept adds no membership and consumes no invitation. `POST …/decline` and
`GET /invitations/{token}` are not limited.

## Party routes — unchanged

`DELETE /api/v1/parties/{id}/members/{userId}` and `DELETE /api/v1/parties/{id}` keep their
responses and send nothing new (FR-009). Only the page asks first.
