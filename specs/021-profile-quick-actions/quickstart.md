# Quickstart / Validation: Profile Quick-Actions

Validates the two profile actions and their visibility rules. Frontend-only; no
migration or backend change.

## Prerequisites

- Local stack up (frontend + backend + Postgres) per the repo README.
- At least: two ordinary players (A, B), one player who administers a team (Admin),
  and a team with a known roster. Optionally a block between two players.

## Automated checks

- **Frontend unit tests**: `npx nx test web` — the new
  `profile-quick-actions.component.spec.ts` covers visibility, message open/start,
  message failure, and invite eligibility branches (hidden / disabled / direct /
  picker).
- **Lint/typecheck**: `npx nx lint web` passes.

## Manual end-to-end scenarios

### Message (US1)

1. **Start a new DM** (FR-003): as A, open B's profile (`/u/{B}`), click **Message** →
   land in a DM with B. Send a message to confirm.
2. **Open existing DM** (FR-003, SC-002): as A (already messaging B), open B's profile,
   click **Message** → land in the *existing* thread, not a duplicate.
3. **Blocked** (FR-004): with A and B blocked, as A click **Message** on B's profile →
   friendly "can't message" message; no conversation opened.
4. **Self** (US1 AS-4): open your **own** public profile (`/u/{me}`) → no Message action.
5. **Anonymous** (US1 AS-5): signed out, open any `/u/{handle}` → no Message action.

### Invite to a team (US2)

6. **Direct invite** (FR-006/FR-007): as Admin (one eligible team), open a non-member's
   profile → **Invite to a team** enabled; click → targeted invite created, action shows
   "invited/sent"; the player receives the invite email and it appears in the team's
   pending invites.
7. **Picker** (FR-006): as Admin of ≥2 eligible teams, click **Invite to a team** →
   pick a team from the list (only eligible teams listed) → invite sent.
8. **No eligible team** (FR-005 / SC-003): as Admin whose team the player already belongs
   to (or already has a pending invite), open their profile → **Invite** shown **disabled**
   with a brief reason; no invite can be sent; no duplicate created.
9. **Not an admin** (US2 AS-4): as an ordinary member, open a profile → no Invite action.
10. **Self / anonymous** (US2 AS-5): own profile or signed out → no Invite action.

### Privacy & authorization (FR-009 / FR-010)

11. Inspect `GET /api/v1/profiles/{handle}` (public) → response contains **no** account
    id, email, or account status (unchanged by this feature).
12. Attempt an invite as a non-admin via the API directly → server returns 403/404
    regardless of what any UI showed (server is the boundary).

## Responsiveness (FR-012)

13. Verify both actions are reachable and operable on a narrow (mobile) and wide
    (desktop) layout of the profile header.

## UI review (Quality Gate 7)

- Instantiate `specs/021-profile-quick-actions/checklists/ui-review.md` from the
  template and verify the action row, disabled/loading/success states, and the team
  picker against DESIGN.md (one coral CTA per view; sage secondary; token spacing/radii;
  visible focus; ≥44px targets).
