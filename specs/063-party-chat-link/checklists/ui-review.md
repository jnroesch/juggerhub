# UI Review Checklist: The Party Page Leads Into the Party Chat

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before a feature is considered done.
**Created**: 2026-09-29
**Feature**: [spec.md](../spec.md)

**Scope of the diff**: `features/parties/party-manage/party-manage.component.html`. The non-admin
"You're in this crew" card gains **Party chat** beside *Leave the party* (now a two-button row). The
admin's readiness card gains **Party chat** after *Apply to event* / *Withdraw from event*. Each card
gets one inline error line, and the page gets one page-level `jh-alert`. *Leave the party* and
*Withdraw from event* move from `size="sm"` (36px) to the default 44px. The diff adds 4 copy keys × 3
catalogues and no new component, style or token.

**Evidence**: a browser walk against the rebuilt compose stack (Playwright, `locale: de-DE`, one
context per actor: party admin, crew member, marketplace guest, no-answer member, declined member,
stranger) at **375px**, **320px** and **1280px**, plus 375px with the root text size doubled. The
final run passed 67 of 69 scripted checks. The 2 failures are pre-existing roster overflow at 200%
text, outside this diff (CHK032). Screenshots 01–17 were read one by one. Walk script deleted
afterwards.

## Color & tokens

- [x] CHK001 Semantic aliases only: `jhButton variant="secondary"`, `text-danger-fg`, `jh-alert tone="info"`. No raw scale step added.
- [x] CHK002 Party chat is never coral (asserted in the spec and the walk: `bg-brand-strong` is on *Für Event bewerben* and not on *Party-Chat*). The crew member's view has no coral at all, as before. **Pre-existing, not introduced here**: the admin's view already had two coral buttons, *Für Event bewerben* and the news composer's *Update posten* (`party-manage.component.html`, news form). This feature adds neither, and it is left for the owner (see Notes).
- [x] CHK003 No lemon used.
- [x] CHK004 The failure line uses `text-danger-fg`, the same idiom as 060's `team-chat-error` and `news-post.component.html`. The not-in-crew note uses `jh-alert tone="info"`, like 060's `team-chat-notice`.
- [x] CHK005 No new colour.

## Typography, numbers & voice

