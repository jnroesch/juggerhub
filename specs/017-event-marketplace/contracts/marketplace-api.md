# Phase 1 Contracts: Event Marketplace REST API

Base: `api/v1`. JWT (httpOnly cookie / bearer) unless marked **anon**. Errors are RFC-7807
`ProblemDetails` mapped from `PartyOutcome` (404/403/400/409) exactly as `PartiesController.Fail`.
All lists take `?skip=&take=` (`PaginationRequest`) and return `PagedResult<T>`. Positions are arrays
of `Pompfe` **names** (e.g. `["Laeufer","QTip"]`).

## Board & mercenary side — `MarketController` (event-scoped)

Route prefix `api/v1/events/{eventId:guid}/market`.

| Method | Path | Auth | Purpose | Body / Query | Success |
|---|---|---|---|---|---|
| GET | `/free-agents` | **anon** | Free-agents board side | `?position=Schild&skip=&take=` | `200 PagedResult<MarketListingCardDto>` |
| GET | `/parties` | **anon** | Recruiting-parties board side | `?position=&skip=&take=` | `200 PagedResult<RecruitingPartyCardDto>` |
| GET | `/me` | user | Caller's market state for this event (eligibility, my listing, invites-to-answer, my-applications) | — | `200 MyMarketDto` (teams event) / `200` empty for non-teams |
| POST | `/listing` | user | Post my free-agent listing | `PostListingRequest { Positions[], Pitch }` | `201 MarketListingDto` |
| PUT | `/listing` | user | Edit my listing | `PostListingRequest` | `200 MarketListingDto` |
| DELETE | `/listing` | user | Take down my listing | — | `204` |
| POST | `/applications` | user | *Deprecated alias*; prefer party-scoped apply below | — | — |

Guards: `/me`, `/listing*` require the event to be a **teams** event and the caller **eligible**
(not In a party here) for POST/PUT; board GETs are public. `POST /listing` 409s if the caller is
ineligible or already has a listing (use PUT to edit).

## Party recruiting & handshake — `PartiesController` additions (party-scoped)

Route prefix `api/v1/parties/{id:guid}`. All require the caller to be a **party admin** unless noted.

| Method | Path | Auth | Purpose | Body | Success |
|---|---|---|---|---|---|
| GET | `/recruiting` | party admin | Recruiting settings + fill | — | `200 RecruitingSettingsDto` |
| PUT | `/recruiting` | party admin | Toggle/set recruiting | `SetRecruitingRequest { IsRecruiting, SpotsAdvertised, PositionsNeeded[], Blurb? }` | `200 RecruitingSettingsDto` |
| GET | `/market/applications` | party admin | Pending applications to this party | `?skip=&take=` | `200 PagedResult<MarketRequestDto>` |
| GET | `/market/invites` | party admin | Invites sent by this party (pending/declined) | `?skip=&take=` | `200 PagedResult<MarketRequestDto>` |
| POST | `/market/applications` | user (applicant) | Apply to this party (mercenary→party) | `ApplyRequest { Positions[] }` | `201 MarketRequestDto` |
| POST | `/market/invites` | party admin | Invite a user (board or direct) | `InviteRequest { UserId, Positions[] }` | `201 MarketRequestDto` |
| GET | `/market/user-search` | party admin | Search any eligible user for a direct invite | `?query=&skip=&take=` | `200 PagedResult<MarketInvitableUserDto>` |

Guards:
- `POST /market/applications` — caller is the **applicant** (any signed-in user), must be **eligible**,
  the party must be **recruiting** and have an open spot state (apply allowed even at cap? no — refuse
  with 409 Full when `OpenSpots == 0`), event open, ≤1 active request per pair.
- `POST /market/invites` — caller is **party admin**; target must be **eligible**; ≤1 active request
  per pair; event open. `UserId` may be any user (board card or direct search).
- Recruiting toggle refused (409 Closed) on a cancelled/ended event.

