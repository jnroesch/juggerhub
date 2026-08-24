# UI Review Checklist: Showcase Image Galleries

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before a feature is considered done.
**Created**: 2026-08-20
**Feature**: [spec.md](../spec.md)

**How to use**: This is an *implementation-quality* gate, run **after** UI is built and
**before** verification. Each item below was checked against the diff; failures record
`file:line`. DESIGN.md is the source of truth — conflicts are reported, not silently resolved.

**Surface under review**: `shared/showcase/showcase-gallery.component.{ts,html,css}`,
`shared/showcase/showcase-manager.component.{ts,html,css}`, and their three call sites
(`profile-view`, `profile-owner`, `teams/team-detail`).

## Color & tokens

- [x] CHK001 Components reference **semantic aliases** (`surface-card`, `text-body`, `brand-primary`, `border-default`…), never raw scale steps (`sand-4`, `coral-5`) — only `surface-card`, `border-border-muted`, `border-border-strong`, `text-body`, `text-muted`, `text-heading` appear
- [x] CHK002 **Exactly one coral `brand-primary` CTA per view**; supporting actions use sage `brand-secondary` — every gallery control is `variant="secondary"`; the sole primary is the caption editor's **Save**, which is the confirming action of an inline form (same reading as this page's existing `approve` / `post news` buttons). See Notes.
- [x] CHK003 Lemon `brand-highlight` is used only for small pops — not used at all
- [x] CHK004 Status uses the paired `*-bg` / `*-border` / `*-fg` tokens — via `jh-alert tone="danger"`, never hand-rolled
- [x] CHK005 No new colors introduced ad hoc — the enlarged view's `bg-black/80` scrim follows the established modal scrim (`bg-black/40`, `team-detail.component.html:250`), deepened because a photo needs a darker ground; no new token

## Typography, numbers & voice

- [x] CHK006 Headings/hero use **Hubot Sans**; body and UI text use **Mona Sans** — headings use the standard `text-body-lg font-semibold text-heading` card-heading treatment
- [x] CHK007 Scores, stats, times, and counts are set in the **mono** face — **no mono is used here, deliberately**. The only number in this UI is inside a sentence ("You've got all 5 — remove one to add another."), and setting a full sentence in the mono face looked wrong on the rendered page. DESIGN.md's mono is for tabular data — scores, times, stats ("5 : 3", "14:00") — not for prose that happens to contain a digit. Recorded rather than silently dropped.
- [x] CHK008 **Sentence case everywhere** — verified across all 33 new keys in en/de/es
- [x] CHK009 Nothing meaningful drops below 12px; body is 16px — captions and counters use `text-body-sm`
- [x] CHK010 Copy addresses the reader as **"you"**, CTAs invite, no emoji — "Show what playing looks like for you", "Add a picture"; no emoji in any of the three catalogues

## Layout & spacing

- [x] CHK011 Interactive controls have a **touch target ≥ 44px** — every control is a `jhButton`, whose directive owns the height
- [x] CHK012 Spacing composes from the 4px scale tokens — `2xs`/`xs`/`sm`/`md` only. The 2px `3xs` half-step was used in the first draft and **removed**: DESIGN.md reserves it for hairline pill insets, not general layout
- [x] CHK013 Content sits in a centered column capped at `container-lg`; mobile-first — the strip inherits the host card's width and scrolls horizontally *within* it (the page itself never scrolls sideways); the enlarged view is capped at `max-w-container-lg`
- [x] CHK014 Section rhythm uses `section-gap` — inherited from the host pages; the gallery is a card inside their existing rhythm

## Shape & elevation

