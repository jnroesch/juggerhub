# Phase 0 Research — Search / Browse (007)

All Technical Context unknowns are resolved below. Each item is a design decision the plan and data-model depend on.

## 1. Endpoint placement — collection roots vs. a dedicated SearchController

**Decision**: Attach browse to the **existing resource controllers at their collection roots**: `GET /api/v1/teams`, `GET /api/v1/events`, `GET /api/v1/profiles`. Each binds a per-entity `*BrowseQuery` (filters + sort + `PaginationRequest`) and delegates to a service in `Services/Search/`.

**Rationale**: RESTful and discoverable — the collection GET *is* "list/browse with filters", matching the constitution's own example (`[HttpGet] GetUsers([FromQuery] PaginationRequest …)`). It keeps each resource's surface in one controller and avoids a cross-cutting `/search` controller that would duplicate resource knowledge. None of these roots is occupied today (only detail/sub-resource routes exist), so there is no collision.

**Alternatives considered**: (a) A single `SearchController` with `/search/teams|events|players` — rejected: splits a resource's surface across two controllers and invites a premature "global search" abstraction that is explicitly out of scope. (b) `/browse/*` API paths — rejected: `/browse` is a **frontend** route concern; the API stays resource-oriented.

## 2. Accent- and case-insensitive search ("koln" → "Köln")

**Decision**: Enable the PostgreSQL **`unaccent`** extension (via the migration: `CREATE EXTENSION IF NOT EXISTS unaccent`) and match with `EF.Functions.ILike(EF.Functions.Unaccent(column), EF.Functions.Unaccent(pattern))`, where `pattern = "%" + normalizedQuery + "%"`. `ILIKE` already gives case-insensitivity; `unaccent` folds diacritics on both sides. The query text is trimmed, collapsed, lower-bounded by a min length (blank/whitespace ⇒ no query = browse all), and never string-concatenated into SQL (parameterized through `EF.Functions`).

**Rationale**: Satisfies the spec's explicit edge case ("koln" matches "Köln") and SC-002 with zero new dependencies. `unaccent` is a standard contrib module available in the Postgres 18 image. Substring `ILIKE` is sufficient for community-scale name/city lookups; full-text/relevance ranking is out of scope (default sort governs order).

**Indexing**: For expected community data volumes, a sequential/`ILIKE` scan is fine and no trigram index is added in v1. If profiling later shows pressure, add a GIN `pg_trgm` index on `unaccent(name)` / `unaccent(city)` — noted as a future optimization, not a v1 task. (The opt-in, `StartsAt`, `Status`, and `EventParticipations.TeamId` indexes *are* added because they back equality/range predicates and the EXISTS.)

**Alternatives considered**: `citext` (case-insensitive only, no accent folding) — insufficient; application-side normalized shadow columns — more storage + write complexity than warranted; external search engine — massively out of scope.

**Verification (2026-07-07, live container DB)**: `unaccent` extension present and `unaccent('Köln') → 'Koln'`; the four browse indexes exist (`IX_PlayerProfiles_AppearInSearch` partial, `IX_Events_Status`, `IX_Events_StartsAt`, `IX_EventParticipations_TeamId`). `EXPLAIN` on the opt-in browse and the events hide-past/not-cancelled query both **seq-scan** at current seed volume — expected and optimal for single-page tables; the planner switches to the indexes as the tables grow. The `pg_trgm` GIN index stays deferred until profiling on production-scale data warrants it.

## 3. Team "active" derivation (12-month window)

**Decision**: A team is **active** iff it has at least one `EventParticipation` whose `Event.StartsAt >= now - 12 months`. Expressed as an EF `EXISTS`/`Any` correlated subquery in the browse predicate; **nothing is stored**. The "Active teams only" filter (default ON) adds this predicate; OFF removes it.

**Rationale**: The clarification chose exactly this rule ("participated in any event within the last 12 months") and flagged it as intentionally simple and extensible. `EventParticipation` already carries the real `TeamId` FK (added in 005) and joins to `Event.StartsAt`, so the data exists with no schema change. An indexed FK (`EventParticipations.TeamId`) keeps the EXISTS cheap.

