# Phase 0 Research: Event Marketplace (Mercenaries)

All decisions below were resolved from the spec, the wireframes (screens 7a–7g), and the existing
feature-006 (events) and feature-016 (parties) code. No open `NEEDS CLARIFICATION` remain — the three
genuine product decisions were locked with the requester (spec `## Clarifications`, 2026-07-14).

## 1. Recruiting state — fields on `Party` vs. a separate entity

- **Decision**: Store recruiting as **columns on `Party`** — `IsRecruiting` (bool, default false),
  `SpotsAdvertised` (int), `RecruitBlurb` (string?, ≤500), `PositionsNeeded` (`int[]` of `Pompfe`).
- **Rationale**: Recruiting is a 1:1 aspect of an existing party managed from the same "Manage party"
  screen; a separate table would add a join for every board read and every party projection with no
  independent lifecycle (it lives and dies with the party). The wireframe (7c) shows it as a block
  inside Manage party. `int[]` positions match the profile pompfen shape and let the board filter with
  a Postgres array-overlap operator.
- **Alternatives**: A `PartyRecruitment` 1:1 entity (rejected — extra table/join, no lifecycle of its
  own); a many-to-many `PartyRecruitmentPosition` table (rejected — over-normalized for an ephemeral,
  small, read-as-a-set list).

## 2. Guest membership — explicit marker vs. derive from "no team membership"

- **Decision**: Add **`PartyMember.ViaMarket`** (bool, default false). An accepted mercenary is a
  normal `PartyMember` (`Status = In`, `Role = Member`) with `ViaMarket = true`.
- **Rationale**: Deriving "guest = In row whose user is not a current team member" requires a
  membership join on every roster read and is fragile if a guest later joins the team. An explicit
  flag is a single boolean, renders the "guest · via market" tag directly, and keeps the 016 roster
  queries a one-line change (OR the flag into the existing team-member filter).
- **Alternatives**: Derive from absence of `TeamMembership` (rejected — join-heavy, ambiguous if the
  user is coincidentally on the team); a separate `PartyGuest` entity (rejected — a guest is a party
  member in every respect but the tag; splitting it would fork the roster/capacity code).

## 3. The handshake — one directional `MarketRequest` vs. two entities

- **Decision**: One **`MarketRequest`** entity: `PartyId`, `UserId`, `Direction`
  (`Application` = user→party, `Invite` = party→user), `Positions` (`int[]` of `Pompfe`), `Status`
  (`Pending`/`Accepted`/`Declined`/`Revoked`), `CreatedByUserId`, timestamps.
- **Rationale**: Both directions share the same shape, the same accept/decline/revoke transitions, the
  same "≤1 active per (party, user)" rule, and populate the same two inboxes. One table with a
  direction discriminator is the same pattern feature-006 uses for link vs targeted invitations
  (`InvitationKind`). `Direction` decides who may accept vs. revoke.
- **Alternatives**: Separate `PartyApplication` and `PartyInvite` tables (rejected — duplicate schema,
  indexes, services, and DTOs for one concept). Reusing `PartyAdminInvitation` (rejected — that is a
  token-link co-admin flow with TTL/expiry; market requests are in-app, non-token, non-expiring, and
  target non-team-members).

## 4. Request lifecycle & the "≤1 active per pair" rule

- **Decision**: Terminal states (`Accepted`/`Declined`/`Revoked`) are retained rows; a **filtered
  unique index** on `(PartyId, UserId) WHERE Status = Pending` enforces at most one **active** request
  per pair (race-safe backstop behind a service pre-check), mirroring the 016 targeted-invite index.
  A declined/revoked pair may be superseded by a **fresh** `Pending` row while the user stays eligible.
  Who may act: the **recipient** accepts/declines (party admin for an application; the target user for
  an invite); the **initiator** revokes (the user for their application; a party admin for an invite).
- **Rationale**: Matches the wireframe (a mercenary can re-appear after a decline) and avoids a
  destructive delete-and-recreate. Terminal rows also feed the "declined" state shown in 7e.
- **Alternatives**: Hard-delete on resolve (rejected — loses the declined/awaiting status the inbox
  shows); a single mutable status without the filtered index (rejected — a concurrent double-apply
  could create two pending rows).

