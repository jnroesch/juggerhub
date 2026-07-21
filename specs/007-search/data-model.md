# Phase 1 Data Model — Search / Browse (007)

**Zero new entities, zero new enums.** Two new columns, several read-only card DTOs, three browse-query bindings, two write DTOs, one migration, added indexes, extended seeding. Everything else is derived from existing data.

## 1. Entity changes (2 columns)

### Team (extend) — `backend/Entities/Team.cs`

| New field | Type | Default | Notes |
|---|---|---|---|
| `BeginnersWelcome` | `bool` | `false` (DB default) | Self-managed by a team admin. Backs the "Beginners" row chip and the "Beginners welcome" filter. |

*No `Active` field.* "Active" is derived per request (see §3).

### PlayerProfile (extend) — `backend/Entities/PlayerProfile.cs`

| New field | Type | Default | Notes |
|---|---|---|---|
| `AppearInSearch` | `bool` | `false` (DB default) | Self-managed opt-in. **Privacy invariant**: only `true` rows are ever returned by player browse (all callers, all queries). |

> **Amended by feature 020 (2026-07-21):** `AppearInSearch` and its partial index were **dropped** (migration `RemoveAppearInSearch`), and it was removed from `UpdateProfileRequest`/`OwnerProfileDto`. Player browse now returns every non-banned player. See `specs/020-remove-search-optout/`.

*No position/looking-for-team/experience fields.* Position is derived from existing `ProfilePompfe` (see §3).

## 2. Card DTOs (read models — public fields only) — `backend/Dtos/Search/`

Projected directly in each search service via `.Select` (`AsNoTracking`), never exposing private data.

**`TeamCardDto`** — `Slug`, `Name`, `City?` (null for Mixteams), `PlayerCount` (`Memberships.Count`), `BeginnersWelcome`, `LogoInitial` (first letter of `Name`, computed client- or server-side). *No membership roster, no invitations.*

**`EventCardDto`** — `Id`, `Name`, `Type` (enum name), `CustomTypeLabel?`, `StartsAt`, `EndsAt`, `LocationKind` (enum name), `City?` (in-person), `VirtualLink?` omitted from card (link is on the detail page) → expose only a `LocationLabel` (city for in-person, "Online" for virtual). *No fee/IBAN, no admin/signup internals.*

**`PlayerCardDto`** — `Handle`, `DisplayName`, `Hometown?` (shown as "city"), `Positions` (`IReadOnlyList<Pompfe>` enum names, derived from `ProfilePompfe`), `HasAvatar` (bool; the avatar image is fetched from the existing `/profiles/{handle}/avatar`). *No email, no `UserId`, no onboarding/internal fields; non-opted-in players never appear.*

## 3. Derived values (no storage)

| Concept | Derivation |
|---|---|
| **Team "active"** | `team.Participations.Any(ep => ep.Event.StartsAt >= now - 12mo)` — EF `EXISTS` over the indexed `EventParticipations.TeamId` FK joined to `Events.StartsAt`. Window months from `SearchOptions` (default 12). |
| **Team `PlayerCount`** | `team.Memberships.Count` (projected in the card query). |
| **Player `Positions`** | `profile.Pompfen.Select(p => p.Pompfe)` — the existing `Pompfe` enum set (`Laeufer`/`Stab`/`QTip`/`Kette`/`Schild`/`Langpompfe`/`DoppelKurz`). Frontend maps to display labels + the wireframe's Runner/etc. |
| **Event "past"** | `event.EndsAt < now`. Hidden when "Hide past events" is ON (default). |

## 4. Browse-query bindings (`[FromQuery]`) — `backend/Dtos/Search/`

Each composes the shared `PaginationRequest` (skip/take) + a normalized free-text `Q` + per-entity filters + a `Sort` enum. Blank/whitespace `Q` ⇒ browse all. Unknown/invalid values fall back to defaults (never error).

**`TeamBrowseQuery`**: `Q?`, `ActiveOnly` (default `true`), `BeginnersWelcome?` (bool; when true, only beginners-welcome), `City?`, `Sort` (`NameAsc` default), + `PaginationRequest`. Search matches team `Name` **or** `City`.

**`EventBrowseQuery`**: `Q?`, `HidePast` (default `true`), `From?` (date), `To?` (date), `Type?` (`EventType`), `City?`, `Sort` (`StartsAtAsc` default), + `PaginationRequest`. Search matches event `Name`. Always excludes `Status == Cancelled`.

**`PlayerBrowseQuery`**: `Q?`, `Positions?` (`IReadOnlyList<Pompfe>`, multi-select; match players having **any** of the selected), `City?`, `Sort` (`DisplayNameAsc` default), + `PaginationRequest`. Search matches `DisplayName`. **Opt-in gate is not a query param** — it is applied unconditionally server-side.

