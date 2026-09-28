# UI Review Checklist: Team Polls

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-09-28
**Feature**: [spec.md](../spec.md)

**Surfaces**:
- the team page's **Polls** card (`features/teams/team-detail/polls/team-polls.component.html`);
- one poll (`poll-item.component.html`), including its admin menu and its close/delete dialog;
- the create/edit form (`poll-editor.component.html`);
- the Alerts row for `TeamPoll` (`notification-row.component.*`);
- Home's *Braucht dich* item (`needs-you-card.component.*`).

**How verified**:
- the diff and the component specs;
- a real browser walk on the rebuilt Docker stack in German (`locale: de-DE`) at 375px and 1280px, with one browser context per actor (author admin, second admin, two members, a non-member).

The walk made 43 automated checks: overflow at 375px on every screen, touch-target heights, focus after a deep link and in the dialog, and what each role sees. It produced 25 screenshots, and every one was looked at. The final run passed 43/43; three earlier runs found the fixes listed under Notes.

## Color & tokens

- [x] CHK001 Semantic aliases only: `surface-card`, `surface-accent-soft` (the result bar), `border-border-accent` (the chosen option), `border-border-default`, `text-body`, `text-muted`, `text-heading`, `text-danger-fg`, `bg-surface-inverse/40` (the dialog scrim, as in `jh-news-post`). No raw scale steps.
- [x] CHK002 **One coral CTA per view.** *Umfrage starten* and the editor's submit are `secondary`; *News posten* remains the page's coral button. **Flagged for the owner:** the editor's three two-way choices use the selected state of the team-type segmented control (`bg-brand-strong`), copied unchanged from team settings and the creation wizard. It is a state, not a call to action, but three coral segments in one form is heavy. If that control's selected state should be sage (DESIGN.md: "toggles … sage"), it is a change to the shared pattern, not to this feature.
- [x] CHK003 No lemon.
- [x] CHK004 Status uses paired tokens:
  - the load error is `jh-alert` (danger);
  - the "deleted meanwhile" notice is `jh-alert tone="info"`;
  - refusals are `text-danger-fg` text;
  - an invalid field takes `border-danger-border`.
- [x] CHK005 No new colours.

## Typography, numbers & voice

