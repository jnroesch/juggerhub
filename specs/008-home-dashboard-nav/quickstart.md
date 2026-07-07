# Quickstart — validate feature 008 (Home dashboard & navigation)

A runnable checklist proving the feature works end-to-end. Details live in `spec.md`,
`data-model.md`, and `contracts/openapi.yaml`; this is the *run & verify* guide.

## Prerequisites

- Docker + docker-compose (the standard local stack: backend, frontend, Postgres, Mailpit).
- The `AddHomeIndexes` migration auto-applies on backend startup.
- `DevDataSeeder` (Development) seeds a **team-member demo player** and a **no-team demo player** (see `data-model.md` → Seeding). Note their sign-in credentials from the seeder output.

## Bring the stack up

```powershell
docker compose up -d --build
# Frontend: http://localhost:4200   API: http://localhost:5xxx/api/v1   Mailpit: http://localhost:8025
```

Backend + frontend tests:

```powershell
# Backend integration tests (Testcontainers spins up Postgres)
dotnet test backend/JuggerHub.sln

# Frontend unit + e2e
npx nx test web
npx nx e2e web-e2e
```

---

## Scenario A — Nav shell, desktop (US1, SC-002)

1. Sign in as the **team-member** demo player at `http://localhost:4200`.
2. **Expect** a single sticky **top bar**: brand · Home · Browse · My team · a bell (Alerts) · avatar. **No** left sidebar drawer.
3. Click Browse → the existing teams/events/players search opens; the **Browse** destination is marked active. Click Home → **Home** active.
4. Open the **avatar menu** → Profile · Account · Sign out. Confirm "create team/event" are **not** top-level destinations.

## Scenario B — Nav shell, mobile (US1)

1. Narrow the viewport (or emulate a phone).
2. **Expect** a fixed **bottom tab bar**: Home · Browse · My team · Alerts, and a slim top strip with the wordmark + avatar.
3. Tap each tab → the correct destination loads and its tab shows the active state (`aria-current="page"`). The bottom bar clears the safe-area inset and never covers the RSVP controls.

## Scenario C — Home leads with Up next + RSVP toggle (US2, SC-001)

1. As the team-member player, land on Home (`/`).
2. **Expect** greeting "Hi {name}" + a short agenda line, then an **Up next** module listing upcoming events **soonest-first** with date/title/location/time/spots.
3. On an **individuals-mode** item you have **not** joined (in Open-to-everyone or a seeded open training), tap **RSVP** → it flips to **"going ✓"** without leaving Home (calls `POST /events/{id}/signup`).
4. Tap **"going ✓"** → confirm → you are withdrawn and it returns to **RSVP** (calls `DELETE /events/{id}/signup/{signupId}`).
5. On a **team-mode** event your team entered, **expect** a read-only **"your team is going"** state — no personal RSVP control.
6. Tap **See all** → the full paginated upcoming list (`GET /home/up-next`).

## Scenario D — Your teams, News, Tournaments (US3, SC-005)

1. **Your teams**: recent activity across **all** your teams, newest-first, each tagged with its team + an "open team" link into `/t/{slug}`.
2. **News**: items newest-first, each tagged **team** or **event** with a relative timestamp; "see all" → `GET /home/news`.
3. **Tournaments**: a promoted tournament card (name/place/date/spots + "view" → `/events/{id}`); "see all" → `/browse/events?type=Tournament`.
4. **Desktop right rail**: one **snapshot per team** (name + next fixture, **no win/loss record**) + the tournament card. On mobile these are inline in the single column.
5. Temporarily remove a module's data (or use a fresh account) → that module shows a **friendly empty state**, never a blank/broken card.

## Scenario E — New-player empty state (US4, SC-004)

1. Sign in as the **no-team** demo player.
2. **Expect** "Welcome, {name} — let's find you a crew"; Up next / Your team replaced by prompts: **"Find a team near you"** (→ Browse teams) + **"Browse open trainings"** (→ open events).
3. **Expect** an **Open to everyone** module of open events with the same one-tap RSVP, and the **News** module still present.
4. Have this player join a team → return to Home → it now shows the **team-member** variant.

## Scenario F — Alerts + account placeholder (US5)

1. From desktop bell / mobile Alerts tab → **expect** a friendly placeholder ("you're all caught up"); the unread count reads **zero / hidden**.
2. Avatar menu → Profile and Account reach the existing screens.

## Scenario G — Entitlement (SC-006) — the security check

1. As player A, note an event you're signed up to and one of your team's news items.
2. As player B (different teams), call `GET /api/v1/home`, `/home/up-next`, `/home/news`.
3. **Expect** none of A's private sign-ups and none of A's team-only news appear for B. (Covered by the `Home/` integration tests — run `dotnet test` and confirm the entitlement-invariant tests pass.)

## Scenario H — Resilience & performance (SC-003, SC-005)

1. With a warm stack, reload Home → greeting + Up next appear within ~1 s.
2. Simulate one module's backend read failing (e.g. temporarily break the news window) → the rest of Home still renders; the failed module shows a **retry**, not a whole-page error.

---

## Done when

- Scenarios A–H pass at **desktop and mobile** viewports.
- `dotnet test`, `nx test web`, `nx e2e web-e2e` are green.
- A visual pass against **DESIGN.md** (warm sand/coral, rounded, Lucide icons, sentence case, mono for scores/times, no emoji) and basic a11y (labeled/active nav, ≥44px targets, keyboard reachability) holds.
