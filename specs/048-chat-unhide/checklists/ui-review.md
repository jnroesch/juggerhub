# UI Review Checklist: Hiding a Chat Is Reversible

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-09-09
**Feature**: [spec.md](../spec.md)

**Scope of this diff** — deliberately small: one existing button in
`frontend/apps/web/src/app/features/chat/chat-details/chat-details.component.html` (~L128-149)
gains a conditional label and a second icon; one new string in three catalogues. **No new
component, no new layout, no new token, no new colour.** Every structural property below is
inherited unchanged from the `toggle-mute` button immediately above it, which already passed
this checklist under feature 019.

## Color & tokens

- [x] CHK001 Semantic aliases only — the button carries `border-border-strong bg-surface-card text-body hover:bg-surface-sunken ring-focus`; the icon is `text-muted`. No scale step appears in the diff
- [x] CHK002 Not a CTA — this is a bordered secondary control in a stack; the coral `brand-primary` count for the view is unchanged
- [x] CHK003 No `brand-highlight` used
- [x] CHK004 No status colours introduced
- [x] CHK005 No new colour value

## Typography, numbers & voice

- [x] CHK006 Inherits the panel's faces; no font class changed
- [x] CHK007 No numerals in the new string
- [x] CHK008 Sentence case: "Show in my messages again" / "Wieder in meinen Nachrichten anzeigen" / "Mostrar de nuevo en mis mensajes"
- [x] CHK009 `text-body-md` (16px), unchanged from the sibling controls
- [x] CHK010 Addresses the reader as "you" in the possessive ("**my** messages", matching the existing `hide` string's voice); invites rather than shouts; no emoji

## Layout & spacing

- [x] CHK011 `min-h-11` (44px) retained verbatim
- [x] CHK012 `px-md`, and the stack's `space-y-sm` — scale tokens, unchanged
- [x] CHK013 Inherited from the details panel; no layout change
- [x] CHK014 No section rhythm change

## Shape & elevation

- [x] CHK015 `rounded-md` retained (button)
- [x] CHK016 No shadow added or changed
- [x] CHK017 Not a card
- [x] CHK018 No elevation change

## Motion & states

- [x] CHK019 `transition-colors duration-fast` retained
- [x] CHK020 `focus-visible:ring-2 focus-visible:ring-focus` retained verbatim
- [x] CHK021 Hover retained; the control is a bordered button, matching `toggle-mute`'s treatment rather than a brand button's
- [x] CHK022 No animation added

## Iconography

- [x] CHK023 Both icons are Lucide line icons at `size-5` (20px) via `currentColor`: `eye-off` for the *hide* action (unchanged from before) and `eye` for the *show* action. Stroke width, linecap and linejoin match the existing icons in the panel
- [x] CHK024 No emoji

## Accessibility

- [x] CHK025 Text is `text-body` on `bg-surface-card` — the same pairing as the sibling controls, unchanged by this diff. (The standing app-wide coral-CTA contrast conflict noted in DESIGN.md review does **not** touch this control, which is not a coral button)
- [x] CHK026 State is conveyed by the **label text**, not by colour — the icon reinforces it. Failing to change the icon would still leave the state readable
- [x] CHK027 Native `<button type="button">`, keyboard-reachable, visible focus ring, `[disabled]="busy()"`. Both `<svg>` elements are `aria-hidden="true"`, so the accessible name is the label alone and changes with the state

## Empty, loading & error states

- [x] CHK028 No empty state in scope
- [x] CHK029 The busy state is the existing `disabled:opacity-50` while the patch is in flight; an error leaves the control enabled and the label unchanged, so a failed toggle does not lie about the state

## Feature-specific UI

- [x] CHK030 The control is a **two-state toggle mirroring `toggle-mute`** — same element, same classes, label swapped by state — so hide reads as the peer of mute rather than as a destructive action (spec FR-002)
- [x] CHK031 **The two directions are asymmetric on purpose**: hiding navigates back to the inbox, un-hiding stays on the conversation and updates the label in place (spec FR-004). Pinned by `chat-details.component.spec.ts`
- [x] CHK032 `data-testid` renamed `hide-chat` → `toggle-hide`, matching `toggle-mute`. Verified no other file referenced the old id
- [x] CHK033 The icon `<svg>` carries `shrink-0` so the longest label cannot squash it. **Note**: the neighbouring `toggle-mute` icon does not have `shrink-0` — left as-is because its strings are short, but the two rows are now marginally inconsistent in class list (not in rendering)
- [x] CHK034 **Verified in a browser** at 375px in German against the running stack. "Wieder in meinen Nachrichten anzeigen" wraps to two lines, the row grows from 44px to **50px**, the icon stays on axis, nothing truncates or overflows
- [x] CHK035 **The label span is `text-left`.** Found by looking at the screenshot, not by reading the code: the details panel header centres its text, and a shrink-to-fit single-line span hides that — but the wrapped German label filled the row and rendered **centred**, starting in a different place from the `toggle-mute` row directly above it. One class fixes it; re-shot to confirm

## Notes

- **CHK034 and CHK035 came out of the browser walk** (quickstart Scenarios 1–3, both viewports,
  en-GB and de-DE, driven with Playwright against the docker stack). CHK034 passed as predicted;
  CHK035 was a real defect that reading the markup did not reveal and only the screenshot did.
- No conflict with DESIGN.md was found. Nothing in this diff introduces a token, colour,
  radius, shadow, motion or icon style that DESIGN.md does not already define.
- Conventions ([constitution](../../../.specify/memory/constitution.md) VI): `.html` / `.css` /
  `.ts` remain separate; only `.html` and `.ts` were touched.