**Edge cases**: Teams with no participations are dormant (correctly hidden by default). A Mixteam with participations counts as active like any other. The window boundary (exactly 12 months) is tested. Because "active" is derived per-request, it self-updates as events age out — no backfill or cron.

**Alternatives considered**: An explicit admin-managed active/dormant flag — rejected in clarification (relies on admins keeping it current, adds a field + UI). Membership-count-only ("has ≥1 member") — rejected: nearly every team qualifies, so the filter wouldn't narrow.

## 4. Player opt-in as a hard privacy invariant

**Decision**: Add `PlayerProfile.AppearInSearch bool NOT NULL DEFAULT false`. `GET /profiles` (browse) applies `WHERE AppearInSearch = true` as the **first, non-negotiable** predicate, before any query/filter/sort, for **both anonymous and authenticated** callers — there is no code path, parameter, or role that bypasses it. A partial index `WHERE AppearInSearch` keeps the filtered scan tight. The toggle is self-service on the owner's `PUT /api/v1/profiles/me` (a caller can only change their own).

**Rationale**: FR-041/042 make this the feature's central security property (SC-003 is a 100% invariant, not a sampled metric). Enforcing it at the query root — not as a filter the client passes — means a bug in filter handling can never leak a non-opted-in player. Default OFF is privacy-safe and means existing users are not retroactively exposed.

**Testing**: The invariant test enumerates: no query, every filter combination, every sort, anonymous vs authed, and a direct name search for a known non-opted-in player — none may return them. Toggling opt-in off mid-session removes the player on the next request.

**Alternatives considered**: A client-passed "includePrivate" style flag — rejected (never trust the client). Row-level security in Postgres — heavier than needed for one boolean and one endpoint; the query-root predicate is simpler and equally safe here.

## 5. Sorting, pagination, and stable ordering

**Decision**: Reuse `PaginationRequest` (skip/take, take capped at 100, default 20) and return `PagedResult<T>` for all three. Default sorts: **Teams A–Z (Name)**, **Players A–Z (DisplayName)**, **Events soonest-upcoming (StartsAt asc)**. **Every** sort appends a stable secondary key — the entity `Id` (UUIDv7, monotonic) — so ties never drop or duplicate a row across page boundaries. The count and the page are computed from **one shared predicate** (filter once, then `Count()` + `Skip/Take/Select`). The frontend renders this as infinite scroll (append pages) and **resets to skip=0** whenever the query or any filter changes.

**Rationale**: Matches the constitution's mandatory pagination and the existing paged endpoints (`GetMembers`, activity, news). Skip/Take is appropriate for these modestly sized, not-rapidly-churning lists; keyset is unnecessary now (noted as a future option if a list grows very large). The `Id` tiebreaker is what makes "scroll for more" safe.

**Alternatives considered**: Keyset/cursor pagination — deferred (more contract complexity than community-scale lists need). Separate count endpoint — unnecessary; the "Show N" live count reuses the same browse call with `take=0` (or reads `totalCount`).

## 6. Live "Show N" pending count in the filter panel

**Decision**: The filter panel's primary button shows a **live count for the pending (unapplied) filter selection**. Implement by calling the same browse endpoint with the pending filters and `take=0` (or a tiny take) and reading `totalCount`, debounced as the user toggles. Applying commits the pending filters to the active set; Reset clears pending back to defaults.

**Rationale**: One endpoint serves both the results and the count (`PagedResult.totalCount`), so no extra contract. `take=0` returns an empty `items` with the real `totalCount` — cheap. Keeps "pending vs applied" filter state entirely in the shell component.

**Alternatives considered**: A dedicated `/count` endpoint — redundant. Counting client-side — impossible (client never has non-matching rows, by design).

## 7. Row links & the auth-guarded team page (known limitation)

**Decision**: Browse rows link to the **canonical existing detail routes**: players → `/u/:handle` (public), events → `/events/:id` (public), teams → `/t/:slug`. The team route is **auth-guarded today** (no public team page exists). v1 keeps this link; an anonymous visitor who opens a team row hits the normal sign-in bounce. A dedicated public team page is **out of scope** for this feature and is recorded as a follow-up.

