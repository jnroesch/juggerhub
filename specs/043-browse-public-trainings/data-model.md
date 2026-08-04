# Phase 1 Data Model: Browse Public Trainings

**Feature**: 043 | **Date**: 2026-08-04

> **There is no schema change in this feature.** No entity is added, altered, or removed; no
> migration is generated. Everything below describes how *existing* persisted state is read. That is
> the headline fact — if a task list ever produces an `Add-Migration`, something has gone wrong.

---

## 1. Entities read (all unchanged)

| Entity | Columns this feature reads | Why |
|--------|---------------------------|-----|
| `TrainingSession` | `Id`, `TrainingId`, `TeamId`, `SessionDate`, `StartTimeOverride`, `EndTimeOverride`, `LocationKindOverride`, `LocationOverride`, `VenueNameOverride`, `StreetOverride`*, `PostalCodeOverride`*, `CityIdOverride`, `VisibilityOverride`, `Status` | The row itself, its effective display values, and the two gates (visibility, status) |
| `Training` | `Name`, `IsRecurring`, `StartTime`, `EndTime`, `LocationKind`, `Location`, `VenueName`, `Street`*, `PostalCode`*, `CityId`, `Visibility` | Series defaults behind every override |
| `Team` | `Slug`, `Name` | Row identity — "whose training is this" |
| `City` | `Name`, `CountryName`, `CountryCode` | City/country filtering and the label |
| `CityDistance` | `FromCityId`, `ToCityId`, `DistanceKm` | Nearest-first ordering |
| `PlayerProfile` | `CityId` (via `IProfileService.GetHomeCityIdAsync`) | The proximity anchor, resolved server-side from the caller |

