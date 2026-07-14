# Contract — Party REST API

Base: `/api/v{version}/` (Asp.Versioning, as events/teams). All endpoints require authentication
unless noted. Authorization is enforced server-side; member-gated reads return **404** for
non-members (no existence leak, matching teams). Lists use the shared `PaginationRequest`
(`?skip=&take=`) → `PagedResult<T>`. Errors are generic problem responses; no stack traces/secrets.

Conventions: `party-admin` = caller has a `PartyMember` row with `Role = Admin`; `party-member` =
any `PartyMember` row or current team member viewing the request; `team-admin` = `TeamRole.Admin` on
the party's team.

## Discovery (drive the two existing pages)

### `GET /events/{eventId}/party-context`
For a **teams-only** event, the signed-in caller's party affordances.
- **200**: `{ mode: "Teams", rosterCap, teams: [{ teamSlug, teamName, partyId|null, canForm, myState }] }`
  where `teams` = teams the caller **administers**; `partyId` set if one already exists; `myState` ∈
  `{ none, in, declined, noResponse, admin }`.
- **200** with `teams: []` when the caller administers no team (viewing still open).
- **204/empty** shape for individuals-only events (client shows the normal individual join).

### `GET /teams/{slug}/party-requests`
The caller's visible **pinned party-request cards** for a team (member-gated).
- **200**: `PagedResult<PartyRequestCardDto>` — `{ partyId, event: {id,name,type,startsAt,endsAt},
  inCount, rosterCap, message, myState, isFull, appliedState }`.
- **404**: caller is not a member of the team (or team unknown).

## Party lifecycle

### `POST /parties`  — form a party (team-admin)
Body `{ eventId, teamId, message? }`.
- **201**: `PartyDto` (id, teamSlug, event summary, rosterCap, status=Open, myRole=Admin). Side
  effects: creator inserted `In+Admin`; participation request fanned out (notifications + email).
- **400**: event not teams-only / cancelled / ended, or `teamId` not administered by caller.
- **409**: a party already exists for `(teamId, eventId)`.

### `GET /parties/{id}` — party detail (party-member)
- **200**: `PartyDto` with roster summary (`inCount`, `declinedCount`, `noResponseCount`,
  `rosterCap`, `isFull`, `readiness`), `status`, `myRole`/`myState`, event summary, and
  `appliedEntry` (event-side group when Applied).
- **404**: caller not a team member.

### `POST /parties/{id}/apply` — apply to event (party-admin)
- **200**: `{ appliedGroup: "Joined"|"AwaitingApproval"|"Waitlisted", eventSignupId }`; sets
  `status=Applied`. Uses `EventCapacity` (event-row lock) + feature-006 free/paid/full routing.
- **400**: event cancelled/ended. **409**: team already entered on the event / already applied.
- **403**: caller not a party admin.

### `POST /parties/{id}/withdraw` — withdraw the team entry (party-admin)
- **200**: deletes the `EventSignup`, `status=Open` (party kept). **403** non-admin. **409** not applied.

### `DELETE /parties/{id}` — disband (party-admin)
- **204**: hard-deletes the party (+ members/news/invites); unpins the request; if `Applied`, also
  deletes the `EventSignup` (event withdrawal). Team/roster/badges untouched.
- **403**: caller not a party admin.

## Roster (answer & manage)

### `GET /parties/{id}/members?group=In|Declined|NoResponse` — roster group (party-member)
- **200**: `PagedResult<PartyMemberDto>` — `{ userId, handle, displayName, pompfe?, status, role,
  isYou }`. `NoResponse` is computed from current team memberships minus existing rows.
- **404**: caller not a team member.

### `POST /parties/{id}/join` — "I'm in" (team-member, self)
- **200**: `PartyMemberDto` (In). Capacity-checked under party-row lock. From `Declined` → `In`.
- **409**: party is **full** (auto-closed) — `{ reason: "full" }`.
- **400**: event cancelled/ended, or caller not a team member.

### `POST /parties/{id}/decline` — "can't make it" (team-member, self)
- **200**: `PartyMemberDto` (Declined). Reversible; keeps the request visible.

### `POST /parties/{id}/leave` — leave (self, currently In)
- **200/204**: row deleted → back to no-response; spot released. **409**: last admin cannot leave.

### `DELETE /parties/{id}/members/{userId}` — remove (party-admin)
- **204**: target row deleted; spot released; **team membership/badges untouched**.
- **403**: non-admin. **409**: would remove the last admin.

### `POST /parties/{id}/members/{userId}/nudge` — re-send request (party-admin)
- **202**: re-sends the request notification + email to that member (fresh dedupe key so it is not
  swallowed). **400**: target has already answered (In/Declined) — nudge only targets no-response.
- **403**: non-admin.

## Party news (private feed)

### `GET /parties/{id}/news` — feed (crew only)
- **200**: `PagedResult<PartyNewsDto>` — `{ id, authorDisplayName, authorRole, body, createdDate }`,
  newest-first. **404**: caller not **in** the party (private to the crew — decliners and team
  non-members refused too).

### `POST /parties/{id}/news` — post (party-admin)
- **201**: `PartyNewsDto`. Side effect: notifies every **in** crew member except the author (in-app
  `PartyNews` notification + email, respecting preferences). **403**: non-admin. **400**:
  empty/oversized body.

## Co-admin invitations (mirror events; team-scoped)

### `GET /parties/{id}/invitations/link` · `POST /parties/{id}/invitations/link` (party-admin)
Get/rotate the shared invite link. **200**: `{ url, token, expiresDate } | null`.

### `GET /parties/{id}/invitations` (party-admin)
- **200**: `PagedResult<PartyInvitationDto>` — pending invites (targeted show target display name).

### `POST /parties/{id}/invitations` — targeted (party-admin)
Body `{ targetUserId }`. **201**: `PartyInvitationDto`. **400**: target not a member of the party's
team, or already a party admin. **409**: already invited.

### `GET /parties/{id}/invitations/member-search?query=` (party-admin)
- **200**: `PagedResult<PartyInvitableUserDto>` — **team members** matching the query with relation
  (`invitable`/`invited`/`admin`). Scoped to `TeamMembership` of the party's team.

### `DELETE /parties/{id}/invitations/{invitationId}` (party-admin)
- **204**: revoke a pending invite. **403**: non-admin.

### `GET /party-invitations/{token}` — preview (auth)
- **200**: `{ partyId, teamName, eventName, startsAt, inviterName, state: Usable|Expired|Invalid }`.

### `POST /party-invitations/{token}/accept` — accept (auth)
- **200**: `{ partyId }`; grants `Admin` role (joins In if room). **403**: not a member of the
  party's team. **409**: party full (cannot seat a new admin). **410**: link expired/invalid.

### `POST /party-invitations/{token}/decline` — decline a targeted invite (auth)
- **200**: marks the targeted invite declined (link invites are simply ignored).

## Changed existing endpoints

### `POST /events/{id}/signup` (feature 006)
- For **teams-only** events, a team subject is now **refused** (**400** `{ reason: "useParty" }`)
  — direct team-join is removed. Individuals-only behavior is unchanged.

### `POST /events` / event-create (feature 006)
- Accepts `rosterCap` when `participantMode = Teams` (default 8, **min 5**); rejected (**400**) if
  below 5 or supplied for an individuals-only event.
