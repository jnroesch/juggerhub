# Data Model: Tournament Results (050)

Three new tables, one migration (`AddTournamentResults`). **No existing table gains or loses a column.** All three entities derive from `BaseEntity` (UUIDv7 `Id`, interceptor-set `CreatedDate`/`ModifiedDate`, Principle III). Configuration goes in `AppDbContext.OnModelCreating` under a `// ---- Feature 050: Tournament results ----` block, the repo's only configuration style.

```text
Event 1 ──── 0..1 TournamentResult 1 ──── 0..128 TournamentPlacement ──── 0..1 Team
                          │                        ▲    ▲
                          └──── 0..n TournamentMatch ─┘    └─ (First/Second side, SetNull)
```

---

## TournamentResult

The results of one tournament event, plus its Tugeny link. It is created by the first action that needs it: linking, saving a ranking, or committing an import. It is never deleted by this feature. Clearing the results removes its placements and matches and keeps the row, so a link survives a clear (FR-014).

| Field | Type | Rules |
|---|---|---|
| `EventId` | `Guid` | **Unique.** FK → `Event`, `Cascade` (events are never hard-deleted; cancel is a status) |
| `Source` | `ResultSource` enum | `None` (link only, no ranking yet) · `Manual` · `TugenyImport`. Serialised by name |
| `ImportedAt` | `DateTime?` (UTC) | Set on import commit; null otherwise |
| `EditedSinceImport` | `bool` | False on commit; true after any hand save while `Source == TugenyImport` (FR-017) |
| `LastChangedByUserId` | `Guid?` | FK → `User`, **`Restrict`** (R10). Null only while `Source == None` |
| `ResultsChangedAt` | `DateTime?` (UTC) | When the ranking or matches last changed, shown on the event (FR-007). Distinct from `ModifiedDate`, which linking also touches |
| `TugenyTournamentId` | `int?` | Tugeny's id; null when unlinked. Indexed (non-unique) for the "linked elsewhere" check |
| `TugenySlug` | `string?` (150) | Used for the live link and the provenance link |
| `TugenyName` | `string?` (200) | Shown to confirm the link (FR-014) |
| `TugenyStartDate` | `DateOnly?` | Shown to confirm the link |
| `Placements` | nav | `Cascade` |
| `Matches` | nav | `Cascade` |

**State**:

```text
            link                   save ranking            import commit
(none) ─────────────► Source=None ──────────────► Manual ◄───────────────► TugenyImport
   │                                  ▲    │ clear                 │  hand save ⇒ EditedSinceImport=true
   └──────── save ranking / import ───┘    ▼                      │  clear ⇒ Source=None
                                    Source=None ◄─────────────────┘
```

- **Unlink** nulls the four `Tugeny*` columns and leaves everything else.
- **Clear** deletes placements and matches, sets `Source = None`, and nulls `ImportedAt`, `EditedSinceImport`, `ResultsChangedAt` and `LastChangedByUserId`.

---

## TournamentPlacement

One line of a ranking.

| Field | Type | Rules |
|---|---|---|
| `TournamentResultId` | `Guid` | FK → `TournamentResult`, `Cascade` |
| `Position` | `int` | ≥ 1. **Stored normalised** to standard competition ranking (1, 2, 3, 3, 5), computed on write from the submitted order and ties (R8) |
| `SortIndex` | `int` | Stable order within a tie (submission order) |
| `SourceName` | `string` (80) | What was typed, pasted or imported. Never changes after the row is created (R9) |
| `Name` | `string` (80) | What is shown. The team's name while connected; `SourceName` when not; the team's last name after the team is deleted |
| `TeamId` | `Guid?` | FK → `Team`, **`SetNull`** (EventParticipation precedent) |
| `ConnectedByUserId` | `Guid?` | FK → `User`, **`Restrict`**. Set exactly when `TeamId` is set by an action; null when unconnected (R10) |
| `ConnectedAt` | `DateTime?` (UTC) | As above |
| `TugenyTeamId` | `int?` | Only on imported rows. Used **inside one commit** to apply the admin's per-team choices and to resolve match sides. **Never read across results** (FR-025) |

