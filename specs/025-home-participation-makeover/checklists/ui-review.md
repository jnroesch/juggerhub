# UI Review Checklist: Home Participation Makeover

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-07-22
**Feature**: [spec.md](../spec.md)

**How to use**: Implementation-quality gate, run **after** UI is built and **before** verification. Check each item against the diff, recording `file:line` for anything that fails. DESIGN.md is the source of truth: if a check conflicts with it, DESIGN.md wins and the conflict is reported rather than silently resolved.

## Color & tokens

- [x] CHK001 Components reference **semantic aliases** (`surface-card`, `text-body`, `brand-primary`, `border-default`…), never raw scale steps — verified across needs-you-card, up-next-card, activity-list, news-list, dashboard
- [~] CHK002 **Exactly one coral `brand-primary` CTA per view** — the Needs-you list renders a `jhButton` (coral) accept per row, so a multi-item block shows several. This matches the prior market-card list pattern (row-level primary actions on an inbox); flagged as an intentional list-action deviation, not a hero CTA. Decline uses `jhButton variant="secondary"` (sage)
- [x] CHK003 Lemon `brand-highlight` not used for large fields (unused here)
- [x] CHK004 Status uses paired tokens — training answers use `success-bg/border/fg`, `warning-*`, `danger-*`; market pending uses `warning-*`
- [x] CHK005 No new color values introduced — only existing semantic tokens used

## Typography, numbers & voice

- [x] CHK006 Reuses the app's global type faces (no per-component font overrides added)
- [x] CHK007 Counts/times set in mono — `font-mono` on the needs-you count, up-next spots/time, activity is text-only
- [x] CHK008 Sentence case throughout ("Needs you", "Up next", "What's going on", "See all"); the activity heading + news pills use the styled uppercase eyebrow
- [x] CHK009 Smallest text is `caption`/`eyebrow` tokens (≥12px); body uses `body-sm`/`body-md`
- [x] CHK010 Copy addresses "you" ("Needs you", "You've got N waiting on you", "RSVP: …"); no emoji

## Layout & spacing

- [~] CHK011 Buttons: `jhButton` meets the 44px target; the small inline training/decline chips use `size="sm"`/`py-2xs` and may sit below 44px — consistent with the prior trainings-card chips, flagged for visual confirmation
- [x] CHK012 Spacing uses the scale tokens (`gap-sm`, `px-md`, `py-sm`, `mb-sm`…) — no arbitrary px
- [x] CHK013 Home stays in the centered `max-w-container-lg` column; single-column stack reflows mobile-first
- [x] CHK014 Section rhythm via `gap-lg` between the four sections (consistent with prior home)

## Shape & elevation

- [x] CHK015 Radii by element type — `jh-card` (lg), row items `rounded-md`, pills `rounded-pill`, date chip `rounded-sm`
- [x] CHK016 Shadows via the `jh-card` component tokens (not raw black)
- [x] CHK017 Needs-you and activity use the shared `jh-card`; up-next/news sections keep the existing `surface-card` + muted border + `sm` shadow
- [x] CHK018 No oversized shadows introduced

## Motion & states

- [x] CHK019 Transitions use `duration-fast` + token easings on the training/toggle buttons
- [~] CHK020 Focus visibility inherited from `jhButton`; the small inline `<button>` chips rely on default focus — flagged for visual confirmation
- [x] CHK021 `jhButton` press/hover behavior reused (unchanged)
- [x] CHK022 No decorative animation loops

## Iconography

- [x] CHK023 Only the existing inline check SVG (up-next "going"/"team going") is reused; no new icon set
- [x] CHK024 No emoji as UI icons

## Accessibility

- [~] CHK025 Body text uses `text-body`/`text-muted`/`text-subtle` on card surfaces; `text-subtle` on the quiet activity log is the lowest-contrast pairing — flagged for a contrast spot-check (subordinate to the app-wide note in CHK034)
- [x] CHK026 Status not by color alone — training answers carry text labels (Going/Maybe/Can't), market pending carries the word "pending", team-going carries a check + label
- [~] CHK027 Action groups carry `role="group"` + `aria-label`; date chip has `aria-label`. Keyboard reachability of the inline chips is inherited from native `<button>` — flagged for a keyboard pass

## Empty, loading & error states

- [x] CHK028 Empty states are warm with a next step — Up-next empty points to "Browse open events"; Needs-you and activity hide entirely when empty (by design, FR-005)
- [x] CHK029 Loading skeleton + styled failure/retry card preserved on the dashboard

## Feature-specific UI

- [x] CHK030 **Needs you** rows pair action buttons (accept/decline, I'm-in/can't, going/maybe/can't) with a title + context line; the block returns nothing when empty (no empty-state card) — verified in `needs-you-card.component.html` + spec
- [x] CHK031 **Up next** renders events and trainings in one timeline via `up-next-card` branching on `kind`; the training vs event distinction is legible by its action row (going/maybe/can't vs RSVP/team-going) + location/label text, not color alone
- [x] CHK032 **News** vs **What's going on** are distinct — News rows are full authored posts with source pills and links; activity is a compact single-line log under a quiet eyebrow heading with no action affordances
- [x] CHK033 Section order is Needs you → Up next → News → What's going on (`dashboard.component.html`); removed sections deleted with no orphaned markup (grep-verified, T037)
- [~] CHK034 New primary buttons reuse the shared `jhButton` (coral) — they inherit, and do not worsen, the standing app-wide white-on-coral ≈3.14:1 contrast conflict. No new bespoke non-conforming button introduced; the brand decision remains the owner's (see [[design-md-contrast-conflict]])

## Notes

- Legend: `[x]` verified against the code diff; `[~]` needs a visual/keyboard pass in the running app (the items below were checked structurally but not rendered).
- **Deferred to a running-app pass** (`[~]`): CHK002 (multiple row-level primary buttons in the Needs-you list — intentional inbox pattern), CHK011/CHK020/CHK027 (touch-target size, focus visibility, keyboard reachability of the small inline chips), CHK025 (contrast spot-check on the quiet `text-subtle` activity log). None are new bespoke violations; all reuse existing components/tokens.
- **Standing conflict, out of scope** (CHK034): the app-wide white-on-coral primary-button contrast (~3.14:1) is inherited via `jhButton`, not worsened here. See [[design-md-contrast-conflict]] — the brand decision is the owner's.
- Conventions (constitution VI): all new components keep `.html` / `.css` / `.ts` separate.
- Verification run: backend Home integration suite **22/22 pass**; frontend unit suite **185/185 pass**; `nx build web` succeeds. These automated checks cover the quickstart scenarios (T036); the literal docker-compose manual walkthrough was not run in this pass.