## 5. Eligibility — "one event, one crew"

- **Decision**: A shared `MarketEligibility` helper answers **"is user *In* a party for this event?"**
  = `PartyMembers` has a row with `Status = In` on any `Party` whose `EventId = eventId`. A user is
  eligible to post a listing, apply, or be invited iff that returns false. Enforced server-side on
  every post/apply/invite/accept path.
- **Rationale**: This is the locked clarification (option "only if already In a party"). It is one
  indexed existence query and is the exact invariant the accept path must also re-check atomically.
- **Alternatives**: Team-based eligibility (rejected in clarification); scanning listings/requests for
  eligibility (rejected — membership is the true "crew seat", not a pending request).

## 6. Accept atomicity & join side-effects

- **Decision**: Accept runs inside a transaction with the existing **`PartyCapacity.LockPartyRowAsync`**
  + **`InCountAsync`** (which already counts *all* `In` rows, guests included), then, if a spot is
  open and the user is still eligible: insert the guest `PartyMember` (`In`, `ViaMarket = true`), set
  the request `Accepted`, **`ExecuteUpdateAsync`** all the joiner's other `Pending` requests for this
  event to `Revoked`, and **delete** their `MercenaryListing` for this event. All in one commit.
- **Rationale**: Reuses the proven 016 pessimistic-lock join so the cap can never be exceeded under
  concurrent accepts; the cleanup is the locked "auto-cancel + take down" clarification. `ViaMarket`
  makes the guest count toward the cap for every subsequent reader.
- **Alternatives**: Optimistic concurrency/retry (rejected — 016 already standardizes on the row
  lock); leaving other requests pending (rejected in clarification).

## 7. Guest reconciliation in the 016 read paths

- **Decision**: In `PartyService.ProjectAsync` (party detail/manage) and `PartyRosterService`
  `ListGroupAsync`/`LoadMineAsync`, change the In/Declined membership predicate from
  `Team.Memberships.Any(tm => tm.UserId == m.UserId)` to **`(m.ViaMarket || Team.Memberships.Any(...))`**
  so guests are counted in `InCount` and listed in the **In** group with a guest flag. The
  `NoResponse` group (derived from team memberships) is unchanged — guests never appear there. Add a
  `ViaMarket` flag to `PartyMemberDto` so the client can render the "guest · via market" tag.
- **Rationale**: Cap accounting already includes guests (`PartyCapacity`); this aligns the *display*
  counts and roster with the cap. It is a minimal, surgical change to two files.
- **Alternatives**: A separate "guests" roster group/tab (rejected — the wireframe 7f shows guests
  inline in the In list with a tag, not a separate group).

## 8. Direct invite — all-user search

- **Decision**: The direct-invite search reuses the **feature-006 `EventInvitationService.SearchUsersAsync`
  shape** (ILike over `PlayerProfile.DisplayName`/`Handle`, paginated, **all users**, not team-scoped),
  annotating each candidate with their relation (invitable / already-invited / ineligible-in-a-party).
  A direct invite is a normal `MarketRequest` (`Invite`) with no listing prerequisite.
- **Rationale**: The wireframe (7g) says "search by name / @handle, exactly like inviting event
  co-admins". Party co-admin search is team-scoped and therefore wrong here; the *event* co-admin
  search is the correct all-user precedent.
- **Alternatives**: Restrict to opted-in searchable profiles (rejected — targeted invite is a
  first-party action like event co-admin invites, which search all users by handle).

## 9. Notifications & email

- **Decision**: Add **`NotificationType.MarketInvite`** mapped to the **`InvitesAndRoster`** category.
  Creating an **invite** (board or direct) sends the target an in-app notification (inline
  Accept/Decline, like `TeamInvite`) **and** a transactional email via a new **`MarketEmailService`**
  (one `market-invite.html` template extending the base). **Applications** surface in the party's
  recruiting inbox without a notification this iteration (admins pull; the spec's reach requirement,
  SC-007, is invite-only). Delivery respects feature-011 preferences and never throws into the
  producer's action (011 contract).
- **Rationale**: Matches FR-019/SC-007 (invited players reachable via notification + email + inbox)
  and reuses the exact 010/011 machinery. Keeping applications notification-free avoids enum sprawl and
  admin-spam while the recruiting inbox already surfaces them.
