# UI Review Checklist: Authenticated-Only Access with Opt-In Public Profiles

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before a feature is considered done.
**Created**: 2026-07-22
**Feature**: [spec.md](../spec.md)

**How to use**: This is an *implementation-quality* gate, run **after** UI is built and
**before** verification. [DESIGN.md](../../../DESIGN.md) is the source of truth: if a check
conflicts with it, DESIGN.md wins and the conflict is reported rather than silently resolved.

## Color & tokens

- [x] CHK001 Components reference **semantic aliases**, never raw scale steps
- [x] CHK002 **Exactly one coral `brand-primary` CTA per view**; supporting actions use sage `brand-secondary`
- [x] CHK003 Lemon `brand-highlight` used only for small pops
- [x] CHK004 Status uses paired `*-bg` / `*-border` / `*-fg` tokens
- [x] CHK005 No new colors introduced ad hoc

## Typography, numbers & voice

- [x] CHK006 Headings use Hubot Sans; body/UI text uses Mona Sans
- [x] CHK007 Counts/stats in mono face
- [x] CHK008 Sentence case everywhere
- [x] CHK009 Nothing meaningful below 12px; body 16px
- [x] CHK010 Copy addresses reader as "you"; no emoji

## Layout & spacing

- [x] CHK011 The visibility toggle control has a touch target ≥ 44px
- [x] CHK012 Spacing composes from 4px scale tokens
- [x] CHK013 Content in centered column capped at `container-lg`; mobile-first
- [x] CHK014 Section rhythm uses `section-gap`

## Shape & elevation

- [x] CHK015 No sharp corners; radius matches element type
- [x] CHK016 Warm-tinted shadow tokens only
- [x] CHK017 Cards are white `surface-card` + 1px border + soft shadow; lift on hover
- [x] CHK018 Larger shadows reserved for floating elements

## Motion & states

- [x] CHK019 Transitions use token durations/easings (toggle uses `ease-bounce`)
- [x] CHK020 Focus visible: 2px coral border + coral `focus-ring`
- [x] CHK021 Buttons darken + glow on hover, nudge on press
- [x] CHK022 No infinite decorative loops

## Iconography

- [x] CHK023 Lucide line icons only, 16–22px, `currentColor`/token
- [x] CHK024 No emoji as UI icons

## Accessibility

- [x] CHK025 Toggle label + helper text meet WCAG AA contrast (≥ 4.5:1)
- [x] CHK026 Visibility state (public/private) is **never conveyed by color alone** — paired with text/icon
- [x] CHK027 The toggle is keyboard-reachable, has a visible focus state, and an accessible label/`aria-checked`

## Empty, loading & error states

- [x] CHK028 The anonymous public-profile **not-found** state offers a warm next step (e.g. "Sign in to see more")
- [x] CHK029 Loading/error states for the toggle save exist and are styled to the system

## Feature-specific UI

- [x] CHK030 The "Make my profile public" toggle clearly explains the effect (public = shareable by link; private = hidden from anonymous visitors) in sentence-case helper text
- [x] CHK031 Signed-out redirects to team/event/browse land on the styled sign-in screen (no unstyled flash of gated content)
- [x] CHK032 A public profile viewed anonymously renders card + teams + activity, and team links are visibly normal but route to sign-in when followed

## Notes

- Check items off as verified: `[x]`. Record `file:line` for any failure.
- Keep `.html` / `.css` / `.ts` separate per component (constitution VI).
- If a check conflicts with DESIGN.md, DESIGN.md wins — note the conflict here.
