# Quickstart & Validation: Home Participation Makeover

A run/validation guide proving the reshaped home end-to-end. Implementation details live in `tasks.md`; this file is how you confirm each user story works.

## Prerequisites

- Full stack up locally via docker-compose (backend, frontend, Postgres, Mailpit) per the constitution.
- A seeded signed-in player on at least one team, plus fixtures created through the existing flows (events, trainings, parties, marketplace, badges).

## Run

```powershell
# from repo root
docker compose up -d            # backend + frontend + postgres + mailpit
# backend tests
dotnet test backend/tests/JuggerHub.Api.IntegrationTests
# frontend unit + e2e
npx nx test web
npx nx e2e web-e2e
```

Open the app, sign in, land on `/` (home).

---

## Validation scenarios (by user story)

### US1 — Needs you (P1)

**Setup**: give the viewer a pending team invite, a pending marketplace invite, a marketplace application, and an un-answered training within ~14 days.

**Verify**:
- The **Needs you** block renders at the **top** of home with all four items, each showing its inline action (accept/decline, or going/maybe/can't; the application shows *pending*).
- Accept the invite → the row leaves the block without a full reload.
- Resolve every item → the whole **Needs you** block disappears.
- Seed an unread chat message → it does **not** appear in Needs you (SC-001, FR-006).
- API check: `GET /api/v1/home` → `needsYou[]` reflects only authoritative pending items; marking a notification read out-of-band does **not** change `needsYou`.

### US2 — Unified Up next (P1)

**Setup**: an individual event sign-up, a team-mode event the viewer's team entered, and an **answered** training — at interleaved dates. Also one upcoming tournament the viewer is unrelated to.

**Verify**:
- **Up next** shows all three participation items in one list, soonest-first.
- Individuals event → inline RSVP/withdraw toggle; team event → read-only "team going"; training → going/maybe/can't.
- The unrelated tournament appears **nowhere** on home (FR-009, FR-015, SC-003).
- Multi-team edge: put the viewer on two teams that both entered one event → it appears **once** (FR-013).
- `GET /home/up-next` paginates the same unified agenda.

### US3 — News (P2)

**Setup**: a team news post, an event news post, and a **party** news post for a party the viewer is `In`.

**Verify**:
- **News** shows all three, newest-first, tagged by source (party included).
- Reschedule a training (a system event) → it appears in **What's going on**, never in **News** (SC-004).
- A non-`In` party's news is not visible (FR-023).

### US4 — What's going on (P3)

**Setup**: a teammate signs up for an event; a new member joins one of the viewer's teams; a badge is awarded to the viewer; a training the viewer responded to is rescheduled.

**Verify**:
- **What's going on** is the **last** section and lists all four as **read-only** entries (no action controls — FR-025).
- No authored news post appears here; no activity entry appears in News (SC-004 — disjoint streams).

### US5 — No-team variant (P2)

**Setup**: a viewer with **no** team memberships.

**Verify**:
- Home shows the welcoming greeting + **find a team** prompt and an **open to everyone** list of joinable events.
- Team/party-only sections (team-mode agenda items, team/party news, activity) are absent (FR-028).

### Cross-cutting

- **Loading/failure**: throttle the composite → skeleton shows; force a 500 → retry affordance shows, page not blanked (FR-029).
- **All-empty (has team)**: a team member with nothing pending/upcoming/news/activity still sees a coherent page with empty states, not a blank screen.
- **UI review**: complete `checklists/ui-review.md` against the diff; DESIGN.md wins on any conflict (Quality Gate 7).

---

## Expected outcome

A home screen that reads top-to-bottom as **what needs me → what's coming up for me → what's new to read → what's going on**, showing zero content the viewer has no participation relationship with, with News and activity as fully separate streams.
