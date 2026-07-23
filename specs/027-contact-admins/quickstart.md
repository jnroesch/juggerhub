# Quickstart: Contact the Admins (027)

End-to-end validation for the "Contact admins" feature. Run against the local docker-compose stack
(constitution Principle V) — the same way feature 019 was verified (drive the real stack, not just
tests).

## Prerequisites

- `docker compose up` (API, Angular, PostgreSQL, Mailpit, Redis) healthy.
- The EF migration for feature 027 applied (columns `EventId`, `RequesterUserId`; re-scoped +
  new indexes; FKs).
- Seed/fixture accounts:
  - **P** — a signed-in player who is *not* an admin of the target.
  - **A1**, **A2** — two admins of team **T** and (separately) event **E**.
  - **A3** — a player who will be *granted* admin mid-scenario.

## Scenario 1 — Player contacts a team's admins (US1, US2)

1. As **P**, open team **T**'s page. Expect a **"Contact admins"** action (visible because P is not a
   T admin — FR-001).
2. Click it, type "When's the next training?", send.
3. **Expect**: the thread now exists (created on first send — FR-005); P lands in the conversation.
4. As **A1** and **A2**, open Chat. **Expect** a new inbox row tagged **ADMINS**, named **"P's
   display name"** (FR-010), with the message. Reply as **A1**.
5. Back as **P**, open Chat. **Expect** the same thread tagged **ADMINS**, named **"Team T"** (FR-009),
   showing A1's reply attributed to **A1 by name** (FR-020).

**Pass**: message delivered to both current admins; both sides see the distinguishing tag; each side's
name is target-appropriate; admin reply carries the admin's own name.

## Scenario 2 — Thread reuse (US1 / FR-004)

1. As **P**, return to team **T**'s page, click **Contact admins** again.
2. **Expect**: P is taken back into the *same* thread with full history — no second thread; no
   duplicate row appears in any admin's inbox.

## Scenario 3 — Event inquiry (US1 on events)

1. Repeat Scenario 1 against event **E** and its admins. **Expect**: identical behaviour; the
   requester sees **E's title**; admins see **P's name**; tag **ADMINS**.

## Scenario 4 — Membership follows the admin roster (US3 / FR-006, FR-019)

1. With P↔T thread active, **grant A3 admin** of team T.
2. As **A3**, open Chat. **Expect** the inquiry thread is now visible, showing history from the grant
   forward (FR-019) — earlier messages are not shown.
3. **Remove A2** as admin of T. As **A2**, refresh Chat / open the thread by id. **Expect** the thread
   is gone from the inbox and the direct open returns **404** (not 403) — FR-006/FR-012.

## Scenario 5 — Not offered to admins (FR-002)

1. As **A1** (a T admin), open team **T**'s page. **Expect**: no **Contact admins** action.
2. Attempt the API call `POST /chat/contact/team/{T}/messages` as A1 directly. **Expect** a rejection
   (`409/400`, "you're already an admin"), no thread created.

## Scenario 6 — Archival on team delete / event cancel (FR-014, FR-015)

1. **Delete team T** (as a T admin). **Expect**: P's T-inquiry thread (and the team chat) become
   **Archived** — P and the former admins can still read history; sending returns "This chat is
   closed."; no thread becomes unreadable.
2. **Cancel event E**. **Expect**: P's E-inquiry thread archives with the same read-only behaviour;
   the event page no longer offers **Contact admins**.

## Scenario 7 — Rate limiting & no alerts (FR-013, FR-018)

1. As **P**, rapidly start inquiries to many targets. **Expect**: the `ChatStart` limit throttles
   (HTTP 429) as it does for DMs.
2. Confirm no rows appear in the Alerts/notifications surface for inquiry messages — only the chat
   unread badge moves (`ChatDoesNotTouchAlertsTests` stays green).

## Automated coverage (where to look)

- `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatInquiryTests.cs` — create-on-send, reuse,
  admin-derived membership, grant/revoke visibility, 404-not-403, not-offered-to-admins, cutoff.
- `ChatArchiveTests` — extend for team-delete-archives-inquiries and event-cancel-archives-inquiries.
- Frontend specs — inbox tag renders "ADMINS" for the two kinds; entry-point button hidden for admins;
  compose calls the contact endpoint.
- UI review checklist: `specs/027-contact-admins/checklists/ui-review.md` (instantiate from template),
  DESIGN.md wins on any conflict.
