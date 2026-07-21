# Phase 0 Research: Profile Quick-Actions

All questions were resolvable from the codebase; no external research and no open
NEEDS CLARIFICATION remain. Feature 020 (merged) removed the player-search opt-out,
so the messaging-reach risk from earlier drafts is void.

## R1. Where the actions are surfaced

**Decision**: A new standalone presentational component
`ProfileQuickActionsComponent` mounted in the header of the **public profile page**
(`ProfilePublicComponent`, `/u/:handle`).

**Rationale**: `/u/:handle` is the only "other person" profile surface. The owner
page (`/profile`) is always self, so it never shows the actions. Keeping the logic in
one component (not inline in the page) makes it testable in isolation and keeps the
page template lean.

**Alternatives considered**: Inline in `profile-public.component.html` — rejected;
harder to unit-test and clutters the page. A shared "user action bar" reused on team
rosters/chat — deferred; out of scope (spec bounds this to the profile page).

## R2. Resolving the target identity from a handle (no public-DTO change)

**Decision**: Resolve `handle → userId` on demand via existing authenticated search:
- **Message**: `ChatService.search(handle)` → find the `people` item whose `handle`
  equals the target (case-insensitive) → use its `userId` / `existingConversationId`.
- **Invite**: `TeamService.searchUsers(slug, handle)` → find the `InvitableUser`
  whose `handle` equals the target → use its `userId` and `relation`.

**Rationale**: The public `PublicProfileDto` deliberately carries no account id
(specs/003 privacy invariant), and `AuthUser` has no handle. Both search endpoints
are authenticated and return `userId` keyed on a name/handle match, so identity is
resolved without ever exposing an id on the anonymous profile response (FR-009).
No backend change is needed.

**Alternatives considered**: Add `userId` to the public profile DTO — rejected
(breaks the privacy invariant). A new "resolve handle" endpoint — unnecessary; the
existing searches already return what we need.

## R3. Viewer context (self-detection + administered teams)

**Decision**: When the viewer is authenticated, load `ProfileService.getMine()` once
to obtain the viewer's own **handle** (for self-detection — hide actions when it
equals the target handle) and **teams** (filter `role === 'Admin'` for administered
teams). Cache the result (e.g. `shareReplay(1)` in `ProfileService`) so repeated
profile views don't refetch.

**Rationale**: `AuthService.currentUser` exposes only `{id, email, onboardingCompleted}`
— no handle or teams. `getMine()` (`OwnerProfileDto`) provides both, and 020 left its
`teams`/`handle` fields intact (only `appearInSearch` was removed). Anonymous viewers
never call it (gated by `isAuthenticated()`).

**Alternatives considered**: Compare account ids — not possible (public DTO has no id).
Add handle/teams to `AuthUser` — unnecessary scope creep.

## R4. Invite eligibility across the viewer's admin teams

**Decision**: For an admin viewer, resolve eligibility eagerly as part of loading
viewer context: for each administered team, call `TeamService.searchUsers(slug,
handle)` (in parallel) and read the target's `relation`. Eligible teams =
`relation === 'Invitable'`; `Member`/`Invited` teams are excluded. Then:
- 0 admin teams → **hide** the Invite action (FR-005).
- ≥1 admin team, 0 eligible → **disabled** with a brief reason (FR-005).
- exactly 1 eligible → invite that team directly on click (FR-006).
- >1 eligible → open a small **picker** of eligible teams (FR-006).

**Rationale**: FR-005 requires the button to render disabled-with-reason when no
administered team is eligible, which needs eligibility known at render time. The cost
is one `searchUsers` call per administered team; admins typically administer 1–2
teams, so this is negligible. `searchUsers`/`createTargetedInvite` are admin-scoped
server-side (403/404 otherwise), and we only call them for the viewer's own admin
teams, so authorization always holds.

**Alternatives considered**: Lazy resolution on click — simpler but can't render the
disabled-with-reason state up front (FR-005). A dedicated "invitable teams for a
handle" endpoint that returns eligibility in one call — a clean future optimization,
but out of scope for this frontend-only v1 (noted as a follow-up).

## R5. Message refusal paths (block / rate-limit / unresolvable)

**Decision**: Handle all three as a single friendly failure with no conversation
opened (FR-004):
- **Blocked (either direction)**: `ChatService.search` (via `SearchPeopleAsync`)
  already excludes users blocked in either direction, so a blocked target simply
  isn't found → treated as "can't message right now." No separate block check needed.
- **Rate-limited / other errors**: `ChatService.start` (or search) may return an
  error (e.g. 429); surface a "try again shortly" style message.
- **Not resolvable** (no exact-handle match, e.g. account gone): friendly "couldn't
  start that message."

**Rationale**: The server is the boundary; the client presents outcomes. Reusing the
block-aware search means the block case needs no extra plumbing.

## R6. Button styling & placement (DESIGN.md)

**Decision**: Render the actions as a compact action row in the profile header near
the existing "Copy link" control. **Message** is the primary action (coral
`brand-primary`); **Invite to a team** is secondary (sage `brand-secondary`); "Copy
link" stays neutral — keeping "exactly one coral CTA per view." Disabled, loading,
and success states use the standard token set. The team picker is a small popover/menu
consistent with existing pickers (e.g. the shared assign/invite pickers).

**Rationale**: DESIGN.md governs; this feature introduces no new visual style. The
standing app-wide primary-button contrast conflict is the owner's call and is not
resolved here (flagged for the UI review).

## R7. Testing approach

**Decision**: Jest unit tests for `ProfileQuickActionsComponent` with mocked
`ChatService`/`TeamService`/`ProfileService`/`AuthService`/`Router`. Cover: hidden
for anon and self; message opens existing vs starts new; message failure path; invite
hidden with no admin teams, disabled with no eligible team, direct for one eligible,
picker for many, and duplicate-prevention (Member/Invited excluded). Follow the
project's **zoneless** convention — no `fakeAsync`; drive async with observable mocks
and `await`/`whenStable` patterns used elsewhere.

**Rationale**: Matches existing component test patterns in the repo (Angular 21
zoneless). E2E is optional and not required to ship this unit-tested UI.
