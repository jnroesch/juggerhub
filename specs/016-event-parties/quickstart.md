# Quickstart — Validate Event Parties end-to-end

A runnable validation guide proving the feature works. Details of shapes live in
[contracts/party-api.md](./contracts/party-api.md) and [data-model.md](./data-model.md).

## Prerequisites

- Local stack up via docker-compose (backend, frontend, Postgres, Mailpit) per the repo README.
- The `AddParties` migration applied (`dotnet ef database update`, or automatic on startup as the
  app is configured).
- Dev seed includes: a **teams-only** event (e.g. "Tempelhof Summer Slam", `rosterCap = 8`), a team
  (e.g. **Rheinfeuer**) with ≥ 6 members, one of whom is a team admin (Ada), and — optionally — a
  pre-seeded sample party for read-only screens.

## Backend build & test

```powershell
# from repo root
dotnet build backend/JuggerHub.Api.sln
dotnet test  backend/tests/JuggerHub.Api.IntegrationTests --filter FullyQualifiedName~Parties
```

Expected: build clean; the `Parties` integration suite green (form authz, request fan-out,
accept/decline/leave, cap auto-close/reopen, manage authz, apply routing, withdraw, private news,
team-scoped co-admins + last-admin guard, disband cascade + event withdrawal).

## Frontend build & test

```powershell
# from frontend/
npx nx build web
npx nx test  web --test-path-pattern=parties
```

Expected: build clean; party component specs green (zoneless — no `fakeAsync`).

## Scenario A — Form → request → answer (US1–US3, US6)

1. Sign in as **Ada** (Rheinfeuer admin). Open the teams-only event page.
   - **Expect**: primary action reads **"Enter a party"** (no direct "join with your team").
2. Form a party (pick Rheinfeuer if prompted, add a message) → submit.
   - **Expect**: party created; you land in **Manage party**; you are `In + Admin` (1/8); the team is
     **not** yet on the event.
3. Open Mailpit + a second member's session (**Ben**): confirm Ben has a party-request **email** and
   an in-app **notification**, and the **pinned card** at the top of the Rheinfeuer team space.
4. As Ben, tap **"I'm in"** → **Expect** 2/8 and Ben in the **In** group. As **Chris**, tap
   **"Can't make it"** → **Expect** Chris in **Declined**, still sees the card; then tap "I'm in"
   later → moves to **In** (decline is reversible).
5. Fill to 8/8 → **Expect** the card shows **full**, no "I'm in". Have one In member **leave** →
   **Expect** the card **auto-reopens** at 7/8; the next member to tap "I'm in" takes the spot.
   Confirm two near-simultaneous joins on the last spot never exceed 8.

## Scenario B — Manage (US4)

1. As Ada in Manage: confirm three groups (**In / Declined / No response**) with correct counts and
   a **readiness** summary ("enough to field a team", "N spots open", "N unanswered").
2. **Nudge** a no-response member → confirm a fresh notification + email arrive.
3. **Remove** an In member → confirm they leave the party but **remain on the team** (check the team
   roster) and no badge changed.
4. As a non-admin member, confirm nudge/remove affordances are absent, and a direct API call is
   refused (**403**).

## Scenario C — Apply & withdraw (US5)

1. As Ada, **Apply to event** on the **free** event → **Expect** Rheinfeuer listed as a single
   **Joined** entry on the event page; the pinned card flips to **applied**.
2. On a **paid** teams-only event, apply → **Expect** **awaiting approval** (pending) with payment
   details, per feature 006. On a **full** event, apply → **Expect** the team on the **event**
   waiting list.
3. **Withdraw** → **Expect** the team leaves the event; the party remains in the team (status back to
   open, still editable).

## Scenario D — News, co-admins, disband (US7–US9)

1. Post **party news** as Ada → visible to party members newest-first; confirm a team member **not**
   in the party is refused (**404**) and a non-admin has no compose box.
2. **Invite a co-admin** by searching team members → accept as that member → they gain admin powers;
   confirm a non-team-member cannot be invited/accept; confirm the **last admin** cannot step down.
3. **Disband** (guarded confirm) an **applied** party → **Expect** party + news gone, pinned request
   removed from the team space, the team **withdrawn** from the event, and the team/roster/badges
   unchanged. Confirm a non-admin cannot disband.

## UI review

Before UI verification, instantiate `.specify/templates/ui-review-checklist-template.md` into
`specs/016-event-parties/checklists/ui-review.md` and verify each item against the diff (DESIGN.md
wins on any conflict): the event "Enter/Manage party" action, the pinned team-space request card,
the Manage hub (dense desktop + tabbed mobile), the private news feed, and empty/loading/error
states throughout.
