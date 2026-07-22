# Quickstart & Validation: Shared UI primitives layer

How to build, run, and prove this feature works. Implementation detail (class
strings, full specs) belongs in `tasks.md` / the code, not here.

## Prerequisites

- Node + npm installed; frontend deps installed: `npm install` in `frontend/`.
- Commands run from `frontend/`.

## Build & run

```powershell
# Dev server
npx nx serve web

# Unit/component specs (Jest, zoneless — no fakeAsync)
npx nx test web --watch=false

# E2E (Playwright) — the behavior-preservation guardrail
npx nx e2e web-e2e

# Production build must stay green (Tailwind purge picks up new shared/ui classes)
npx nx build web
```

## Validation scenarios

### V1 — Primitives exist and encode DESIGN.md (Phase P-A)

- **Given** the new `app/shared/ui/` primitives, **when** their Jest specs run,
  **then** each asserts its contract:
  - `jhButton size="md"` → host has `min-h-11` (≥44px); `variant="primary"` →
    coral bg + `text-on-accent` + hover-glow + press class; focus class present.
  - `jh-alert` → `role="alert"` on host and `danger-fg` for default tone.
  - `jh-loading` → single `text-body-sm text-muted` line; default label `Loading…`.
  - `jh-empty-state` → renders projected message + `[action]` slot.
  - `jh-page-container width="lg"` → `max-w-container-lg` + centered.
  - `jh-icon name="plus"` → inline SVG, `aria-hidden="true"`.
- **Expected**: `nx test web` green; no screen changed yet.

### V2 — A migrated screen is behavior-identical (each of P-B…P-I)

- **Given** a migrated feature area, **when** `nx test web` and `nx e2e web-e2e`
  run, **then** all pre-existing specs/e2e for that area pass unchanged.
- **Manual smoke**: for a representative screen (e.g. sign-in), confirm the same
  actions, labels, `data-testid`s, routes, and disabled/busy behavior as before —
  only the look changed.
- **Keyboard**: Tab through the screen; every action shows a visible coral focus
  ring; every primary button is ≥44px tall.

### V3 — App-wide consistency (final, P-I)

Run the drift guard (research R10) — a grep/lint step asserting **zero**
occurrences outside `app/shared/ui/` of the retired patterns:

```powershell
# Each of these should return no matches in src/app/features and src/app/layout:
# - raw `text-white` on brand/action elements
# - bare `text-danger ` error text (use jh-alert / danger-fg)
# - `rounded-pill` on a brand action button
# - a literal "+ " used as a button/link icon
# - `text-subtle` / `text-faint` used for loading/empty status text
# - the word "invitation" in user-facing copy
```

- **Expected**: no matches → SC-001…SC-007 satisfied; the `ui-review.md` checklist
  passes for every phase; `nx test web`, `nx e2e web-e2e`, and `nx build web` all green.

### V4 — New-screen ergonomics (SC-009)

- **Given** a throwaway demo route composed only from the primitives, **when** it
  renders a button, card, empty, loading, and alert, **then** no bespoke raw-utility
  markup was required. (Delete the demo route before merge, or keep behind a dev-only guard.)

## Done signals

- All `SC-001…SC-009` verifiable via V1–V4.
- `checklists/ui-review.md` fully checked across phases.
- No regressions: full Jest + Playwright suites green; production build green.
