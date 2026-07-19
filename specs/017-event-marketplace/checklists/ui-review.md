# UI Review Checklist: Event Marketplace (Mercenaries)

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is done.
**Created**: 2026-07-14
**Feature**: [spec.md](../spec.md)

Reviewed surfaces: the market board (`market-board`, embedded in event-detail), the recruiting page
(`recruiting`), the dashboard market module (`market-card`), the guest tag + recruiting link in
party-manage, and the `MarketInvite` alert row.

## Color & tokens

- [x] CHK001 Components reference **semantic aliases** (`surface-card`, `text-body`, `brand`, `border`, `surface-*-soft`, `warning-*`, `success-fg`…) — verified against `tailwind.config.js` after fixing three stray tokens (`brand-soft`/`accent-soft`/`secondary-soft` → `surface-*-soft`)
- [~] CHK002 One coral `brand-primary` CTA per view — **noted**: list surfaces (inbox rows, party/free-agent cards) use one coral affirmative per row (Accept/Apply/Invite) with all secondary actions as outline/ghost. This is the per-row-primary reading; a single-CTA-per-view purist reading would differ. Reported, not silently resolved.
- [x] CHK003 Lemon highlight not used for large fields (not used here)
- [x] CHK004 Status uses paired tokens — `warning-bg/-fg` (pending), `success-fg` (live), `danger` (revoke/take-down), `surface-sunken` (declined)
- [x] CHK005 No new colors introduced ad hoc (one arbitrary overlay `rgba(26,22,15,0.45)` for the modal scrim, derived from the ink shadow tone)

## Typography, numbers & voice

- [x] CHK006 Headings use the display face (inherited); body/UI use Mona Sans (inherited)
- [x] CHK007 Counts set in **mono** (`font-mono`): board side counts, open spots, fill `X / Y`, spots stepper
- [x] CHK008 **Sentence case** throughout (headings, buttons, labels); eyebrows use the uppercase eyebrow style
- [x] CHK009 Nothing below `caption`; body is `body-sm`/`body-md`
- [x] CHK010 Copy is "you"/"we", inviting ("Post yourself as a mercenary", "come play"); no emoji

## Layout & spacing

- [x] CHK011 Buttons/inputs use `py-sm`/`min-h`-equivalent paddings; primary controls ≥ 44px (matching event-detail); some dense inbox row buttons are compact (`py-1`) consistent with existing party roster row controls
- [x] CHK012 Spacing composes from scale tokens (`px-md`, `gap-sm`, `mt-md`, `space-y-sm`)
- [x] CHK013 Board sits in the event page's centered column; recruiting page capped at `max-w-2xl`; mobile-first, reflows
- [x] CHK014 Section rhythm inherited from the event/dashboard shells

## Shape & elevation

- [x] CHK015 No sharp corners — cards `rounded-lg`, controls/inputs `rounded-md`, chips/avatars `rounded-full`, modal `rounded-xl`
- [x] CHK016 Shadows are the warm tokens (`shadow-sm` on cards, `shadow-xl` on the modal)
- [x] CHK017 Cards use `surface-card` + 1px `border` + `shadow-sm` (matching the event page's card treatment)
- [x] CHK018 Larger shadow reserved for the floating modal

## Motion & states

- [x] CHK019 Transitions use token durations (`transition-colors` on toggles/chips)
- [x] CHK020 Focus visible: inputs/textarea use `focus:ring-2 focus:ring-coral-1` + `focus:border-border-focus`
- [x] CHK021 Buttons darken a brand step on hover (`hover:bg-brand-hover`); ghost/outline warm to `surface-sunken`
- [x] CHK022 No infinite decorative loops

## Iconography

- [x] CHK023 No non-Lucide icons introduced — the marketplace uses text affordances and initial-avatar circles rather than icons (no filled/duotone glyphs)
- [x] CHK024 No emoji as UI icons

## Accessibility

- [x] CHK025 Body text on card/sunken surfaces meets AA (reuses the tuned sand text ramp)
- [x] CHK026 Status never by color alone — every status pill carries text ("pending", "declined", "live", "applied", "guest · via market")
- [x] CHK027 Interactive elements are buttons/inputs/links (keyboard-reachable); the segmented control uses `role="tab"`/`aria-selected`; the modal dismisses via a Cancel button (click-to-dismiss backdrop removed for a11y-lint compliance)

## Empty, loading & error states

- [x] CHK028 Warm empty states — "No crews are recruiting here yet. Post yourself and they'll find you." / "No free agents on the board yet." / empty inboxes
- [x] CHK029 Loading ("Loading the market…") and error ("Couldn't load the market. Try again shortly.") states present and styled

## Feature-specific UI

- [x] CHK030 Position chips reuse the pompfen catalog labels (English) as pill chips (sage `surface-secondary-soft`), matching the profile "Plays" treatment
- [x] CHK031 The two-sided board uses a pill segmented control with mono counts; the position filter is a pill chip row
- [x] CHK032 The "guest · via market" tag renders as a small sage pill in the party roster row, alongside the existing Admin tag
- [~] CHK033 Avatars render as **initial circles** (from `hasAvatar`/name) rather than the profile photo — a deliberate placeholder consistent with the wireframe; wiring the real avatar image is a follow-up

## Notes

- `.html` / `.css` / `.ts` kept separate per component (constitution VI).
- CHK002 and CHK033 are the two honest deviations, both noted above rather than silently resolved.
- No DESIGN.md conflicts encountered; three stray token names were corrected to real semantic aliases.
