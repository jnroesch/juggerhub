# Quickstart & Validation: Structured Locations for Trainings

**Feature**: 042-training-locations | **Date**: 2026-08-04

This guide proves the feature end-to-end locally. It is a **validation guide**, not implementation
— code lives in `tasks.md` and the implementation phase. Shapes are in
[data-model.md](./data-model.md) and [contracts/trainings-api.md](./contracts/trainings-api.md).

## Prerequisites

- Docker + docker-compose, `.env` populated from `.env.sample`. **No new variable is added** —
  city resolution is a local SQL lookup against the seeded `CityReference` table, not an external
  geocoder (research R6).
- A team you are an **admin** of. Trainings are team-scoped and admin-gated.

## Bring up the stack

```powershell
docker compose up -d          # backend, frontend, database, redis, mailpit
dotnet ef database update     # run from backend/ — applies AddTrainingStructuredLocations
```

The migration adds columns only. Existing trainings keep their free-text location and still render
it through the display fallback (research R7) — a blank location on an old training is a **failure**,
not expected behaviour.

Confirm the city source answers before testing the forms:

```powershell
curl "http://localhost:5000/api/v1/cities/search?q=kol&take=5"   # expect city options with displayLabel
```

---

## Validate the user stories

### US1 — Admin captures a real address when scheduling a training (P1)

1. As a team admin, go to the team → **Trainings** → **+ New training**.
2. Advance to the **Where** step with **In person** selected.
3. Leave street/postal/city empty → **Continue is blocked** and names what is missing (scenario 2).
4. Fill venue `Sportpark Müngersdorf`, street `Aachener Str. 999`, postal `50933`.
5. Type `köl` in the city field → debounced suggestions appear; select **Köln, Germany** (scenario 1).
6. On the **review** step, all four values are shown back before committing (scenario 5).
7. Create. Then create a **one-off** the same way.
8. Create a **virtual** training: the Where step shows **only** the link field — no venue, street,
   postal or city input (scenario 3).
9. Create an in-person training, pick a city, then switch to virtual and submit (scenario 4).

**Pass**: the two in-person trainings have `VenueName` / `Street` / `PostalCode` / `CityId`
populated; the virtual ones have all four `null` and a `VirtualLink` set. Verify in the API
response, not only the screen:

```powershell
curl "http://localhost:5000/api/v1/trainings/sessions/<sessionId>"   # venueName/street/postalCode/location
```

**Negative**: post a create with an invented `cityExternalId` → `400`, message names the city, and
**no training row is created** (FR-005).

---

### US2 — Players read one consistent location everywhere (P2)

1. Open the team's **Trainings tab** → the row shows the city-anchored `locationLabel`.
2. Open a **training session** → same label, plus the full structured address.
3. Open the **dashboard agenda** → same label on the up-next card.
4. Create a training with a city but **no venue name** → the label is the city alone, with no
   dangling separator or blank (scenario 2).
5. Open a **virtual** training on all three surfaces → reads as online (scenario 3).

**Pass (SC-003)**: create an **event** at the same address as one of the trainings and compare the
two labels character for character across all three surfaces. They must be identical — both come
from `HomeProjections.LocationLabel`.

---

### US3 — Admin corrects a training's address later (P3)

1. Open a training session of a recurring series → **Edit** → **the whole series**.
2. The venue, street, postal code and the **currently selected city** are pre-filled (scenario 1).
3. Change the address and the city; save.
4. Every upcoming session that still follows the series shows the new location (scenario 2); past
   sessions are unchanged.
5. Clear the city on an in-person training and save → **refused**, missing city named (scenario 4).
6. Switch the series to virtual and save → the stored address is cleared (scenario 5).

**Pass**: one series edit updates every upcoming non-detached session with no per-session write —
the sessions inherit. Confirm no `TrainingSessions` row gained override values.

---

### US4 — Admin relocates a single session (P4)

1. Open an upcoming session of the series → **Edit** → **just this session**.
2. Give it its own venue, street, postal code and city; save.
3. Only that date's label changed; every sibling is unchanged (scenario 1).
4. **The venue-leak check (FR-007, the load-bearing one)**: ensure the *series* has a venue name,
   then relocate one session to an address with **no** venue name. The relocated session must show
   **no** venue — if the series' venue appears next to the session's street, the block rule was
   implemented as per-field `??` and is wrong (research R1).
5. Supply street + postal but no city on a single-session edit → **refused** (scenario 2).
6. Now edit the **series** address again → the relocated session keeps its own address (scenario 5).
7. Clear the session's relocation → it returns to the series address (scenario 3).
8. Make one session **virtual** → it shows online with its link and stores no address (scenario 4).

**Pass**: `CityIdOverride` non-null on exactly the relocated session; all four override columns
`null` on the session switched to virtual.

---

## Edge cases to exercise

| Case | Expected |
|---|---|
| City search unreachable mid-form | The field reports the search is temporarily unavailable; the rest of the form is preserved; an in-person training **cannot** be completed |
| Street with no postal code | Refused, both on the form and by the API |
| Venue name only | Refused — a venue name is not an address |
| Two cities with the same name | Search results disambiguate; the training records the option picked, not a name match |
| Pre-042 training with legacy free text | Still renders its old text; never blank |
| Past session | Read-only; a later series edit does not rewrite its location |

---

## Verification suite

```powershell
# Backend
dotnet test backend/tests/JuggerHub.Api.IntegrationTests

# Frontend
cd frontend
npx jest
npx nx lint web

# End-to-end (city selection helper already exists: apps/web-e2e/src/support/city.ts)
npx nx e2e web-e2e
```

Also required before verification (Constitution gate 7): copy
`.specify/templates/ui-review-checklist-template.md` to
`specs/042-training-locations/checklists/ui-review.md` and run it against the diff. Three forms and
three read surfaces change; DESIGN.md wins on any conflict.

## Definition of done

- [ ] All four user stories validated above
- [ ] Every contract test in [contracts/trainings-api.md §6](./contracts/trainings-api.md) passes,
      including the venue-leak guard
- [ ] Event tests still green after `EventService` is refactored onto the shared helpers
      (no behaviour change — research R2)
- [ ] `en` / `de` / `es` catalogues at key parity, enforced by the new guard spec (research R8)
- [ ] UI review checklist complete
- [ ] No `HttpClient`, retry policy or circuit breaker added anywhere in the diff (research R6)
