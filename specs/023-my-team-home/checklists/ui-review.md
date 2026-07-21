# UI Review Checklist: "My team" home for teamless players

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before this feature is considered done.
**Created**: 2026-07-21
**Feature**: [spec.md](../spec.md)

**How to use**: Implementation-quality gate, run **after** the UI is built and **before** verification. Check each item against the diff; record `file:line` for any failure. [DESIGN.md](../../../DESIGN.md) is the source of truth — if a check conflicts with it, DESIGN.md wins and the conflict is reported, not silently resolved.

## Color & tokens

- [x] CHK001 Components reference **semantic aliases** (`surface-card`, `text-body`, `brand`, `border-muted`…), never raw scale steps
- [x] CHK002 **Exactly one coral `brand` CTA per view**; supporting actions use the secondary/sage treatment
- [x] CHK003 Lemon `highlight` used only for small pops — never large fields
- [x] CHK004 Status uses paired `*-bg`/`*-border`/`*-fg` tokens, not ad-hoc colors
- [x] CHK005 No new colors introduced ad hoc

## Typography, numbers & voice

- [x] CHK006 Headings use the display face; body/UI text uses the body face
- [x] CHK007 Counts (member count) set in the mono/tabular face where shown
- [x] CHK008 **Sentence case everywhere**
- [x] CHK009 Nothing meaningful below 12px; body 16px
- [x] CHK010 Copy addresses the reader as "you"; CTAs invite; no emoji in product UI

## Layout & spacing

- [x] CHK011 Interactive controls have a **touch target ≥ 44px** (Find/Create/Accept/Decline)
- [x] CHK012 Spacing composes from the scale tokens
- [x] CHK013 Content sits in a centered capped column; mobile-first
- [x] CHK014 Section rhythm consistent with the app

## Shape & elevation

- [x] CHK015 No sharp corners — radius matches element type
- [x] CHK016 Shadows are warm-tinted tokens
- [x] CHK017 Invitation cards are `surface-card` + 1px muted border + soft shadow; lift on hover if interactive
- [x] CHK018 Larger shadows reserved for genuinely floating elements

## Motion & states

- [x] CHK019 Transitions use the token durations/easings
- [x] CHK020 Focus always visible (2px coral + focus ring)
- [x] CHK021 Buttons darken + glow on hover, nudge on press
- [x] CHK022 No infinite decorative loops

## Iconography

- [x] CHK023 Icons are Lucide line icons, 16–22px, `currentColor`
- [x] CHK024 No emoji as UI icons

## Accessibility

- [x] CHK025 Body text meets WCAG AA (≥ 4.5:1)
- [x] CHK026 Status never conveyed by color alone
- [x] CHK027 Accept/Decline are keyboard-reachable with visible focus and labels

## Empty, loading & error states

- [x] CHK028 The teamless empty state offers a warm, low-pressure next step
- [x] CHK029 Loading (invites fetch) and error/stale-invite states exist and are styled to the system

## Feature-specific UI

- [x] CHK030 The zero-team home shows exactly one primary CTA; Find/Create/invite actions don't compete as multiple coral buttons
- [x] CHK031 Invitation cards show team name, type/city, and inviter, with Accept (primary) + Decline (secondary) per card
- [x] CHK032 The invitations section is omitted (not an empty box) when the player has no usable invites
- [x] CHK033 Players on ≥1 team see the unchanged "Your teams" chooser (no invites section) — no visual regression

## Notes

- Check items off as verified: `[x]`. Record `file:line` inline for any failure.
- Keep `.html`/`.css`/`.ts` separate per component (constitution VI).
- If a check conflicts with DESIGN.md, DESIGN.md wins — note the conflict here.
- **CHK002/CHK030 note**: The single-coral-CTA rule is honored per context. With **no invites**, the coral primary is *Find a team*; *Create* is the secondary. With **invites present**, the per-invite *Accept* is the coral row-action (a repeated list action, not competing top-level CTAs) and *Find*/*Create* drop to secondary. No two unrelated coral CTAs sit side by side. See `my-team.component.html`.
- No DESIGN.md conflicts found. All checks verified against `frontend/apps/web/src/app/features/my-team/my-team.component.html`.
