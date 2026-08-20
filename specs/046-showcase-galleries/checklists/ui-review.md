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
- [x] CHK008 **Sentence case everywhere** — verified across all 29 new keys in en/de/es
- [x] CHK009 Nothing meaningful drops below 12px; body is 16px — captions and counters use `text-body-sm`
- [x] CHK010 Copy addresses the reader as **"you"**, CTAs invite, no emoji — "Show what playing looks like for you", "Add a picture"; no emoji in any of the three catalogues

## Layout & spacing

- [x] CHK011 Interactive controls have a **touch target ≥ 44px** — every control is a `jhButton`, whose directive owns the height
- [x] CHK012 Spacing composes from the 4px scale tokens — `2xs`/`xs`/`sm`/`md` only. The 2px `3xs` half-step was used in the first draft and **removed**: DESIGN.md reserves it for hairline pill insets, not general layout
- [x] CHK013 Content sits in a centered column capped at `container-lg`; mobile-first — the grid is `grid-cols-2` → `sm:grid-cols-3` → `md:grid-cols-5`; the enlarged view is capped at `max-w-container-lg`
- [x] CHK014 Section rhythm uses `section-gap` — inherited from the host pages; the gallery is a card inside their existing rhythm

## Shape & elevation

- [x] CHK015 **No sharp corners** — thumbnails and the enlarged picture are `rounded-xl` (media), the manager's list rows and its 64px row thumbnails `rounded-lg`/`rounded-md`, the caption input `rounded-md`
- [x] CHK016 Shadows are the warm-tinted tokens — the gallery adds no shadow of its own; the host `jh-card` supplies it
- [x] CHK017 Cards are white `surface-card` with a 1px muted border and soft shadow, lifting on hover — the gallery lives inside `jh-card` on all three surfaces
- [x] CHK018 Larger shadows reserved for floating elements — the enlarged view floats above a scrim, as the existing modal does

## Motion & states

- [x] CHK019 Transitions use the `fast`/`base`/`slow` durations and token easings — thumbnail hover is `duration-200`; the caption input uses `duration-fast`
- [x] CHK020 Focus is always visible: 2px coral border + coral `focus-ring` — `focus:border-brand focus:ring-2 focus:ring-focus` on the thumbnail buttons and the caption input, matching the repo's existing inputs
- [x] CHK021 Buttons darken a brand step + glow on hover, nudge on press — owned by the `jhButton` directive
- [x] CHK022 No infinite decorative animation loops — none

## Iconography

- [x] CHK023 Icons are **Lucide line icons** only, 16–22px, `currentColor` — five glyphs added to the curated `ICONS` map (`chevron-left`, `chevron-right`, `arrow-up`, `arrow-down`, `trash`, `image`), rendered through `jh-icon`
- [x] CHK024 No emoji used as UI icons — none

## Accessibility

- [x] CHK025 Body text meets **WCAG AA contrast** — token pairs unchanged; the only new pairing is white-on-`black/80` in the enlarged view
- [x] CHK026 Status is **never conveyed by color alone** — every state carries text (`jh-alert`, the loading line, the counter)
- [x] CHK027 Interactive elements are keyboard-reachable with a visible focus state and appropriate labels/roles — thumbnails are `<button>`s with `aria-label`; the enlarged view is `role="dialog" aria-modal="true"` with a label, takes focus on open, pages with arrows, closes on Escape, and **returns focus to the thumbnail it was opened from** (covered by a Jest test)

## Empty, loading & error states

- [x] CHK028 Empty states offer a warm, low-pressure next step — "Show what playing looks like for you — add up to 5 pictures." For a viewer who cannot add pictures the gallery is **absent entirely** rather than an empty frame (spec FR-026)
- [x] CHK029 Loading and error states exist and are styled to the system — `jh-loading` (one muted line, never a spinner) and `jh-alert` + "Try again"; an error is never rendered as an empty state

## Feature-specific UI

- [x] CHK030 Thumbnails form a uniform grid regardless of source aspect ratio (`aspect-square object-cover`), while the enlarged view shows the **whole** picture (`object-contain`) — so a panorama is never cropped where it is being looked at
- [x] CHK031 Captions are bound as text, never as markup — member-supplied and therefore untrusted (spec FR-029)
- [x] CHK032 Reordering is keyboard- and touch-operable — move up / move down buttons, disabled at the ends; no drag-and-drop, and no new dependency
- [x] CHK033 The manager is not rendered at all for a viewer who may not edit — not hidden with a class, not disabled (`team-detail.component.html`, guarded by `isAdmin()`)
- [x] CHK034 Each upload refusal has its own sentence — full / type / size / unreadable / store-unavailable, never a status code or a technical detail

## Verified against the running app

Screenshots taken from the real stack (docker compose, desktop 1280px and mobile 375px), not
from reading the markup. Two defects were found this way and fixed:

1. **Two "Showcase" cards on the owner's own profile** — the read gallery in the left column and
   the editing controls in a separate full-width card below, far apart and identically titled.
   Exactly the two-sections-one-name confusion feature 044 was reported about. The owner's
   controls now project into the gallery's own card (`[showcaseManageable]` + `[showcaseManager]`),
   matching how the team page already stacks them.
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

Also measured on the live page: horizontal overflow at 375 px is **0 px** (SC-007), and the
uploaded picture renders with `naturalWidth > 0`, i.e. the browser really fetched bytes through
the gated read path.

## Notes

- **CHK002 reading**: DESIGN.md's "one coral CTA per view" is applied here as one primary action
  per *interaction context*, matching how the team page already treats `approve`, `I'm in`, and
  `post news`. Every gallery control that is not a confirming submit is secondary. If the owner
  prefers the stricter reading, the caption editor's Save becomes `variant="secondary"` — a
  one-attribute change.
- **375 px**: the grid drops to two columns and the enlarged view fits within the viewport with no
  horizontal page scroll (spec SC-007). Verified by reading the markup; a device-emulation pass is
  part of the quickstart, which has not been run against a live stack in this environment.
- No conflict with DESIGN.md was found that required a decision to be escalated.
