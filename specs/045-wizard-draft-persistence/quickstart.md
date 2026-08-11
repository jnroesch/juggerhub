# Quickstart: proving wizard drafts survive

How to validate this feature. The interesting scenario is #3 — the one the tester reported — and it is the one most likely to be skipped because it looks hard to reproduce. It is not.

## Prerequisites

- The stack running locally (`docker compose up`), or at minimum the frontend dev server against a running backend.
- A signed-in account that is an **admin of at least one team** (the create-training wizard is admin-only; the API is the real guard).

## Automated checks

```powershell
cd frontend
npm test          # nx test web --watch=false
npm run lint
npm run build
```

Three groups must be green:

| Suite | Proves |
|---|---|
| `core/drafts/wizard-draft.store.spec.ts` | Round-trip, the compatibility rule (R5) — bad JSON, wrong `v`, non-object — and that every operation survives storage throwing (FR-015). |
| `features/**/training-create.component.spec.ts`, `event-create.component.spec.ts` | Restore of **every** field (SC-003), the step, clearing on create/cancel/sign-out, and the empty-draft rule. |
| `core/i18n/legal-catalog.spec.ts` | Pre-existing. The three legal catalogues still have identical key sets after the privacy-policy edit (FR-021). Editing `en.json` alone turns this red — that is the guard working, not a broken test. |

**Field coverage is the one thing not to eyeball.** SC-003 is 16 training answers and 21 event answers restored individually. A test that fills three fields and asserts three fields passes while the city chip is empty.

## Manual scenarios

### 1 — In-app navigation (US1 scenario 1)

1. Go to a team → **New training**.
2. Fill step 1 (name), step 2 (schedule), step 3 (address **including picking a city**, and a description).
3. Navigate away — tap the team in the nav, or press browser back.
4. Return to **New training** for the same team.

**Expect**: the wizard opens on step 3 with every answer present, **and the city chip shows the city you picked**. An empty chip beside a filled street is the R3 failure — the most likely way this ships half-done.

### 2 — Reload (US1 scenario 2)

Same setup, then press F5. Same expectation.

### 3 — The reported case: a discarded mobile tab

Chrome reproduces this on the desktop without any device:

1. Fill the wizard through step 3 as above.
2. Open `chrome://discards` in a second tab.
3. Find the wizard's tab in the list and click **Urgent Discard** — this is the same mechanism a phone uses under memory pressure.
4. Switch back to the wizard's tab. Chrome re-creates the page from scratch.

**Expect**: still signed in (the auth cookie is unaffected — that is why the tester came back to a blank wizard rather than a sign-in screen), URL still `…/trainings/new`, and **every answer restored on step 3**. Before this feature, this is where the wizard reappears blank at step 1.

On a real device: fill the wizard, switch apps, use the phone for a minute or two, return to the browser.

### 4 — Cleared after creating (US1 scenario 3)

Complete the wizard and create the training. Then open **New training** for the same team again.

**Expect**: a blank step 1. Nothing carried over.

### 5 — Cancel clears (US1 scenario 4)

Fill two steps, press **Cancel** on step 1, reopen the wizard.

**Expect**: blank step 1.

### 6 — A failed create keeps the draft (US1 scenario 6)

Fill the wizard so the server will reject it (e.g. an end date before the start date), press Create, and when the error appears, reload the page.

**Expect**: the answers are still there. Clearing on the click rather than on the server's acceptance would have thrown them away at the worst possible moment.

### 7 — Per team (US1 scenario 5)

Start a draft for team A, then open **New training** for team B.

**Expect**: team B is blank; returning to team A still has its draft.

### 8 — The event wizard (US2)

Repeat scenarios 1–4 at `/events/new`, filling through the **fee** step with a recipient name and IBAN.

**Expect**: everything restored, fee details included, on the step you left, with the city chip populated. Note the accepted consequence: abandon this draft without publishing and it returns next time you open the event wizard in the same tab (spec: Decision on restore surfacing).

### 9 — Sign-out clears (SC-005)

Fill either wizard, sign out, sign in again, reopen the wizard.

**Expect**: blank. This is what stops a shared device handing over an IBAN.

### 10 — Closing the tab clears (FR-010)

Fill a wizard, **close the tab**, open a new one, navigate back to the wizard.

**Expect**: blank. This is the deliberate boundary of the feature and the reason `sessionStorage` was chosen.

### 11 — Storage unavailable (SC-007)

In Chrome DevTools → Application → Storage, or in a browser with site data blocked, disable storage; alternatively run the wizard in a context where `sessionStorage` throws.

**Expect**: both wizards work end to end. No error, no blocked button, no console noise the user could see. The only difference is that leaving the page loses the answers, as today.

### 12 — Nothing crosses the network (SC-006)

With DevTools → Network open, fill both wizards completely without submitting.

**Expect**: no request carries any draft content. The only traffic is the city picker's own lookups, which are pre-existing.

### 13 — The privacy policy (US3)

Visit `/privacy` in German, English and Spanish.

**Expect**: the section on what is kept in the browser names the unfinished create-form draft, says it stays on the device, and says when it goes. The no-cookie-banner section no longer says nothing at all is stored, and the reasoning it gives instead still holds. German is authoritative; en/es match it in substance.
