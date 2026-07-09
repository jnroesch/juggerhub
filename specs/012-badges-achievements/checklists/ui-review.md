# UI Review Checklist: Badges & Achievements

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before this feature is considered done.
**Created**: 2026-07-09
**Feature**: [spec.md](../spec.md)

**How to use**: Run this *after* the UI is built and *before* verification. Check each
item against the diff; record `file:line` for any failure. [DESIGN.md](../../../DESIGN.md)
is the source of truth — if a check conflicts with it, DESIGN.md wins and the conflict
is reported. Standing items (CHK001–CHK029) are the DESIGN.md compliance set from
[`.specify/templates/ui-review-checklist-template.md`](../../../.specify/templates/ui-review-checklist-template.md);
CHK030+ are specific to this feature.

## Color & tokens

- [ ] CHK001 Components reference **semantic aliases** (`surface-card`, `text-body`, `brand-primary`, `border-default`…), never raw scale steps
- [ ] CHK002 **Exactly one coral `brand-primary` CTA per view**; supporting actions use sage `brand-secondary`
- [ ] CHK003 Lemon `brand-highlight` is used only for small pops ("New"/streak) — never large fields
- [ ] CHK004 Status colors use the paired `*-bg` / `*-border` / `*-fg` tokens
- [ ] CHK005 No new colors introduced ad hoc — any new value was added to DESIGN.md tokens first

## Typography, numbers & voice

- [ ] CHK006 Headings use **Hubot Sans**; body/UI text uses **Mona Sans**
- [ ] CHK007 Earned-date counts / any numeric stats are set in the **mono** face
- [ ] CHK008 **Sentence case everywhere**; UPPERCASE only as a styled eyebrow
- [ ] CHK009 Nothing meaningful below 12px (`caption`); body is 16px (`body-md`)
- [ ] CHK010 Copy is warm ("you"/"we"); no emoji in product UI

## Layout & spacing

- [ ] CHK011 Interactive controls have a **touch target ≥ 44px**
- [ ] CHK012 Spacing composes from the 4px scale tokens — no arbitrary pixel values
- [ ] CHK013 Content sits in a centered ≤1100px column; mobile-first and reflows down
- [ ] CHK014 Section rhythm uses `section-gap`

## Shape & elevation

- [ ] CHK015 **No sharp corners** — chips/avatars use `pill`, cards `lg`, controls `md`/`sm`
- [ ] CHK016 Shadows are warm-tinted tokens — never pure black
- [ ] CHK017 Cards use white `surface-card` + 1px muted border + soft `sm` shadow; lift 3px on hover
- [ ] CHK018 Larger shadows reserved for genuinely floating elements

## Motion & states

- [ ] CHK019 Transitions use token durations (120/200/320ms) and easings
- [ ] CHK020 Focus is always visible: 2px coral border + coral `focus-ring`
- [ ] CHK021 Buttons darken + glow on hover, nudge down 1px / scale 0.99 on press
- [ ] CHK022 No infinite decorative animation loops in content

## Iconography

- [ ] CHK023 Chrome icons are **Lucide line icons** (16–22px, `currentColor`). *(Note: the admin-defined recognition **icon** per FR-001 is feature content, not UI chrome — see CHK032.)*
- [ ] CHK024 No emoji used as UI icons

## Accessibility

- [ ] CHK025 Body text meets **WCAG AA contrast (≥ 4.5:1)**
- [ ] CHK026 Badge vs achievement grouping is **not conveyed by color alone** — labeled with text/heading
- [ ] CHK027 Recognition items are keyboard-reachable with visible focus and accessible names; icons have alt/`aria-label`

## Empty, loading & error states

- [ ] CHK028 Subjects with no recognitions show a **warm empty state** (FR-008 / SC-003), not a blank or broken area
- [ ] CHK029 Loading and error states for the recognitions area are styled to the system

## Feature-specific UI

- [ ] CHK030 Badges and achievements render as **two visually distinct groups** on both the player profile and the team page (FR-003, FR-008)
- [ ] CHK031 The **same recognition component/style** is reused across profile and team page — one consistent presentation (FR-008, SC-003)
- [ ] CHK032 Each award shows **icon, name, description, and earned date**; the admin-defined icon renders at a consistent size with a sensible fallback when missing/broken (FR-008)
- [ ] CHK033 Recognition area layout holds up across supported viewports with 0, few, and many awards (SC-003) — no overflow or broken wrap
- [ ] CHK034 **Revoked awards are not shown** on the public profile/team page (FR-009, SC-005)
- [ ] CHK035 Admin catalog/grant/revoke surfaces follow the same system (cards, buttons, inputs, ≥44px targets); destructive **revoke** is clearly distinguished (danger tokens + confirmation), not a bare coral CTA
- [ ] CHK036 Admin-only controls are hidden for non-admins in the UI **for UX only** — the real gate is server-side (FR-010; not a UI concern to verify here)

## Notes

- Check items off as verified: `[x]`. Record `file:line` inline for any failure.
- Conventions ([constitution](../../../.specify/memory/constitution.md) VI): keep `.html` / `.css` / `.ts` separate per component.
- If a check conflicts with DESIGN.md, DESIGN.md wins — note the conflict here.
