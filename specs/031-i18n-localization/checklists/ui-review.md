# UI Review Checklist: Localization — German & Spanish (i18n)

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-07-27
**Feature**: [spec.md](../spec.md)

**Scope of this review**: the UI this feature actually ships — the `jh-language-switcher` control and the surfaces it was placed on (shell public bar, top-nav, sign-in, register), plus the first translated screens (sign-in + public chrome). String extraction of the remaining feature screens is tracked follow-up; those screens are unchanged visually and render via the English fallback.

## Color & tokens

- [x] CHK001 Switcher uses semantic tokens (`--color-surface`, `--color-border`, `--color-ink`, `--color-brand`) via CSS vars; no raw scale steps
- [x] CHK002 Switcher is a neutral control, not a CTA — the single coral CTA per auth view (Sign in / Register button) is unchanged
- [x] CHK003 No lemon highlight misuse
- [x] CHK004 No status colors introduced
- [x] CHK005 No new ad-hoc colors — switcher reuses existing tokens

## Typography, numbers & voice

- [x] CHK006 Switcher text inherits the UI (Mona Sans) face
- [x] CHK007 N/A — no numeric data in the switcher
- [x] CHK008 Sentence case; language options shown as endonyms ("English/Deutsch/Español") which are correctly capitalized names
- [x] CHK009 Switcher text is `0.875rem` (≥ 12px)
- [x] CHK010 Translated copy keeps the "you"/"we" voice (drafts flagged for native review, #77)

## Layout & spacing

- [ ] CHK011 Switcher select height is ~34px — **below the 44px touch target**. Acceptable for a secondary desktop chrome control but flagged; a larger tap target should be considered when the mobile signed-in placement is revisited. `language-switcher.component.css`
- [x] CHK012 Spacing uses rem steps consistent with the scale
- [x] CHK013 Placement respects existing layout containers (nav / auth card)
- [x] CHK014 N/A — no new sections

## Shape & elevation

- [x] CHK015 Switcher uses `0.5rem` radius (control-appropriate); no sharp corners
- [x] CHK016 No new shadows
- [x] CHK017 N/A — not a card
- [x] CHK018 N/A

## Motion & states

- [x] CHK019 No custom transitions added
- [x] CHK020 Focus visible: `:focus-visible` 2px coral outline on the select
- [x] CHK021 N/A — not a brand button
- [x] CHK022 No animation loops

## Iconography

- [x] CHK023 Globe icon is an inline Lucide-style line icon, 16px, `currentColor`
- [x] CHK024 No emoji

## Accessibility

- [x] CHK025 Switcher text/border meet AA against surface
- [x] CHK026 Language is conveyed by text (endonyms), not color
- [x] CHK027 Native `<select>` is keyboard-reachable with a visible focus state and an `aria-label`; `<html lang>` tracks the active language (FR-016)

## Empty, loading & error states

- [x] CHK028 N/A — switcher has no empty state
- [x] CHK029 N/A — no async state in the switcher

## Feature-specific UI

- [x] CHK030 Switcher shows the active language as the selected option and lists each language in its own name (FR-014)
- [x] CHK031 Switcher is reachable signed-out (public bar + auth screens) and signed-in (top-nav) — clarification Q2 / FR-004a
- [ ] CHK032 **German long-string tolerance (SC-006)**: verified on the extracted screens (sign-in, public bar) — no truncation/overflow. **Not yet verified on un-extracted screens** because they still render English; re-run this check per screen as extraction proceeds.

## Notes

- DESIGN.md wins on any conflict. Two items are intentionally left unchecked as honest flags: CHK011 (touch-target size on the desktop chrome select) and CHK032 (German-length check pending on screens not yet extracted).
- The switcher keeps `.html` / `.css` / `.ts` separate per constitution VI.
