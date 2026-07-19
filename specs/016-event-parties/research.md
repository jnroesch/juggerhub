# Phase 0 Research — Event Parties

All items below were resolvable from the existing codebase (features 005/006/010/011) and the
locked clarifications; no open `NEEDS CLARIFICATION` remain.

## R1. Party lifecycle & status model

**Decision**: `Party` carries `PartyStatus { Open, Applied }`. **Disband is a hard delete** of the
`Party` row; `PartyMember`, `PartyNewsPost`, and `PartyAdminInvitation` cascade-delete with it.
`Applied` is set when the party is entered on the event and reverts to `Open` on withdraw.

**Rationale**: The spec says disband "removes the party and its news"; a hard delete is the literal
model and needs no tombstone/query-filter. Only two live states are meaningful (being filled vs.
listed on the event); "disbanded" is simply "no row".

**Alternatives considered**: A soft-delete `Disbanded` status with a global query filter (as feature
013 uses for banned accounts) — rejected as unnecessary machinery: nothing needs to read a disbanded
party, and hard cascade delete matches the wireframe's "gone" semantics.

## R2. Roster cap: where it lives and how it is enforced

**Decision**: Add nullable `Event.RosterCap` (int?), set only for **teams-only** events at creation
(default 8, minimum 5, no upper cap beyond a sane guard). At party formation the value is **snapshotted**
onto `Party.RosterCap`. "Open for joining" is **derived** — `In-count < Party.RosterCap` — never a
stored flag. Joining takes a pessimistic row lock on the `Parties` row (`SELECT … FOR UPDATE`) then
counts `In` members and inserts, so the cap can never be exceeded under concurrency.

**Rationale**: Mirrors `EventCapacity` exactly (proven pattern, `EventCapacity.LockEventRowAsync`).
Snapshotting the cap satisfies the assumption that later event-cap edits do not retroactively resize
existing parties. A derived open/closed state is what makes auto-close/auto-reopen "happen by itself"
with no admin action and no stored transitions.

**Alternatives considered**: Reading the cap live off the event each time (rejected — a cap edit
would silently resize live parties, breaking the snapshot assumption). A stored `IsFull`/waitlist
(rejected — the spec explicitly has no party-level waitlist).

## R3. PartyMember materialization & the three roster groups

**Decision**: `PartyMember` rows exist **only for `In` and `Declined`** members. The **"no response"**
group is **derived** = current team members (`TeamMembership`) with no `PartyMember` row. The creator
is inserted as `In` + `Admin`. Answering: "I'm in" upserts an `In` row (capacity-checked); "can't
make it" upserts a `Declined` row; "leave" and admin "remove" delete the row (back to no-response).
Arrival order for first-come reopen = `PartyMember.CreatedDate`.

**Rationale**: Deriving no-response from live memberships handles team roster drift for free — a
member who joins the team after the party formed automatically appears in no-response and can join;
a member who leaves the team drops out of every group via the join to `TeamMembership`. Avoids a
fan-out of one row per team member at creation.

**Alternatives considered**: Materializing a `NoResponse` row per team member at formation (rejected
— fan-out writes, and stale rows when team membership changes). Storing declines as a flag on a
pre-created row (same drift problem).

## R4. Party admins & the last-admin guard

**Decision**: A party admin is a `PartyMember` with `Role = Admin` (and `Status = In`) — the creator
is the first. Co-admin acceptance elevates a team member's membership to `In + Admin` (joining the
`In` roster if not already, subject to the cap; if the party is full, acceptance is refused with a
clear "party is full" outcome). The party always retains ≥ 1 admin; the last admin cannot leave, be
removed, or step down (atomic guard, mirroring the team/event last-admin guards).

**Rationale**: The wireframe shows admins inside the `In` roster with an `ADMIN` marker (Ada K., Ben
R.), so admin is a role on membership rather than a separate grant table. Reusing the "≥ 1 admin"
invariant keeps parity with `TeamMembership`/`EventAdmin`.

**Alternatives considered**: A separate `PartyAdmin` table like `EventAdmin` (rejected — admins are
part of the crew and the wireframe renders them in the roster, so a role flag is simpler and avoids
join duplication). Admins exempt from the cap (rejected — the wireframe counts them toward 5/8).

## R5. Applying to the event — reuse the events entry, not the team-admin check

