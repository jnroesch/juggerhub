# Phase 0 Research: Home dashboard & top-level navigation

**Feature**: 008-home-dashboard-nav | **Date**: 2026-07-07

This feature has no unknown technologies — it composes existing JuggerHub data behind read endpoints and reworks the Angular shell. The decisions below resolve the *design* choices that shape data-model, contracts, and tasks. Each is Decision / Rationale / Alternatives.

---

## §1 — Composite dashboard read vs. per-module endpoints

**Decision**: One **composite `GET /api/v1/home`** returning a `HomeDto` with the viewer summary plus a **capped top-N** for each module (Up next, Your teams activity, News, Tournaments, per-team snapshots), **plus** two paginated **"see all"** reads (`GET /home/up-next`, `GET /home/news`) for the full lists. Tournaments "see all" reuses the existing Browse events page; team activity "open team" reuses the existing team space.

**Rationale**: The landing screen must paint the greeting + Up next fast (SC-003). One round-trip of small bounded projected queries beats 4–5 parallel client calls (fewer connections, one auth check, no client-side orchestration, no waterfall). The modules are naturally capped ("top 5", "top 3"), so the composite never returns unbounded data (constitution III). "See all" is a separate, genuinely paginated concern and gets its own `PagedResult<T>` endpoints. This mirrors how a dashboard differs from a resource: it is a *view*, so it earns its own `HomeController` rather than being bolted onto `EventsController`/`TeamsController`.

**Alternatives considered**: (a) *Per-module endpoints only, client fan-out* — more HTTP, slower first paint, more client state; rejected. (b) *Everything paginated in the composite* — over-engineered for fixed top-N modules; rejected. (c) *GraphQL/BFF layer* — not in the stack; rejected.

---

## §2 — "Up next" as a union of the viewer's and their teams' sign-ups

**Decision**: Up next is the **union of two sources**, de-duplicated by event id, ordered soonest-first (`StartsAt`, then `Id`):
1. **Individuals-mode** events where the viewer has an `EventSignup` (`UserId == me`) — carries the viewer's `signupId` + `Status` so the frontend can render the RSVP/withdraw toggle.
2. **Team-mode** events where a team the viewer is a `TeamMembership` of has an `EventSignup` (`TeamId IN myTeams`) — carries `teamGoing { slug, name }`, **no personal signup id** (the team is the participant → read-only "your team is going").

Both exclude **past** (`EndsAt < now`) and **cancelled** (`Status == Cancelled`) events. Spots remaining is derived from `ParticipationLimit − occupied` where occupied = `Joined + AwaitingApproval` (reusing the `EventCapacity` definition; `Waitlisted` never counts).

**Rationale**: Directly matches the clarified behavior (team-mode shows "team is going", no per-member RSVP; individuals-mode is a toggle) and the events model's **mutually-exclusive subject** (`EventSignup` has exactly one of `UserId`/`TeamId`; enforced by a DB CHECK). The two sources can't collide on the same event (an event is one mode), so dedup only matters when the viewer is on **two teams that both entered the same team-mode event** → dedupe by event id keeps one row. Soonest-first with `Id` as the tiebreak gives stable pagination for "see all".

