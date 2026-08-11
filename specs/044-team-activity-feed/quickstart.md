# Quickstart & Validation: Team-internal "What's happening" section

**Feature**: 044-team-activity-feed | **Date**: 2026-08-11

Proves the feature end-to-end locally. This is a **validation guide**, not implementation — code
belongs in `tasks.md` and the implementation phase. Shapes are in
[data-model.md](./data-model.md) and [contracts/team-happenings.md](./contracts/team-happenings.md).

## Prerequisites

- Docker + docker-compose, `.env` populated from `.env.sample`. **No new variable is added.**
- **No migration.** If `dotnet ef migrations list` shows a new one for this feature, stop — this
  feature reads existing state and adds no schema.
- Two accounts: one that is an **admin** of a team (to grant awards you need a platform admin
  too), and one that **is not a member of that team**. Most scenarios below are worthless run as
  a member, and G4 can only be checked with the outsider.

```powershell
docker compose up -d          # backend, frontend, database, mailpit
```

### Sanity check before any UI exists

The endpoint should be reachable and members-only from the first backend task:

```powershell
# As a member — expect 200 and a JSON array (possibly empty)
curl "http://localhost:5000/api/v1/teams/<slug>/happenings" -H "Authorization: Bearer <member-token>"

# As a non-member — expect 404, byte-identical to an unknown slug
curl -i "http://localhost:5000/api/v1/teams/<slug>/happenings"       -H "Authorization: Bearer <outsider-token>"
curl -i "http://localhost:5000/api/v1/teams/no-such-team/happenings" -H "Authorization: Bearer <outsider-token>"

# Anonymous — expect 401
curl -i "http://localhost:5000/api/v1/teams/<slug>/happenings"
```

---

## Validate the user stories

### US1 — A member catches up on their own team (P1)

Sign in as the **member**. Open `/t/<slug>`.

1. **Section exists.** A "What's happening" card is on the page, distinct from the events card.
2. **Member joined.** Have the outsider request to join and an admin approve. Reload → the join
   appears as the newest entry, naming the joiner, with a relative timestamp.
   → *FR-004, SC-001*
3. **Session cancelled.** As team admin, cancel a future training session. Reload → the
   cancellation is now newest, naming the training **and the session's date**.
   → *FR-007*
4. **Series created.** Create a **weekly recurring** training running two years. Reload → **exactly
   one** entry appears for it.
   → *D3, SC-004.* This is the highest-value single check in the guide: a wrong implementation
   shows up to 520 entries and swamps everything else.
5. **Award.** As a platform admin, grant the **team** a badge. Reload → one entry naming the badge.
   → *FR-005*
6. **No events.** Confirm no entry describes an event the team played — those stay in the events
   card only. → *FR-008, G7*
7. **Empty state.** On a team with nothing in the last 30 days, the card is **still rendered** and
   shows a "nothing lately" empty state — not hidden, not an empty box, and not an error style.
   → *FR-014.* Note this differs from the dashboard's activity list, which renders nothing when
   empty; do not copy that behaviour.
8. **German.** Switch language to Deutsch. Every sentence is German; only names (player, training,
   badge) stay as entered. → *FR-023, SC-006*

### US2 — The team page stops contradicting itself (P2)

1. The card that lists events the team played is headed **"Recent events"** — not "Recent
   activity". → *FR-017*
2. In German the two headings read **"Letzte Events"** and **"Was passiert gerade"**, and neither
   is the dashboard's **"Was ist los"**. → *SC-010, research R9*
3. Repeat in Spanish: **"Eventos recientes"** and **"Qué está pasando"**.
4. No single happening appears in both cards.
5. The events card's contents, order, and cap of 6 are exactly as before the feature.
   → *FR-016, SC-002*

### US3 — A non-member's view is unchanged (P3)

Sign in as the **outsider** and open the same `/t/<slug>`.

1. The "What's happening" card is **absent entirely** — not present-and-empty. → *FR-002*
2. Nothing on the page reveals the team-only training you cancelled in US1 — no name, no date, no
   location. → *SC-003*
3. Hitting the endpoint directly returns `404`, identical to an unknown slug (see the sanity
   check above). → *FR-003, G4*
4. The events card shows exactly what it showed before the feature. → *SC-002*

---

## Validate the bounds

| Check | How | Expects |
|---|---|---|
| 10-entry cap | Add 12 members to a team in one sitting | Exactly 10 entries render | 
| 30-day window | Backdate a `JoinedDate` to 40 days ago in the database, reload | That join is absent | 
| Both together | 12 members added, 3 of them backdated past the window | ≤ 9 entries, none older than 30 days |
| Stable order | Call the endpoint twice without changing data | Identical order both times → *FR-015* |

> The two bounds are **hardcoded constants** (owner decision D4), so there is no setting to flip
> for these checks — backdate the data instead.

## Validate the self-correcting behaviour

These follow from the feed being derived rather than stored. Each is a regression test for a
future "let's persist it" refactor:

| Action | Expected next load |
|---|---|
| Remove the member who joined | Their join entry is **gone** |
| Revoke the team's badge | Its entry is **gone** |
| Ban the joiner's account | The entry stays; the name becomes a **translated** stand-in ("Someone" / "Jemand"), never English on a German page |
| Delete the joiner's account (feature 037) | Same — translated stand-in, name and handle not resurrected |

## Regression checks (must not change)

| Surface | Check |
|---|---|
| Dashboard "Was ist los" | Identical entries, order, cap, and wording as before → *FR-027, SC-009* |
| `GET /teams/{slug}/activity` | Still `PagedResult<ActivityItemDto>`, still event-shaped → *FR-018* |
| Profile pages | Recent-activity sections unchanged → *FR-028* |
| Notifications | Cancelling a session still notifies exactly as before; the feed sends nothing → *FR-029* |
| Badges & achievements card | Still undated, still a standing collection, no date ordering → *FR-019, FR-020* |

## Automated verification

```powershell
# Backend — unit + integration (Testcontainers)
dotnet test backend/JuggerHub.sln

# Frontend — component + i18n catalogue parity
npx nx test web
```

The i18n parity guard (`frontend/apps/web/src/app/core/i18n/catalog-parity.spec.ts`) already walks
the whole catalogue, so it covers the 7 new keys and the 2 renames for free — **run it to confirm,
do not assume**. A rename applied to `en.json` but not `de.json` fails there.

## Design review (constitution gate 7)

This ships UI, so before sign-off:

1. Copy `.specify/templates/ui-review-checklist-template.md` → `specs/044-team-activity-feed/checklists/ui-review.md`
2. Verify each item against the diff. DESIGN.md wins on any conflict.
3. Pay particular attention to:
   - **The awards overlap** (FR-019/FR-020) — for a member, one award now appears both in the
     dated card and the standing-collection card. They must read as a *log* and a *trophy shelf*,
     not as two happenings. This is the feature's main UI risk.
   - **375 px** — every kind, including the longest German wording
     ("… wurde abgesagt" with a long training name). → *SC-007*
   - **Sentence case** everywhere, **no emoji**, empty state styled as `jh-empty-state` and
     never as an error.