- [x] CHK006 Inherited. No font set in the diff.
- [x] CHK007 No numbers added.
- [x] CHK008 Sentence case: *Party chat*, *Opening…*. German *Party-Chat* (the chat catalogue's own `kindParty`), *Wird geöffnet…*. Spanish *Chat de la party*, *Abriendo…*.
- [x] CHK009 `text-body-sm` (14px) for the error line. The buttons are the default `text-body-md`.
- [x] CHK010 *"We couldn't open the party chat just now."* / *"You're no longer in this crew."*: plain "we"/"you", no shouting, no emoji.

## Layout & spacing

- [x] CHK011 Every button in both cards is ≥ 44px (the walk measured each one at 375px, 320px and 1280px). This is why *Leave the party* and *Withdraw from event* moved off `size="sm"` (36px): a row of mixed heights, and two controls below the touch-target rule. That is a deliberate visible change to two existing buttons (research R6).
- [x] CHK012 `gap-sm`, `mt-sm`, `mt-xs` tokens only.
- [x] CHK013 Page column unchanged.
- [x] CHK014 Unchanged.
- [x] CHK030 **German at 375px**: *Party-Chat*, *Party verlassen*, *Für Event bewerben*, *Vom Event zurückziehen* all in full (01, 02, 05, 07). No label is clipped (`scrollWidth <= clientWidth` per button) or broken over two lines. The first walk showed the crew row wrapping at two unequal content widths. Equal `flex-1` widths then broke *Party verlassen* over two lines inside its button, so the labels are now `whitespace-nowrap` and the row wraps to two full-width buttons instead (02, 11). On desktop the crew row is two equal buttons side by side (14).
- [x] CHK031 `document.scrollWidth > innerWidth` false at 375px and 320px for the crew member and the admin.
- [x] CHK032 **In this diff**: at 320px nothing overflows or clips. At 375px with the root text size doubled, both cards' buttons stay within the card and no label is clipped (17-crew, 17-adm). The admin's *Vom Event zurückziehen* wraps inside its own button there, which is how the existing control always behaved. **Pre-existing, outside the diff**: at doubled text the roster tabs (*Keine Antwort · 0*), the roster chips (*Admin*, *Gast · über Markt*, *· du*) and the app's bottom bar (*Meldun…*) run past the edge. The walk's two FAIL lines are exactly these; nothing in this feature's cards is among them.

## Shape & elevation

- [x] CHK015 Buttons use the directive's `md` radius; the cards are the existing `jh-card`.
- [x] CHK016 No shadow added.
- [x] CHK017 Existing `jh-card`, unchanged.
- [x] CHK018 Nothing new floats.

## Motion & states

- [x] CHK019 No transition added; the directive's own apply.
- [x] CHK020 Focus ring from `jhButton` (native `<button>`s).
- [x] CHK021 Directive-owned hover and press. Screenshot 12 shows the hover fill on *Party-Chat*, because the pointer is still over it after the failed press.
- [x] CHK022 No animation.
- [x] CHK033 No motion added.

## Iconography

- [x] CHK023 No icon added.
- [x] CHK024 No emoji.

## Accessibility

- [x] CHK025 Existing token pairs: the secondary button label on `surface-card`, and `danger-fg` on the white card as in the other inline errors.
- [x] CHK026 Busy is conveyed by the label change (*Wird geöffnet…*) and `disabled`, not by colour alone (03). Failure is a sentence (12).
- [x] CHK027 Native `<button type="button">` elements, keyboard-reachable. The failure line has `role="alert"`, and the notice is a `jh-alert`.
- [x] CHK034 No `<img>` added.
- [x] CHK035 DOM order = visual order. Crew card: sentence → Party chat → Leave → error line. Readiness card: readiness list → Apply/Withdraw → Party chat → error line.
- [x] CHK036 No heading added.

## Browser surfaces

- [x] CHK038 No new scroll region, input or prose link.

## Empty, loading & error states

- [x] CHK028 No empty state involved.
- [x] CHK029 Busy (03), failure (12) and not-in-crew (13) are all styled to the system.
- [x] CHK037 No skeleton or spinner. The busy state is the button's own label.

## Feature-specific UI

- [x] CHK039 **The viewer's own card, at the top** (owner decision): crew member and guest → *Party-Chat · Party verlassen* in the crew card (01, 08). Admin → *Für Event bewerben · Party-Chat*, and after applying *Vom Event zurückziehen · Party-Chat*, in the readiness card (05, 07). The admin gets no crew card. On a phone both are visible without scrolling (02, 06).
- [x] CHK040 **Outside the crew, the page is as before**: a no-answer and a declined member see *Ich bin dabei · Kann nicht* and no Party chat (09, 10). After *Ich bin dabei* the crew card with Party chat appears (11).
- [x] CHK041 **The not-in-crew notice survives the reload that swaps the card**: it renders above the request card (13).
- [x] CHK042 **The button lands in the party's own chat, named**: the landed header reads *Party chat* with the *Party* tag (04). The first walk found it reading "…", with a blank inbox row, which is a pre-existing regression from feature 056 (the fallback names were a `record struct` built with `new()`). It is fixed in this branch with a regression test. The name is the long-standing English fallback, not a German label (046 drift, unchanged).

## Notes

- **Seen, pre-existing, out of scope, filed**: the date line shows the applied signup status as a raw
  English enum, *"beworben · Joined"* (13, 14) → GH #388.
- **Seen, pre-existing, out of scope, for the owner**: two coral buttons in the admin's view
  (*Für Event bewerben* and *Update posten*). DESIGN.md's one-coral-CTA rule is broken there
  independently of this feature.
- **Seen, pre-existing, out of scope**: the roster tabs, roster chips and bottom bar overflow at doubled
  text size (CHK032).
- **Seen, pre-existing, out of scope**: the page's other actions (join, leave, apply, …) still render
  the server's English `detail` on failure (`fail()`, GH #179). The new button does not go through it.
- No conflict with DESIGN.md in this diff.
