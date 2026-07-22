# Phase 1 Data Model: Home Participation Makeover

**No database entities or migrations change.** This feature only reshapes the read-side DTOs in `backend/Dtos/Home/HomeDtos.cs` (and their `home.models.ts` mirror). Every field below is projected from existing entities.

---

## Reshaped: `HomeDto`

```
HomeDto(
    ViewerSummaryDto           Viewer,          // unchanged
    IReadOnlyList<MyTeamDto>   Teams,           // unchanged (client derives no-team variant from Teams.Count)
    IReadOnlyList<NeedsYouItemDto>   NeedsYou,        // NEW — actionable, pinned top
    IReadOnlyList<AgendaItemDto>     UpNext,          // CHANGED — unified events + trainings
    IReadOnlyList<AgendaItemDto>     OpenToEveryone,  // CHANGED type — no-team discovery (events only, Kind=Event)
    IReadOnlyList<HomeNewsDto>       News,            // unchanged shape; +party source
    IReadOnlyList<ActivityEntryDto>  Activity)        // NEW — "What's going on"
```

**Removed** from `HomeDto`: `TeamsActivity`, `Tournaments`, `Snapshots`.
**Removed records**: `TournamentCardDto`, `TeamSnapshotDto`, `TeamActivityDto`, and `NextFixtureDto` (verify no external use before deleting).

---

## New: `NeedsYouItemDto`

A discriminated actionable item. `Kind` drives the icon, copy, and which action endpoints the client calls.

```
NeedsYouItemDto(
    NeedsYouKind  Kind,         // TeamInvite | PartyRequest | PartyCoAdminInvite | MarketInvite | MarketApplication
    string        Id,           // the underlying actionable row id (invitation token | requestId | partyId)
    string        Title,        // primary line, e.g. "Rheinfire invited you"
    string?       Context,      // secondary line, e.g. event name / team name
    string?       LinkTarget,   // optional route for "view" (event id, team slug)
    DateTime      OccurredAt)   // shown as a relative "when" + orders the block
```

> **Revised 2026-07-22**: `TrainingResponse` was removed from `NeedsYouKind` — "Needs you" is invites and requests only. Training RSVP lives inline in "Up next"; the near-window rule is withdrawn.

- `Kind` is a new string-serialized enum (`NeedsYouKind`).
- **Ordering**: newest-first, or a fixed priority order (invites → requests → trainings) — decided in tasks; default newest-first.
- **Actions**: resolved by the client via each domain's existing endpoint (accept/decline/respond). No new action endpoints.
- **Visibility**: the section is omitted/empty when the list is empty (client hides it) — FR-005.

## Validation / rules

- `MarketApplication` rows are shown as *pending* (no accept/decline for the applicant — they can only withdraw); `MarketInvite` rows carry accept/decline. This mirrors the current market card.

---

## New: `AgendaItemDto`

One item in the unified Up-next timeline. `Kind` selects which optional block is populated.

```
AgendaItemDto(
    AgendaKind    Kind,             // Event | Training
    string        Id,              // eventId (Event) | sessionId (Training)
    string        Title,
    DateTime      StartsAt,
    DateTime?     EndsAt,
    string        LocationLabel,

    // --- Event-only (Kind == Event) ---
    string?           TypeLabel,
    int?              SpotsRemaining,
    int?              ParticipationLimit,
    ParticipantMode?  Mode,            // Individuals | Teams
    Guid?             ViewerSignupId,  // set ⇒ viewer is going (individuals) → toggle to withdraw
    SignupStatus?     ViewerStatus,
    TeamGoingDto?     TeamGoing,       // set ⇒ team-mode, read-only "your team is going"

    // --- Training-only (Kind == Training) ---
    string?       TrainingName,
    string?       StartTime,           // HH:mm
    bool?         IsPublicGuest,
    TrainingRsvp? MyAnswer)            // Going | Maybe | Cant | null
```

- `TeamGoingDto` (existing) is reused.
- The list is sorted by `StartsAt` ascending, de-duped by event (multi-team viewer sees one row — FR-013).
- **All** of the viewer's upcoming trainings appear here (answered or not), each with an inline going/maybe/can't — FR-008. *(Revised 2026-07-22: trainings no longer route to "Needs you".)*

---

## New: `ActivityEntryDto`

A passive, read-only "What's going on" entry.

```
ActivityEntryDto(
    ActivityKind  Kind,        // TeammateJoinedEvent | NewTeamMember | BadgeAwarded | PartyMemberJoined | RoleChanged | TrainingChanged
    string        Summary,     // rendered sentence, e.g. "Jonas signed up for Summer Slam"
    string?       LinkTarget,  // optional route (event id, team slug, profile handle)
    DateTime      OccurredAt)  // newest-first ordering
```

- No action fields (FR-025).
- Derived from domain rows (`EventSignup`, `TeamMembership`, `BadgeAward`, `PartyMember`) and, for `RoleChanged`/`TrainingChanged`, from the viewer's passive `Notification` rows (see research R3).
- All entries scoped to the viewer or the viewer's teams/parties, server-side (FR-027a).

---

## `HomeNewsDto` (unchanged shape; new source value)

```
HomeNewsDto(string Source, string SourceName, string SourceSlugOrId, string Body, DateTime CreatedDate)
```

- `Source` gains the value `"party"` (alongside `"team"`, `"event"`). No structural change — the field already documents that new source values may appear.
- `SourceSlugOrId` for a party entry is the link target the client uses to open the party/event context.

---

## `HomeOptions` (config caps)

Add:
- `NearWindowDays` (default 14) — the FR-006a training near-window.
- `ActivityCap` — top-N activity entries in the composite.
- `NeedsYouCap` — top-N actionable items (with a "+N more" affordance if truncated — decided in tasks).

Existing caps (`UpNextCap`, `NewsCap`, `TeamsCap`, `OpenCap`) retained; `TournamentCap` removed.

---

## Frontend mirror (`home.models.ts`)

Mirror all of the above: add `NeedsYouItem`, `AgendaItem` (replacing `UpNextItem` usage in the composite — the `UpNextItem` interface may be retained for the see-all endpoint or folded into `AgendaItem`), `ActivityEntry`, and the `NeedsYouKind` / `AgendaKind` / `ActivityKind` string unions. Remove `TournamentCard`, `TeamSnapshot`, `NextFixture`, `TeamActivity`. Add `"party"` to `NewsSource`.