**Alternatives considered**: (a) *Only the viewer's personal sign-ups* — drops team matches the wireframe leads with; rejected (contradicts FR-013). (b) *Also surface open events the viewer could join but hasn't* — that is the **new-player "Open to everyone"** module, kept separate so the team-member Up next stays "things I'm committed to". (c) *A personal per-member RSVP on team events* — rejected at clarify (the model doesn't track per-member attendance for team entries).

**RSVP action**: **reuse** `POST /events/{id}/signup` (body `{ teamId: null }` for individuals-mode) and `DELETE /events/{id}/signup/{signupId}`. Home adds **no** new write endpoint — the composite/up-next read simply carries the viewer's `signupId` so the withdraw call has its target. The confirm-before-withdraw is a client concern.

---

## §3 — News aggregation across two source tables

**Decision**: Aggregate `TeamNewsPost` (from the viewer's **member teams**) and `EventNewsPost` (from events the viewer is **connected to** — has a personal/team sign-up to, or admins) into one list, each item **tagged by source** (`team` | `event`) with the source's name + a link target and `CreatedDate`, ordered **newest-first**. Implementation: query a **bounded window** (newest `W` from each table, `W` = configurable, default 50), project both to a common `HomeNewsDto`, **merge-sort in memory**, then apply the page (`Skip/Take`). The composite carries the top 5; `GET /home/news` pages within the window.

**Rationale**: There is no unified feed table today, and the two sources are different entities. A cross-table SQL `UNION ALL` with `ORDER BY … LIMIT/OFFSET` is possible but awkward through EF (heterogeneous projections) and premature at community scale. Reading a bounded newest-`W` slice from each indexed table and merging is correct for the newest pages (which is all a dashboard shows), cheap, and simple. The window bound guarantees no unbounded read (constitution III).

**Scale path (out of scope)**: when a community outgrows the window, introduce a denormalized `FeedItem` (or a DB view / materialized feed) written on post — the same `HomeNewsDto` shape, so the endpoint contract is unchanged. The deferred **official "League" source** slots in as a third source tag with zero contract change. Documented so the later feature is additive.

**"Connected events" predicate**: `EventNewsPost.EventId IN ( events where EXISTS EventSignup(UserId==me) OR EventSignup(TeamId IN myTeams) OR EventAdmin(UserId==me) )`. This is the entitlement boundary for event news and is asserted by an integration test.

**Alternatives considered**: (a) *SQL UNION view* — deferred to the scale path. (b) *Only team news* — drops event news the wireframe tags `EVENT`; rejected. (c) *Unbounded merge* — violates III; rejected.

---

## §4 — "My team" for a player on zero, one, or many teams

**Decision**: A small **`GET /api/v1/profiles/me/teams`** returns the viewer's memberships (`PagedResult<MyTeamDto>` — `slug`, `name`, `role`), loaded once by a frontend `MembershipService` signal. The shell's **"My team"** destination resolves its target from that list:
- **0 teams** → route to the **find-a-team** experience (Browse teams) — mirrors the dashboard new-player prompt.
- **1 team** → route to that team's space (`/t/{slug}`).
- **many** → route to a lightweight **team chooser** (a small `/my-team` page listing the player's teams) — no new backend, it reads the same `me/teams`.

The dashboard's **team-scoped modules aggregate all** of the viewer's teams (clarified): Your teams activity is merged across teams and tagged by team; the right rail shows **one snapshot per team** (name + next fixture, **no record**).

**Rationale**: The shell is on every screen and must not fetch the whole dashboard just to route "My team"; a tiny memberships read is the right seam and doubles as the source for the dashboard snapshots and the new-player detection (`hasTeam = count > 0`). Aggregating all teams (rather than a "primary team") is the clarified product decision and avoids inventing a stored preference.

**Alternatives considered**: (a) *Extend `GET /profiles/me`* to include teams — couples the profile-owner contract to the shell; a dedicated read is cleaner and independently cacheable. (b) *A stored primary team* — rejected at clarify. (c) *"My team" always goes to the first team* — loses the other teams for multi-team players; the chooser is a small, honest solution.

---

## §5 — Navigation shell: top bar + bottom tab bar from one model

**Decision**: Replace the off-canvas `sidebar` with a **desktop sticky top bar** and a **mobile fixed bottom tab bar**, both driven by a single `nav-model.ts` (the destination list + an `isActive(route)` matcher + the "My team" target resolver). Desktop top bar: brand · Home · Browse · My team · 🔔 Alerts (bell, count hidden at 0) · avatar-menu. Mobile: a **slim top strip** (wordmark + avatar) + a **bottom tab bar** (Home · Browse · My team · Alerts). Profile/Account/Sign out live in the **avatar menu** (desktop dropdown / reachable from the mobile top strip). Active destination is visually marked via `routerLinkActive`; Profile marks none.

**Rationale**: One model guarantees desktop and mobile expose the *same* destinations (SC-002) and keeps active-state logic in one place. A bottom tab bar is the thumb-reachable, ≤5-item mobile pattern the wireframe specifies; a single top bar is the desktop equivalent. Reusing `routerLinkActive` and the existing sticky-header/Tailwind tokens keeps it consistent with the current shell. Safe-area insets (`env(safe-area-inset-bottom)`) keep the bottom bar clear of the home indicator.

**Alternatives considered**: (a) *Keep the sidebar drawer* — contradicts the feature; rejected. (b) *Bottom bar on desktop too* — wastes vertical space; the wireframe's "1a" chose top-bar-on-desktop. (c) *Duplicate destination lists per component* — drift risk; the shared model prevents it.

**Accessibility**: `nav` landmarks with `aria-label`; the bottom bar items are links with `aria-current="page"` on the active tab; touch targets ≥44px (DESIGN.md); the avatar menu is a keyboard-navigable `menu`/`menuitem` with focus trap + Escape.

---

## §6 — Tournaments module

**Decision**: The Tournaments module reads **upcoming published Events of `Type == Tournament`** (`EndsAt >= now`, `Status == Published`), soonest-first, projected to a `TournamentCardDto` (id, name, city/location label, `StartsAt`, spots remaining). The composite carries the top 3; **"see all" links to the existing `/browse/events`** filtered to Tournament (feature 007), so no new tournaments endpoint is introduced.

**Rationale**: Tournaments are just a public event query; Browse events already lists/paginates/filters them. Reusing it avoids a redundant endpoint and keeps one place that lists tournaments. The composite's small top-N query is a cheap indexed read (`Events(StartsAt)`/`Events(Status)` indexes from 007).

**Alternatives considered**: A dedicated `GET /home/tournaments` — unnecessary given Browse events; rejected.

---

## §7 — Indexes & seeding

**Decision**: Add read indexes only where missing: `EventSignups(UserId)`, `EventSignups(TeamId)` (007 may already index `TeamId` for the active-team EXISTS — verify and skip if present), `TeamMemberships(UserId)`, `TeamNewsPosts(TeamId, CreatedDate DESC)`, `EventNewsPosts(EventId, CreatedDate DESC)`. One index-only migration `AddHomeIndexes`, auto-applied on startup. Extend `DevDataSeeder` with a **team-member demo player** (on ≥1 team; upcoming individuals sign-up + a team-entered tournament/match-style event; team news + event news; an upcoming tournament) and a **no-team demo player** (for the empty-state path).

**Rationale**: The up-next union and news aggregation filter on these FKs and order by `CreatedDate`; the indexes keep the composite's first-paint queries on the fast path. Seeding both player shapes lets `quickstart.md` and Playwright exercise both Home variants without manual data setup. Verifying-before-adding avoids duplicate indexes.

**Alternatives considered**: No indexes (rely on scans) — fine at tiny scale but cheap to do right; added. A denormalized feed/counter — deferred (see §3).
