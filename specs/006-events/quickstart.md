# Phase 1 — Quickstart & Validation: Events

Runnable end-to-end checks that prove feature 006. Implementation detail (entity/service/migration bodies) lives in [data-model.md](./data-model.md) and `tasks.md`; this is a **validation/run guide**. All commands run against the containerized stack.

## Prerequisites

- Docker + docker-compose. Bring the stack up from the repo root:
  ```powershell
  docker compose up --build
  ```
- Backend applies the `AddEvents` migration on startup and `DevDataSeeder` seeds demo events (Dev/local only).
- Frontend on its dev port; API under `/api/v1`. Mailpit UI for emails (cancellation + co-admin invites).
- Two seeded accounts (an organiser **O** and a participant **P**), and a team **T** that O administers (from feature 005 seed) for teams-only checks.

## Contract & data references

- HTTP surface: [contracts/openapi.yaml](./contracts/openapi.yaml) + [contracts/README.md](./contracts/README.md) (authorization map + invariants).
- Entities/enums/indexes/migration: [data-model.md](./data-model.md).

## Scenario A — Create via the wizard (US1, FR-001…007)

1. As **O**, open **Create event**, walk the wizard: type **Tournament** + name; dates (multi-day, end ≥ start); **In person** with a full address **incl. country**; **Teams only**, limit **2** (small, to exercise capacity fast); **Paid** with recipient + IBAN + a deadline; review → **Publish**.
2. **Expect**: redirected to `/events/{id}`; O is the sole admin (manage toolkit visible). ✅ FR-007.
3. Negative (API or wizard): blank name, end before start, In-person without country, Virtual without link, limit 0, Paid without IBAN → each refused. ✅ FR-002…006.
4. Create a second event: **Workshop**, **Virtual** (link), **Individuals only**, limit **2**, **Free**. Used by later scenarios.

## Scenario B — Public page, anonymous (US2, FR-008…011)

1. Log out. Open `/events/{id}` for both events.
2. **Expect**: name/type/description, start/end, address (event 1) or link (event 2), fee block incl. recipient/IBAN (event 1) marked info-only, latest news, the three participant groups, and contacts — all visible without signing in. ✅ FR-008.
3. Empty event shows friendly empty states for news/participants/contacts (not errors). ✅ FR-011.

## Scenario C — Sign-up routing & capacity (US3, FR-012…018; SC-004)

1. **Free individuals-only** (event 2), spots open: as **P**, join → lands in **Joined**. ✅ FR-014.
2. **Paid teams-only** (event 1): as **O**, enter team **T** → **AwaitingApproval** (holds a spot); page shows pay-to recipient/IBAN/deadline. ✅ FR-014.
3. Mismatch: as P, try to join event 1 as an individual → refused; try to enter a team you don't administer → refused. ✅ FR-012/013.
4. Fill event 1 (limit 2): a second team occupies the last spot (joined/awaiting). A third team sign-up → **Waitlisted**, not asked to pay. ✅ FR-015.
5. **Capacity race** (integration test): two sign-ups for the final spot concurrently → exactly one occupies it, the other is **Waitlisted**; occupied never exceeds the limit. ✅ FR-016, SC-004.
6. Duplicate: same team/user signs up twice → refused. ✅ FR-018.
7. Withdraw: P withdraws; a team admin withdraws T → spot released, **no one auto-promoted**. ✅ FR-017.

## Scenario D — Participant administration (US4, FR-019…022; SC-005/006)

1. As **O** on event 1, **approve** an AwaitingApproval team → moves to **Joined**, keeps its spot, `PaymentConfirmedDate` set. ✅ FR-019.
2. **Promote** a waitlisted team when a spot is open → free would go Joined; paid goes **AwaitingApproval**. With no open spot, promote → **409**. ✅ FR-020.
3. **Remove** a joined and a waitlisted entry → each leaves, spot released, nobody auto-promoted. ✅ FR-021.
4. As **P** (non-admin), attempt approve/promote/remove via API → **403**. ✅ FR-022, SC-002.

## Scenario E — News & contacts (US5/US6, FR-023…026)

1. As O, **post news** → appears newest-first for everyone (log out to confirm public). Non-admin post via API → 403. Empty feed → friendly empty state. ✅ FR-023/024.
2. As O, **add a contact** (name + role + email) → shows on the public page. Add with neither phone nor email → refused. **Update** phone, **remove** the contact → reflected. Non-admin CUD → 403. ✅ FR-025/026.

## Scenario F — Co-admins (US7, FR-027…029)

1. As O on **Admins**, copy the invite **link**; also **search** user P and send a targeted invite (Mailpit shows the email). ✅ FR-028.
2. As **P**, open `/event-invite/{token}` → anonymous preview (event name + inviter + state); **accept** (after signing in) → P can now edit, post news, and manage participants/contacts. ✅ FR-028.
3. Revoke a pending invite → no longer acceptable. Non-admin invite/revoke → 403. ✅ FR-027.
4. **Last-admin guard**: with O the only admin, O stepping down / being removed → **409**; after P is a co-admin, either can step down leaving ≥1 admin. ✅ FR-029, SC-009.

## Scenario G — Edit guards (FR-030)

1. As O, raise the limit → allowed. Lower it below current occupied → refused. ✅ FR-030.
2. Change participantMode after sign-ups exist → refused; other fields (name/description/dates/location/fee/type) edit fine. ✅ FR-030.

## Scenario H — Cancel (US8, FR-031/032; SC-007)

1. As O, open the **danger zone**, cancel with confirmation. ✅ FR-031.
2. **Expect**: page stays up marked **cancelled by the organiser**, retaining details/news/contacts; sign-up / waitlist / approve / promote all refused (**409**); Mailpit shows a cancellation email to each joined/awaiting/waiting recipient (individual users + team admins). No reactivate path. ✅ FR-032, SC-007.

## Automated coverage

- **Backend** (`tests/JuggerHub.Api.IntegrationTests/Events/`): Scenarios A–H as xUnit cases, including the capacity race and the last-admin race (both concurrent), pagination on every list, and all non-admin/anonymous refusals.
- **Frontend**: Jest unit for `EventService`, wizard step validators, and capacity/role gating; Playwright `events.spec.ts` runs the create → view → signup (2nd user) → approve/promote/remove → news/contacts → invite co-admin → cancel flow on **desktop and mobile** viewports, checking empty/loading/error states per DESIGN.md.

## Definition of done (feature-level)

All spec Success Criteria (SC-001…009) demonstrated by the scenarios above with their automated equivalents green; migration applies cleanly on a fresh DB; no raw exceptions/secrets reach the client; UI responsive with friendly empty/loading/error states.
