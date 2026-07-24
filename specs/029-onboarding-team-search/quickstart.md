# Quickstart: Onboarding Team Search

**Feature**: `specs/029-onboarding-team-search/` | **Date**: 2026-07-24

How to run and prove this feature. Frontend-only — no migration, no backend rebuild needed beyond a
normal local stack.

## Prerequisites

```powershell
# Full local stack (backend + Postgres + Mailpit + Redis)
docker compose up -d

# Frontend dev server
cd frontend
npx nx serve web
```

Seed data you need: at least three teams, of which **at least one has `beginnersWelcome = true`** and
at least one does **not**. Create them through the UI as any signed-in account (`/teams/new`), then
toggle "Beginners welcome" in team settings. Without a mixed set, the opening-list-vs-search
distinction cannot be observed.

## Reaching the step

Onboarding shows once per account, so use a **fresh registration** each time:

1. Register at `/register` with a new address.
2. Confirm via Mailpit (`http://localhost:8025`).
3. Sign in → the onboarding wizard opens.
4. Get started → display name → city → pompfen → **"Find your team"**.

Re-testing on the same account means clearing its onboarding-completion mark in the database, or
just registering again — registering again is faster.

## Validation scenarios

Each maps to spec requirements; see [spec.md](spec.md) and [contracts/](contracts/consumed-endpoints.md).

### 1. The opening list is real and beginner-friendly (FR-001, FR-002)

The step shows actual teams from the database with initial, name, city, player count, and a
"Beginners" pill. Only beginners-welcome teams appear. Visible copy points at searching by name.

**Fails if**: sample teams appear, the field is disabled, or a "coming soon" note is present.

### 2. Searching covers all teams (FR-003, FR-004)

Type the name of a team that is **not** beginners-welcome. It appears. Watch the network panel: one
request per typing pause, not one per keystroke, and the query request carries `q` with **no**
`beginnersWelcome` parameter.

Clear the field → the beginner-friendly opening list returns.

### 3. Asking to join creates a pending request (FR-011, FR-013)

Pick a team → an "Ask to join *{team}*" action appears. Press it.

- The confirmation says the request is pending and an admin will let you in. It must not say you
  joined.
- The row shows as already asked and cannot be asked again (FR-015).
- Sign in as that team's admin → the join-request queue shows exactly one pending entry.
- Sign back in as the player → the team page shows the "Requested" relation, **not** membership.

### 4. Continue never sends anything (FR-012, FR-018, SC-009)

With the network panel open and a team **selected but not asked**, press Continue.

**Zero requests must leave the browser.** Complete onboarding; the admin queue stays empty.

Repeat for "I'm not on a team yet" and Back.

### 5. A broken search never traps the player (FR-017, US2)

Throttle to offline in devtools, then enter the step (or type a query).

- A failure message appears with a **secondary** "Try again" — different wording and treatment from
  the no-results state (FR-008).
- Continue, "I'm not on a team yet", and Back all still work; onboarding completes and the app opens.

**Fails if**: any of the three is disabled, or the failure reads like an empty state.

### 6. A refused join request is reported and does not block (FR-016)

Sign in as an account that already belongs to a team, reach the step (needs a fresh onboarding
mark), pick that team and ask. The step says you're already on that team — no status code, no stack
trace — and the flow still advances.

### 7. Empty and error are visibly different (FR-006, FR-008)

Search for `zzzznotateam` → "no teams match that", no retry button. Compare against scenario 5's
failure state. They must not look or read alike.

### 8. Loading is a line, never a spinner (FR-007)

Throttle to "Slow 3G" and enter the step: one muted text line. Leave it throttled past two seconds
and the same line switches to patient copy with no layout shift.

### 9. Nothing else about onboarding changed (FR-021, SC-005)

Walk the whole flow skipping the team step. The saved profile and the `PUT /api/v1/profiles/me`
payload are identical to before this feature. The Done screen says nothing about teams (FR-014).

## Automated verification

```powershell
cd frontend
npx nx test web --testPathPattern=onboarding   # component specs for this feature
npx nx lint web
npx nx build web
```

The backend is untouched; its suites need no re-run for this feature, though CI runs them anyway.

## UI review (Quality Gate 7)

Before calling this done, instantiate and complete
`specs/029-onboarding-team-search/checklists/ui-review.md` from
`.specify/templates/ui-review-checklist-template.md` against the diff. Watch specifically for:

- **one coral CTA per view** — Continue is it; the ask action and the retry are secondary;
- sentence case everywhere, warm "you" voice, no emoji;
- ≥44px touch targets on the rows and the ask action;
- mono face for the player count;
- visible focus ring on the search field, rows, and the ask action;
- the step still reads well at `max-w-sm` on a phone and on a desktop.
