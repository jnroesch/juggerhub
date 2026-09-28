# UI Review Checklist: Team News Posts Can Be Edited and Deleted

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-09-28
**Feature**: [spec.md](../spec.md)

**Surfaces reviewed**: the team page's News card (per-post menu, in-place editor, "edited"
marker, notice line), the delete dialog, and Home's news list (dashboard module and
"See all"). **Evidence**: the diff, and a scripted German browser walk (Playwright, one
context per actor) at 375px, 768px and 1280px covering quickstart scenarios 1–10 against a
freshly rebuilt compose stack. 21 asserted checks passed, and all 26 screenshots were reviewed
by eye. They stay local to the session rather than being committed to this public
repository, the 052/055 practice. The walk's first pass found two layout defects
(CHK030, CHK031); both were fixed, and the second pass confirmed the fixes on screen.

## Color & tokens

- [x] CHK001 Semantic aliases only. New markup uses `text-body`, `text-muted`, `text-danger-fg`, `bg-surface-card`, `bg-surface-sunken`, `border-border-default`, `ring-focus`, `bg-surface-inverse/40` (scrim token); no raw scale steps
- [x] CHK002 One coral CTA per view. With the editor open, the composer's **Post news** stays the only coral button: the walk counted 1 at 375px. The editor's Save is `secondary`, and the dialog's confirm is `danger`, not primary (053 precedent, research R9)
- [x] CHK003 No lemon used
- [x] CHK004 The delete confirm uses `jhButton variant="danger"` (the paired `danger-fg`/`danger-border`/`danger-bg` tokens); errors use `text-danger-fg`
- [x] CHK005 No new colour

## Typography, numbers & voice

- [x] CHK006 Inherited faces; the dialog title reuses the join dialog's heading classes
- [x] CHK007 No new numbers or times (the date keeps the page's existing date pipe)
- [x] CHK008 Sentence case. "edited" / "bearbeitet" / "editada" is a lower-case continuation of the meta line after the middot joint, like "jetzt" beside it
- [x] CHK009 Caption (12px) only for metadata (meta line, editor hint); the post body stays `body-sm` like before
- [x] CHK010 "You" voice. Errors open "We couldn't … Try again." / "Wir konnten …" / "No pudimos …" (the one error voice `catalog-punctuation.spec.ts` enforces); German uses no `—`; no emoji

## Layout & spacing

- [x] CHK011 Touch targets ≥ 44px. The menu trigger is `h-11 w-11` (the `event-results` icon-button classes) and the menu items are `min-h-11`. *Note*: the roster's own menu on the same page is still 32px / 36px; aligning it is outside this feature's scope
- [x] CHK012 Scale tokens (`gap-xs`, `mt-xs`, `mt-sm`, `mb-sm`, `px-md`, `py-xs`); `scale-keys.spec.ts` passes
- [x] CHK013 Unchanged page column
- [x] CHK014 No new sections
- [x] CHK030 Translated text fits, **verified in German at 375px and 768px**: the editor hint wraps on two lines, "Abbrechen" / "Speichern" and "Beitrag behalten" / "Beitrag löschen" fit side by side, and the menu uses `min-w-40`, not a fixed width. The walk's first pass found the meta line breaking inside the date ("28. Sept." / "2026") and a stranded middot before "bearbeitet"; **fixed** by wrapping the date plus marker as one unit (`whitespace-nowrap`) on the team page and on Home
- [x] CHK031 No horizontal scroll at 375px with the editor open and with the dialog open, nor at 768px and 1280px (walk: `scrollWidth - clientWidth <= 1`). Long unbroken text (a URL) wraps inside the post column (`break-words`). The walk also found a pre-existing overflow of the same kind in Home's "See all" list; **fixed** with the same class
- [x] CHK032 Nothing fixed-height. The one unbreakable run (date + marker, ~165px) fits the narrowest column (~243px)

## Shape & elevation

- [x] CHK015 Buttons `rounded-md` via `jhButton`; menu `rounded-md`; dialog `rounded-t-xl` / `sm:rounded-xl` (the join dialog's)
- [x] CHK016 `shadow-md` (menu) and `shadow-xl` (dialog), the existing tokens
- [x] CHK017 The News card is the existing `jh-card`
- [x] CHK018 Larger shadows only on the floating menu and dialog

## Motion & states

- [x] CHK019 Hover transitions use `duration-fast` (trigger); buttons inherit `jhButton`'s
- [x] CHK020 Visible focus: the trigger and menu items carry `focus-visible:ring-2 ring-focus`; the textarea uses the composer's focus ring; the dialog buttons use `jhButton`'s ring (seen on "Beitrag behalten" in the 375px walk)
- [x] CHK021 `jhButton` press and hover behaviour, inherited
- [x] CHK022 No loops
- [x] CHK033 No new motion. The menu does not `pop`, matching the roster menu on the same page; DESIGN.md's `pop` list names neither

## Iconography

- [x] CHK023 The trigger is `jh-icon name="ellipsis"` (Lucide), `sm`, as on the roster
- [x] CHK024 No emoji; no `⋯` typed as text

## Accessibility

- [x] CHK025 Existing text tokens on white
- [x] CHK026 The danger item says "Löschen" in words, not only in red; the edited state is the word "bearbeitet"
- [x] CHK027 Trigger: `aria-label` "Beitrag verwalten", `aria-haspopup="menu"`, `aria-expanded`; items `role="menuitem"`; textarea `aria-label` "Text des Beitrags"; dialog `role="dialog"`, `aria-modal`, `aria-labelledby` / `aria-describedby`; errors `role="alert"`, notice `role="status"`
- [x] CHK034 No images added
- [x] CHK035 Focus order matches the visuals, verified in the walk. Opening the editor focuses the textarea; Cancel and Save return focus to the post's menu button. The dialog opens on **Keep** (Enter on arrival cannot delete), and Tab / Shift+Tab stay inside it. Escape closes the menu and the dialog and returns focus to the button that opened them. After a delete, focus lands on the News heading (`tabindex="-1"`), not on a removed element
- [x] CHK036 Dialog title `h3` under the page's `h2` card headings, like the join dialog

## Browser surfaces

- [x] CHK038 The textarea inherits the themed caret and selection; no overrides

## Empty, loading & error states

- [x] CHK028 Deleting the last post shows the existing "Noch keine Neuigkeiten." empty state
- [x] CHK029 States exist and are styled: saving ("Wird gespeichert…"), deleting ("Wird gelöscht…"), a failed save (text kept, inline error), a failed delete (dialog stays, inline error), and a post gone meanwhile (a status line in the News card)
- [x] CHK037 No skeletons or spinners added

## Feature-specific UI

- [x] CHK039 A plain member sees no menu on any post (walk + spec); admins see one on **every** post, whoever wrote it (FR-013/FR-014)
- [x] CHK040 While a post is being edited, every post's menu button is disabled, so choosing another post can never silently discard the text being typed
- [x] CHK041 The marker sits in the meta line at `caption` / `text-muted` on the team page, the Home module and "See all", and never on event or party items (spec + walk)
- [x] CHK042 The dialog states both FR-008 facts: the post disappears for the whole team, and emailed copies stay with their recipients
- [x] CHK043 Post bodies render their line breaks (`whitespace-pre-line`, as party news already does). This is a deliberate visible change for existing posts that contain line breaks, called out in the PR

## Notes

- No DESIGN.md conflicts found.
- Two gaps outside this feature's scope, recorded rather than fixed: the roster menu's 32px
  trigger and 36px items (CHK011), and neither team-page menu using `pop` (CHK033). Aligning
  the roster menu with the news menu is a small follow-up.
