---
description: "Task list for feature: Profile Quick-Actions (Message & Invite to a Team)"
---

# Tasks: Profile Quick-Actions (Message & Invite to a Team)

**Input**: Design documents from `specs/021-profile-quick-actions/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/)

**Tests**: Unit tests ARE included — the spec/plan call for `nx test web` coverage of
the component's visibility and action branches (research R7). They are written per
story (not strict TDD-first).

**Organization**: By user story. Note: both actions live in **one new component**
(`ProfileQuickActionsComponent`), so US1 and US2 edit the same files — they are
sequential by file, not parallel. US1 (Message) is a complete, shippable increment on
its own; US2 (Invite) is added to the same component afterward.

**Path base**: `frontend/apps/web/src/app` (abbreviated `…/app` below).

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: different files, no dependency — safe to parallelize
- **[Story]**: US1 (Message) / US2 (Invite). Setup/Foundational/Polish: no label.

---

## Phase 1: Setup

**Purpose**: Scaffold the component and mount it (renders nothing yet).

- [X] T001 Create the standalone component scaffold `ProfileQuickActionsComponent` with a required `handle` input (and optional `displayName`) — files `…/app/features/profile/components/quick-actions/profile-quick-actions.component.ts`, `.html`, `.css`. Empty template for now; selector `jh-profile-quick-actions`.
- [X] T002 Mount `<jh-profile-quick-actions [handle]="p.handle" />` in the profile header of `…/app/features/profile/profile-public/profile-public.component.html` (near the "Copy link" control) and add the component to `ProfilePublicComponent` imports in `…/app/features/profile/profile-public/profile-public.component.ts`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared viewer-context + visibility gate both actions depend on.

**⚠️ CRITICAL**: Neither user story can render until this is in place.

- [X] T003 Add a cached viewer read to `…/app/core/services/profile.service.ts` (e.g. `getMineCached()` returning `getMine()` via `shareReplay(1)`), so the component reads the viewer's handle + teams once across profile views.
- [X] T004 In `profile-quick-actions.component.ts`, inject `AuthService` + `ProfileService`; expose `isAuthenticated`; when authenticated, load viewer context → `viewerHandle` and `adminTeams` (`teams` where `role === 'Admin'`); compute `isSelf = viewerHandle === handle`. In `.html`, render the action-row container ONLY when `isAuthenticated() && !isSelf()` (nothing for anonymous or self).
- [X] T005 [P] Foundational unit tests in `profile-quick-actions.component.spec.ts`: renders nothing for an anonymous viewer; renders nothing on the viewer's own profile (self); renders the container for a signed-in non-self viewer. Mock `AuthService`/`ProfileService` (zoneless — no `fakeAsync`).

**Checkpoint**: Visibility rules hold; both actions can now be added to the container.

---

## Phase 3: User Story 1 - Message a player from their profile (Priority: P1) 🎯 MVP

**Goal**: A signed-in viewer opens or starts a DM with the profile's player in one action.

**Independent Test**: Sign in, open another player's profile, click **Message** → land
in the existing DM if one exists, otherwise a newly started one; blocked/unresolvable
shows a friendly failure and opens nothing.

- [X] T006 [US1] In `profile-quick-actions.component.ts`, inject `ChatService` + `Router`; implement `message()`: `chat.search(handle)` → find the `people` item whose `handle` case-insensitively equals the target → if `existingConversationId`, `router.navigate(['/chat', id])`; else `chat.start([userId], null)` then navigate to the returned conversation id. Guard against double-submit with an in-flight signal.
- [X] T007 [US1] Add message failure handling in the component: no exact-handle match (includes blocked users, which chat search excludes) → friendly "couldn't start that message"; request error (e.g. rate-limited) → friendly "try again shortly". Set an error signal; open no conversation.
- [X] T008 [US1] Render the **Message** button (primary/coral per DESIGN.md) in the action row in `profile-quick-actions.component.html` with in-flight (disabled + pending label) and error-message states; style in `.css`.
- [X] T009 [P] [US1] Unit tests in `profile-quick-actions.component.spec.ts`: opens the existing conversation when `existingConversationId` is set; starts a new DM and navigates when none; friendly failure when the target is unresolved/blocked; friendly failure on a start error. Mock `ChatService`/`Router`.

**Checkpoint**: Message works end-to-end and is independently shippable (MVP).

---

## Phase 4: User Story 2 - Invite a player to a team I administer (Priority: P2)

**Goal**: An admin viewer invites the player to an eligible team they administer.

**Independent Test**: As an admin of ≥1 team, open a non-member's profile → **Invite to
a team** enabled; one eligible team invites directly, several show a picker; a player
already on/invited to all your teams shows the disabled-with-reason state; non-admins
see no Invite action.

- [X] T010 [US2] In `profile-quick-actions.component.ts`, inject `TeamService`; implement eligibility resolution — for each `adminTeams` slug, `forkJoin` `teams.searchUsers(slug, handle)` → the target's `relation`; derive `eligibleTeams = relation === 'Invitable'`. Compute invite state: hidden (0 admin teams) / disabled+reason (≥1 admin team, 0 eligible) / direct (1 eligible) / picker (>1 eligible).
- [X] T011 [US2] Implement `invite(team)` in the component: `teams.createTargetedInvite(slug, userId)` (userId from the resolved `InvitableUser`); one eligible team invites on the button click, multiple via picker selection; on success set a per-target "invited/sent" state; on server refusal (403/404/error) show a friendly message. No duplicate for Member/Invited (already excluded from `eligibleTeams`).
- [X] T012 [US2] Render the **Invite to a team** control (secondary/sage per DESIGN.md) in `profile-quick-actions.component.html`: hidden / disabled-with-brief-reason / enabled; a small team **picker** popover listing only eligible teams; and the post-send "Invited" confirmation. Style states in `.css`.
- [X] T013 [P] [US2] Unit tests in `profile-quick-actions.component.spec.ts`: hidden with no admin teams; disabled+reason when every admin team is Member/Invited; direct invite for exactly one eligible; picker for multiple eligible; Member/Invited teams excluded from the picker; success sets sent state; server refusal handled. Mock `TeamService`.

**Checkpoint**: Both actions work independently and together.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Design compliance and whole-feature verification.

- [X] T014 [P] DESIGN.md styling pass on `profile-quick-actions.component.html`/`.css`: exactly one coral `brand-primary` CTA (Message) with sage `brand-secondary` (Invite); token spacing/radii; visible focus rings; ≥44px touch targets; responsive header layout on mobile + desktop (FR-011/FR-012).
- [X] T015 Instantiate `specs/021-profile-quick-actions/checklists/ui-review.md` from `.specify/templates/ui-review-checklist-template.md` and verify the action row, states, and picker against DESIGN.md (Quality Gate 7); note (do not resolve) the standing primary-button contrast conflict.
- [X] T016 Run the [quickstart.md](quickstart.md) manual scenarios, then `npx nx lint web` and `npx nx test web`; confirm all green with the new component and specs.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none — start immediately.
- **Foundational (Phase 2)**: after Setup. **Blocks both stories** (shared visibility gate + viewer context).
- **US1 (Phase 3)**: after Phase 2.
- **US2 (Phase 4)**: after Phase 2. Independent of US1 in behavior, but **edits the same component files**, so schedule it after US1 (or coordinate edits) rather than truly in parallel.
- **Polish (Phase 5)**: after US1 and US2.

### Within a story

- US1: T006 (resolve/navigate) → T007 (failure) → T008 (render) → T009 (tests).
- US2: T010 (eligibility) → T011 (send) → T012 (render) → T013 (tests).

### Parallel Opportunities

- Limited by design: the two actions share one component (`.ts`/`.html`/`.css`/`.spec.ts`).
- Genuinely parallel: T005 / T009 / T013 are the spec file but for disjoint concerns — only parallel if worked as separate spec sections without merge conflict; otherwise sequential. T014 (`.css`) can run alongside `.ts` work.
- The safest parallel split is **person-level across phases**, not within these shared files.

---

## Parallel Example

```bash
# Foundational tests and the CSS polish are the main independent slices:
Task: "T005 Foundational visibility tests in profile-quick-actions.component.spec.ts"
Task: "T014 DESIGN.md styling pass in profile-quick-actions.component.css"
# Most other tasks touch the shared component .ts/.html and run sequentially.
```

---

## Implementation Strategy

### MVP scope — US1 (Message) only

1. Phase 1 Setup → Phase 2 Foundational → Phase 3 US1.
2. **STOP & validate**: Message opens/starts a DM; hidden for anon/self; friendly
   failures. This is a complete, shippable increment.

### Incremental delivery

1. Setup + Foundational → visibility gate ready.
2. US1 (Message) → test → ship (MVP).
3. US2 (Invite) → test → ship.
4. Polish (design pass, UI review, quickstart, lint/test).

---

## Notes

- Frontend-only: no backend, API, schema, or migration. Server remains the authz
  boundary (FR-010); the public profile DTO is untouched and exposes no account id (FR-009).
- Identity is resolved by **handle** via existing `ChatService.search` /
  `TeamService.searchUsers` — no new endpoint.
- Zoneless testing convention: no `fakeAsync`; drive async via observable mocks.
- Feature 020 removed the search opt-out, so messaging reach is universal — no
  opt-out special-casing anywhere.
