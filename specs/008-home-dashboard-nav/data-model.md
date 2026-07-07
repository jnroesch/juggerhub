# Phase 1 Data Model: Home dashboard & top-level navigation

**Feature**: 008-home-dashboard-nav | **Date**: 2026-07-07

**No new entities. No new columns. No new enums.** This feature reads and composes existing data. What follows is the **DTO / read model**, the **entitlement predicates** that scope each read, the **caps**, and the **index/seed** notes. All field names are camelCased over the wire (existing global JSON policy); `EventType`/`SignupStatus` serialize as **names** via the existing global `JsonStringEnumConverter`.

## Existing entities consumed (read-only)

| Entity | Used for | Key fields read |
|---|---|---|
| `User` + `PlayerProfile` | Viewer greeting, avatars, names | `Profile.DisplayName`, `Profile.Handle`, avatar presence |
| `TeamMembership` | "My teams", entitlement scoping, snapshots | `UserId`, `TeamId`, `Role`, `JoinedDate` |
| `Team` | Team name/slug for snapshots + tags | `Slug`, `Name` |
| `Event` | Up next, tournaments, next fixture | `Type`, `Status`, `StartsAt`, `EndsAt`, `Name`, `City`/`VenueName`/`Location`, `ParticipantMode`, `ParticipationLimit` |
| `EventSignup` | Up next union, occupied count, viewer's toggle | `EventId`, `UserId`, `TeamId`, `Status` |
| `TeamNewsPost` | News (source = team) | `TeamId`, `Body`, `CreatedDate`, `Author` |
| `EventNewsPost` | News (source = event) | `EventId`, `Body`, `CreatedDate`, `Author` |
| `EventAdmin` | "Connected events" for event-news entitlement | `EventId`, `UserId` |

`EventCapacity` (existing helper) defines **occupied = Joined + AwaitingApproval**; `spotsRemaining = max(ParticipationLimit − occupied, 0)`.

## Response DTOs (`backend/Dtos/Home/`)

> Records; projected directly via `.Select` / `Expression<>` in `HomeProjections.cs`. Public/permitted fields only.

### `HomeDto` — composite (`GET /api/v1/home`)
```
HomeDto(
  ViewerSummaryDto viewer,
  IReadOnlyList<MyTeamDto> teams,               // all of the viewer's teams (capped)
  IReadOnlyList<UpNextItemDto> upNext,          // capped top-N, soonest-first
  IReadOnlyList<TeamActivityDto> teamsActivity, // capped, merged across teams, newest-first
  IReadOnlyList<HomeNewsDto> news,              // capped top-N, newest-first
  IReadOnlyList<TournamentCardDto> tournaments, // capped top-N, soonest-first
  IReadOnlyList<TeamSnapshotDto> snapshots      // one per team (capped)
)
```
`hasTeam` is derivable client-side from `teams.length > 0`; the frontend uses it to pick the **team-member** vs **new-player** Home variant. For a new-player viewer, `upNext`/`teamsActivity`/`snapshots` are empty and the frontend swaps in the find-a-team prompts + the **Open to everyone** module (which reuses `UpNextItemDto`, fed by `openToEveryone` when present — see below).

### `ViewerSummaryDto`
```
ViewerSummaryDto(string displayName, string handle, bool hasAvatar)
```

### `MyTeamDto` — also the payload of `GET /profiles/me/teams`
```
MyTeamDto(string slug, string name, TeamRole role)
```

