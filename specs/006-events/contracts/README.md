# Contracts — Events (feature 006)

[`openapi.yaml`](./openapi.yaml) describes the HTTP surface for events. It is a **design contract** for `/speckit-tasks` and implementation, not a generated artifact — keep it in sync with `EventsController` / `EventInvitationsController` as they land.

## Conventions (inherited)

- Base path `**/api/v{version}**` (`v1`). JWT in an **httpOnly cookie** (bearer scheme); no tokens in `localStorage`.
- Lists use `skip`/`take` query params and return a **`PagedResult<T>`** envelope (`items`, `totalCount`, `skip`, `take`); `take` is capped server-side.
- Enums serialize as **names** (`"Tournament"`, `"AwaitingApproval"`, `"Link"`).
- Errors are RFC7807 **`ProblemDetails`**; no stack traces/secrets. Typical: `400` validation, `401` unauthenticated, `403` not an admin / not authorized, `404` unknown event, `409` capacity/last-admin/duplicate conflicts.

## Authorization map (server-enforced)

| Surface | Who |
|---|---|
| `GET /events/{id}`, `/participants`, `/news`, `/contacts` | **Anonymous** (public event fields only) |
| `POST /events` (create) | Any **authenticated** user |
| `POST /events/{id}/signup`, `DELETE …/signup/{signupId}` (withdraw) | **Authenticated**; team entry requires caller to **administer** that team; withdraw requires being the participant or an admin of the entered team |
| `PATCH /events/{id}`, `/cancel`, participant **approve/promote/remove**, news **post**, contacts **CUD**, admins list/remove/step-down, invitations | **Event admin** |
| `GET /event-invitations/{token}` (preview) | **Anonymous** (public fields + inviter) |
| `POST /event-invitations/{token}/accept`, `/decline` | **Authenticated** |

## Invariants surfaced as status codes

- **Capacity** — sign-up when full ⇒ `Waitlisted` (still `201`, body shows status); promote beyond limit ⇒ `409`.
- **No auto-promotion** — withdraw/remove never promotes; promotion is a distinct admin call.
- **Last-admin** — removing/stepping-down the final admin ⇒ `409`.
- **Edit guards** — changing `participantMode` with signups present, or lowering `participationLimit` below occupied ⇒ `409`/`400`.
- **Cancelled/ended** — sign-up/approve/promote on a cancelled or past event ⇒ `409`.
- **Duplicate** — second signup by same user/team ⇒ `409`.
- **Contact** — neither phone nor email ⇒ `400`.
