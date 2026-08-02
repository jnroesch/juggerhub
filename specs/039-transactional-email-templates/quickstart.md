# Quickstart & Validation: Transactional Email Templates & Notification Preference Gating

**Feature**: 039-transactional-email-templates | **Date**: 2026-08-01

How to run and prove this feature end to end. Contracts live in
[contracts/](./contracts/); the data model is in [data-model.md](./data-model.md).

---

## Prerequisites

- Docker Desktop running (Postgres, Mailpit, and Testcontainers for the integration suite)
- .NET 10 SDK, Node 22 + npm
- A `.env` at the repo root (copy `.env.sample`) — no new variable is added by this feature

## Bring the stack up

```powershell
docker compose up -d
cd backend; dotnet run
cd frontend; npm start
```

Mailpit's web UI is the primary manual-validation surface for this feature — every email
below is captured there rather than sent.

| Surface | URL |
|---|---|
| Mailpit inbox | http://localhost:8025 |
| SPA | http://localhost:4200 |
| Notification settings | http://localhost:4200/settings/notifications |

---

## Automated verification

```powershell
# Backend — integration suite (Testcontainers Postgres, capturing mail sink)
cd backend; dotnet test

# Just this feature's email + notification coverage
cd backend; dotnet test --filter "FullyQualifiedName~Email|FullyQualifiedName~Notification"

# Frontend
cd frontend; npx nx test web
cd frontend; npx nx lint web; npx nx build web
```

### What the new tests must prove

| # | Assertion | Requirement |
|---|---|---|
| 1 | A non-auth email carries the shared header, footer, address block, and footer reason | FR-001, FR-002, FR-024 |
| 2 | The shared footer contains privacy and imprint links built from the configured host | FR-022, FR-023, FR-024 |
| 3 | A team named `<b>Ravens</b>` arrives escaped — no live markup in the body | FR-006, FR-025 |
| 4 | Subject lines are **not** escaped — a team named `Ravens & Co` reads literally | FR-010 |
| 5 | No new email ships an unresolved `{{PLACEHOLDER}}` | FR-026 |
| 6 | The en/de/es variants of each new template carry an identical placeholder set | FR-026a |
| 7 | Disabling Email for a category suppresses that email | FR-012, FR-027 |
| 8 | The in-app notification still arrives when Email is disabled | FR-016, FR-027 |
| 9 | A user with no stored preference still receives the email | FR-014 |
| 10 | `NotificationCategories.For(EventCancelled)` returns `Events`, not the `TeamNews` default | data-model.md |
| 11 | Cancelling an event creates one notification per recipient, de-duplicated by user | FR-017 |
| 12 | The party-request **nudge** path is gated, not only the initial fan-out | NC-3, plan Phase C |

---

## Manual validation

### Scenario A — Shared chrome and language (User Story 1)

1. Register two accounts; set one to German at `/settings/language`.
2. Add both to a team, create an event, and open a party for it.
3. In Mailpit, open the party-request email for each account.

**Expect**: both carry the JuggerHub header, footer, address block, and a footer reason. The
German account's subject and chrome are German. Neither ends in a bare `— JuggerHub`.

4. Repeat for party news, market invite, and event cancellation — 4 emails × 3 languages is
   the full matrix behind SC-002.

### Scenario B — Escaping (User Story 1, scenario 4)

1. Rename a team to `<b>Ravens</b>` and trigger any of the four emails.

**Expect**: the name renders literally in the body, not bold, and contains no live link. In the
**subject**, it reads as typed — not as `&lt;b&gt;`.

### Scenario C — Preference gating (User Story 2)

1. At `/settings/notifications`, turn **off** Email for "Invites & roster changes".
2. Have a party invite that account via the marketplace.

**Expect**: no email in Mailpit; the in-app notification still appears in the alerts inbox.

3. Turn Email back on and repeat.

**Expect**: the email arrives.

### Scenario D — Cancellation as a notification (User Story 3)

1. Sign up individually for one event; sign a team up for another.
2. Cancel both as the organiser.

**Expect**: each participant and each admin of the signed-up team gets one cancellation
notification linking to the event, plus one email. A user who is both an individual participant
and a team admin gets exactly one of each.

3. Open `/settings/notifications`.

**Expect**: an "Events" row with working In-app and Email toggles, labelled in the active
language.

### Scenario E — Legal links (User Story 4)

1. Open any captured email's footer and follow both new links.

**Expect**: `/privacy` and `/imprint` load without signing in, on the same host as the other
links in the message.

### Scenario F — Failure behaviour

1. Stop Mailpit (`docker compose stop mailpit`) and cancel an event.

**Expect**: the cancellation still succeeds, participants still see the in-app notification, and
the send failure is logged rather than surfaced. Restart Mailpit afterwards.

---

## UI review (Constitution Gate 7)

The notification row gains a type and the preferences screen gains a category row, so a UI
review is required before verification is considered complete:

```powershell
Copy-Item .specify/templates/ui-review-checklist-template.md `
          specs/039-transactional-email-templates/checklists/ui-review.md
```

Verify each item against the diff. DESIGN.md wins on any conflict. Both surfaces reuse existing
components and tokens, so the review should confirm *absence* of new visual patterns rather
than approve new ones.

---

## Rollback

No migration runs, so rollback is a code revert with no data step. Reverting restores the four
inline-HTML emails and un-gates them; any `NotificationPreference` rows users created for the
`Events` category become inert but harmless, and would apply again on re-deploy.
