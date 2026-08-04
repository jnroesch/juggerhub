# Quickstart & Validation: Browse Public Trainings

**Feature**: 043-browse-public-trainings | **Date**: 2026-08-04

This guide proves the feature end-to-end locally. It is a **validation guide**, not implementation —
code lives in `tasks.md` and the implementation phase. Shapes are in
[data-model.md](./data-model.md) and [contracts/trainings-browse.md](./contracts/trainings-browse.md).

## Prerequisites

- Docker + docker-compose, `.env` populated from `.env.sample`. **No new variable is added.**
- **No migration.** If `dotnet ef migrations list` shows a new one for this feature, something has
  gone wrong — this feature reads existing state and adds no schema.
- A team you are an **admin** of (to open trainings to the public), plus a **second account that
  belongs to no team** — that second account is the one this feature is for, and most scenarios below
  are worthless when run as a member.
- A **home city** set on the profile of the account used for US3.

```powershell
docker compose up -d          # backend, frontend, database, redis, mailpit
```

Sanity-check the fixtures the seeder already provides — `DevDataSeeder` creates one `TeamOnly` and
one `Public` training (`DevDataSeeder.cs:306,332`):

```powershell
curl "http://localhost:5000/api/v1/trainings?take=50" -H "Authorization: Bearer <token>"
```

Expect the public one present and the team-only one absent, **before writing any UI**.

---

## Validate the user stories

### US1 — Find an open training I could actually attend (P1)

Sign in as the **teamless** account.

1. Open **Browse** → the strip now shows four destinations → select **Trainings**.
2. The list shows upcoming public sessions with name, team, date, start–end time, and location.
3. Confirm the team-only series is **absent**. Then sign in as a **member of that team** and confirm
   it is *still* absent (scenario 2 — this is the one people get wrong).
4. As a team admin, open a **single session** of a team-only series to the public. Back in browse:
   exactly that session appears, the rest of the series does not (scenario 3).
5. Reverse it — set one session of a **public** series back to team-only. That session disappears;
   its siblings remain (scenario 4).
6. Select a row → the existing session page opens → respond **Going**. You are recorded as a guest
   (scenario 5).
7. From the home screen with nothing coming up, select **Browse open trainings** → lands on
   `/browse/trainings`, **not** `/browse/events` (scenario 6).
8. Sign out and request `/browse/trainings` → redirected to sign-in (scenario 7).

**Pass**: steps 3–5 behave identically for a member and a non-member. Verify against the API, not
only the screen:

```powershell
# as a MEMBER of the owning team — the team-only session must still be absent
curl "http://localhost:5000/api/v1/trainings?take=100" -H "Authorization: Bearer <memberToken>"
```

**Negative**: cancel a listed public session → it disappears from browse while remaining visible,
marked off, on the team's trainings tab. Skip another → it disappears from both.

---

### US2 — Narrow the list down (P2)

Seed public sessions across at least two cities, two countries, and a spread of dates.

1. Type part of a training name → the list narrows (debounced).
2. Search **`anfanger`** for a training named **`Anfängertraining`** → found (accent- and
   case-insensitive, scenario 4).
3. Open **Filters** → pick a **city** → only that city's sessions remain and the count line agrees.
4. Pick a **country** → only that country's sessions remain.
5. Set a **date range** → only sessions inside it remain; then set only a `from`, then only a `to`.
6. Remove one chip → only that filter clears, the rest stay applied (scenario 5).
7. Turn off "upcoming only" → past sessions appear. Default state shows none (scenario 6).
8. Filter to something impossible → **no-results** state offering to clear filters — visibly
   different from the **empty** state (scenario 7).

**Pass**: every filter narrows server-side. Confirm the client is not filtering:

```powershell
curl "http://localhost:5000/api/v1/trainings?city=Hamburg&take=100" -H "Authorization: Bearer <token>"
# every returned item is in Hamburg — the endpoint never returns a non-matching row
```

**The relocated-session check (SC-004 — do not skip).** Take a series whose address **has** a venue
name. Move one session to a different city at an address with **no** venue name.

- That session's row must show **no trace** of the series' venue name.
- Filtering by the **session's** city returns it; filtering by the **series'** city does not.

This is the 042 guard re-pointed at browse, and it is the failure a per-field `??` produces.

---

### US3 — Show me the closest ones first (P3)

1. As an account **with** a home city, open the **Sort** menu → **Nearest first** is offered.
2. Choose it → sessions are ordered by distance from your home city.
3. Note the **date chip** that appears: nearest-first defaults the range to the next 14 days
   (research R1). Remove it → the full range returns and one nearby team's schedule dominates,
   which is the behaviour the default exists to avoid.
4. Confirm **virtual** trainings vanish under nearest-first and reappear under soonest-first
   (scenario 4).
5. Sign in as an account with **no** home city → the Nearest-first option is **not offered**
   (scenario 2).
6. Force it by hand (scenario 3):

```powershell
curl -i "http://localhost:5000/api/v1/trainings?sort=Proximity" -H "Authorization: Bearer <noHomeCityToken>"
# expect 409, title "No home city" — never a 200 with a different ordering
```

**Pass**: `totalCount` under `sort=Proximity` equals the number of rows you can actually page to.
Page all the way through and count:

```powershell
curl "http://localhost:5000/api/v1/trainings?sort=Proximity&take=100" -H "Authorization: Bearer <token>"
# items.length (across pages) == totalCount — not the unfiltered total
```

That is the check that separates this implementation from `EventSearchService`'s latent
count-before-join defect (research R5).

---

## Cross-cutting checks

### SC-003 — a training and an event at the same address read identically

Create an event and a training at the **same** city and venue. Compare the two browse lists:

```powershell
curl "http://localhost:5000/api/v1/events?take=100"    -H "Authorization: Bearer <token>"
curl "http://localhost:5000/api/v1/trainings?take=100" -H "Authorization: Bearer <token>"
```

`locationLabel` must match **character for character**. It is composed by the same helper on both
sides; if they differ, someone copied the helper instead of calling it.

### FR-026 / SC-008 — the fourth tab on a narrow screen

The real risk in this feature. Check at **375px** in **all three languages** — Spanish
"Entrenamientos" is the binding case (research R9):

```powershell
npx nx e2e web-e2e --grep "browse"
```

No clipped, wrapped-to-illegible, or overlapping labels; every tab remains tappable at the 44px
minimum. **A smaller font or a truncation is not an acceptable fix** — DESIGN.md governs, and the
UI review checklist (constitution gate 7) is the gate.

### FR-029 / SC-009 — i18n parity

Run it; do not assume it:

```powershell
npx nx test web --testPathPattern catalog-parity
```

Adding `browse.trainings.*` to `en.json` alone turns this red. That is the point — fix the catalogue,
never the test.

---

## Full verification sweep

```powershell
# backend
dotnet test backend/JuggerHub.slnx

# frontend unit
npx nx test web

# lint + typecheck
npx nx lint web
npx nx build web

# e2e
npx nx e2e web-e2e
```

**SC-010 — the events browse must be untouched.** `EventBrowseTests` must pass **unmodified**. If a
task edited it, that is scope creep into FR-030 territory, not a fix.

---

## Known-good end state

- `/browse/trainings` lists public sessions across all teams, one row per session.
- Team-only sessions are absent for every viewer, members included.
- The home empty-state button lands on trainings, with zero events in the list.
- A relocated session shows and filters under its own address, with no leakage from the series.
- `sort=Proximity` orders by distance, excludes cityless sessions from both items and count, and
  409s without a home city.
- All three catalogues have identical key sets, and the four-tab strip is legible at 375px in each.