- **Alternatives**: A second `MarketApplication` type notifying admins (deferred — not required by the
  spec; easy to add later); email digests for admins (explicitly out of scope).

## 10. Board reads — public vs. authenticated

- **Decision**: The board **GET** (free-agents list + recruiting-parties list for an event) is
  **`AllowAnonymous`**, like the public event detail; it returns listing/party cards with no
  per-viewer affordances. The mercenary **inbox**, **dashboard summary**, **eligibility/my-listing**,
  and every **write** require authentication and are scoped to the caller's user id.
- **Rationale**: "The board lives on the event page" (a public page). Affordances (Apply/Invite) are
  resolved client-side from the authenticated caller's context and re-checked server-side on write.
- **Alternatives**: Whole controller authenticated (rejected — would blank the board for anonymous
  visitors who can already read the rest of the event page).

## 11. Positions storage & filtering

- **Decision**: Positions on listings, requests, and `Party.PositionsNeeded` are a **Postgres `int[]`**
  of `Pompfe` values (Npgsql maps `int[]`/`List<int>` natively). The board position filter uses the
  array-**overlap** operator (`&&` via `EF.Functions.ArrayOverlap`/`.Any(...)`) so "filter = Schild"
  returns listings that *play* Schild and recruiting parties that *need* Schild.
- **Rationale**: Positions are a small unordered set read together; an array column avoids extra join
  tables and supports server-side filtering. Consistent with treating pompfen as a set.
- **Alternatives**: Join tables per surface (rejected — three extra tables for ephemeral sets);
  comma-joined string (rejected — not queryable, not typed).

## 12. Availability display — `SpotsAdvertised` vs. real open spots

- **Decision**: Store the admin's **`SpotsAdvertised`** (their stated "looking for N", shown on the
  card) but gate apply/accept on the **real** availability `OpenSpots = RosterCap − InCount`; the card
  shows real open spots and auto-closes (no Apply) at `OpenSpots = 0`, reopening when it rises.
- **Rationale**: The wireframe shows both the admin's setter ("− 2 +") and the true fill ("of 8 · 6
  in"); the cap is the authority for whether a seat exists, mirroring the 016 auto-close/reopen.
- **Alternatives**: Enforce on `SpotsAdvertised` (rejected — could exceed the real cap or block a real
  open seat); drop `SpotsAdvertised` entirely (rejected — the admin explicitly sets it in 7c).

## 13. Cleanup on disband / withdraw / event close

- **Decision**: Extend `PartyService.DisbandAsync` to also cascade the party's `MarketRequest`s and
  clear recruiting (the FK cascade from `Party` removes requests; guests are `PartyMember`s already
  cascaded). Listings are per-user/event and are cleared on join; on event cancel/end the board turns
  read-only (writes refused via the event-open check) and stale listings/requests simply become inert.
- **Rationale**: Reuses the 016 hard-delete-in-transaction disband; a `MarketRequest.PartyId` FK with
  `OnDelete(Cascade)` means no orphaned requests. Keeping listings inert (not mass-deleted) on event
  end avoids a background job and matches "no auto-cleanup" precedent.
- **Alternatives**: A scheduled sweeper for ended events (rejected — no scheduler in the stack;
  inert-on-read is sufficient and matches events being terminal/irreversible).

## Reused building blocks (no new abstractions)

| Concern | Reused from |
|---|---|
| Access resolution for a party | `PartyGuard.ResolveAsync` / `PartyAccess` (016) |
| Uniform result → HTTP mapping | `PartyResult`/`PartyResult<T>` + `PartyOutcome` (016) |
| Atomic cap enforcement | `PartyCapacity.LockPartyRowAsync` + `InCountAsync` (016) |
| All-user handle/name search | `EventInvitationService.SearchUsersAsync` shape (006) |
| In-app notification fan-out | `INotificationService.CreateAsync`/`CreateManyAsync` (010) |
| Preference-aware delivery | feature-011 categories (`InvitesAndRoster`) |
| Transactional email base template | `EmailTemplateService` + base header/footer (constitution) |
| Pagination envelope | `PaginationRequest` / `PagedResult<T>` (constitution) |
| Positions vocabulary | `Pompfe` enum (003) |
