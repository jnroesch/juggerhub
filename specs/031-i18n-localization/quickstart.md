# Quickstart / Validation Guide: i18n

End-to-end scenarios that prove the feature works. Run against the local stack (`docker-compose up`). Assumes at least the `auth`, `settings`, and `notifications` scopes have been extracted, plus one email localized (e.g. verification), so the slice is demonstrable.

## Prerequisites

- Local stack running; a seeded user you can sign in as.
- Browser dev tools available (to set `navigator.language` / inspect `Accept-Language`).
- A way to view outbound email in local (the dev sender / Resend sandbox / logged HTML).

## Scenario 1 — Auto-detect from browser (US1)

1. Set the browser (or a fresh profile) language to **German**; open the app signed out.
2. **Expect**: UI chrome, auth screens, and any extracted feature render in German; `<html lang="de">`; dates/numbers in German format.
3. Repeat with **Spanish** → Spanish; with **French** (unsupported) → **English** (FR-002); with **`de-AT`** → German (base-match, FR-003).

## Scenario 2 — Manual override + anonymous persistence (US2)

1. Signed out, browser = English. Use the language control (reachable signed-out — clarification Q2) to pick **German**.
2. **Expect**: UI switches to German **immediately, no full reload** (SC-003); `localStorage` holds the choice.
3. Reload the tab → still German (anonymous persistence, FR-006).

## Scenario 3 — Signed-in preference across devices (US2)

1. Sign in; set language to **Spanish** via the switcher/settings.
2. **Expect**: `PUT /api/v{n}/account/language` succeeds; you remain signed in (FR-015); `GET /auth/me` now returns `preferredLanguage: "es"`.
3. Sign in from a different browser/profile (even with a German browser) → app is **Spanish** (account preference wins, FR-005/FR-007).

## Scenario 4 — Missing-translation fallback (FR-008)

1. Temporarily remove a key from a `de` catalog (or point at a key with no `de` value).
2. In German, that one string renders the **English** text — never blank or a raw key.

## Scenario 5 — Pre-account email in the caller's effective language (FR-012a)

1. Signed out, override language to **German** (browser can be English).
2. Register a new account (or trigger password reset / resend).
3. Inspect the outbound request → `Accept-Language: de`. Inspect the email → **German** subject and body.
4. Repeat with no override on a French browser → email is **English** (fallback).

## Scenario 6 — Recipient-addressed content uses the recipient's language (FR-012 / research D9)

1. User A prefers **English**; User B prefers **German**.
2. As A, trigger content addressed to B (e.g. a team-news email / a notification mirror to B).
3. **Expect**: B's email is **German** (recipient's stored preference), even though the actor (A) is English. In-app notification for B also renders in German.

## Scenario 7 — Admin area is localized too (clarification Q1)

1. As an admin, switch to German and open the admin area (catalogue, users, teams, overview).
2. **Expect**: admin screens render in German — no English-only region.

## Scenario 8 — Long-German layout (SC-006, gate 7)

1. In German, walk the key screens (nav, buttons, chips/badges, table headers, forms).
2. **Expect**: no truncation, overlap, or overflow. Record results in `checklists/ui-review.md`.

## Automated checks

- **FE unit**: `LanguageService` precedence + base-matching; missing-key → English; switch updates rendered text (Transloco testing harness; zoneless — no `fakeAsync`).
- **BE unit** (xUnit): culture base-matching + allowlist rejection + `en` fallback; recipient-vs-request culture selection (D9); email language selection.
- **e2e** (Playwright): Scenarios 1–3 + a German long-string smoke.

## References

- Data shape: [data-model.md](./data-model.md)
- API deltas: [contracts/language-preference.md](./contracts/language-preference.md), [contracts/request-language-propagation.md](./contracts/request-language-propagation.md)
- Decisions/rationale: [research.md](./research.md)
