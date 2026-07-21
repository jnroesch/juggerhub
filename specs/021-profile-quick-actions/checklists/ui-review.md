# UI Review Checklist: Profile Quick-Actions

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is done.
**Created**: 2026-07-21
**Feature**: [spec.md](../spec.md)

**Scope**: New action bar on the public profile page — a **Message** button, an
**Invite to a team** button (with disabled/reason, picker, and sent states), and inline
outcome messages. Files: `…/features/profile/components/quick-actions/*`.

## Color & tokens

- [x] CHK001 Uses semantic tokens only (`bg-brand`, `bg-secondary`, `text-on-accent`, `text-danger`, `text-success`, `surface-card`, `border-border`…) — no raw scale steps.
- [x] CHK002 Exactly one coral `brand-primary` CTA in view (**Message**); the supporting **Invite** action uses sage `brand-secondary`; picker items are neutral.
- [x] CHK004 Outcome messages use status text tokens (`text-danger`/`text-success`) with `role="alert"` on errors — not ad-hoc colors.

## Typography & voice

- [x] CHK008 Sentence case throughout ("Message", "Invite to a team", "Opening…", "Inviting…", "Invited to …", "Already on your team").
- [x] CHK010 Copy is calm and addresses the reader plainly; CTAs invite; no emoji.

## Layout & spacing

- [x] CHK012 Spacing composes from tokens (`gap-sm`, `gap-xs`, `px-lg`, `py-sm`); the bar reflows (`flex-wrap`) on narrow widths (FR-012).
- [~] CHK011 Touch targets: buttons use `py-xs` to **match the adjacent existing "Copy link" and owner-profile buttons**. This is the app's standing compact-button size, not introduced here; the strict ≥44px target is an app-wide concern for the owner, not this feature.

## Shape, elevation & motion

- [x] CHK015 `rounded-md` buttons; `rounded-sm` picker items; `rounded-md` popover — radius matches element type.
- [x] CHK016/017 Picker is a `surface-card` popover with a muted border + soft `shadow-md`.
- [x] CHK019/021 Buttons transition on `duration-fast`, darken a brand step + gain a colored glow on hover (`hover:shadow-coral`/`hover:shadow-teal`), and nudge down on press (`active:translate-y-px`).

## Accessibility

- [~] CHK025 Contrast: **Message** is white-on-`coral-4` and **Invite** is white-on-`teal-4` — the same standing app-wide primary/secondary contrast pattern flagged in the DESIGN.md contrast note. Not resolved here; it is the owner's brand decision and applies to every primary/secondary button in the app.
- [x] CHK026 Status is never color-only — errors/success carry text; the disabled Invite has a visible reason caption.
- [x] CHK027 Buttons are keyboard-reachable with visible focus (`focus-visible:ring-2 ring-focus`); the picker uses `role="menu"`/`menuitem` and `aria-haspopup`/`aria-expanded`.

## States

- [x] CHK028/029 Loading (`Opening…`/`Inviting…`, disabled), disabled-with-reason (no eligible team), success (`Invited to …`), and error states all exist and are styled to the system. The whole bar renders nothing for anonymous/self viewers (no empty shell).

## Feature-specific UI

- [x] CHK030 Message is the single primary action; Invite is secondary; both sit in one action bar under the profile header, not competing with the neutral "Copy link".
- [x] CHK031 The team **picker** appears only when >1 team is eligible; one eligible team invites directly; ineligible (Member/Invited) teams never appear.

## Notes

- Two items marked `[~]` are **standing, app-wide** matters (compact button height; primary/secondary contrast) that this feature follows for consistency rather than diverging on — flagged for the owner, not resolved here, per DESIGN.md-wins.
- No new tokens, colors, or visual style introduced.
