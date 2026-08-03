# Quickstart: validating the Terms of Use feature

Runnable scenarios that prove the feature works end to end. Scenarios 1–7 are automated;
8–10 are manual because they check rendered prose and reading experience.

## Prerequisites

```powershell
# Full stack, local
docker compose up -d

# Backend integration tests (Testcontainers — Docker must be running)
dotnet test backend/tests/JuggerHub.Api.IntegrationTests

# Frontend unit tests
npm --prefix frontend test
```

---

## Scenario 1 — Registration is refused without acceptance *(FR-018, SC-002)*

**The central security property.** Exercised against the endpoint directly, never through the
form — the disabled button is not the boundary.

```powershell
# acceptsTerms omitted entirely
curl -X POST http://localhost:8080/auth/register -H "Content-Type: application/json" -d '{
  "email":"nope@example.com","password":"Correct-Horse-9!","handle":"nope-one"
}'
```

**Expect** `400` · title `Terms not accepted`. Then confirm **nothing** was created — no account,
no profile, no acceptance row (FR-019). Repeat with `"acceptsTerms": false` — same result.

---

## Scenario 2 — Registration with acceptance records the evidence *(FR-020, SC-001)*

```powershell
curl -X POST http://localhost:8080/auth/register -H "Content-Type: application/json" -d '{
  "email":"yes@example.com","password":"Correct-Horse-9!","handle":"yes-one",
  "acceptsTerms":true,"termsVersion":"2026-08-03","termsLanguage":"de"
}'
```

**Expect** `200` neutral response, and exactly **one** `TermsAcceptances` row for the new account
carrying the version, a `CreatedDate` at the moment of registration, and `DisplayLanguage = "de"`.

---

## Scenario 3 — A stale version is refused *(research R1)*

Same request with `"termsVersion":"2020-01-01"`.

**Expect** `409` · title `Terms have changed`, no account created. This is the rolling-deploy and
stale-cache path; the message must tell the reader to reload, not fail generically.

Also send an unsupported `"termsLanguage":"fr"` → **expect** `400`, no account created. An
unvalidated language would write arbitrary client text into an evidence row.

---

## Scenario 4 — A failed registration leaves no acceptance record *(FR-022)*

Register successfully, then register **again with the same handle** and valid acceptance.

**Expect** the second attempt is refused for the handle, and the `TermsAcceptances` count is
**unchanged**. Proves the row is bound to the account's own `SaveChanges` rather than written
ahead of it.

---

## Scenario 5 — The record survives suspension and ban *(FR-023)*

Suspend, then ban the account from scenario 2 via the existing admin endpoints.

**Expect** the acceptance row is present and unchanged after each transition — same version, same
timestamp. A ban is a retained soft-delete; the evidence of what the banned account agreed to is
the reason to keep it.

---

## Scenario 6 — The record survives self-erasure *(FR-024)* ⚠

**The one most likely to regress.** Erase the account from scenario 2 through the feature-037
self-service flow.

**Expect**:

- the `TermsAcceptances` row **still exists**, with its original version and timestamp
- the `User` row it points at has `Status = Deleted` and neutralised identifying columns — so the
  record evidences an agreement while identifying nobody
- re-registering with the released email creates a **new** account and a **second** acceptance
  row; the original is not reused, rewritten, or deleted

This is the test that fails if someone adds `TermsAcceptances` to
`AccountDeletionService.EraseOwnedDataAsync`. See [data-model.md](./data-model.md).

---

## Scenario 7 — Release guards *(FR-026, FR-027, SC-004, SC-005)*

```powershell
npm --prefix frontend test -- legal-catalog          # G1 key sets · G2 __TODO__ sentinel
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter Terms   # G3 version parity
```

Then verify each guard actually bites:

| Break this | Expect |
|---|---|
| Delete one paragraph from `terms` in `de.json` | G1 fails. **Fix by adding the German text — never by changing the global fallback** |
| Put `__TODO__` in any terms value | G2 fails |
| Change `terms.version` in `en.json` only | G3 fails on cross-language parity |
| Change `TermsOptions.CurrentVersion` alone | G3 fails on catalogue-vs-server parity |

A guard that passes because it never ran is the failure mode here — confirm each one fails when
it should.

---

## Scenario 8 — Reading the document *(FR-001, FR-002, FR-003, FR-011, FR-012)* — manual

Signed out, open `http://localhost:4200/terms`:

- [ ] Renders in full with **no** sign-in wall and **no** redirect
- [ ] No backend call in the network tab — static asset only
- [ ] Version **and** last-updated both visible in the meta line
- [ ] Table of contents present; clicking "How to behave here" anchors correctly and the URL stays
      on `/terms` (a bare `#id` resolves against `<base href="/">` and would bounce to the guarded
      dashboard — the reason the existing component uses `routerLink` + `fragment`)
- [ ] Switch to `en` and `es`: authoritative notice appears. Switch to `de`: it does not
- [ ] Block `/i18n/legal/*.json` in devtools and reload: visible error + working retry, never a
      blank or half-rendered document
- [ ] `Terms · Privacy · Imprint` in the footer; each document links to the other two

---

## Scenario 9 — The acceptance control *(FR-015, FR-016, FR-017)* — manual

On `/register`:

- [ ] The checkbox is **unticked** on first render, in all three languages, after a reload, and
      after a browser back-navigation. Never pre-ticked
- [ ] Submit is disabled until it is ticked, and the reason is stated in plain language
- [ ] The link in the label opens the full terms; **returning does not clear** the email, handle,
      or password fields
- [ ] Block the legal catalogue fetch: the control is disabled and submission is blocked — a
      member cannot agree to a document the app could not load
- [ ] With the terms open in one tab, change `TermsOptions.CurrentVersion` and restart the
      backend, then submit the stale tab: a specific "the terms have been updated" message, not
      the generic error string

---

## Scenario 10 — Consistency between the documents *(FR-006, FR-008, FR-009, SC-007)* — manual

Read `/terms` beside `/privacy` and `/imprint` and confirm no contradiction:

- [ ] **Content ownership** — the terms grant a *display permission only*. The privacy policy says
      "What you write and upload is yours until you say otherwise"; a broad content licence would
      contradict it
- [ ] **Deletion** — the terms describe erasure as self-service and immediate, with messages and
      news posts surviving as "A former player" and the email released. Matches the privacy
      policy's `rights` section and feature 037's actual behaviour
- [ ] **Admin records** — the terms' account-action wording matches the privacy policy's retention
      statement about suspend/ban/reinstate records
- [ ] **No invented tooling** — no review timeline, no appeal procedure, no report button, no
      moderation team. None exist (FR-008)
- [ ] **Contact** — `hello@juggerhub.com`, the address already published in both other documents
- [ ] **Age** — the guardian clause is present and there is **no** age field, age confirmation, or
      age gate anywhere in registration (FR-013)
- [ ] **Changes** — publish-only wording; no promise of notification or re-acceptance (FR-014)

---

## Scenario 11 — UI review *(constitution gate 7)* — manual

Work through [`checklists/ui-review.md`](./checklists/ui-review.md) against the diff. DESIGN.md
wins on any conflict.
