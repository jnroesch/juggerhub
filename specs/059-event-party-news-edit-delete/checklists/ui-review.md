# UI Review Checklist: Event and Party News Posts Can Be Edited and Deleted

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-09-28
**Feature**: [spec.md](../spec.md)

**Surfaces reviewed**: the shared `jh-news-post` controls (per-post menu, in-place editor, delete
dialog) on the event page's News card, the party page's news section, the party news page, and
the team page, which was moved onto them; the edited marker on those three new surfaces and on
Home's news list. **Evidence**: the diff, and a scripted German browser walk (Playwright, one
context per actor, real invites for the event and party co-admins) at **375px, 768px and 1280px**
covering quickstart scenarios 1–11 against a freshly rebuilt compose stack. 84 asserted checks
passed, and the screenshots were reviewed by eye. They stay local to the session rather than being
committed to this public repository, the 052/055/057 practice. The walk found one wrapping defect
(CHK030) and one heading-level defect (CHK036). Both were fixed, and the next pass confirmed them.

## Color & tokens

- [x] CHK001 Semantic aliases only. The controls are 057's markup moved verbatim (`text-body`, `text-muted`, `text-danger-fg`, `bg-surface-card`, `bg-surface-sunken`, `border-border-default`, `ring-focus`, `bg-surface-inverse/40`); the hosts add only existing text tokens
- [x] CHK002 One coral CTA per view. With an editor open the walk found exactly one coral **button** on the event page (*Posten*) and on the party news page (*Update posten*) at all three widths; the only other coral element is the nav's unread badge, which is not a CTA. Save stays `secondary`; the dialog's confirm stays `danger`
- [x] CHK003 No lemon used
- [x] CHK004 Delete confirm is `jhButton variant="danger"`; errors use `text-danger-fg`
- [x] CHK005 No new colour

## Typography, numbers & voice

- [x] CHK006 Inherited faces. The dialog title is now an `h2` (CHK036); `h1`–`h3` share one base rule in `styles.css`, so it looks the same
- [x] CHK007 No new numbers; each page keeps its own date pipe and format
- [x] CHK008 Sentence case; *bearbeitet* is a lower-case continuation of each meta line after the middot. The party page's label keeps its styled uppercase eyebrow (now on an `h2`)
- [x] CHK009 Caption (12px) only for metadata and the editor hint
- [x] CHK010 New copy (R10): *Beim Speichern wird niemand benachrichtigt.* / *…die Crew nicht noch einmal…*; the delete texts say who the post disappears for. Errors keep the enforced *Wir konnten…* voice; no `—` in German; no emoji

## Layout & spacing

- [x] CHK011 Menu trigger `h-11 w-11`, menu items `min-h-11` (unchanged from 057)
- [x] CHK012 Scale tokens only (`mt-sm`, `mt-md`, `gap-xs`, …)
- [x] CHK013 Unchanged page columns
- [x] CHK014 No new sections
- [x] CHK030 Translated text fits, **verified in German at 375px and 768px**. The editor hint wraps on two lines in the narrow party card, the button pairs fit side by side, and the dialog is a bottom sheet at 375px. **Found and fixed**: on both party pages the role label broke at its hyphen (*Party-* / *Admin*) in the meta line this feature lengthened. It is now its own `whitespace-nowrap` unit beside the date+marker unit, the 057 idiom (`party-news.component.html:46`, `party-manage.component.html:161`)
- [x] CHK031 No horizontal scroll with the editor open and with the dialog open, on the event page and the party pages, at 375, 768 and 1280px (walk: `scrollWidth - clientWidth <= 1`). Event and party post bodies gained `break-words`
- [x] CHK032 Nothing fixed-height; the menus use `min-w-40`

## Shape & elevation