### `UpNextItemDto` — Up next + Open to everyone
```
UpNextItemDto(
  Guid eventId,
  string title,
  string typeLabel,          // EventType name (or CustomTypeLabel when Other)
  DateTime startsAt,
  DateTime endsAt,
  string locationLabel,      // city / venue / legacy Location, best available
  int spotsRemaining,
  int participationLimit,
  ParticipantMode mode,      // Individuals | Teams
  Guid? viewerSignupId,      // set ⇒ individuals-mode, viewer is signed up (enables withdraw)
  SignupStatus? viewerStatus,// Joined | AwaitingApproval | Waitlisted (individuals-mode)
  TeamGoingDto? teamGoing    // set ⇒ team-mode: which of the viewer's teams entered (read-only)
)
TeamGoingDto(string slug, string name)
```
- **Individuals-mode, viewer signed up** → `viewerSignupId`+`viewerStatus` set, `teamGoing` null → frontend shows "going ✓" (toggle to withdraw).
- **Individuals-mode, viewer not signed up** (only in **Open to everyone**) → all three null → frontend shows an **RSVP** button (`POST /events/{id}/signup`, `teamId: null`).
- **Team-mode** (a viewer's team entered) → `teamGoing` set, `viewerSignupId` null → read-only "your team is going".

### `HomeNewsDto`
```
HomeNewsDto(
  string source,         // "team" | "event"  (a future "league" adds a value; contract unchanged)
  string sourceName,     // team name or event name
  string sourceSlugOrId, // team slug or event id → link target
  string body,
  DateTime createdDate
)
```

### `TeamActivityDto`
```
TeamActivityDto(string teamSlug, string teamName, string summary, DateTime occurredAt)
```
Sourced from `TeamNewsPost` today (aggregated across the viewer's teams, newest-first). Reuses the existing team-activity shape where practical; the richer roster/result activity is a later feature.

### `TournamentCardDto`
```
TournamentCardDto(Guid eventId, string name, string locationLabel, DateTime startsAt, int spotsRemaining)
```

### `TeamSnapshotDto` — desktop right rail (one per team)
```
TeamSnapshotDto(
  string slug,
  string name,
  NextFixtureDto? nextFixture   // soonest upcoming event the team is in; null ⇒ "no upcoming fixture"
)
NextFixtureDto(Guid eventId, string name, DateTime startsAt)
```
**No win/loss record** (match/results modeling deferred — clarified 2026-07-07).

## Entitlement predicates (server-side, non-negotiable)

Let `me` = caller id (from JWT `sub`); `myTeams` = `TeamMemberships.Where(m => m.UserId == me).Select(m => m.TeamId)`.

| Read | Predicate |
|---|---|
| Up next (personal) | `EventSignups.Where(s => s.UserId == me)` → individuals-mode events, future, not cancelled |
| Up next (team) | `EventSignups.Where(s => myTeams.Contains(s.TeamId!.Value))` → team-mode events, future, not cancelled |
| Team activity / Your teams | `TeamNewsPosts.Where(n => myTeams.Contains(n.TeamId))` |
| News (team source) | same as above |
| News (event source) | `EventNewsPosts.Where(n => connectedEventIds.Contains(n.EventId))` where `connectedEventIds = events with EventSignup(UserId==me) OR EventSignup(TeamId in myTeams) OR EventAdmin(UserId==me)` |
| Tournaments | public: `Events.Where(e => e.Type == Tournament && e.Status == Published && e.EndsAt >= now)` |
| Snapshots / next fixture | per team in `myTeams` only |
| `me/teams` | `TeamMemberships.Where(m => m.UserId == me)` |

**Invariant (tested):** no read ever returns a sign-up whose subject is neither `me` nor one of `myTeams`, nor news from a team the caller is not a member of / an event they are not connected to — asserted across the composite and both "see all" reads for authenticated callers.

## Ordering, caps & pagination

- **Up next**: order `StartsAt ASC, EventId ASC`; dedupe by `eventId` (multi-team same event). Composite cap `HomeOptions.UpNextCap` (default 5). `GET /home/up-next` → `PagedResult<UpNextItemDto>` via `Skip/Take`.
- **News**: order `CreatedDate DESC, Id DESC` after the bounded-window merge (research §3). Composite cap `NewsCap` (default 5). `GET /home/news` → `PagedResult<HomeNewsDto>` paged within window size `NewsWindow` (default 50 per source).
- **Team activity**: order `occurredAt DESC`; composite cap `ActivityCap` (default 6).
- **Tournaments**: order `StartsAt ASC, EventId ASC`; composite cap `TournamentCap` (default 3).
- **Snapshots / teams**: capped `TeamsCap` (default 12) — a sane bound for the aggregate.
- Every "see all" list carries a **stable secondary key** (`Id`) so paging never drops/dupes on ties (constitution III).

## `HomeOptions` (`backend/Common/HomeOptions.cs`, optional, safe defaults)
```
UpNextCap=5, NewsCap=5, ActivityCap=6, TournamentCap=3, TeamsCap=12, NewsWindow=50
```
Bound from config with the defaults above; no secrets.

## Indexes (`AddHomeIndexes`, index-only migration)

Add where not already present (verify against `AppDbContext`/007's migration first):
- `EventSignups(UserId)`, `EventSignups(TeamId)`
- `TeamMemberships(UserId)`
- `TeamNewsPosts(TeamId, CreatedDate DESC)`
- `EventNewsPosts(EventId, CreatedDate DESC)`

All support the entitlement/order predicates above. Auto-applied on startup (env parity).

## Seeding (`DevDataSeeder` extension)

- **Team-member demo player**: member of ≥1 team; has an upcoming **individuals-mode** sign-up (Joined) and belongs to a team that **entered** an upcoming team-mode event; the team has `TeamNewsPost`s and one of its events has `EventNewsPost`s; an upcoming **Tournament** exists. Exercises the full team-member Home + RSVP toggle.
- **No-team demo player**: no `TeamMembership`; upcoming open individuals-mode events exist for the **Open to everyone** module; global News still present. Exercises the new-player variant.
