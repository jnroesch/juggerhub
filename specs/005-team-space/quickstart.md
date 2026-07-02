# Quickstart — Team Space & Member Handling

End-to-end validation that the feature works. Docker-only workflow (no host runtimes). Assumes the 002 auth + 003 profile stacks are functioning.

## Prerequisites

- `.env` present (see `.env.sample`); `docker compose` available.
- Stack up: `docker compose up --build` → backend `:8080`, frontend `:3000`, Postgres `:5432`, Mailpit `:8025`.
- Migrations auto-apply on startup (`AddTeams` creates the team tables + adds `EventParticipation.TeamId`).
- Dev seed creates demo teams (a CityTeam "Rheinfeuer" + a Mixteam), memberships, a few news posts, and team-attributed participations so Activity/News render. Register/verify at least two accounts (an admin owner + a second user to invite/manage).

## Scenario A — Create a team; become its admin (US1)

1. Signed in, open `http://localhost:3000/teams/new`.
2. Enter a name (e.g. `Rheinfeuer`) and a team address `rheinfeuer` — availability updates live (a taken/reserved/malformed slug shows **unavailable** + reason). Pick **City team** → the **city** field appears (enter `Berlin`). Submit.
3. Repeat with **Mixteam** → the city field is **hidden**; submit with no city.

**Expected**: each team is created; you land on `/t/<slug>` tagged **admin** with invite/manage/settings controls. Two teams may share a display name; the slug must be unique.

**Contract checks**: `POST /teams` duplicate slug → 409; `GET /teams/slug-available?slug=new` → `available:false` (reserved); city on a Mixteam or missing city on a CityTeam → 400.

## Scenario B — Visit the team space (US2)

1. Open `/t/rheinfeuer`. **Members** tab: every member appears once with name, profile positions (pompfen), and an **admin/member** tag; admins see a per-row "…" menu.
2. **Activity** tab: the team's seeded events newest-first (month, name, location; no scores); an eventless team shows a friendly empty state.
3. **News** tab: seeded posts newest-first (author, author role, relative time, body); admins see a disabled "+ Post" (composer deferred). **Trainings** tab is visibly disabled.

**Contract checks**: `GET /teams/{slug}/members|activity|news` return `PagedResult<T>` (bounded).

## Scenario C — Invite by link and by search (US3)

1. On Members, open **Invite people** → Invitations.
2. **Link**: copy the invite link (`/join/rheinfeuer/<token>`, "expires in 7 days"). Rotate it → the old link stops working, a new one is active. Revoke → no active link.
3. **Search**: search a user by name → **Invite** creates a targeted invite; an already-invited user shows **Invited**, an existing member shows **Member**. Check **Mailpit `:8025`** for the invite email containing the accept link.
4. The pending list shows the link + targeted invites with remaining validity; **Revoke** cancels one.

**Contract checks**: `POST /teams/{slug}/invitations/link` returns a new `InviteLinkDto` and revokes the prior (partial-unique active link); re-inviting the same user → 200 "already invited" (no duplicate); inviting a member → 400.

## Scenario D — Accept / decline (US4)

1. As the invited second user, open the emailed/copied `/join/rheinfeuer/<token>`. Confirm the **public preview** (name, city, member count, "…invited you") — **no roster/news**.
2. **Accept & join** → added to the roster as a **member**; member count reflects you.
3. Open a **revoked/expired** link → "this invite has expired" terminal state, no join. Open an invite while **signed out** → sign-in/register, then return to the same accept screen.

**Contract checks**: `GET /invitations/{token}` → `state` `Usable|Expired|Invalid`; `POST /invitations/{token}/accept` on an expired/revoked/used token → 409; accepting while already a member → 200 no-op.

## Scenario E — Roles & the last-admin guard (US5)

1. With two admins: from a member's "…" menu, **Make admin** / **Remove admin**; **Remove from team** takes them off the roster. From **settings**, **Step down to member**.
2. As the **only admin**, try to demote yourself, remove yourself, or step down → **blocked** with "appoint another admin first"; the team keeps ≥ 1 admin.

**Contract checks**: `PATCH /members/{userId}/role` and `DELETE /members/{userId}` that would zero-out admins → **409** with no state change (verify concurrently — two admins demoting each other cannot both succeed).

## Scenario F — Delete a team (US6)

1. As an admin, **Team settings → Danger zone → Delete team**, confirm.
2. As a non-admin, confirm no delete affordance and a direct `DELETE` → 403.

**Expected**: the team, its roster, pending invites, and news are gone; `/t/<slug>` and former invite links show friendly not-found/expired. Players' **profile activity for past events remains** (event records intact; attribution becomes "former team").

**Contract checks**: `DELETE /teams/{slug}` (admin) → 204; afterwards `GET /teams/{slug}` → 404; `GET /u/<member-handle>/activity` still lists the event.

## Scenario G — Visibility & never-trust-the-client

- As a **non-member** (or signed out), `GET /teams/{slug}` / `/members` / `/news` / `/activity` / `/invitations*` → **404** (no roster/news bytes); `GET /teams/{slug}/public` → 200 with only name/type/city/count.
- Every admin action attempted by a non-admin member or non-member → **403/404**, independent of the client UI.

## Responsiveness (SC-008)

Validate `/t/:slug` (tabs), `/teams/new`, and `/join/:slug/:token` at phone (~375px) and desktop (~1280px) — layout, empty/loading/error states legible per `DESIGN.md` (Playwright runs both).

## Automated validation

- Backend: `docker compose -f docker-compose.test.yml ...` runs the xUnit integration suite (create+slug uniqueness/immutability, city rule, member-only reads → 404 for non-members, admin-only mutations, last-admin guard incl. concurrency, invite link rotate/revoke + 7-day expiry, targeted-invite email + duplicate/member handling, accept/decline + already-member, delete cascade + participation SetNull, pagination).
- Frontend: Jest unit (TeamService, slug validator, role/last-admin UI gating) + Playwright `teams.spec.ts` (create → invite → accept as 2nd user → manage roles/last-admin → delete), desktop + mobile.
