# UI Review Checklist: Removing a Member Is Confirmed, and the People It Concerns Are Told

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before a feature is considered done.
**Created**: 2026-09-29
**Feature**: [spec.md](../spec.md)

**Scope of the diff**: one new presentational component, `shared/ui/confirm-dialog/` (`jh-confirm-dialog`),
used three times: Remove on the team roster (`team-detail`), and Remove (In and Declined tabs) and Disband on
the party page (`party-manage`, replacing `window.confirm`). Two new Alerts rows (`notification-row`) with
Lucide's `user-minus` glyph added to the icon map. One page-level `jh-alert` on the team page, one on the
party page, one above the Alerts list and one on the invite page. *My team*, onboarding and Home reuse
their existing notice places for the invitation limit. Copy: about 40 keys × 3 catalogues.

**Evidence**: two browser walks against the rebuilt compose stack (Playwright, `locale: de-DE`, one
context per actor: two admins, five members, one looping joiner) at **375px** and **1280px**. The
first walk found ragged right-aligned answers in the dialog at 375px, fixed in `38435f7`. The second
walk re-shot everything. Every scripted check passed in both runs: focus on the safe answer on open and
back on the asking button after Escape, one notice per recipient, no native `confirm`, one accept request
from the page, and 0px horizontal overflow on all 22 screenshots. Each screenshot was looked at. The walk
script was deleted afterwards.

## Color & tokens

