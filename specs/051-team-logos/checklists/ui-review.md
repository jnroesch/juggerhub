# UI Review Checklist: Team Logos

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-09-16
**Feature**: [spec.md](../spec.md)

**Scope of this review**: six rendering surfaces (team page header, browse-teams row, onboarding
suggestion row, "My team" row, chat inbox row, chat conversation header) plus the new logo section
in team settings. Run against the diff; DESIGN.md wins any conflict.

## Color & tokens

- [x] CHK001 Components reference **semantic aliases** — `border-border`, `bg-brand-gradient`, `text-on-accent`, `bg-surface-muted`, `text-faint`. No scale step appears in the diff.
- [x] CHK002 **Exactly one coral `brand-primary` CTA per view**. Team settings' one primary remains the danger-zone delete; the logo controls are `secondary` (pick) and `ghost` (remove) — a coral "Upload a logo" would have competed with it.
- [x] CHK003 Lemon is not used by this feature.
- [x] CHK004 The upload failure reuses the page's existing `jh-alert`, so it keeps the paired danger tokens rather than inventing a message style.
- [x] CHK005 No new colors. Every placeholder keeps the exact classes it had before the logo was added.

## Typography, numbers & voice

- [x] CHK006 No new headings; the section label is the page's existing eyebrow treatment.
- [x] CHK007 No numbers are introduced.
- [x] CHK008 **Sentence case** — "Team logo", "Upload a logo", "Change logo", "Remove logo", and the German/Spanish equivalents.
- [x] CHK009 Nothing below `caption`; the hint is `body-sm`, the "No logo yet" note `body-sm`.
- [x] CHK010 Copy addresses the reader as "you" ("Shown wherever your team appears…"). No emoji.

## Layout & spacing

- [x] CHK011 The pick and remove controls are `jhButton` at the default `md` size — 44px tall by the directive's own contract. The hidden `<input type="file">` is never the touch target; the button is.
- [x] CHK012 Spacing is `gap-md` / `gap-sm` / `mt-xs` / `mt-sm` / `px-md` / `py-sm` — scale tokens only.
- [x] CHK013 Team settings keeps its `max-w-container-sm` column. The logo row is `flex … flex-wrap`, so the two buttons wrap under the preview at 375px rather than overflowing.
- [x] CHK014 Section rhythm unchanged; the new block sits above Recruitment separated by the same `<hr class="my-lg border-border">` the page already uses between sections.

## Shape & elevation

- [x] CHK015 **Each logo keeps the radius of the placeholder it replaces**, so no surface changes shape when a team uploads one: `rounded-lg` (team page header, settings preview), `rounded-md` (browse + onboarding rows), `rounded-pill` ("My team" row, chat inbox row and header). This is the check the feature is most likely to fail, and it is the reason the `<img>` was written into the existing tile rather than beside it.
- [x] CHK016 No new shadows.
- [x] CHK017 Cards unchanged — the logo block sits inside team settings' existing bordered `surface-card` row, matching the Recruitment row beside it.
- [x] CHK018 Nothing floats.

## Motion & states

- [x] CHK019 No new transitions; the buttons inherit the directive's `duration-fast`.
- [x] CHK020 Focus is the directive's coral ring on both controls. The hidden file input is not focusable, which is why the visible control is a real `<button>` and not a styled `<label>`.
- [x] CHK021 Hover/press come from the button directive.
- [x] CHK022 No animation loops.

## Iconography

- [x] CHK023 No icons added — the controls are text buttons. (The avatar editor uses a pencil glyph because it overlays a 80px picture; a settings row has room for a word.)
- [x] CHK024 No emoji.

## Accessibility

- [x] CHK025 Contrast unchanged; no text sits on the logo.
- [x] CHK026 Logo presence is never the only signal — the team's name is beside it on every one of the six surfaces.
- [x] CHK027 Both controls are keyboard-reachable buttons with visible focus. `logoBusy()` disables them during a write so a double-submit is not reachable by repeated Enter.
- [x] CHK028 **Alt text is decorative (`alt=""`) on all five list/header surfaces**, because the team's name is rendered immediately beside the image and a described logo makes a screen reader announce the name twice. The one exception is the settings preview, where the logo *is* the subject being edited, so it carries `teams.settings.logoAlt` ("{{name}} logo").

## Empty, loading & error states

- [x] CHK029 The empty state is the letter tile that has always been there, plus a "No logo yet" note next to the upload button so the absence reads as a state rather than a failure to load.
- [x] CHK030 In-flight writes swap the acting button's label ("Uploading…" / "Removing…") and disable both; failures render in the page's existing alert with the server's own reason.

## Feature-specific UI

- [x] CHK031 A team **without** a logo renders byte-identically to before this feature on all six surfaces (each `@else` branch is the untouched original markup).
- [x] CHK032 Remove is **not** in the danger zone. Removing a logo is reversible by uploading another; putting it beside "Delete team" would have borrowed that weight for something that is not irreversible.
- [x] CHK033 The chat inbox falls back to its 2×2 cluster whenever no URL is present, so party, event and manual group conversations are visually unchanged.
- [x] CHK034 The chat conversation header needed **no markup change** — it already rendered `avatar.url` with a placeholder branch. Verified by reading `chat-conversation.component.html` L14-18 rather than by editing it.

## Notes

- **One incidental change, accepted**: the inbox's non-`Direct` branch now renders any avatar URL it
  is given, so a **team-inquiry row seen by an admin** shows the asking player's avatar instead of
  the grey cluster. The server has always sent that URL; only the template ignored it. This is the
  intended reading of `ConversationAvatarDto` and matches how the same conversation is already
  pictured in its header.
- **Deliberate divergence from the avatar editor**: the profile avatar uses a pencil badge overlaid
  on a round picture; this uses labelled buttons in a settings row. The surfaces differ (an 80px
  identity portrait versus a settings list), and DESIGN.md's ≥44px touch-target rule is easier to
  hold honestly with a real button than with an overlaid 32px badge.
