# Quickstart & Validation: Trainings

End-to-end validation for feature 018. Proves the core loops from the spec's user stories. Details of
shapes live in [data-model.md](./data-model.md) and [contracts/trainings-api.md](./contracts/trainings-api.md).

## Prerequisites

- Local stack up via docker-compose (backend + Postgres + Mailpit) per the constitution.
- `AddTrainings` migration applied (`dotnet ef database update` runs on startup in local).
- `DevDataSeeder` has seeded the demo team (Rheinfeuer) with: one weekly series (mixed responses), one
  one-off, and one public session carrying a guest response.
- Two seeded accounts: a **team admin** on Rheinfeuer, a **plain member** of Rheinfeuer, and a **third
  user who is NOT on Rheinfeuer** (the outsider).

## Run

```powershell
docker compose up -d --build         # backend + db + mailpit
# frontend
cd frontend; npm run start           # Nx serve → http://localhost:4200
```

## Scenario A — Member responds (User Story 1, P1)

1. Sign in as the plain member. Open Rheinfeuer → **Trainings** tab.
2. **Expect**: upcoming sessions as a dated list; each row shows a Series/One-off badge, a going count,
   and your inline answer (or a prompt). No "+ New training" button.
3. Open a session. Set **Going** → the going count increments, you appear in the Going group.
4. Change to **Can't** → your row moves to Can't; the going count decrements; no duplicate response.
5. **Verify**: who's-coming is grouped Going / Maybe / Can't with per-group counts and no cap.

## Scenario B — Admin creates a series and a one-off (User Story 2, P1)

1. Sign in as the team admin. Trainings tab → **+ New training**.
2. Wizard: choose **Recurring series**, name it, pick a weekday + start/end time + **Weekly** + an end
   date ~9 weeks out → review states "~9 sessions" → **Create training**.
3. **Verify**: the series' dated sessions appear in the tab; the count matches the weekday occurrences
   between start and end date inclusive (spot-check against a calendar). The member receives a
   `TrainingScheduled` notification.
4. Repeat choosing **One-off session** → the schedule step collapses to a single date → exactly one
   session is created.
5. **Negative**: try an end date before the start, or an end-time ≤ start-time → creation is blocked
   with a clear message (no empty training persisted).

## Scenario C — Edit this-vs-series, skip, cancel (User Story 3, P2)

1. As admin, edit a recurring session → you are first asked **This session only** vs **The whole series**.
2. **Whole series → change the time** (in-place): every upcoming non-detached session shifts; past
   sessions unchanged; responders get a `TrainingUpdated` notice.
3. **This session only → change its location**: only that session changes and it shows **detached**; a
   subsequent whole-series edit skips it (keeps its own values).
4. **Extend the end date** (regenerate): new sessions appear for the added weeks; existing upcoming
   sessions and their responses are preserved.
5. **Change the weekday** (regenerate): the future set moves to the new weekday; past & detached
   untouched; responses on surviving dates preserved.
6. **Skip a date**: it disappears quietly from the list (no notification).
7. **Cancel a session**: it stays visible marked **Cancelled**, blocks new responses, and notifies its
   responders.

## Scenario D — Public session, guests (User Story 4, P2)

1. As admin, open a session → **Manage** → **Make this session public** (or the series visibility toggle).
2. Copy the shareable link. Sign in as the **outsider** and open that link.
3. **Verify**: the outsider can view and set **Going/Maybe/Can't** with no approval, recorded as a
   **guest**.
4. As admin, open **attendance**: the guest shows a **guest tag**, is counted in the going total, and can
   be **removed**. After removal the guest is gone and is **not** on the team roster.
5. **Verify isolation**: as the outsider, a **team-only** session of Rheinfeuer is **not** accessible
   (404). Flip the public session back to team-only → the outsider loses access.

## Scenario E — Dashboard "Your trainings" agenda (User Story 5, P3)

1. Ensure the member belongs to a second team with upcoming sessions and has joined one public session as
   a guest.
2. Sign in as the member → **home dashboard**.
3. **Verify**: the "Your trainings" agenda lists upcoming sessions across **both** teams plus the public
   guest session, in chronological order, each with an inline RSVP. Changing an RSVP inline saves the
   same as the session page.
4. A member with no upcoming sessions sees a gentle empty state.

## Automated checks

- **Backend**: `dotnet test backend/tests/JuggerHub.Api.IntegrationTests` — Trainings suite covers
  create (series/one-off + validation), RSVP upsert + who's-coming, edit-fork (in-place, detach,
  regenerate on pattern/end-date), skip vs cancel, public/guest access + removal + team-only isolation,
  and the `me/trainings` agenda. Plus `RecurrenceExpander` unit tests (weekly/bi-weekly/monthly-by-
  weekday, 5th-weekday months, single-day range, zero-session guard).
- **Frontend**: `npx nx test web` — Trainings components + the dashboard module (zoneless specs).
- **UI review**: instantiate `checklists/ui-review.md` from the template and check the Trainings tab,
  wizard, session page, manage/edit sheets, attendance, empty states, and the dashboard module against
  DESIGN.md.

## Definition of done (traceability)

| Success criterion | Validated by |
|---|---|
| SC-001 create series < 2 min | Scenario B |
| SC-002 exact session count per interval | Scenario B + RecurrenceExpander unit tests |
| SC-003 single current RSVP + live who's-coming | Scenario A |
| SC-004 series edit affects upcoming non-detached only | Scenario C.2–C.5 |
| SC-005 public guest RSVP, counted/removable; team-only isolated | Scenario D |
| SC-006 cross-team agenda chronological | Scenario E |
| SC-007 no unbounded lists | All list endpoints paginate (contracts) |
