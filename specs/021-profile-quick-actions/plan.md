# Implementation Plan: Profile Quick-Actions (Message & Invite to a Team)

**Branch**: `021-profile-quick-actions` | **Date**: 2026-07-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/021-profile-quick-actions/spec.md`

## Summary

Add two shorthand actions to a player's public profile page (`/u/:handle`): a
**Message** action that opens or starts a direct message with that player, and an
**Invite to a team** action (admin-only) that sends a targeted team invitation. Both
are visible only to a signed-in viewer and never on the viewer's own profile.

This is **frontend-only**. It reuses existing endpoints and adds no API, schema, or
permission change. The target player's identity (a `userId`) is resolved on demand
from the public **handle** via existing authenticated search endpoints, so the
public profile response keeps its privacy guarantee (no account id). Feature 020
removed the player-search opt-out, so messaging reach is unconditionally universal
and the earlier "does search exclude opted-out players?" risk is void.

## Technical Context

**Language/Version**: TypeScript / Angular (Nx workspace), zoneless change detection

**Primary Dependencies**: Angular signals + Router; existing `ChatService`,
`TeamService`, `ProfileService`, `AuthService`; Tailwind + DESIGN.md tokens

**Storage**: None (no persistence; reads/writes go through existing endpoints)

**Testing**: Jest (`nx test web`) — component + logic units, mocking services
(zoneless: no `fakeAsync`, per project convention)

**Target Platform**: Web (mobile + desktop; the profile page is responsive)

**Project Type**: Web application (frontend change only)

**Performance Goals**: Message resolves in one action (one search + at most one
start call). Invite eligibility for an admin viewer costs one user-search per
administered team (admins typically administer 1–2 teams).

**Constraints**: No public-DTO change (no account id exposed); server remains the
authorization boundary; no new visual style (DESIGN.md governs).

**Scale/Scope**: One new presentational component + its wiring into the public
profile page; a handful of service calls already in the codebase.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Security-First / Never Trust the Client** — PASS. All authorization stays
  server-side: chat start honors block/rate-limit; `searchUsers`/`createTargetedInvite`
  return 403/404 for non-admins. The profile's show/hide rules are UX only (FR-010).
  Identity is resolved by handle via authenticated endpoints; the anonymous public
  profile response is unchanged and still carries no account id (FR-009, preserves
  the specs/003 privacy invariant).
- **II. Thin Controllers, Service-Centric** — N/A backend (no backend change). On the
  frontend, logic lives in a component + existing services (no new HTTP surface).
- **III. Disciplined Data Access** — N/A (no EF/database work).
- **IV. Auth & Session** — PASS. Reuses `AuthService` state; actions are gated on
  `isAuthenticated()` for UX and enforced server-side.
- **V. Environment Parity** — PASS. Pure frontend, identical across environments.
- **VI. Conventions & Tooling** — PASS. New component keeps separate `.html` / `.css`
  / `.ts`; no scripts added.
- **Quality Gate 7 (UI/Design compliance)** — APPLIES. New UI on the profile page →
  instantiate `specs/021-profile-quick-actions/checklists/ui-review.md` and verify
  against DESIGN.md before done. The standing app-wide primary-button contrast
  conflict is the owner's decision, not resolved here.

**Result**: No violations. Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/021-profile-quick-actions/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (client view-models only)
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (existing endpoints consumed — no new API)
├── checklists/
│   ├── requirements.md  # spec quality (done)
│   └── ui-review.md     # created during implementation (Gate 7)
└── tasks.md             # /speckit-tasks output (NOT created here)
```

### Source Code (repository root)

```text
frontend/apps/web/src/app/
├── features/profile/
│   ├── components/quick-actions/
│   │   ├── profile-quick-actions.component.ts      # NEW — the two actions + logic
│   │   ├── profile-quick-actions.component.html    # NEW
│   │   ├── profile-quick-actions.component.css     # NEW
│   │   └── profile-quick-actions.component.spec.ts # NEW — unit tests
│   └── profile-public/
│       ├── profile-public.component.html           # EDIT — mount the actions in the header
│       └── profile-public.component.ts             # EDIT if passing inputs requires it
└── core/services/
    └── profile.service.ts                          # OPTIONAL — cache getMine() (shareReplay) for viewer context
```

Consumed (unchanged) services/endpoints:
- `ChatService.search(term)` → resolve handle→`PersonHit{userId, existingConversationId}`; `ChatService.start([userId], null)` → conversation; navigate `/chat/:id`.
- `TeamService.searchUsers(slug, handle)` → `InvitableUser{userId, relation}`; `TeamService.createTargetedInvite(slug, userId)`.
- `ProfileService.getMine()` → viewer handle (self-detection) + `teams` with `role` (admin teams).
- `AuthService.isAuthenticated()` → gate visibility.

**Structure Decision**: A single new standalone presentational component under the
existing profile feature, mounted on the public profile page. No new routes,
services, or backend. Existing Angular + services layout is reused.

## Complexity Tracking

No constitution violations; section intentionally empty.