- [x] CHK006 Headings and body come from the base layer (Hubot/Mona).
- [x] CHK007 Per-option counts are `font-mono`, as DESIGN.md reserves mono for numbers. The "N von M haben geantwortet" line is a sentence and stays in the body face. *Pre-existing*: `font-mono` renders in the platform fallback (#339).
- [x] CHK008 Sentence case throughout. Uppercase only in the editor's eyebrow labels (the team-settings pattern).
- [x] CHK009 Captions (meta line, answered line, voters, not-answered, hints) are `caption`/`body-sm`, never below 12px.
- [x] CHK010 "Du/you" voice in all three languages. Errors say what happened and what to do ("Das hat gerade nicht geklappt. Versuch es bitte noch einmal."). `catalog-parity`, `catalog-punctuation` and `legal-catalog` pass. No emoji.

## Layout & spacing

- [x] CHK011 **Touch targets ≥ 44px**, measured at 375px:
  - option buttons 110 (at 80 characters; one line is 50);
  - menu trigger 44, *Umfrage starten* 50;
  - *Antwort speichern* 50, *Meine Antwort zurückziehen* 72 (wrapped);
  - editor: inputs 50, remove 44, *Antwort hinzufügen* 50, the two-way choices 66, closing time 52, *Abbrechen* 48, submit 50;
  - dialog buttons 50.

  The first walk showed `size="sm"` buttons at 36px, so every poll button moved to the default size (061's precedent). Home's *Antworten* keeps `sm` to match the Accept/Decline buttons beside it in the same card (pre-existing pattern).
- [x] CHK012 Spacing uses the scale tokens (`2xs`/`xs`/`sm`/`md`/`lg`). The eyebrow's `tracking-[0.06em]` is the team-settings label, copied.
- [x] CHK013 The card sits in the team page's existing main column. Mobile-first: the choices are a two-column grid that holds at 375px.
- [x] CHK014 No new sections; the card follows the page's existing `gap-lg` rhythm.
- [x] CHK030 **Translated text fits** (German at 375px, the binding case):
  - a 118-character question and ten 80-character options wrap inside their buttons, with the count kept at the right;
  - the not-answered list wraps;
  - the chips wrap onto a second line (*Anonym* / *Ergebnis nach deiner Antwort*);
  - the dialog text wraps.

  Nothing a reader acts on is truncated. The editor's option inputs are single-line fields that scroll their own text while typing, which is the same as the team-settings link fields.
- [x] CHK031 `scrollWidth - clientWidth = 0` on all 14 walked screens at 375px.
- [x] CHK032 Every container is fluid (`min-w-0`, `break-words`, no fixed widths), so larger text and zoom reflow instead of clipping. The walk checked 375px, which is the narrow end that zoom produces.

## Shape & elevation

- [x] CHK015 Radii: options, inputs and buttons `md`; the card `lg` (`jh-card`); chips `pill`; the dialog `xl` top corners (bottom sheet), as in `jh-news-post`.
- [x] CHK016 Shadows come from the tokens: the menu `shadow-md`, the dialog `shadow-xl`.
- [x] CHK017 `jh-card` as-is. It holds buttons but is not itself a link, so it does not lift (DESIGN.md: "a card that merely contains links must not lift").
- [x] CHK018 Larger shadows only on the floating menu and the dialog.

## Motion & states

- [x] CHK019 `transition-colors duration-fast` only (options, segmented choices, menu items).
- [x] CHK020 Focus is visible:
  - option buttons, menu items and the menu trigger have `focus-visible:ring-2 ring-focus`;
  - inputs have `focus:border-border-focus focus:ring-2`;
  - `jhButton` carries its own ring;
  - the anchored question heading is `tabindex=-1` with `outline-none`, a programmatic target only, as for the news heading.
- [x] CHK021 Buttons are `jhButton` (hover and press come from the primitive).
- [x] CHK022 No looping animation.
- [x] CHK033 No movement is added. The only transitions are colour fades.

## Iconography

- [x] CHK023 Lucide line icons already in the set: `ellipsis`, `check`, `plus`, `x`, and `circle-help` for the Alerts row. None added.
- [x] CHK024 No emoji.

## Accessibility

- [x] CHK025 Text uses `text-body`/`text-muted`/`text-heading` on `surface-card`. The result bar is `surface-accent-soft` *behind* body text, the pairing `jh-card`'s soft surfaces already use.
- [x] CHK026 The chosen option is never colour alone: a `check` icon plus sr-only "Deine Antwort", and `aria-pressed`. Hidden results say so in words. Closed carries a chip.
- [x] CHK027 Keyboard access:
  - options are `<button aria-pressed>` in a `role="group"` named by the question;
  - the menu has `aria-haspopup`/`aria-expanded`/`role="menu"` and closes on Escape and on an outside click (its own wrapper);
  - the dialog is `role="dialog" aria-modal`, labelled and described, traps Tab, closes on Escape, and focuses the safe answer (walk: `poll-confirm-keep`);
  - each two-way choice is a `role="group"` labelled by its heading, with `aria-pressed` buttons;
  - every editor input has a label or `aria-label` ("Antwort 3").
- [x] CHK034 No images.
- [x] CHK035 DOM order is visual order.
  - After a delete, focus returns to the card heading.
  - After a create, focus moves to the new poll's heading.
  - After a deep link, focus moves to the linked poll's heading (walk: `focused=poll-…`).
  - After closing a dialog, focus returns to the menu trigger.
- [x] CHK036 The team page's `h1` is followed by the card's `h2` "Umfragen", then each poll's `h3` and the editor's `h3` "Neue Umfrage". The dialog heading is an `h2`, a sibling at page level as in `jh-news-post`.

## Browser surfaces

- [x] CHK038 Inputs and the dialog inherit the base layer (selection, caret, scrollbar). Voter and not-answered names are ordinary links with the base underline offset.

## Empty, loading & error states

- [x] CHK028 Empty:
  - members see "Noch keine Umfragen.";
  - admins see an invitation to ask, with the consequence ("… und alle bekommen eine Nachricht").
- [x] CHK029 Load error: "Wir konnten die Umfragen nicht laden." plus *Erneut versuchen*. An action error is an inline sentence chosen by `code`, never the server's `detail`.
- [x] CHK037 Loading is `jh-loading`; there is no skeleton.

## Feature-specific UI

- [x] CHK039 **Anonymity is stated before answering.** The chip reads *Anonym* / *Namen sichtbar*. An anonymous poll adds "Niemand sieht, wer was gewählt hat." The promise is about **seeing**, never "can't be worked out" (FR-019).
- [x] CHK040 **Hidden results show no numbers.** With *Ergebnis nach deiner Antwort*, before answering there are no bars and no counts, only "N von M haben geantwortet" and the hint. The server sends none either (`TeamPollPrivacyTests`).
- [x] CHK041 **The locked editor reads as locked.** After the first answer, the question and option fields are disabled with a sunken fill, the two-way choices are at 60% opacity, and a sunken note says only the closing time can change. Anonymity is shown as a fixed sentence, never as a control.
- [x] CHK042 **Deep links land on the poll** from Home, the Alerts row, the email and a device notice. If the page is already open and the poll is newer than it, the card fetches once more (found by the walk; see Notes).

## Notes

**What the walk found, and what changed:**

1. **A link to a poll newer than the open page landed nowhere.** A member already on the team page who follows a link to a poll started since the page loaded only changes the URL fragment, so the card never had that poll. The card now fetches its lists once more when a link names a poll it does not hold. It does this at most once per fragment, so a deleted poll cannot loop. A spec covers it.
2. **`size="sm"` buttons measured 36px.** Every poll button moved to the default 44px+ size.
3. **The locked editor's choices looked active.** They now render at 60% opacity when disabled.
4. **The editor's notice caption pushed its two buttons onto separate lines at 375px.** It now sits on its own line above them.
5. **The anchored heading sat flush against the top edge after the scroll.** It now has `scroll-mt-lg`.

**Walk-harness lessons (not product defects):**
- A second-actor email check failed until that member **saved** German. Emails follow the saved language (058's pattern), not the browser's.
- `page.goto` to the same path with a new fragment is a same-document navigation, which is how finding 1 surfaced.

**Pre-existing, not this feature:**
- `font-mono` falls back to the platform font (#339).
- The native `datetime-local` placeholder follows the browser's UI locale, not the page's.
