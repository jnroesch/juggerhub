# Quickstart: Privacy Policy & Imprint (036)

How to run and validate this feature. Frontend-only — the backend, database, and migrations are
untouched, so no `dotnet ef` step and no reseed are involved.

---

## Prerequisites

```powershell
cd frontend
npm ci
```

The app can be served on its own for everything in §1–§4; only §5 (the analytics opt-out check)
needs the full compose stack.

---

## 1. Run it

```powershell
cd frontend
npx nx serve web
```

Then open, **with no session** (a private window, or clear cookies first):

- `http://localhost:4200/privacy`
- `http://localhost:4200/imprint`

**Expected**: both render in full. No redirect to `/sign-in`, no `returnUrl` in the URL, no
sign-in prompt.

---

## 2. Validate the reachability guarantee (FR-002, SC-001, SC-008)

| Check | Steps | Expected |
|---|---|---|
| Signed out, in the shell | Open a public profile `/u/<handle>`, scroll to the bottom | Footer with privacy + imprint links. **1 click** to either |
| Signed out, off the shell | Open `/register` | Legal links at the bottom of the card. **1 click**. This is the most important placement — it is where an email address is handed over |
| Signed in, desktop | Sign in, land on the dashboard, scroll down | Footer present. **1 click** |
| Signed in, **mobile** | Same at a 375px viewport | Footer sits **above** the fixed bottom bar, fully visible, not occluded |
| Deep link | Paste `/privacy` into a fresh private window | Renders directly (FR-003) |
| No auth traffic | DevTools → Network, filter `api`, load `/privacy` | **Zero** `/api/**` requests (RC-2, SC-008) |

---

## 3. Validate language handling (FR-019, SC-009)

Use the language switcher on the page itself.

| Language | Expected |
|---|---|
| Deutsch | Full German text. **No** authoritative-language notice — this *is* the authoritative version |
| English | Full English text **plus** a visible notice that the German version governs |
| Español | Full Spanish text **plus** the same notice, in Spanish |

Switching must update in place — no reload, no navigating away (031 FR-004, inherited).

**Watch for**: an English paragraph appearing inside the German document. That is the Transloco
fallback masking a missing `de` key (contracts/content-catalog.md §4). It is caught by the catalog
test in §4 below, but if you see it by eye, the test was skipped.

---

## 4. Run the automated checks

```powershell
cd frontend

# Unit + catalog completeness + placeholder guard
npx nx test web --watch=false

# Lint
npx nx run-many -t lint

# Production build (proves the legal scope stays OUT of the initial bundle)
npx nx build web --configuration=production

# End-to-end
npx nx e2e web-e2e
```

**Two failures are expected and meaningful, not broken:**

| Failure | Meaning |
|---|---|
| Placeholder guard fails on `__TODO__` | Spec **Q1 is unanswered** — the operator's imprint particulars are still placeholders. This is the guard working. It goes green when the real particulars are supplied, with no code change (contracts/content-catalog.md §5) |
| Catalog completeness fails | A key exists in one language and not another. Fix the catalog; do not relax the test — it is what stops English text appearing inside the legally authoritative German document |

**Bundle check**: after the production build, confirm the legal catalogs are served from
`public/i18n/legal/` as separate files and are *not* inlined into the initial chunk. Loading any
non-legal route must fetch **no** `legal/*.json`.

---

## 5. Validate the analytics opt-out (FR-013, SC-004)

This one needs the real stack, because it is a claim the policy makes about deployed behaviour and
research R5 requires re-verifying it rather than citing 033.

```powershell
docker compose up -d          # with analytics enabled
```

1. Enable Do Not Track (or Global Privacy Control) in the browser.
2. Browse several pages, including `/privacy` itself.
3. Open the Umami dashboard and confirm **zero** events were recorded for that session.
4. Disable DNT, browse again, confirm events **do** appear — so step 3 proves suppression rather
   than a broken pipeline.

**Not automated**: Playwright cannot set the DNT signal reliably across engines. This is a manual
step, stated plainly rather than quietly dropped.

---

## 6. Content review (SC-002, SC-003, SC-005, SC-006)

Not testable — a review pass against the table in
[contracts/content-catalog.md §6](./contracts/content-catalog.md). Check each claim the policy makes
against its source of truth, in particular:

- The data-category list against `backend/Entities/` — the audit SC-002 describes.
- That session records retaining an originating IP (`RefreshToken.CreatedByIp`) is disclosed.
- That chat being **snapshotted rather than deleted** on team delete / event cancel is disclosed.
- That every right in the policy names a route that actually exists — there is **no self-service
  export or deletion**, so the route is the manual contact one (FR-009).
- That the retention section states the honest position: no automated retention or deletion runs
  today.

---

## 7. UI review (Constitution Gate 7)

```powershell
Copy-Item .specify/templates/ui-review-checklist-template.md `
          specs/036-privacy-policy-imprint/checklists/ui-review.md
```

Work through it against the diff. Specific to this feature:

- The new DESIGN.md **Long-form content** section is an *addition*, built entirely from existing
  tokens — flag any new colour, width, or type step as a deviation.
- Measure is `container-sm` (640px), not `container-md`.
- In-prose links are underlined; navigation links elsewhere are not. Intentional (research R7).
- No horizontal scroll at 320px; heading levels never skip.

**Do not re-open** the known DESIGN.md contrast conflict (≥4.5:1 versus white-on-`coral-4` primary
buttons). It is an open app-wide issue and this feature ships no primary button.