## Request actions — `MarketController` (request-scoped)

Route prefix `api/v1/market/requests/{id:guid}`. The service resolves the request's party/user/event
and authorizes by role.

| Method | Path | Auth | Purpose | Success |
|---|---|---|---|---|
| POST | `/accept` | recipient | Accept — seats the guest (atomic, cap-checked) | `200 MarketRequestDto` |
| POST | `/decline` | recipient | Decline (drops) | `200 MarketRequestDto` |
| POST | `/revoke` | initiator | Revoke/withdraw (drops) | `204` |

Recipient/initiator by direction:
- **Application** (user→party): recipient = a **party admin** (accepts/declines); initiator = the
  **applicant user** (revokes/withdraws).
- **Invite** (party→user): recipient = the **target user** (accepts/declines); initiator = a **party
  admin** (revokes).

`accept` runs under the `PartyCapacity` party-row lock: 409 **Full** if `OpenSpots == 0`; 409
**Conflict** if the accepter is no longer eligible; on success inserts the guest `PartyMember`
(`In`, `ViaMarket`), sets the request `Accepted`, revokes the joiner's other pending requests for the
event, deletes their listing, and (for an **Invite**) the notification is marked actioned.

## Dashboard — `MarketController` (user-scoped)

| Method | Path | Auth | Purpose | Success |
|---|---|---|---|---|
| GET | `api/v1/market/mine` | user | Cross-event summary: my pending invites (to answer) + my pending applications, newest-first | `200 PagedResult<MyMarketRequestDto>` |

## DTOs (shapes)

```text
MarketListingCardDto   { UserId, Handle, DisplayName, AvatarUrl?, Positions[], Pitch, MyRelation }
RecruitingPartyCardDto { PartyId, TeamId, TeamName, TeamSlug, EventId, OpenSpots, RosterCap, InCount,
                         PositionsNeeded[], Blurb?, MyRelation }
MyMarketDto            { Mode, Eligible, IneligibleReason?, MyListing: MarketListingDto?,
                         InvitesToAnswer: MarketRequestDto[], MyApplications: MarketRequestDto[] }
MarketListingDto       { Id, EventId, Positions[], Pitch }
MarketRequestDto       { Id, PartyId, TeamName, TeamSlug, EventId, EventName, UserId, Handle,
                         DisplayName, Direction, Positions[], Status, CreatedDate }
MyMarketRequestDto     { Id, PartyId, TeamName, EventId, EventName, Direction, Positions[], Status,
                         CreatedDate }   // dashboard, compact
RecruitingSettingsDto  { PartyId, IsRecruiting, SpotsAdvertised, PositionsNeeded[], Blurb?,
                         RosterCap, InCount, OpenSpots }
MarketInvitableUserDto { UserId, Handle, DisplayName, Hometown?, Relation }  // Invitable/Invited/Ineligible

PostListingRequest  { Positions: Pompfe[] (1..7), Pitch: string (1..280) }
ApplyRequest        { Positions: Pompfe[] (0..7) }
InviteRequest       { UserId: Guid, Positions: Pompfe[] (0..7) }
SetRecruitingRequest{ IsRecruiting: bool, SpotsAdvertised: int (0..RosterCap), PositionsNeeded: Pompfe[], Blurb?: string (<=500) }
```

`MyRelation` / `Relation` enums tell the client which affordance to render (e.g. a free-agent card
shows **Invite** only when the viewer is an admin of a recruiting party here with an open spot; a
recruiting-party card shows **Apply** only when the viewer is eligible). All affordances are re-checked
server-side; the client value is a hint.

## Notification payload (`MarketInvite`)

`{ requestId, partyId, teamName, teamSlug, eventId, eventName, positions[] }` — rendered with inline
**Accept**/**Decline** (posting to `market/requests/{requestId}/accept|decline`), mirroring the
`TeamInvite`/`PartyRequest` inline-action notifications.
