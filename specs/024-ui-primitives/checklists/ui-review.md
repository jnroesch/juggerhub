# UI Review Checklist: Shared UI primitives layer

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before this feature is considered done.
**Created**: 2026-07-22
**Feature**: [spec.md](../spec.md)

**How to use**: Implementation-quality gate, run **after** UI is built and **before**
verification. Because this feature migrates the whole app in phases (research R9),
run this checklist against **each migration phase's diff**, recording `file:line`
for anything that fails. DESIGN.md wins any conflict.

## Color & tokens

- [ ] CHK001 Components reference **semantic aliases** (`surface-card`, `text-body`, `brand-primary`, `border-default`…), never raw scale steps
- [ ] CHK002 **Exactly one coral `brand-primary` CTA per view**; supporting actions use sage `brand-secondary`
- [ ] CHK003 Lemon `brand-highlight` only for small pops — never large fields
- [ ] CHK004 Status uses the paired `*-bg` / `*-border` / `*-fg` tokens, not ad-hoc colors
- [ ] CHK005 No new colors ad hoc — any new value added to DESIGN.md tokens first

## Typography, numbers & voice

- [ ] CHK006 Display face for headings, Mona Sans for body/UI
- [ ] CHK007 Scores/stats/times/counts set in the **mono** face
- [ ] CHK008 **Sentence case everywhere**; UPPERCASE only as eyebrow
- [ ] CHK009 Nothing meaningful below 12px; body 16px
- [ ] CHK010 "you"/"we" voice; CTAs invite; no emoji

## Layout & spacing

- [ ] CHK011 Interactive controls have a **touch target ≥ 44px** (enforced by `jhButton size=md`)
- [ ] CHK012 Spacing composes from the 4px scale tokens
- [ ] CHK013 Content sits in a centered column; page widths follow the `jh-page-container` taxonomy (research R6); mobile-first reflow
- [ ] CHK014 Section rhythm uses `section-gap`

## Shape & elevation

- [ ] CHK015 **No sharp corners** — radius matches element type (buttons/inputs `md`, cards `lg`)
- [ ] CHK016 Shadows are the warm-tinted tokens — never pure black
- [ ] CHK017 Cards are white `surface-card` + 1px muted border + soft `sm` shadow; lift on hover (`jh-card`)
- [ ] CHK018 Larger shadows reserved for genuinely floating elements

## Motion & states

- [ ] CHK019 Transitions use the token durations/easings
- [x] CHK020 Focus always visible: coral border + `focus-ring` (enforced by `jhButton` + inputs)
- [x] CHK021 Buttons darken + gain glow on hover, nudge down 1px on press (`jhButton`)
- [ ] CHK022 No infinite decorative loops in content

## Iconography

- [x] CHK023 Icons are **Lucide line icons** (via `jh-icon`), 16–22px, `currentColor`
- [x] CHK024 No emoji and **no text-glyph icons** (the `+` is now `jh-icon name="plus"`)

## Accessibility

- [ ] CHK025 Body text meets WCAG AA contrast (≥ 4.5:1) — **except** the standing DESIGN.md primary-button contrast conflict, which is out of scope (spec FR-014) and reported, not resolved
- [x] CHK026 Status never conveyed by color alone — paired with text/icon (`jh-alert` includes text)
- [x] CHK027 Interactive elements keyboard-reachable with visible focus and labels/roles

## Empty, loading & error states

- [x] CHK028 Empty states offer a warm next step where relevant (`jh-empty-state`); bare sub-list one-liners kept as small notes (documented follow-up)
- [x] CHK029 Loading (`jh-loading`) and error (`jh-alert`) states use the shared primitives, not raw markup

## Feature-specific UI

- [x] CHK030 Every action button uses `jhButton` (drift guard passes; only intentional exceptions — dark `bg-ink` invite buttons — remain)
- [x] CHK031 Every page/form error uses `jh-alert` with `role="alert"` and `danger-fg`; bare `text-danger` (red-5) error text retired (field hints unified to `danger-fg`)
- [x] CHK032 Every loading line uses `jh-loading`; `text-subtle`/`text-faint` no longer used for loading status
- [x] CHK033 Boxed empty states (sunken/bordered/dashed) use `jh-empty-state`; bare sub-list one-liners left as small notes
- [x] CHK034 On-brand label text uses `text-on-accent` — no raw `text-white` on brand surfaces (nav badges converted)
- [x] CHK035 "invite" is the canonical noun in copy — no user-facing "invitation" (routes/testids keep the legacy path)
- [x] CHK036 Page roots use `max-w-container-*` tokens per the width taxonomy (research R6); no arbitrary `max-w-2xl`/`4xl` roots
- [x] CHK037 A CI drift guard (`scripts/check-ui-drift.ps1`) fails the build if retired hand-assembled patterns return

## Notes

- Check items off as verified: `[x]`. Record `file:line` for any failure.
- Keep `.html` / `.css` / `.ts` separate per component (constitution VI).
- DESIGN.md wins conflicts — note them here rather than resolving silently.
- CHK025's exception is the known white-on-`coral-4` ≈ 3.14:1 issue; tracked as an owner-level DESIGN.md decision, not fixed by this feature.