- [x] CHK015 **No sharp corners** — the strip frames and the enlarged picture are `rounded-xl` (media), the manager's list rows and its 64px row thumbnails `rounded-lg`/`rounded-md`, the caption input `rounded-md`
- [x] CHK016 Shadows are the warm-tinted tokens — the gallery adds no shadow of its own; the host `jh-card` supplies it
- [x] CHK017 Cards are white `surface-card` with a 1px muted border and soft shadow, lifting on hover — the gallery lives inside `jh-card` on all three surfaces
- [x] CHK018 Larger shadows reserved for floating elements — the enlarged view floats above a scrim, as the existing modal does

## Motion & states

- [x] CHK019 Transitions use the `fast`/`base`/`slow` durations and token easings — strip-item hover is `duration-200`; the caption input uses `duration-fast`
- [x] CHK020 Focus is always visible: 2px coral border + coral `focus-ring` — `focus:border-brand focus:ring-2 focus:ring-focus` on the strip's picture buttons and the caption input; the strip itself takes `focus-visible:ring-2 focus-visible:ring-focus` when tabbed to, matching the repo's existing inputs
- [x] CHK021 Buttons darken a brand step + glow on hover, nudge on press — owned by the `jhButton` directive
- [x] CHK022 No infinite decorative animation loops — none

## Iconography

- [x] CHK023 Icons are **Lucide line icons** only, 16–22px, `currentColor` — five glyphs added to the curated `ICONS` map (`chevron-left`, `chevron-right`, `arrow-up`, `arrow-down`, `trash`, `image`), rendered through `jh-icon`
- [x] CHK024 No emoji used as UI icons — none

## Accessibility

- [x] CHK025 Body text meets **WCAG AA contrast** — token pairs unchanged; the only new pairing is white-on-`black/80` in the enlarged view
- [x] CHK026 Status is **never conveyed by color alone** — every state carries text (`jh-alert`, the loading line, the counter)
- [x] CHK027 Interactive elements are keyboard-reachable with a visible focus state and appropriate labels/roles — each picture is a `<button>` with an `aria-label`; the strip is focusable and scrolls with the arrow keys, keeps its native list role so a screen reader announces how many pictures there are, and — where it overflows — offers previous/next buttons for pointer users; the enlarged view is `role="dialog" aria-modal="true"` with a label, takes focus on open, pages with arrows, closes on Escape, and **returns focus to the picture it was opened from** (covered by a Jest test)

## Empty, loading & error states

- [x] CHK028 Empty states offer a warm, low-pressure next step — "Show what playing looks like for you — add up to 5 pictures.", shown where pictures are added (profile edit mode, the team card's editing view). For anyone looking rather than editing, an empty gallery is **absent entirely** rather than an empty frame (spec FR-026)
- [x] CHK029 Loading and error states exist and are styled to the system — `jh-loading` (one muted line, never a spinner) and `jh-alert` + "Try again"; an error is never rendered as an empty state

## Feature-specific UI

- [x] CHK030 No picture is ever cropped, in the strip or enlarged (`object-contain` in both) — each sits inside a frame that caps both dimensions and fixes neither, so a panorama comes out short and wide and a portrait tall and narrow. See Notes for why the original uniform grid was replaced
- [x] CHK031 Captions are bound as text, never as markup — member-supplied and therefore untrusted (spec FR-029)
- [x] CHK032 Reordering is keyboard- and touch-operable — move up / move down buttons, disabled at the ends; no drag-and-drop, and no new dependency
- [x] CHK033 The manager is not rendered at all for a viewer who may not edit — not hidden with a class, not disabled (`team-detail.component.html`, guarded by `isAdmin()`)
- [x] CHK035 Looking and editing are never on screen at once — the profile edits its gallery in edit mode (like the avatar), and the team card toggles between the gallery and the editing list. The same picture is never listed twice on one screen
- [x] CHK034 Each upload refusal has its own sentence — full / type / size / unreadable / store-unavailable, never a status code or a technical detail

## Verified against the running app

Screenshots taken from the real stack (docker compose, desktop 1280px and mobile 375px), not
from reading the markup. Two defects were found this way and fixed:

1. **The gallery was editable in view mode, listing the same five pictures twice** — first as
   thumbnails, then as an editing list, while the page's own Edit button implied nothing was
   editable yet. Two rounds fixed this: the controls first moved into the gallery's card (which
   removed the duplicate *heading* but not the duplicate *content*), and then out of view mode
   altogether. The profile now follows the model it already had — view mode shows what a visitor
   sees, and the showcase is edited in **edit mode**, beside the avatar, which is edited the same
   way. The team page has no such mode, so its card carries an explicit **Edit gallery / Done**
   toggle: an admin is either looking at the gallery or changing it, never both.