**Rationale**: Teams' internal space is members-only by current design (005); building a public team page is a separate feature with its own DTO/visibility decisions. Linking to the existing route is honest and consistent; the browse list itself (name/city/player-count/beginners) is fully public and useful without opening the page. Flagged in the plan's Constitution Check as a UX limitation, not a deviation.

**Follow-up**: "Public team page" (reuse the existing `GET /teams/{slug}/public` + `TeamPublicDto`) — a candidate backlog item, not built here.

## 8. Team `BeginnersWelcome` write path (Teams has no update endpoint)

**Decision**: Add `Team.BeginnersWelcome bool NOT NULL DEFAULT false` and a **new admin-only** `PATCH /api/v1/teams/{slug}` taking `UpdateTeamSettingsRequest { bool beginnersWelcome }`, delegating to `ITeamService.UpdateSettingsAsync` guarded by the existing team-admin check (mirrors `SetRole`/`RemoveMember` authorization). Wired into the existing **Team settings** screen.

**Rationale**: TeamsController currently has create/get/delete/members/news/invitations but **no team-field update**. Rather than overload another route, a small focused settings PATCH matches REST and leaves room for future team settings. Admin-guarded per constitution (server-side authz); non-admin ⇒ `403`.

**Alternatives considered**: Piggyback on team create only (no edit) — rejected: the flag must be changeable. A generic full-team PUT — heavier than one boolean needs; PATCH-settings is the minimal surface.

## 9. Shared shell strategy (one behaviour, three pages)

**Decision**: One `BrowseShellComponent` owns all shared behaviour (search input + debounce, Filters button + badge, Sort control, active-filter chip row, count line, results list, infinite scroll, and the empty/no-results/loading/error states, plus hosting the `FilterPanelComponent`). Each page (`browse-teams|events|players`) supplies a small **config**: entity labels, the filter schema (which controls render in the panel), sort options + default, the row template (via content projection / `ng-template`), and the fetch function that calls `SearchService`. `FilterPanelComponent` renders as a **bottom sheet ≤ mobile breakpoint, slide-over drawer above it** (CSS/media-query driven), and always includes the locked "Near me — coming soon" item.

**Rationale**: Directly delivers SC-004 (behaviourally identical pages) — there is literally one implementation of the behaviour. Per-entity differences are data (config), not divergent code. Matches Angular standalone + signals idioms already used in the app.

**Alternatives considered**: Three independent page components duplicating the shell — rejected (drift risk, fails SC-004 by construction). A fully generic table/list library — over-engineered for three known shapes.

## 10. Frontend route guarding & lazy loading

**Decision**: `browse`, `browse/teams`, `browse/events`, `browse/players` are **in-shell** (share the app chrome) but **un-guarded** (anonymous), and **lazy-loaded** (`loadComponent`) like events. `browse` redirects to `browse/teams`. A "Browse" nav entry is added to the shell side/top nav.

**Rationale**: Browse is anonymous by spec (FR-013); putting it in the shell keeps navigation consistent while `authGuard` is deliberately omitted. Lazy loading keeps these three pages out of the initial bundle (constitution VI / existing events precedent).

---

### Resolved unknowns summary

| Unknown | Resolution |
|---|---|
| Where do browse endpoints live? | Collection roots on existing controllers (§1) |
| How is search accent/case-insensitive? | Postgres `unaccent` + `ILIKE` via `EF.Functions` (§2) |
| What makes a team "active"? | Derived: `EventParticipation` within 12 months (§3) |
| How is the player opt-in enforced? | `AppearInSearch` predicate at query root, all callers (§4) |
| Pagination & stable order? | `PagedResult<T>` + `Id` tiebreaker; first-page reset on change (§5) |
| Live "Show N" count? | Same endpoint, `take=0`, read `totalCount` (§6) |
| Team row link with no public page? | Link `/t/:slug` (guarded); public team page deferred (§7) |
| Where does `BeginnersWelcome` get written? | New admin-only `PATCH /teams/{slug}` (§8) |
| How are the three pages identical? | One `BrowseShellComponent` + per-entity config (§9) |
| Guarding & bundling of browse routes? | In-shell, un-guarded, lazy-loaded (§10) |