**Decision**: `PartyService.ApplyAsync` performs the event entry directly using the shared
`EventCapacity` (lock the **event** row, count occupied, decide `Joined`/`AwaitingApproval`/
`Waitlisted` per the event's free/paid/full rules), inserts an `EventSignup { EventId, TeamId }`, and
links `Party.EventSignupId` + sets `Status = Applied`. Withdraw deletes that signup and reverts to
`Open`; disband (if applied) deletes it too. The existing `EventSignupService.SignupAsync` **teams
branch is removed** — teams-only entry now flows only through a party.

**Rationale**: Authorization for entering the team is now the **party admin** check (a team admin
formed the party; co-admins may be regular team members), which the raw `SignupAsync` team-admin
guard would wrongly block. Reusing `EventCapacity` keeps the single source of truth for event
capacity while relocating the authorization boundary to the party.

**Alternatives considered**: Calling `SignupAsync` with a trusted flag (rejected — leaks a bypass
into the public sign-up path). Keeping the direct team-join alongside parties (rejected — the locked
decision is full replacement).

## R6. Notifications & email

**Decision**: Add `NotificationType.PartyRequest` mapped to the **existing**
`NotificationCategory.InvitesAndRoster` (no new settings-matrix category, so feature-011 UI is
untouched). Forming a party fans out via `INotificationService.CreateManyAsync` to all team members
(minus the creator) with a `dedupeKeyPrefix` of `party-request:{partyId}`; **Nudge** re-sends to the
still-unanswered members using a fresh dedupe prefix (`party-nudge:{partyId}:{round}`) so a nudge is
not swallowed by the original dedupe key. Email reuses the base header/footer templates via a new
`PartyEmailService` (`party-request.html`, `party-coadmin-invite.html`, `party-news.html`) and the
existing `IEmailSender` (Mailpit/Resend).

**Party news notifications** (per the requester): posting party news fans out
`NotificationType.PartyNews` (mapped to the **existing** `NotificationCategory.TeamNews`, so no new
settings-matrix category) plus an email to every **in** crew member except the author — mirroring how
a team news post notifies the team. Recipients are the crew (In members), not decliners or the wider
team, keeping news private to the party. Dedupe prefix `party-news:{postId}`.

**Rationale**: `CreateManyAsync` already honors per-recipient preferences and idempotency. Mapping
`PartyRequest`→`InvitesAndRoster` and `PartyNews`→`TeamNews` reuses existing categories, so no
feature-011 preference rows need migrating and no settings UI changes. The nudge needs a distinct
dedupe key to actually re-alert.

**Alternatives considered**: New dedicated `Parties`/`PartyNews` notification categories (rejected —
forces feature-011 settings UI + docs changes for little benefit; the existing categories are a
natural fit). Feed-only party news with no push (rejected by the requester — the crew should be
alerted like team news).

## R7. Routing, addressing & discovery endpoints

**Decision**:
- Party REST under `api/v{version}/parties` (`PartiesController`) + token flow under
  `api/v{version}/party-invitations` (`PartyInvitationsController`), mirroring events/event-invitations.
- Client route `/(t)/:slug/party/:eventId` resolves to the manage hub (matches the wireframe URL
  `jugger.app/t/rheinfeuer/party/summer-slam`); internally the party is fetched by id after a
  lookup, or the route resolves `(teamSlug, eventId) → party`.
- **Event page discovery**: `GET /events/{id}/party-context` returns, for the signed-in caller on a
  teams-only event, the teams they administer that could form a party and, for each, whether a party
  already exists (+ `partyId`, + caller's membership state). Drives "Enter a party" vs "Manage party".
- **Team space discovery**: `GET /teams/{slug}/party-requests` returns the caller's visible
  (member-gated) active parties for that team — the pinned cards — each with event summary, `In/cap`
  fill, the admin's message, and the caller's own membership state (in/declined/no-response/admin).

**Rationale**: A dedicated `parties` resource keeps controllers thin and mirrors the events slice;
the two discovery endpoints are the minimal read surfaces the two existing pages need to render the
new affordances without over-fetching.

**Alternatives considered**: Nesting parties under `teams/{slug}/parties` for all operations
(rejected — forming starts from the event and co-admin/roster ops read cleaner off a flat party id;
discovery endpoints cover the team- and event-scoped reads).

## R8. Uniqueness & integrity (DB-enforced)

**Decision**: Partial-unique index on `Parties(TeamId, EventId)` guarantees **one party per team per
event** (race-safe, like the team join-request and event-signup partial indexes). Unique
`PartyMembers(PartyId, UserId)`. `PartyAdminInvitation` reuses the event-invitation index shape
(unique token; one active link per party; one pending targeted invite per (party, user)). A unique
index on `Party.EventSignupId` (partial, non-null) keeps the applied↔signup link 1:1.

**Rationale**: Push invariants into the database as the backstop behind service pre-checks —
exactly the pattern already used across signups, invites, and join-requests.

## R9. Testing approach

**Decision**: Backend xUnit integration tests under `tests/.../Parties/` covering: form (team-admin
only, one-per-team-per-event, cancelled/ended refused), request fan-out (notifications/email,
non-members excluded), accept/decline/leave (cap enforcement, reversible decline, atomic last-spot),
auto-close/reopen, manage (nudge/remove authz, remove leaves team intact), apply (free/paid/full
routing, duplicate guard, party-admin authz), withdraw, news (private, admin-only compose), co-admin
(team-scoped, last-admin guard), disband (cascade + event withdrawal). Frontend zoneless component
specs (no `fakeAsync`), and a UI review checklist instantiated from the template before UI
verification. Optional Playwright e2e for the form→request→answer→apply happy path.

**Rationale**: Mirrors the existing events/teams test suites and the constitution's quality gates;
the zoneless/no-`fakeAsync` note is carried from `catalogue-014-decisions`.
