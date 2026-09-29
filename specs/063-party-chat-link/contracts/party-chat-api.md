# Contract: Party chat resolver (063)

Phase 1 for [plan.md](../plan.md). An addition to the chat API
([`../../019-chat/contracts/chat-api.md`](../../019-chat/contracts/chat-api.md)), and the sibling of
060's `GET /api/v1/chat/team/{teamId}`
([`../../060-team-chat-link/contracts/team-chat-api.md`](../../060-team-chat-link/contracts/team-chat-api.md)).

## `GET /api/v1/chat/party/{partyId}`

Returns the id of the party's own chat (`Kind = Party`) for a current crew member, **creating the
chat first if it does not exist yet**, exactly as opening the inbox does.

**Auth**: required (JWT in the httpOnly cookie), like every chat endpoint. No rate-limit policy.

**Path**: `partyId`, a GUID (`{partyId:guid}`). A non-GUID gives a routing 404.

### 200 OK

```json
{ "conversationId": "0193f1c2-…" }
```

- Always the conversation with `kind = "Party"` and `partyId = {partyId}`: the same id the inbox
  lists for that party's chat (FR-002).
- Returned to every crew member: party admins, crew members from the team, and marketplace guests.
- Returned whether or not the caller has hidden or muted the chat. Neither flag is changed
  (FR-012).
- Calling it again returns the same id. Concurrent first calls still leave exactly one chat
  (FR-013).

### 404 Not Found

The standard chat problem body (`ChatOutcome.NotFound`, identical to 060's):

```json
{ "type": "…", "title": "Not found", "status": 404, "detail": "No such chat." }
```

Identical for every case below, and nothing about them is distinguishable (FR-005):

- the caller is not in the party's crew: a team member who has not answered or has declined, a
  former crew member, or someone not on the team;
- the party does not exist, including a party that has been disbanded;
- the id is some other kind of entity's id (for example the party's team id).

A 404 request creates nothing for the party asked about (FR-007). It may still create the
**caller's own** missing team or party chats, as opening their inbox would.

### 401 Unauthorized

No valid session. The standard auth pipeline, unchanged.

## Changed behaviour of existing endpoints

None. `GET /api/v1/chat/team/{teamId}` answers exactly as before; its implementation now shares the
private three-step helper with this endpoint (research R1).

## Client

`ChatService.openPartyChat(partyId: string): Observable<PartyChatRef>` with
`PartyChatRef { readonly conversationId: string }`. The call is made when the button is pressed,
never on page load. It is a `GET`, so transient faults are retried by the interceptor, which is
safe because the call is replay-safe (060 research R3).
