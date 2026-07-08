# Contract: Notifications API

Base path: `/api/v1/notifications`. All endpoints require authentication (JWT-in-cookie). The
recipient is always the authenticated subject — never a request parameter. Unknown/other-user
resources return 404 (no existence leak) or are simply never in scope.

## REST

### GET `/api/v1/notifications`
List the caller's notifications, newest-first, paginated.

- Query: `skip` (default 0), `take` (default 20, max 100) — bound via shared `PaginationRequest`.
- 200 → `PagedResult<NotificationDto>`.
- 401 if unauthenticated.

`NotificationDto`:
```jsonc
{
  "id": "guid",
  "type": "TeamInvite | TeamRoleChanged | TeamNews",
  "createdDate": "ISO-8601",
  "isRead": false,
  "actorDisplayName": "string | null",
  "resolved": false,              // TeamInvite only: underlying invite no longer usable
  "payload": {                    // shape depends on type (see data-model.md)
    // TeamInvite:      { invitationId, token, teamSlug, teamName, inviterName }
    // TeamRoleChanged: { teamSlug, teamName, newRole }
    // TeamNews:        { teamSlug, teamName, newsPostId, excerpt }
  }
}
```

### GET `/api/v1/notifications/unread-count`
- 200 → `{ "count": 3 }`.

### POST `/api/v1/notifications/{id}/read`
Mark one notification read. Idempotent.
- 204 on success (also 204 if already read).
- 404 if the id is not the caller's notification (no leak of existence).

### POST `/api/v1/notifications/read-all`
Mark all the caller's unread notifications read.
- 204. Body: none.

> Invite actions are **not** duplicated here. Inline Accept/Decline reuse the existing
> `POST /api/v1/invitations/{token}/accept` and `POST /api/v1/invitations/{token}/decline`.

## SignalR hub: `/hubs/notifications`

- Transport: WebSockets (with SignalR fallback). `[Authorize]` — anonymous handshakes rejected.
- On connect: server adds the connection to group `user:{authenticatedSubjectId}` (derived from the
  token, not from the client). No client-invokable server methods are required for MVP.

### Server → client events

- `notificationCreated` → `NotificationDto` — a new notification for this user.
- `unreadCountChanged` → `number` — the user's current unread count (sent after create and after a
  mark-read that originated elsewhere, so multiple tabs converge).

Clients treat both as advisory over a durable store: on (re)connect and on navigation to Alerts the
client re-fetches `GET /notifications` + `/unread-count` so a missed push self-heals (FR-019).

## New team-news endpoint (producer trigger)

### POST `/api/v1/teams/{slug}/news`
Admin-only. Posts a news update to the team; fans out `TeamNews` notifications to all other members.
- Body: `{ "body": "string (1..1000)" }`.
- 201 → `TeamNewsDto` (the created post).
- 400 on empty/too-long body.
- 403 if the caller is a member but not an admin.
- 404 if the team doesn't exist or the caller isn't a member (no leak).

## Authorization invariants (tested)

- A user requesting another user's notification id → 404, never the data.
- Anonymous → 401 on all REST endpoints; handshake rejected on the hub.
- A user only ever receives `notificationCreated`/`unreadCountChanged` for their own account.
- Non-admin posting team news → 403; the underlying action is refused server-side.