- [x] CHK001 Semantic aliases only: `jhButton variant="secondary"`/`"danger"`, `bg-surface-inverse/40` scrim, `bg-surface-card`, `border-border-muted`, `text-heading`/`text-body`/`text-danger-fg`, `bg-info-bg text-info-fg` for the rows' icon tile (the tone `TeamRoleChanged` uses). No raw scale step added.
- [x] CHK002 No coral added. The destructive answer is the `danger` variant (red text on white), not a primary. The team page's and the party page's existing coral CTAs are unchanged. **Pre-existing, not introduced here**: the party admin's view has two coral buttons (*Für Event bewerben* and *Update posten*), as 063 recorded.
- [x] CHK003 No lemon.
- [x] CHK004 Notes use `jh-alert tone="info"`; dialog errors use `text-danger-fg` with `role="alert"` (news-post's idiom).
- [x] CHK005 No new colour.

## Typography, numbers & voice

- [x] CHK006 Inherited. The dialog heading is `text-body-lg font-semibold text-heading`, as in the page's existing confirmation.
- [x] CHK007 No numbers added.
- [x] CHK008 Sentence case: *Remove from team*, *Keep {name}*, *Disband party*. German: *Aus dem Team entfernen*, *{Name} behalten*, *Party auflösen*.
- [x] CHK009 Body `text-body-sm` (14px) like the page's other confirmations; nothing below 12px.
- [x] CHK010 "you"/"we": *We'll let them know.* / *Wir sagen ihr Bescheid.* No emoji. The party question states *They won't be sent a message about it.*, the honest opposite of the team dialog (owner decision: party removals tell nobody).

## Layout & spacing

- [x] CHK011 Both answers use the default `jhButton` size (`min-h-11`, 44px), asserted in the component spec. `size="sm"` (36px) was deliberately not used (the 063 lesson).
- [x] CHK012 `px-md py-sm`, `gap-sm`, `mt-sm` tokens only.
- [x] CHK013 The dialog is capped at `max-w-container-sm`; a bottom sheet below `sm`, a centred card from `sm` (01/08 at 1280).
- [x] CHK014 No new sections.
- [x] CHK030 German at 375px: the longest heading, *Jonas Weber aus Rheinfeuer Köln entfernen?*, wraps over two lines in full. Both answers show in full. The walk **found** a defect here: wrapped answers sat ragged and right-aligned at two widths. Fixed so each answer is `flex-1 whitespace-nowrap sm:flex-none`: a label never breaks inside its button, and a wrapped row gives each answer the full width (01, 08, 09 at 375). Short pairs share the row (10, *Party behalten · Party auflösen*). The Alerts rows wrap title and supporting line in full (03, 05).
- [x] CHK031 Horizontal overflow measured on every screenshot: 0px at 375 and 1280.
- [ ] CHK032 **Not separately measured.** 200% zoom was not driven. The 375px pass is narrower than a 1280px window at 200% (640 CSS px), and every new element wraps rather than clips there. 063 recorded pre-existing overflow at doubled text on the party page's roster tabs; this diff does not touch them.

## Shape & elevation

- [x] CHK015 `rounded-t-xl` sheet / `sm:rounded-xl` card, the page's existing confirmation shape; buttons `rounded-md` via `jhButton`.
- [x] CHK016 `shadow-xl`, the warm token, for an element that genuinely floats above the page.
- [x] CHK017 Alerts rows are the existing `jh-card`; unchanged.
- [x] CHK018 Only the dialog floats.

## Motion & states

- [x] CHK019 No new transition. The dialog does not animate, like the page's two existing confirmations. DESIGN.md's `pop` names the menus, filter panel and assign dialog, not confirmations. This is recorded as a gap for the owner rather than invented here.
- [x] CHK020 `jhButton`'s `focus-visible:ring-2 ring-focus ring-offset-2` on both answers.
- [x] CHK021 Inherited from `jhButton` (danger hover `bg-danger-bg`, 1px press).
- [x] CHK022 None.
- [x] CHK033 No motion added.

## Iconography

- [x] CHK023 `user-minus` copied verbatim from Lucide (lucide-static 1.48.0), drawn through `jh-icon` at `lg` in both new rows. The icon-system guard (`icon-system.spec.ts`) passes: the glyph is drawn.
- [x] CHK024 No emoji.

## Accessibility

- [x] CHK025 Text tokens unchanged; the danger label is `text-danger-fg` on white, as on every existing danger button.
- [x] CHK026 The departure rows say *left* / *was removed* in words; the icon is decorative (`aria-hidden`).
- [x] CHK027 `role="dialog"`, `aria-modal="true"`, `aria-labelledby` → the heading, `aria-describedby` → the body. The safe answer is focused on open (asserted in three specs and in the walk: `confirm-dialog-keep`). Escape and the safe answer dismiss. While busy, neither answer nor Escape acts.
- [x] CHK034 No images added.
- [x] CHK035 Focus stays inside the dialog (Tab and Shift+Tab wrap, asserted in the spec). On dismiss it returns to the button that asked: the member's ⋯ button, the party's *Entfernen* or *Diese Party auflösen*. This was verified in the walk and the specs. After a removal it lands on the roster heading (`#team-roster-heading`, `tabindex="-1"`).
- [x] CHK036 The dialog heading is an `h2` under the page `h1` on both pages (news-post's reasoning: never skip a level).

## Browser surfaces

- [x] CHK038 Nothing browser-drawn added (no scroll region, input or prose link).

## Empty, loading & error states

- [x] CHK028 No new empty state.
- [x] CHK029 Busy: the destructive answer reads *Wird entfernt…* / *Wird zurückgesetzt…* / *Wird aufgelöst…* and both answers disable. Failure: one line in the dialog in our words, and confirming again is the retry. Gone: a page-level note (07: *Mia Wagner ist nicht mehr im Team.*). The server's `detail` is never rendered (asserted).
- [x] CHK037 No loading placeholder added.

## Feature-specific UI

- [x] CHK039 **Nothing is removed or disbanded without the in-page question.** The team roster (01), party *Dabei* (08), party *Abgesagt* (09) and Disband (10) all ask. `window.confirm` is gone from the party page, and the walk recorded no native dialog.
- [x] CHK040 **The two party questions differ by tab**: *Paul Fischer aus der Party nehmen?* vs *Antwort von Dana Koch zurücksetzen?* with *„Kann nicht" … „Keine Antwort"* in the body (09).
- [x] CHK041 **The removed player's row names the team and nobody else** (03: *Du bist kein Mitglied von Rheinfeuer Köln mehr*). Its email says the same and names no admin (04, checked in the walk: no *Mara* in the HTML).
- [x] CHK042 **The admins' rows name the player and say how they went, never by whom** (05: *Lena Hoffmann hat Rheinfeuer Köln verlassen*, *Jonas Weber wurde aus Rheinfeuer Köln entfernt*). The acting admin received none (walk: Mara's inbox has only Lena's leave).
- [x] CHK043 **The invitation limit is told in the player's language and never retried** (11: *Du bist in kurzer Zeit vielen Teams beigetreten. Versuch es später noch einmal.*; one accept request in the network log).

## Notes

- **Seen, pre-existing, filed**: after Tom was *promoted to admin*, his role-change alert reads *Du bist
  jetzt Mitglied von Rheinfeuer Köln* (05). This is GH #370: payload enums are stored as numbers, so
  every promotion reads "member". Not touched here.
- **Seen, pre-existing, for the owner**: two coral buttons in the party admin's view (063 recorded it).
- **Not moved onto `jh-confirm-dialog`** (out of scope, follow-up GH #392): news-post's delete dialog and the
  team page's join/cancel confirmation. The latter still lacks the focus rules this component has.
- **DESIGN.md gap**: it has no confirmation-dialog spec and no motion for one. `jh-confirm-dialog`
  follows the product's two existing confirmations.
