# UI Review Checklist: Team Details — Editable Name, Type and City, a Description, and Links

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-09-28
**Feature**: [spec.md](../spec.md)

**Surfaces**:
- the **Team details** section of Manage team (`team-settings.component.html`);
- the **About** card on the team page (`team-detail.component.html`);
- the wizard's **about** step (`team-create.component.html`).

**How verified**: the diff, the component specs, and a real browser walk. The walk ran on the rebuilt Docker stack in German (`locale: de-DE`) at 375px, `md` (768px), 640px (≈200% zoom on a 1280px screen) and 1280px, with one browser context per actor (admin, member, non-member). It made 36 automated checks and 16 screenshots, and every screenshot was looked at. The final run passed 36/36.

## Color & tokens

- [x] CHK001 Components use **semantic aliases** only: `surface-card`, `border-border-strong`, `text-body`, `text-muted`, `text-link`, `info-*`, `danger-*`, `success-fg`. No raw scale steps.
- [x] CHK002 **Exactly one coral CTA per view.**
  - Settings: **Speichern** is the only primary. The selected half of the type control uses `bg-brand-strong`, which is the wizard's existing segmented control, copied unchanged.
  - Wizard about step: **Weiter**/**Überspringen** is the primary, and Skip is `secondary` when it appears.
  - Team page: the About card adds no button. The links are text links.
- [x] CHK003 No lemon.
- [x] CHK004 Status uses the paired tokens:
  - the error is a `jh-alert` (danger);
  - the removed-admin notice is a `jh-alert tone="info"`;
  - an invalid link row takes `border-danger-border`;
  - *Gespeichert* uses `text-success-fg` with a `check` icon.
- [x] CHK005 No new colours.

## Typography, numbers & voice

- [x] CHK006 Headings come from the base layer. Body and UI text are Mona Sans.
- [x] CHK007 The description counter (a count) is `font-mono`. The **address inputs are NOT mono** (changed after the walk): DESIGN.md reserves mono for numbers, scores, times and counts, not addresses.
  - *Pre-existing, not this feature*: `font-mono` renders in the platform fallback because "Mona Sans Mono" is never loaded. This is **#339**.
- [x] CHK008 Sentence case throughout. Uppercase appears only as the eyebrow style on the section and field labels (the page's existing pattern).
- [x] CHK009 Link hosts and the counter are `caption` (12px), the smallest step. The description is `body-md`, because it is prose.
- [x] CHK010 "You/we" voice in all three languages. Errors open with "We couldn't…" / "Das konnten wir…" / "No pudimos…" (`catalog-punctuation.spec.ts` passes). No emoji.

## Layout & spacing

- [x] CHK011 **Touch targets ≥ 44px**, measured at 375px:
  - name 50, label 50, address 50;
  - type toggle 45;
  - remove 44, **Link hinzufügen 50**, Speichern 48.

  *Link hinzufügen* measured **39px** at `size="sm"` in the first walk and was changed to the default size. The About links are 41px link blocks: text links, which DESIGN.md's 44px rule (buttons and inputs) does not cover. They are above WCAG 2.5.8's 24px.
- [x] CHK012 Spacing uses the scale tokens (`xs`/`sm`/`md`/`2xs`). The one reserved step `3xs` was replaced with `2xs` before the walk. The arbitrary grid templates (`[auto_minmax(0,1fr)]`, `sm:[minmax(0,2fr)_minmax(0,3fr)]`) follow the team page's existing `lg:grid-cols-[minmax(0,1fr)_300px]`.
- [x] CHK013 Existing page containers are unchanged: settings and wizard `container-sm`, team page `container-md`.
- [x] CHK014 No new page-level sections, so the rhythm is unchanged.
- [x] CHK030 **German fits at 375px and `md`** (screenshots 01–05, 08, 10–16):
  - the section with five link rows (label and address stacked at 375, 2:3 from `sm`);
  - the refusal "Link 3 braucht eine sichere Webadresse, die mit https:// beginnt.";
  - the About card with a 28-character label and a 37-character host (it wraps, nothing cut);
  - the wizard step (blank, typed, failed).

  An address or label longer than its input scrolls inside the input, which is normal input behaviour. It is not truncated text.
- [x] CHK031 No horizontal scroll: settings, team page and wizard at 375, and settings and team page at `md`. Measured `scrollWidth - clientWidth ≤ 1`.
- [x] CHK032 No horizontal scroll on the settings page at 640px (≈200% zoom on a 1280px screen).

## Shape & elevation

- [x] CHK015 Inputs, buttons and the link rows' focus shape are `md`/`sm`. The About card is `jh-card` (`lg`). The segmented control reuses the wizard's.
- [x] CHK016 No new shadows.
- [x] CHK017 The About card is a plain `jh-card`. It contains links but is not itself a link, so it correctly does **not** lift. The settings section uses the page's existing bordered box.
- [x] CHK018 No floating elements added.

## Motion & states

- [x] CHK019 `duration-fast` on colour and shadow transitions. The wizard step enters through the existing `stepMotion`.
- [x] CHK020 Focus is visible everywhere:
  - inputs use `focus:border-border-focus focus:ring-2 focus:ring-focus`;
  - buttons come from `jhButton`;
  - **About links carry `focus-visible:ring-2 ring-focus ring-offset-2`**. The walk measured the ring on keyboard focus (screenshot 16).
- [x] CHK021 Buttons are `jhButton` (hover and press come from the primitive).
- [x] CHK022 No looping animation.
- [x] CHK033 No new motion. The wizard step uses `stepMotion`, which already honours reduced motion.

## Iconography

- [x] CHK023 `jh-icon` only: `external-link` (sm), `x`, `plus` (sm), `check` (sm). All are already in the Lucide set.
- [x] CHK024 No emoji.

## Accessibility

- [x] CHK025 Only measured token pairs (`contrast.spec.ts`): `text-link` and `text-muted` on `surface-card`.
- [x] CHK026 Never colour alone:
  - an invalid row has a red border **and** an error that names the row ("Link 3 …") **and** `aria-invalid`;
  - *Gespeichert* is text plus an icon.
- [x] CHK027 Labels and roles are in place:
  - every input has a `<label for>` or an `aria-label` ("Name von Link 3", "Adresse von Link 3");
  - the remove buttons are labelled per row;
  - the type buttons carry `aria-pressed`;
  - the links list is labelled;
  - each external link carries an `sr-only` "(öffnet in einem neuen Tab)", and the host is inside the link, so it is announced.
- [x] CHK034 No `<img>` added.
- [x] CHK035 DOM order matches visual order. Tab reaches each About link in list order. No trap.
- [x] CHK036 Headings:
  - team page: `h1` (name) → `h2` "Über das Team";
  - wizard: `h1` per step, as the others;
  - settings: the page's existing pattern of an `h1` plus eyebrow labels.

## Browser surfaces

- [x] CHK038 The About links inherit the base layer's `text-underline-offset: 0.18em`. An explicit `underline-offset-2` was removed so the base rule applies. The textarea and inputs inherit the themed caret and selection.

## Empty, loading & error states

- [x] CHK028 A team with neither a description nor links shows **no** About card (FR-014, by design). Admins reach both from *Verwalten* in the team-tools card, following the owner's rule that members' actions live in that card. There is no empty-state nudge on the page.
- [x] CHK029 Errors are styled `jh-alert`s:
  - the settings section maps the server's `code` to German copy (never the English `detail`, #179);
  - the wizard step shows its own copy and keeps the text.
- [x] CHK037 No skeletons. The page's existing `jh-loading` is unchanged.

## Feature-specific UI

- [x] CHK039 Each link shows its **real host** (punycode for lookalikes: `xn--nstagram-shh.com` in the walk) under its label. The link opens with `target="_blank" rel="noopener noreferrer nofollow ugc"`; in the popup the walk measured `window.opener === null` and `document.referrer === ''`.
- [x] CHK040 The description renders as literal text: `**fett**` and `www.example.com` stay text, and the line break is kept (`whitespace-pre-line`).
- [x] CHK041 Team details is the page's **first** section and is admin-only. A plain member sees none of it (walk + spec).
- [x] CHK042 The wizard's about step has one control (Skip ↔ Continue by content), matching the logo step, and a secondary Skip only after a failed save.

## Notes

- **Changed because of the walk** (none of these could be seen in the specs):
  1. The host moved from beside the label to under it. A long label stranded a leading "·" at 375px.
  2. The address inputs lost `font-mono` (DESIGN.md scope, and #339's fallback face).
  3. The label/address split became 2:3, because 10rem clipped a 28-character label on desktop.
  4. *Link hinzufügen* went from 39px to 50px.
  5. An explicit underline offset was removed.
  6. A focus ring was added to the About links.
- **Harness lesson**: one check failed on a re-run because `isVisible()` does not wait, and in the zoneless app the re-render lands a tick after the click. The screenshot showed the correct state. The fix was to wait for the element, never to weaken the check.
- The city lookup in the walk took the first search result for "Köln", which was "Kolno, Poland". That is a harness shortcut, not a product defect: the picker's ranking decides, and a person picks from the list.
- No conflict with DESIGN.md was found.
