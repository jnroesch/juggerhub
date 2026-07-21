# Phase 0 Research: "My team" home for teamless players

No open `NEEDS CLARIFICATION` items remained after `/speckit-clarify`. This document records the design decisions that shape the plan, each grounded in the existing codebase.

## Decision 1 — How to accept/decline from the list

**Decision**: Reuse the existing token-based invitee endpoints. The new "list my invites" read returns each invite's `Token`, and the frontend calls the existing `POST /api/v1/invitations/{token}/accept` and `POST /api/v1/invitations/{token}/decline`.

**Rationale**:
- `TeamInvitationService.AcceptAsync`/`DeclineAsync` already encode every outcome the spec needs: `Joined`, `AlreadyMember` (idempotent consume of a targeted invite), `NotUsable` (expired/revoked/consumed → FR-015), `NotFound`, plus unique-violation race handling.
- Returning the caller's **own** token is not new exposure: the list is scoped server-side to `TargetUserId == self`, and that user already received the same token by email. High-entropy token + 7-day expiry + revoke bound the risk exactly as today.
- Avoids a parallel accept-by-id mutation surface and keeps a single source of truth for accept/decline semantics.

**Alternatives considered**:
- *New `POST /me/invitations/{id}/accept|decline` endpoints scoped by user id.* Rejected: duplicates existing, well-tested logic; more surface to secure and test for no behavioral gain.
- *Omit the token, return only an id, and add id-based mutations.* Rejected: same duplication cost as above.

## Decision 2 — What the list returns

**Decision**: A new `MyInvitationDto` carrying `token`, `teamName`, `teamSlug`, `teamType`, `city`, `memberCount`, `inviterDisplayName`, `createdDate`, `expiresDate`.

**Rationale**: Mirrors `InvitePreviewDto` (the invitee already sees this shape on the token landing page) plus the `token` (to act) and dates (freshness). Gives the UI enough to render a decision-ready card (FR-010) without a second round-trip.

**Filter**: `Kind == Targeted && Status == Pending && ExpiresDate > now && TargetUserId == callerId`. Excludes link invites and any non-usable invite (FR-008, FR-009, edge cases).

**Ordering / bounding**: newest-first (`OrderByDescending(CreatedDate)`), paginated via the shared `PaginationRequest`/`PagedResult<T>` (constitution III; FR-016).

## Decision 3 — Endpoint placement

**Decision**: `GET /api/v1/profiles/me/invitations` on `ProfilesController`, backed by a new `ITeamInvitationService.ListMineAsync(userId, pagination)`.

**Rationale**: The parallel "my resources" read `GET /api/v1/profiles/me/teams` already lives on `ProfilesController` (backed by `IHomeService`). Placing `me/invitations` beside it keeps the frontend's `/profiles/me/*` surface consistent (the same place `MembershipService` already calls). The invitation query logic stays in `TeamInvitationService` (thin controller, constitution II).

**Alternatives considered**:
- *`GET /api/v1/invitations/mine` on `InvitationsController`.* Reasonable (that controller holds the invitee token flow), but it splits "my resources" reads across two controllers. Rejected for consistency with `me/teams`.

## Decision 4 — The navigation fix

**Decision**: Change `myTeamTarget(teams)` in `frontend/apps/web/src/app/layout/nav-model.ts` so `teams.length === 0` returns `/my-team` instead of `/browse/teams`. One- and many-team branches are unchanged.

**Rationale**: Both the desktop top bar and mobile bottom bar bind their "My team" link to `MembershipService.myTeamTarget` (which delegates to `myTeamTarget`). `isActiveDestination('my-team', url)` already matches `/my-team`, so lighting up the destination (FR-002) requires no further change. This is the minimal, provably-shared fix.

**Follow-through**: `nav-model.spec.ts` asserts the 0-team target is `/browse/teams` today — update that expectation. Grep confirms `myTeamTarget` is consumed only through `MembershipService`/the two nav components, so no other caller relies on the old value.

## Decision 5 — Post-accept state transition

**Decision**: On a successful accept (`Joined` or `AlreadyMember`), the `my-team` component calls `MembershipService.load()` to refresh cached memberships, then navigates to `/t/{slug}` (the returned team slug). On `NotUsable`/`NotFound`, show a friendly message and remove the stale invite from the list (FR-015). Decline removes the invite from the list without navigating.

**Rationale**: `AcceptAsync` returns the `teamSlug`; refreshing membership makes `myTeamTarget` resolve to a team on the next nav (FR-017), and navigating fulfils the clarified auto-navigate decision (FR-018). `MembershipService` already exposes `load()` and is `providedIn: 'root'`.

## Decision 6 — No database migration

**Decision**: No EF migration. This feature only reads existing columns.

**Rationale**: `TeamInvitation.TargetUserId`, `Kind`, `Status`, `ExpiresDate`, `Token` all exist; `TeamMembership` is untouched by the read. Accept/decline already mutate via existing endpoints. Confirmed against `backend/Entities/TeamInvitation.cs`.

## Testing approach

- **Backend (xUnit integration)**: `me/invitations` returns only the caller's targeted, usable invites; excludes other users' invites, link invites, expired/revoked/accepted invites; requires authentication (401 anonymous); respects pagination. Accept/decline paths are already covered by existing invitation tests; add one asserting an invite listed by `me/invitations` can be accepted via the token endpoint and then no longer appears.
- **Frontend**: update `nav-model.spec.ts` (0-team → `/my-team`); add `my-team.component.spec.ts` coverage for the three states (no teams+no invites, no teams+invites, ≥1 team unchanged) and the accept (refresh+navigate) / decline (remove) flows. Zoneless — no `fakeAsync`.