- [x] CHK015 Unchanged from 057: buttons `rounded-md`, menu `rounded-md`, dialog `rounded-t-xl` / `sm:rounded-xl`
- [x] CHK016 `shadow-md` (menu), `shadow-xl` (dialog)
- [x] CHK017 The cards are the existing `jhCard`. **The News cards and each party post card now pass `overflowVisible`** (the roster's idiom; none has the accent strip that needs the clip), so a post's menu is not cut off at the card's edge. The walk hit-tested the last menu item under the last, one-line post on all four surfaces at all three widths. On the team page this also fixes a latent 057 clip under a short last post
- [x] CHK018 Larger shadows only on the floating menu and dialog

## Motion & states

- [x] CHK019 `duration-fast` on the trigger; buttons inherit `jhButton`
- [x] CHK020 Visible focus rings on trigger, items, textarea and dialog buttons (moved verbatim)
- [x] CHK021 `jhButton` behaviour, inherited
- [x] CHK022 No loops
- [x] CHK033 No new motion

## Iconography

- [x] CHK023 `jh-icon name="ellipsis"` (Lucide), `sm`
- [x] CHK024 No emoji

## Accessibility

- [x] CHK025 Existing text tokens on white
- [x] CHK026 *Löschen* is a word, not only red; *bearbeitet* is a word
- [x] CHK027 Unchanged from 057: `aria-label` *Beitrag verwalten*, `aria-haspopup`, `aria-expanded`, `role="menuitem"`, textarea label, `role="dialog"` + `aria-modal` + `aria-labelledby`/`aria-describedby` (ids now made unique per post), `role="alert"` errors, `role="status"` notices
- [x] CHK034 No images
- [x] CHK035 Focus: the editor opens focused; Cancel/Save return to the post's menu button; the dialog opens on *Beitrag behalten* (walk-asserted on the event page); Tab stays inside it; Escape closes the menu or the dialog. After a delete, focus lands on each surface's news heading (`event-news-heading`, `party-news-heading`, `team-news-heading`, all `tabindex="-1"`). Walk-asserted on the event page and the party page
- [x] CHK036 **Found and fixed**: 057's dialog title was an `h3`, which is right under the team page's `h2` card headings, but on the party news page the dialog sits directly under the page `h1` and would skip a level. It is now an `h2` everywhere (beside a section `h2` it is a sibling). The party page's news label became a real `h2` (it was a `<p>`), under the page `h1`

## Browser surfaces

- [x] CHK038 The textarea inherits the themed caret and selection

## Empty, loading & error states

- [x] CHK028 Deleting the last post shows each page's existing empty state (*Noch keine Updates.*)
- [x] CHK029 Saving / deleting / failed save (text kept) / failed delete (dialog stays) / gone meanwhile (*Diesen Beitrag gibt es nicht mehr.* as a status line in the news section, walk-asserted, with the menus unlocked afterwards). A failed party post now shows *Wir konnten das nicht posten.*, never the server's English `detail`
- [x] CHK037 No skeletons or spinners added

## Feature-specific UI

- [x] CHK039 Controls for the event's admins on the event page and for the party's admins on both party pages; none for a signed-in player without that role (walk: event page) or for a crew member who is not a party admin (spec)
- [x] CHK040 One behaviour everywhere (FR-022): the four surfaces render the same component; the team page's 057 spec passes unedited
- [x] CHK041 The marker is on the event page, both party pages and Home for event and party posts, and nowhere for a post never edited (walk + specs)
- [x] CHK042 The party dialog names the crew, the alerts and the email copies; the event dialog names the event page and Home (FR-008)
- [x] CHK043 Event and party post bodies render their line breaks and wrap long words (`whitespace-pre-line break-words`); party posts already rendered line breaks, event posts did not. A visible change for existing event posts that contain line breaks, called out in the PR

## Notes

- No DESIGN.md conflicts found.
- The walk's harness (not the product) needed two corrections, both recorded so a future walk does
  not trip on them: a menu under the last post can hang below the **viewport** (scroll it into view
  before a hit test), and the nav's coral unread badge is not a CTA (count buttons and links only).
