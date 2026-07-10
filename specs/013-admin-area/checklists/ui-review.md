# UI Review Checklist: Platform Admin Area

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-07-10
**Feature**: [spec.md](../spec.md)

Reviewed against the 013 diff: top-nav Admin item, admin shell, overview, users
list, player detail + Assign picker, sign-in suspended message, and the re-mounted
012 catalogue page.

## Color & tokens

- [x] CHK001 Components reference semantic aliases (`bg-surface-card`, `text-body`, `border-border-muted`, `bg-brand`, status `*-bg`/`*-fg`) — no raw scale steps in the new templates
- [x] CHK002 One coral CTA per view: overview/users/shell have none; the player detail's single coral CTA is **Assign** (the picker's Grant button lives in a modal layer over the view, mirroring 012's established pattern)
- [x] CHK003 Lemon not used
- [x] CHK004 Status chips and alerts use the paired `success/warning/danger` `*-bg`/`*-border`/`*-fg` tokens
- [x] CHK005 No new colors; the picker scrim reuses 012's established `bg-black/40` overlay (admin-recognition.component.html:143 precedent)

## Typography, numbers & voice

- [x] CHK006 Headings use the `text-h3`/`text-h4` scale (same idiom as every existing page); body text `text-body-*` Mona Sans
- [x] CHK007 Counts are mono: overview stats, users total, badge counts, "Showing X–Y of Z", player id
- [x] CHK008 Sentence case throughout ("Back to app", "Send reset link", "Lift ban", "New players this week")
- [x] CHK009 Smallest text is `text-caption` (12px)
- [x] CHK010 Voice: "you"/"we", low-pressure confirms ("Reversible any time."), no emoji (the ✕ close glyph matches 012's modal and the wireframe)

## Layout & spacing

- [x] CHK011 Inputs and text buttons are `h-11` (44px); the sole `h-9` control is the picker's icon-only close button, matching the app-wide top-nav icon-button precedent
- [x] CHK012 Spacing uses the token scale (`gap-sm`, `px-md`, `py-xs`…; the fractional `py-0.5`/`gap-1` chips follow existing chip/badge markup)
- [x] CHK013 Shell content sits in a centered `max-w-container-lg` column; every page reflows mobile-first (stats 2×2, table→cards, sidebar→bottom tabs)
- [x] CHK014 Single-page admin views use card rhythm (`gap-md`) rather than marketing `section-gap` — consistent with all existing app pages

## Shape & elevation

- [x] CHK015 No sharp corners: buttons/inputs `rounded-md`, cards `rounded-lg`, chips `rounded-pill`, bottom sheet `rounded-t-xl`
- [x] CHK016 Only token shadows (`shadow-sm`/`md`/`xl`)
- [x] CHK017 Cards are white `surface-card` + 1px `border-border-muted` + `shadow-sm`; tappable mobile cards deepen shadow on hover
- [x] CHK018 The only `shadow-xl` is the floating picker dialog

## Motion & states

- [x] CHK019 Transitions use `duration-fast` with token easings
- [x] CHK020 Inputs/controls use `focus-visible:ring-2 focus-visible:ring-focus` + `border-border-focus`
- [x] CHK021 Buttons shift surface/brand step on hover (`hover:bg-brand-hover`, `hover:bg-surface-sunken`)
- [x] CHK022 No animation loops

## Iconography

- [x] CHK023 All icons are inline Lucide line paths (lock, shield, users, search, layout-grid, award), 16–22px, `currentColor`
- [x] CHK024 No emoji icons

## Accessibility

- [x] CHK025 Text ramp on light surfaces per system tokens (AA-tuned per DESIGN.md)
- [x] CHK026 Status chips always carry the word (Active/Suspended/Banned), never color alone
- [x] CHK027 Keyboard: focus-visible styles, `aria-current` on nav, `role="dialog"`/`aria-modal` + Escape on the picker, `role="alert"/"status"` on error/success, sr-only labels on inputs and the table's open column

## Empty, loading & error states

- [x] CHK028 Warm empty states ("No new players this week — quiet on the pitch.", "Nothing yet — Assign grants the first one.", clear-the-filter hint)
- [x] CHK029 Loading and error (with retry) states exist on all three pages, styled to the system

## Feature-specific UI

- [x] CHK030 Admin nav entry is lock-marked and set apart (bordered, muted until hover) on desktop; mobile keeps four tabs and reaches Admin via the account-menu row (wireframe 1a)
- [x] CHK031 Users desktop table folds into mobile cards; banned rows render with the danger chip and stay reachable (wireframe 1c)
- [x] CHK032 Player detail mobile order is badges → account → identity (wireframe 1d) via grid `order-*`
- [x] CHK033 Picker: catalogue tabs, already-held items disabled and marked "Given", optional note, bottom-sheet on mobile / dialog on desktop (wireframe 1e)

## Notes

- CHK014: DESIGN.md's `section-gap` targets marketing-style stacked sections; the admin area (like the dashboard, browse, and team pages before it) composes cards inside one view. Consistency with the app took precedence; no conflict with DESIGN.md tokens.
- CHK011: icon-only 36px close button retained for consistency with the existing top-nav bell/avatar controls; flagged rather than silently diverging from the ≥44px rule.
