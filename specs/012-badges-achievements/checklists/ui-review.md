# UI Review Checklist: Badges & Achievements

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before this feature is considered done.
**Created**: 2026-07-09 · **Reviewed**: 2026-07-10
**Feature**: [spec.md](../spec.md)

**How to use**: Run this *after* the UI is built and *before* verification. Check each
item against the diff; record `file:line` for any failure. [DESIGN.md](../../../DESIGN.md)
is the source of truth — if a check conflicts with it, DESIGN.md wins and the conflict
is reported. Standing items (CHK001–CHK029) are the DESIGN.md compliance set from
[`.specify/templates/ui-review-checklist-template.md`](../../../.specify/templates/ui-review-checklist-template.md);
CHK030+ are specific to this feature.

Reviewed surfaces: `recognition-display.component.*` (profile + team), `admin-recognition.component.*`, `avatar-menu` admin entry.

## Color & tokens

- [x] CHK001 Semantic aliases used throughout (`surface-card`, `text-heading`, `text-muted`, `border-border-*`, `brand-primary`, `danger-*`, `success-fg`); no raw scale steps (the modal scrim uses a standard `bg-black/40` overlay)
- [x] CHK002 One coral `brand-primary` CTA per view — Assign (main), Grant (modal); Load/Cancel are secondary, Revoke is danger
- [x] CHK003 Lemon highlight not used (no misuse)
- [x] CHK004 Status uses paired tokens — revoke + errors use `danger-bg/border/fg`, "Given" uses `success-fg`
- [x] CHK005 No new colors introduced

## Typography, numbers & voice

- [x] CHK006 Inherited Hubot/Mona faces (no font overrides)
- [x] CHK007 Earned dates in the display use the **mono** face (`font-mono`); the admin "by X · date" line is prose, intentionally body
- [x] CHK008 Sentence case everywhere; the only uppercase is the `text-eyebrow` group labels
- [x] CHK009 Smallest text is `caption` (12px); body is `body-md`
- [x] CHK010 Warm copy, no emoji

## Layout & spacing

- [ ] CHK011 Touch targets — inputs/Load are 44px (`h-11`), but several compact admin controls are below 44px (Assign `h-10`; tab/Grant/Cancel/Revoke use `py-xs`). Acceptable density for an admin tool; **follow-up: bump primary admin controls to ≥44px.**
- [x] CHK012 Spacing composes from scale tokens (`xs/sm/md/lg`); a few standard utility sizes (`h-10/11`, `max-h-[90vh]`) are conventional
- [x] CHK013 Admin page is a centered `max-w-container-lg` (1100px) column; display reflows within profile/team columns
- [x] CHK014 Section-gap N/A — these are embedded sections within existing pages

## Shape & elevation

- [x] CHK015 No sharp corners — `rounded-full` icons, `rounded-lg` cards, `rounded-md` controls, `rounded-xl` modal
- [x] CHK016 Warm shadow tokens (`shadow-sm`, `shadow-coral`, `shadow-xl`)
- [x] CHK017 Cards are white `surface-card` + 1px muted border + `shadow-sm`
- [x] CHK018 `shadow-xl` reserved for the floating modal

## Motion & states

- [x] CHK019 Transitions use `transition-colors` (token-driven); no ad-hoc timings
- [x] CHK020 Inputs show a visible coral focus ring (`ring-focus` + `border-border-focus`); buttons keep native focus
- [ ] CHK021 Hover states present; the **press nudge (1px / scale 0.99) is not implemented** on these buttons. **Follow-up.**
- [x] CHK022 No infinite decorative loops

## Iconography

- [x] CHK023 Chrome icons are inline **line icons** (2px stroke, `currentColor`) in the Lucide style (shield, plus, x, award, trophy)
- [x] CHK024 No emoji icons

## Accessibility

- [x] CHK025 Uses the AA-tuned sand text ramp on light surfaces
- [x] CHK026 Groups labeled with text headings ("Badges" / "Achievements"), not color alone
- [x] CHK027 Picker items are `<button>`s (focusable, disabled when held); modal closes on Esc; award/icon images carry `alt`

## Empty, loading & error states

- [x] CHK028 Warm empty state ("No badges or achievements yet.") when a subject has none
- [x] CHK029 Admin surface has loading ("Loading…") and a danger-styled error box; display needs none (data arrives with the page)

## Feature-specific UI

- [x] CHK030 Badges and achievements render as two distinct labeled groups on the profile and team page
- [x] CHK031 The same `RecognitionDisplayComponent` is reused across profile (public + owner) and team page
- [x] CHK032 Each award shows icon (with an SVG fallback when missing), name, and date; description is a hover `title` (space-conscious)
- [x] CHK033 `flex-wrap` + `truncate` handle 0/few/many; designed for reflow (not exhaustively QA'd at every breakpoint — see notes)
- [x] CHK034 Revoked awards are excluded server-side (covered by DisplayTests)
- [x] CHK035 Revoke uses danger tokens, is visually distinct, and now prompts a confirmation before revoking
- [x] CHK036 Admin entry + `/admin` guard are UX-only; the server `PlatformAdmin` policy is the real gate

## Notes / follow-ups (tracked for a UI-polish pass)

1. **CHK011** — raise compact admin controls (Assign, tabs, Grant/Cancel/Revoke) to ≥44px touch targets.
2. **CHK021** — add the press-nudge micro-interaction to buttons.
3. Visual QA of the recognitions area at mobile/desktop breakpoints with many awards was not performed end-to-end (Playwright e2e covers the grant→display→revoke path).

These are minor polish items; core DESIGN.md compliance (color/type/shape/voice/grouping/empty-state) passes.