\* `Street` / `PostalCode` are read only as part of resolving the address block's shape; they are
**not projected onto the card** — a browse list never renders a street (042's rule, restated in
`TrainingSessionRowDto`'s doc comment).

---

## 2. Effective-value rules

These are the whole feature. Each is an expression evaluated **in SQL**, on `TrainingSession s`.

### 2.1 Effective visibility — the gate

```csharp
s.VisibilityOverride ?? s.Training.Visibility
```

A row is listed **iff** this equals `TrainingVisibility.Public`. Identical to the expression already
used by `ChatLinkResolver.ResolveTrainingsAsync` (`ChatLinkResolver.cs:141`) and
`TrainingSeriesService.RowProjection` (`:433`).

Bidirectional by construction: a public session inside a team-only series is listed; a team-only
session inside a public series is not. **No viewer input, no membership join** — that is what makes
FR-004 structural rather than a rule someone must remember.

### 2.2 Effective status — the second gate

```csharp
s.Status == TrainingSessionStatus.Scheduled
```

One comparison excludes both `Cancelled` and `Skipped` (FR-005). Preferred over
`s.Status != Cancelled && s.Status != Skipped` because a future enum member defaults to hidden rather
than to leaked.

### 2.3 Effective address — ⚠ the indivisible block

Keyed on `CityIdOverride`. **Never per-field `??`.**

```csharp
hasOwnAddress = s.CityIdOverride != null

venue  = hasOwnAddress ? s.VenueNameOverride : s.Training.VenueName
legacy = hasOwnAddress ? s.LocationOverride  : s.Training.Location
city   = hasOwnAddress ? s.CityOverride      : s.Training.City
```

Resolving these per-field is a defect, not a style choice: a session relocated to a venue-less
address under a series that has a venue would render the **series'** venue name against the
**session's** street. See `TrainingSession.cs:41-59` and 042 research R1.

**The city id is the one exception where `??` would be equivalent** — the block is *keyed* on it, so
`s.CityIdOverride ?? s.Training.CityId` and the ternary form are the same value. Write the ternary
anyway (research R3): the shorthand is indistinguishable at a glance from the defect the entity
comment forbids.

### 2.4 Effective kind and times

Ordinary per-field coalescing — these are not part of the address block:

```csharp
kind      = s.LocationKindOverride ?? s.Training.LocationKind
startTime = s.StartTimeOverride    ?? s.Training.StartTime
endTime   = s.EndTimeOverride      ?? s.Training.EndTime
```

### 2.5 Location label — composed in memory, after paging

```csharp
TrainingSeriesService.LocationLabelFor(kind, city?.Name, venue, legacy)
// → Virtual: string.Empty
// → otherwise: HomeProjections.LocationLabel(city → venue → legacy)
```

`string.IsNullOrWhiteSpace` cannot be translated to SQL, so this runs after materialisation. **Call
the existing helper; do not copy it** — one implementation is what makes SC-003 (a training and an
event at the same address read identically) structural rather than a convention.

---

## 3. Read model — `TrainingCardDto`

New, in `Dtos/Search/SearchDtos.cs` beside `EventCardDto`.

| Field | Type | Notes |
|-------|------|-------|
| `SessionId` | `Guid` | Row identity; the link target is `/trainings/sessions/{SessionId}` |
| `TrainingId` | `Guid` | The owning series |
| `Name` | `string` | `Training.Name` |
| `TeamSlug` | `string` | Links the team name |
| `TeamName` | `string` | FR-008 — no other browse card carries this |
| `IsOneOff` | `bool` | `!Training.IsRecurring` → the One-off / Series badge |
| `SessionDate` | `DateOnly` | |
| `StartTime` | `TimeOnly` | Effective |
| `EndTime` | `TimeOnly` | Effective |
| `LocationKind` | `LocationKind` | The client renders "Online" from this |
| `Location` | `LocationDto?` | Canonical city, via `LocationLabels.ToLocation`; null when virtual or pre-042 |
| `LocationLabel` | `string` | Server-composed (§2.5) |

**Deliberately absent** (research R12): `GoingCount` / `MaybeCount` / `CantCount` / `MyAnswer` —
three correlated subqueries per row for decoration that reads as capacity, which trainings do not
have. Also absent: `Description`, `VirtualLink`, `Street`, `PostalCode`, `Visibility` (every listed
row is public by construction — shipping the field would invite a client-side check that is not the
boundary), `Status` (every listed row is `Scheduled`).

---

## 4. Query model — `TrainingBrowseQuery`

`{ get; init; }` with defaults, bound `[FromQuery]`, matching the other three (`SearchDtos.cs`).

| Field | Type | Default | Semantics |
|-------|------|---------|-----------|
| `Q` | `string?` | `null` | Accent/case-insensitive substring over `Training.Name` |
| `HidePast` | `bool` | `true` | `SessionDate >= today` (UTC, **day-granular** — research R2) |
| `From` | `DateOnly?` | `null` | `SessionDate >= From` |
| `To` | `DateOnly?` | `null` | `SessionDate <= To` |
| `City` | `string?` | `null` | Effective city name, accent-insensitive |
| `Country` | `string?` | `null` | Effective city's ISO code **or** country name |
| `Sort` | `TrainingSort` | `SessionDateAsc` | |

`TrainingSort` (new, in `Services/Search/SearchQuery.cs` beside `EventSort`):

```csharp
public enum TrainingSort
{
    SessionDateAsc = 0,
    Proximity = 1,   // feature 030 pattern; requires a home city
}
```

**Query normalisation** (research R1): when `Sort == Proximity` and `To is null`, set
`To = today.AddDays(14)` before filtering. The effective `From`/`To` are echoed in the response so
the chip states what actually happened.

---

## 5. Ordering

| Sort | Order by | Total |
|------|----------|-------|
| `SessionDateAsc` | `SessionDate`, `Id` | `q.CountAsync()` |
| `Proximity` | `DistanceKm`, `SessionDate`, `Id` | recomputed with the join's own predicate |

`Id` is the stable tiebreaker (UUIDv7, so ties break in creation order) — without it `Skip`/`Take`
can repeat or skip a row across pages (FR-019).

**Proximity join**, on the *effective* city id:

```csharp
from s in q
join d in _db.CityDistances.Where(cd => cd.FromCityId == homeCityId)
    on (s.CityIdOverride != null ? s.CityIdOverride : s.Training.CityId) equals (Guid?)d.ToCityId
orderby d.DistanceKm, s.SessionDate, s.Id
select s
```

The inner join is what excludes virtual and pre-042 cityless sessions (FR-022). The total **must** be
recomputed with the same `Any()` predicate — follow `TeamSearchService.cs:94-97`, **not**
`EventSearchService`, whose count is computed before the join and would overstate a proximity page
(research R5). This is how FR-023 is satisfied: an unknown distance excludes the row *and* the count
agrees, so nothing vanishes untraceably.

---

## 6. Indexes

No index is added. The query is served by what 030/042 already created:

- `IX_CityDistances_FromCityId_DistanceKm` — the proximity join and its ordering
- The `TrainingSessions` FKs on `TrainingId` / `CityIdOverride`, and `Trainings.CityId`

At the stated scale (tens of teams, low thousands of sessions) the default-sort path is a scan with a
sort; revisit only if the session table grows by orders of magnitude.

---

## 7. Invariants a test must pin

| # | Invariant | Guards |
|---|-----------|--------|
| DM-1 | A team-only session never appears, for **any** viewer, including members of the owning team | FR-003, FR-004, SC-002 |
| DM-2 | A public session inside a team-only series **does** appear; a team-only session inside a public series does not | FR-003 |
| DM-3 | `Cancelled` and `Skipped` never appear under any filter or sort | FR-005 |
| DM-4 | A relocated session (series has a venue, session's address has none) shows **no** element of the series' address | SC-004, edge case 1 — the 042 guard test, re-pointed at browse |
| DM-5 | A relocated session matches a city filter on **its** city and not on the series' city | SC-004 |
| DM-6 | A pre-042 training (legacy `Location`, no `CityId`) is still listed and still shows a label; matches no city filter; absent under proximity | Edge case 2, FR-022 |
| DM-7 | Identical address ⇒ byte-identical `LocationLabel` on the training card and the event card | SC-003 |
| DM-8 | Proximity `totalCount` equals the number of rows the view can actually produce | FR-023, R5 |
| DM-9 | Paging with `Skip`/`Take` across the whole result set repeats no session and skips none, under both sorts | FR-019 |
