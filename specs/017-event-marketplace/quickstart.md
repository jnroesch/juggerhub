# Quickstart: Event Marketplace (Mercenaries)

End-to-end validation for feature 017. Proves the two-sided board, the two-way handshake, guest
membership, cap enforcement, "one crew" cleanup, and reach (inbox + dashboard + notification/email).

## Prerequisites

- Stack up locally: `docker compose up -d` (backend, frontend, Postgres, Mailpit) from repo root.
- Apply the migration: `dotnet ef database update` (from `backend/`) — includes `AddEventMarketplace`.
- Dev data seeded (`DevDataSeeder`) with: a **teams** event *"Tempelhof Summer Slam"* (roster cap 8),
  a team *"Rheinfeuer"* with an **open party** for it (some members In), a handful of individual
  accounts not on any entered crew, plus a couple of seeded listings and one recruiting party.
- Mailpit UI at `http://localhost:8025` to observe transactional emails.

## Scenario A — Post yourself as a mercenary (US1, FR-004..007)

1. Sign in as an individual **not In any party** for Summer Slam. Open the event page.
2. Confirm the **mercenary market** renders below "Who's taking part" with two sides (free agents /
   parties recruiting) and a position filter.
3. Post a listing: pick positions (e.g. Läufer, Q-Tip), write a pitch, submit.
   - **Expect**: `201`; the listing appears on the free-agents side with your profile name/photo,
     chosen positions, and pitch.
4. Edit the pitch, then take the listing down.
   - **Expect**: board reflects the edit; a taken-down listing is gone (`204`).
5. Sign in as a user who **is In** Rheinfeuer's party and open the event. Attempt to post.
   - **Expect**: post action unavailable with a clear reason; `POST /listing` returns `409`; board
     still browsable.

## Scenario B — Party goes recruiting (US2, FR-008..010)

1. As a **Rheinfeuer party admin**, open Manage party → Recruiting block.
   - **Expect**: recruiting is **off** by default; the party is not on the board.
2. Flip it on, set spots (2), positions needed (Läufer, Schild), a blurb; save.
   - **Expect**: `200`; the party appears on the board's parties side showing open spots
     (`RosterCap − InCount`), needed positions, and blurb.
3. As a **non-admin** party member, `PUT /parties/{id}/recruiting`.
   - **Expect**: `403`.
4. Flip recruiting off.
   - **Expect**: the party leaves the board; any already-received application still lists in the
     recruiting inbox.

## Scenario C — The two-way handshake (US3, FR-011..013)

1. As an **eligible free agent**, apply to Rheinfeuer (`POST /parties/{id}/market/applications`) with
   positions you'd play.
   - **Expect**: `201` pending application; visible in the party's recruiting inbox (applications) and
     in your market view (my applications, status pending). No roster change.
2. As a **Rheinfeuer admin**, invite a different free agent (`POST /parties/{id}/market/invites`).
   - **Expect**: `201` pending invite; the target receives an **in-app notification** and an **email**
     (check Mailpit); it shows in the party's "invites sent" (awaiting) and the target's market inbox.
3. Revoke the invite as the admin; decline the application as the admin.
   - **Expect**: revoked/declined requests drop from both inboxes; no roster change.
4. Attempt a duplicate application to the same party while one is pending.
   - **Expect**: `409` (≤1 active request per pair).

## Scenario D — Accept lands the guest (US4, FR-014..016)

1. Re-apply as the free agent, then as the admin **accept** the application
   (`POST /market/requests/{id}/accept`).
   - **Expect**: `200`; the mercenary now appears in the party's **In** roster with a
     **"guest · via market"** tag; `InCount` increments; readiness/fill reflect it.
2. As a second free agent, accept a pending **invite** (`.../accept`).
   - **Expect**: same guest-seating result from the other direction.
3. Have the joiner check: their listing is **gone** and their **other pending requests** for this event
   are **cancelled**.
4. Fill the party to `RosterCap` (8). Attempt one more accept.
   - **Expect**: `409 Full`; the board card shows full and offers no Apply. Free a spot → the card
     reopens.
5. Apply the party to the event (016) and confirm the **team** is a single entry (the guest does **not**
   create a separate individual signup).

## Scenario E — Reach: inbox, dashboard, notification (US5/US6, FR-017..020)

1. As a mercenary with a pending invite, open the **dashboard**.
   - **Expect**: a **market module** summarizes pending invites/applications with accept/decline.
2. Open the event page's **my market** section.
   - **Expect**: invites to answer + your applications with status render, paginated, with empty states
     when none.
3. As a party admin, **direct-invite** a user with no listing via `GET /parties/{id}/market/user-search`
   then `POST /market/invites`.
   - **Expect**: search returns users by name/@handle; the invite delivers as notification + market
     inbox; an already-In-a-party user is annotated **ineligible** and the invite is refused (`409`).
4. Attempt to read another user's `api/v1/market/mine` (by faking the JWT subject is not possible —
   confirm the endpoint only ever returns the caller's own requests).

## Scenario F — Guest lifecycle & cleanup (US7, FR-021..023)

1. As a party admin, **remove** the guest.
   - **Expect**: `204`; spot frees, board card may reopen; the guest has **no** team membership, badge,
     or history created/changed.
2. Re-seat a guest, then **disband** the party.
   - **Expect**: the party, its guests, its recruiting listing, and all its pending market requests are
     gone; no orphaned board entries; the team/roster/badges untouched.

## Automated checks

- Backend: `dotnet test` — the `Marketplace/*` integration suite covers eligibility, the handshake,
  atomic accept at the cap (concurrent), auto-cancel + take-down on join, guest reconciliation in the
  roster/counts, direct-invite eligibility, inbox/dashboard scoping, notification+email on invite, and
  disband cleanup.
- Frontend: `npx nx test web` — specs for the board, listing editor, apply/invite sheets, recruiting
  block, inboxes, dashboard module, and the `MarketInvite` alert renderer (zoneless; no `fakeAsync`).
- UI review: complete `specs/017-event-marketplace/checklists/ui-review.md` against the diff before
  verification (DESIGN.md wins on conflict).