**Indexes**:
- `(TournamentResultId, Position, SortIndex)` for ranking order.
- **Unique** `(TournamentResultId, TeamId) WHERE TeamId IS NOT NULL` (FR-005).
- `(TeamId)` for the team history.
- `(TeamId) WHERE TeamId IS NULL` is **not** needed: the admin "unconnected" list pages through `TeamId IS NULL` joined to `Event.StartsAt`. At the expected scale (hundreds of rows) a plain scan is fine. Revisit if the table grows past ~10⁵.

**Invariants** (enforced in the service; the DB index backs FR-005):
1. `TeamId != null ⇔ ConnectedByUserId != null ⇔ ConnectedAt != null`.
2. A connection may be created only by (a) an admin of the event, to a team with a `Joined` `EventSignup` at that event (R4), or (b) a platform admin, to any existing team.
3. Connecting sets `Name = Team.Name`. Disconnecting sets `Name = SourceName`.
4. At most 128 placements per result.

---

## TournamentMatch

One match of an **imported** tournament. There is no hand entry (spec Assumptions).

| Field | Type | Rules |
|---|---|---|
| `TournamentResultId` | `Guid` | FK → `TournamentResult`, `Cascade` |
| `SortIndex` | `int` | Order by Tugeny `timestamp`, then Tugeny's response order (R2). The only order ever used |
| `Stage` | `string?` (80) | Tugeny `group` (e.g. "Group 1"); **null for knockout matches**, rendered under one "Knockout" heading |
| `Name` | `string` (120) | Tugeny match name ("QF1 1-8", "Group A - 2. Match"); carries the round |
| `FirstName` / `SecondName` | `string` (80) | Name snapshots of the two sides |
| `FirstPlacementId` / `SecondPlacementId` | `Guid?` | FK → `TournamentPlacement` of the **same** result, **`SetNull`**. Resolved at commit through `TugenyTeamId`. A connected placement makes its matches link to the team with no per-match action |
| `FirstScores` / `SecondScores` | `int[]` | Per-set points, same length. Empty when the score text could not be parsed (R2) |
| `Winner` | `MatchWinner` enum | `First` · `Second` · `Draw` (Tugeny `victorious_team_id: null`) |

**Index**: `(TournamentResultId, SortIndex)` for paging.

---

## Enums (new file `Entities/ResultEnums.cs`)

```text
ResultSource { None = 0, Manual = 1, TugenyImport = 2 }
MatchWinner  { First = 0, Second = 1, Draw = 2 }
```

Serialised by name through the global `JsonStringEnumConverter` (ef-gotchas memory).

---

## Validation summary

| Rule | Where | Spec |
|---|---|---|
| Event is `Tournament`, not `Cancelled`, `StartsAt <= now` for any write | `TournamentResultService` | FR-001 |
| Caller is an event admin (writes) / platform admin (admin endpoints) | `EventAdminGuard` / `PlatformAdmin` policy | FR-008, FR-024 |
| ≤ 128 placements; names 1–80 after trim; positions 1–999; no team twice | service + unique index | FR-003, FR-005 |
| Team connection by an event admin requires a `Joined` entry, or an unchanged existing connection on the same row `id` | service | FR-004, FR-027 |
| Slug `^[a-z0-9]+(?:-[a-z0-9]+)*$` ≤ 150; host from configuration only | `TugenyLinkParser` | FR-014, R6 |
| Tugeny body ≤ 4 MiB | `ResponseSizeLimitHandler` | R5 |
| Import only when Tugeny serves a non-empty ranking | `TugenyImportService` | FR-016 |

## What is deliberately *not* modelled

- **A remembered Tugeny-team → JuggerHub-team mapping.** The owner rejected it (FR-025). `TugenyTeamId` is per-row and per-commit.
- **Connection history.** The current attribution answers FR-026 (R10).
- **Match timestamps.** Zoneless local times; order is enough (R2).
- **A short name for teams.** Out of scope (spec Assumptions).
- **Writes to `EventParticipation`.** They would switch on profile activity and team "active" flags (R3).