### Sort enums (`Services/Search/SearchQuery.cs`)

- `TeamSort { NameAsc }` (v1 default only; room to add `PlayerCountDesc` later)
- `EventSort { StartsAtAsc }` (default; room for `StartsAtDesc`/`NewestCreated`)
- `PlayerSort { DisplayNameAsc }` (default)

Each sort **always** appends `.ThenBy(x => x.Id)` for stable paging (research §5).

## 5. Write DTOs

- **`UpdateTeamSettingsRequest`** — `{ bool BeginnersWelcome }`. Bound by the new admin-only `PATCH /api/v1/teams/{slug}`.
- **`UpdateProfileRequest`** (extend existing) — add `bool AppearInSearch`. Carried by the existing `PUT /api/v1/profiles/me`; only mutates the caller's own profile. `OwnerProfileDto` gains `AppearInSearch` so the owner screen can render the toggle state.

## 6. Indexes & migration — `AddDiscoveryFields`

The single migration:

1. `ALTER TABLE "Teams" ADD "BeginnersWelcome" boolean NOT NULL DEFAULT false;`
2. `ALTER TABLE "PlayerProfiles" ADD "AppearInSearch" boolean NOT NULL DEFAULT false;`
3. `CREATE EXTENSION IF NOT EXISTS unaccent;`
4. Indexes:
   - `CREATE INDEX … ON "PlayerProfiles" ("AppearInSearch") WHERE "AppearInSearch";` (partial — the opt-in scan)
   - `CREATE INDEX … ON "Events" ("StartsAt");` (hide-past, date range, default sort)
   - `CREATE INDEX … ON "Events" ("Status");` (exclude cancelled)
   - `CREATE INDEX … ON "EventParticipations" ("TeamId");` (active-team EXISTS) — add only if not already present from 005.

Configured in `AppDbContext.OnModelCreating` (column defaults + the indexes). Migration auto-applies on startup in every environment. `unaccent` is idempotent (`IF NOT EXISTS`).

*Trigram/GIN name-search indexes are intentionally NOT added in v1 (research §2) — a documented future optimization.*

## 7. Seeding — `DevDataSeeder` (Dev/local only)

Extend so each page has meaningful, varied data:

- **Teams**: a mix of **active** (with a recent `EventParticipation`) and **dormant** (none, or only > 12 mo old); some `BeginnersWelcome = true`; several cities (Köln, Berlin, Hamburg, Leipzig, Kiel) incl. at least one **Mixteam** (no city); enough to exceed one page (> 20) to exercise infinite scroll.
- **Events**: **past** (ended), **upcoming**, and at least one **cancelled**; mixed `Type` (Tournament/Workshop/Other) and cities; some virtual.
- **Players**: a mix of `AppearInSearch = true` and `false` (verify the opted-out never surface); varied `DisplayName`, `Hometown`, and `ProfilePompfe` positions; include accented cities (Köln) to exercise `unaccent`.

## 8. Validation rules

- `Q` is trimmed; whitespace-only ⇒ treated as absent (browse all). Min length from `SearchOptions` (e.g. 1) — below it, `Q` is ignored rather than erroring.
- `take` clamped by `PaginationRequest` (≤ 100, default 20); `skip` floored at 0.
- Event `From`/`To`: if both present and `From > To`, treat as no-range (or swap) — documented; never error.
- `Positions` unknown enum names ignored; empty ⇒ no position filter.
- `BeginnersWelcome` write: only a team **admin** may set it (`403` otherwise). `AppearInSearch` write: only on `me`.
- All browse endpoints are anonymous-safe and return `PagedResult<T>` with an accurate `TotalCount` for the same predicate.

## 9. Traceability (spec FR → model)

| FR | Model element |
|---|---|
| FR-003/004/005 | Browse-query bindings + server-side projected predicates (§4) |
| FR-008 | `PagedResult.TotalCount` + client word-summary of active filters |
| FR-010 | `PaginationRequest`/`PagedResult` + `Id` tiebreaker (§4) |
| FR-014 | Card DTOs expose public fields only (§2) |
| FR-020/023 | `TeamCardDto` + `BeginnersWelcome` column + `PATCH /teams/{slug}` (§1,§5) |
| FR-022 | Derived active EXISTS (§3) |
| FR-030/032 | `EventCardDto` + `HidePast`/cancelled predicates (§2,§4) |
| FR-040 | `PlayerCardDto.Positions` derived from pompfen (§2,§3) |
| FR-041/042/045 | `AppearInSearch` column + query-root gate + `PUT /profiles/me` (§1,§3,§5) |
| FR-043 | `PlayerBrowseQuery.Positions`/`City` (§4) |