2. **Mono prose** — see CHK007.
3. **Cramped manager rows at 375 px** — the caption was squeezed to "Te…" and the edit button
   wrapped underneath the arrows. The row now wraps as a unit, dropping the controls onto their
   own right-aligned line, so the caption keeps its width.
4. **⚠ Uploads over 1 MB never reached the application at all** — `location /api/` in
   `frontend/nginx.conf.template` set no `client_max_body_size`, so nginx's 1 MB default rejected
   every upload between 1 MB and the backend's 8 MB `MaxInputBytes` with a raw HTML 413. A phone
   photo — precisely the case the 8 MB cap exists for — failed with an error page instead of the
   application's plain-language reason. Pre-existing (it affected avatars too since 034), found
   only by uploading a realistically sized picture through the real proxy. Fixed by setting the
   limit to 8 MB with a comment tying it to `MaxInputBytes`.

A second mobile pass, after the view/edit rework, walked all six states at 375 px — profile edit
(empty and filled), profile view, the enlarged view, and the team card reading, empty and managing.
Every one measured **0 px** horizontal overflow (SC-007), and two more defects surfaced:

5. **The empty team card was a bare heading and a button**, where every neighbouring card on that
   page ("Recent events", "Badges & achievements") says why it is empty. It now carries the
   invitation.
6. **The shared manager spoke profile copy on a team's gallery** — "Show what playing looks like
   for *you*" for a team. The `emptyTeamOwner` string existed from the start and was never wired
   up; the manager now picks its invitation from the owner kind. Caught only because the empty
   team state had never been looked at.

Uploaded pictures render with `naturalWidth > 0`, i.e. the browser really fetched bytes through the
gated read path.

A third pass replaced the thumbnail grid with the **scroll-snap filmstrip** (owner's choice among
four carousel treatments). Screenshots at 1280 px and 375 px confirm each picture is shown whole,
that the next one peeks in on a phone so the strip reads as scrollable, and that the
previous/next buttons appear on a desktop card — where the frame fills the card and there is no
peek to rely on. Horizontal page overflow remains **0 px** at 375 px.

## Notes

- **CHK002 reading**: DESIGN.md's "one coral CTA per view" is applied here as one primary action
  per *interaction context*, matching how the team page already treats `approve`, `I'm in`, and
  `post news`. Every gallery control that is not a confirming submit is secondary. If the owner
  prefers the stricter reading, the caption editor's Save becomes `variant="secondary"` — a
  one-attribute change.
- **375 px**: the strip and the enlarged view both fit within the viewport with no horizontal page
  scroll (spec SC-007) — verified in device emulation against the running stack, not by reading the
  markup.
- **CHK030 / the grid that was replaced**: the first implementation used `aspect-square
  object-cover` tiles, which satisfied "uniform grid" but crop — undoing, at the last step, the
  reason the stored image is processed with the `Fit` profile rather than square-cropped. Five
  tiles across a narrow card were also too small to show anything. The filmstrip keeps the
  uncropped picture and gives it the room to be seen; the spec's aspect-ratio edge case was
  updated to say *uncropped* rather than *uniform*. The scrolling is CSS (`snap-x` +
  `overflow-x-auto`), so touch, trackpad and keyboard all work with no new dependency.
- No conflict with DESIGN.md was found that required a decision to be escalated.
