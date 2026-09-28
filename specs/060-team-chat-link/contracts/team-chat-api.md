# Contract: Team chat resolver (060)

Phase 1 for [plan.md](../plan.md). An addition to the chat API
([`../../019-chat/contracts/chat-api.md`](../../019-chat/contracts/chat-api.md)), and the sibling of
027's `GET /api/v1/chat/contact/team/{teamId}`
([`../../027-contact-admins/contracts/chat-contact-api.md`](../../027-contact-admins/contracts/chat-contact-api.md)).

## `GET /api/v1/chat/team/{teamId}`

Returns the id of the team's own chat (`Kind = Team`) for a current member, **creating the chat
first if it does not exist yet**, exactly as opening the inbox does.

**Auth**: required (JWT in the httpOnly cookie), like every chat endpoint. No rate-limit policy.

**Path**: `teamId`, a GUID (`{teamId:guid}`). A non-GUID gives a routing 404.

### 200 OK

```json
{ "conversationId": "0193f1c2-…" }
```

- Always the conversation with `kind = "Team"` and `teamId = {teamId}`: the same id the inbox
  lists for that team's chat. **Never** a `TeamInquiry` thread, even for an admin who is a member
  of one (FR-002).
- Returned whether or not the caller has hidden or muted the chat. Neither flag is changed
  (FR-012).
- Calling it again returns the same id. Concurrent first calls still leave exactly one chat
  (FR-015).

### 404 Not Found

The standard chat problem body (`ChatHttp.Fail`, `ChatOutcome.NotFound`):

```json
{ "type": "…", "title": "Not found", "status": 404, "detail": "No such chat." }
```

Identical for every case below, and nothing about them is distinguishable (FR-005):

- the caller is not a current member of the team (including a player with a pending join request);
- the team does not exist;
- the id is some other kind of entity's id.

A 404 request creates nothing for the team asked about (FR-007). It may still create the
**caller's own** missing team or party chats, as opening their inbox would.

### 401 Unauthorized

No valid session. The standard auth pipeline, unchanged.

## Changed behaviour of existing endpoints

`GET /api/v1/chat/conversations` (the inbox): a team whose only conversation was a Contact-admins
thread now gets its team chat created and listed on the next load. Before this change it never
did (research R1). No field, status or parameter changed.

## Client

`ChatService.openTeamChat(teamId: string): Observable<TeamChatRef>` with
`TeamChatRef { readonly conversationId: string }`. The call is made when the button is pressed,
never on page load. It is a `GET`, so transient faults are retried by the interceptor, which is
safe because the call is replay-safe (research R3).
