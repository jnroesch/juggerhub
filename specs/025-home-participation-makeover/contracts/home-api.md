# API Contract: Home (reshaped) — feature 025

All endpoints unchanged in **surface** (`HomeController`), reshaped in **payload**. JWT-cookie auth; acts only on the authenticated subject; every section entitlement-scoped server-side.

---

## `GET /api/v1/home` — composite dashboard

First-paint path. Returns a capped top-N per section for a fast first paint.

**Response `200` — `HomeDto`** (camelCase JSON, enums by name):

```jsonc
{
  "viewer": { "displayName": "Mara", "handle": "mara", "hasAvatar": true },
  "teams": [ { "slug": "rheinfire", "name": "Rheinfire", "role": "Admin" } ],

  "needsYou": [
    { "kind": "TeamInvite",      "id": "…", "title": "Rheinfire invited you", "context": "as a member", "linkTarget": "rheinfire", "occurredAt": "…" },
    { "kind": "MarketInvite",    "id": "…", "title": "Rheinfire want you",    "context": "Summer Slam · Läufer", "linkTarget": "<eventId>", "occurredAt": "…" },
    { "kind": "TrainingResponse","id": "<sessionId>", "title": "RSVP: Tue drills", "context": "Tue 19:00", "linkTarget": "<sessionId>", "occurredAt": "…" }
  ],

  "upNext": [
    { "kind": "Event",    "id": "<eventId>", "title": "League night", "startsAt": "…", "endsAt": "…", "locationLabel": "Köln",
      "typeLabel": "League", "spotsRemaining": 3, "participationLimit": 14, "mode": "Individuals",
      "viewerSignupId": "…", "viewerStatus": "Joined", "teamGoing": null },
    { "kind": "Training", "id": "<sessionId>", "title": "Thu drills", "startsAt": "…", "endsAt": null, "locationLabel": "Halle Süd",
      "trainingName": "Thu drills", "startTime": "19:00", "isPublicGuest": false, "myAnswer": "Going" }
  ],

  "openToEveryone": [ /* AgendaItemDto, Kind=Event — populated only for no-team viewers */ ],

  "news": [
    { "source": "party", "sourceName": "Rheinfire @ Summer Slam", "sourceSlugOrId": "<eventId>", "body": "Bring cash for the ref…", "createdDate": "…" },
    { "source": "team",  "sourceName": "Rheinfire", "sourceSlugOrId": "rheinfire", "body": "New kit arrived…", "createdDate": "…" }
  ],

  "activity": [
    { "kind": "TeammateJoinedEvent", "summary": "Jonas signed up for Summer Slam", "linkTarget": "<eventId>", "occurredAt": "…" },
    { "kind": "BadgeAwarded",        "summary": "You earned the \"Iron Wall\" badge", "linkTarget": "mara", "occurredAt": "…" }
  ]
}
```

**Variant rules**:
- **No-team viewer** (`teams.length == 0`): `upNext`, `news`, `activity` are empty/omitted for team-scoped content; `openToEveryone` is populated. Client shows the "find a team" prompt (FR-028).
- **Empty sections**: any section may be an empty array; the client hides "Needs you" entirely when empty (FR-005) and shows empty states / hides others (FR-030).
- **Removed keys**: `teamsActivity`, `tournaments`, `snapshots` no longer appear. (Frontend and backend ship together; no back-compat shim — spec assumption.)

---

## `GET /api/v1/home/up-next` — unified agenda, paginated ("see all")

`[FromQuery] PaginationRequest` → **`PagedResult<AgendaItemDto>`**.

- Same unified event + training merge as the composite, ordered soonest-first, de-duped by event.
- Near-window un-answered trainings are excluded (they live in Needs-you); answered and far-out trainings included.

---

## `GET /api/v1/home/news` — aggregated news, paginated ("see all")

`[FromQuery] PaginationRequest` → **`PagedResult<HomeNewsDto>`**.

- Team + event + **party** posts, merged newest-first. Party posts gated to `In` members (FR-023).

---

## Not added in v1

- **No `GET /home/activity` see-all.** The activity feed is home-preview only (capped). If added later, it is keyset-paginated by `occurredAt` (research R3). No unbounded list is ever exposed.
- **No new action endpoints.** "Needs you" resolves each item through its existing per-domain endpoint (team/party invite accept-decline, marketplace accept/decline, training `SetResponse`).

---

## Authorization notes (never trust the client)

- `needsYou` reads authoritative pending state from each source domain — a read/stale notification never produces a ghost action.
- `news` party rows require `PartyMemberStatus.In` on the viewer.
- `activity` entries are scoped to the viewer or the viewer's teams/parties in each source query; `roleChanged`/`trainingChanged` come from the viewer's own notification rows.
- All payload ids are render/navigation hints only; each action re-authorizes server-side.
