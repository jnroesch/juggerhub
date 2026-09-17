# UI Review Checklist: Team Creation Wizard

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-09-17
**Feature**: [spec.md](../spec.md)

**How to use**: This is an *implementation-quality* gate, run **after** UI is built and
**before** verification — not a spec-quality gate like `requirements.md`.
[DESIGN.md](../../../DESIGN.md) is the source of truth: if a check ever conflicts with it,
DESIGN.md wins and the conflict is reported rather than silently resolved.

**Scope of this review**: `team-create.component.html` (rewritten — five steps, progress and
navigation), `components/invite-search/invite-search.component.html` (new, lifted from
`team-invitations.component.html`), and the copy added to the three catalogues.

## Color & tokens

- [x] CHK001 Components reference **semantic aliases** (`surface-card`, `text-body`, `brand-primary`, `border-default`…), never raw scale steps (`sand-4`, `coral-5`) — every class is a semantic alias; the progress knobs use `bg-brand` / `bg-brand/60` / `bg-surface-sunken`, the review list `surface-card` + `border-border-strong` + `divide-border-default`
- [x] CHK002 **Exactly one coral `brand-primary` CTA per view** — one `jhButton` (default variant) in the navigation row on every step. The logo step's picker is `variant="secondary"`; the invite rows are outline (`border-brand` + `text-brand-strong`), carried over unchanged from the invitations screen
- [x] CHK003 Lemon `brand-highlight` used only for small pops — not used at all here
- [x] CHK004 Status uses paired `*-bg` / `*-border` / `*-fg` tokens — the Mixteam note is `info-bg` + `info-border` + `info-fg`; refusals are `text-danger-fg`; the available verdict `text-success-fg`
- [x] CHK005 No new colors introduced ad hoc — no new value anywhere in the diff

## Typography, numbers & voice

- [x] CHK006 Headings use the display face, body the UI face — step titles are `text-h3`, body copy `text-body-md` / `text-body-sm`, driven by the existing scale classes
- [x] CHK007 Scores, stats, times, counts set in **mono** — the team handle is the only such value and is `font-mono` on both the input (carried over) and the review row
- [x] CHK008 **Sentence case everywhere** — "Does this look right?", "Give your team a logo", "Who's in the team?", "Continue", "Skip for now", "Go to your team", "Change". UPPERCASE only on the `text-eyebrow` field labels, which is the styled eyebrow
- [x] CHK009 Nothing meaningful below 12px — smallest is `text-body-sm`; `text-eyebrow` is the label step, as elsewhere
- [x] CHK010 Copy addresses the reader as **"you"**, CTAs invite, no emoji — "Give **your** team a logo", "Optional — you can add one any time". No emoji in any of the three catalogues

## Layout & spacing

- [x] CHK011 Touch targets ≥ 44px — `jhButton` carries `minHeight: 44px` by definition. **Found failing and fixed during this review**: the review step's four "Change" controls were bare text buttons with no height; they now carry `inline-flex min-h-11 items-center px-2xs` (`team-create.component.html:112` and the three rows below it)
- [x] CHK012 Spacing composes from the 4px scale tokens — `space-y-md`, `gap-sm`, `py-sm`, `px-md`, `pt-sm`, `mt-xs`, `px-2xs`. The one non-token value is `w-28` on the review `<dt>`, a column width rather than spacing (see notes)
- [x] CHK013 Centered column, mobile-first — `max-w-container-sm`, unchanged from the screen this replaces
- [x] CHK014 Section rhythm uses `section-gap` — not applicable: a single-column form, not a sectioned page

## Shape & elevation

- [x] CHK015 **No sharp corners** — `rounded-md` on inputs and the review list, `rounded-lg` on the logo tile, `rounded-pill` on the progress knobs and the result avatars, `rounded-sm` on the type toggle segments
- [x] CHK016 Shadows are the warm-tinted tokens — no shadow utility is used in the diff; surfaces are distinguished by border and background
- [x] CHK017 Cards are `surface-card` + 1px muted border — the review list and the logo panel follow this. Neither lifts on hover, deliberately: they are not navigable cards (see notes)
- [x] CHK018 Larger shadows reserved for floating elements — none used

## Motion & states

- [x] CHK019 Transitions use the `fast`/`base`/`slow` durations and token easings — `transition-all duration-fast` on the progress knobs, `transition-colors duration-fast` on the type toggle, `transition-shadow duration-fast` on inputs
- [x] CHK020 Focus always visible — inputs carry `focus:border-border-focus focus:ring-2 focus:ring-focus`; `jhButton` carries its own focus ring
- [x] CHK021 Buttons darken a brand step + glow on hover, nudge on press — inherited from `jhButton`; the outline invite button uses `hover:bg-surface-accent-soft`, carried over unchanged
- [x] CHK022 No infinite decorative animation loops — none

## Iconography

- [x] CHK023 Icons are Lucide line icons via the `jh-icon` primitive — `chevron-left` on Back, `check` on the available verdict and the invited chip. No raw `<svg>` anywhere (GH #300's rule)
- [x] CHK024 No emoji used as UI icons — none

## Accessibility

- [x] CHK025 Body text meets WCAG AA — all foreground/surface pairings are existing token combinations already covered by `contrast.spec.ts`
- [x] CHK026 Status never conveyed by colour alone — the available verdict pairs green with a check icon **and** a sentence; refusals are sentences; the "invited" state is a chip with an icon and the word
- [x] CHK027 Interactive elements keyboard-reachable with visible focus and labels — every control is a native `<button>` or `<input>`; the type toggle keeps its `role="group"` + `aria-label`; the progress row is `aria-hidden="true"` (decorative, and the step's own heading carries the meaning); both refusal lines carry `role="alert"`

## Empty, loading & error states

- [x] CHK028 Empty states offer a warm, low-pressure next step — the invite search's new empty state is "No players match that. Try another spelling, or their @handle." (a suggestion, not a dead end)
- [x] CHK029 Loading and error states exist and are styled — "Checking…", "Searching…", "Uploading…"; failures render through `jh-alert` or a `text-danger-fg` line, never raw

## Feature-specific UI

- [x] CHK030 **Five knobs at 375px** — the row is five `h-2` pills (one `w-6`, four `w-2`) with `gap-xs`: 38px total. The event wizard already renders six in the same row
- [x] CHK031 **The review step reads as a summary, not a form** — a `<dl>` of label/value rows, no inputs, no field borders; only the "Change" controls are interactive, and they are text actions rather than input-shaped
- [x] CHK032 **German at 375px, the binding case** — the longest new German strings are `reviewSubtitle` ("Das Team-Handle ist dauerhaft – alles andere kannst du später ändern.") and `inviteLater`. Both are `<p>` body copy that wraps freely. The review rows pair a fixed `w-28` label column with `min-w-0 flex-1` values carrying `break-words` (`break-all` for the handle), so no value can overflow the column
- [x] CHK033 **Skip reads as a choice, not as leaving something unfinished** — the label is "Skip for now" / "Erst mal überspringen" / "Saltar por ahora", and both optional steps say "Optional" in their subtitle. No step says the team is incomplete, because it is not (FR-013)
- [x] CHK034 **The one-way door is visible** — no Back control is rendered once the team exists, and the review step states the consequence before the press ("Your team handle is permanent…", "Creating the team makes you its first admin")
- [x] CHK035 **German uses the Halbgeviertstrich** — `catalog-punctuation.spec.ts` caught three em dashes in the German copy on the first run; all three are now `–`. Verified green

## Notes

**One failure found and fixed during this review**: CHK011, the review step's "Change" controls.
They were bare text buttons at roughly 20px tall, well under the 44px DESIGN.md sets as the
default control height, and they are the only way back to an answer from the review step — the
worst place to put an unreliable tap target. Fixed in place rather than deferred.

**Two deliberate non-conformances, both reported rather than silently resolved:**

1. **CHK017 — the review list and logo panel do not lift on hover.** DESIGN.md's card spec
   includes "lift 3px + deepen shadow on hover". These two surfaces are `surface-card` with a
   border, but they are not cards in the sense the spec means: nothing about them is navigable,
   and a panel that lifts under the cursor while only a small control inside it is clickable
   advertises an interaction that does not exist. Reading DESIGN.md's own framing — the hover
   lift belongs to cards that *go somewhere* — these are panels. Raised here for a second
   opinion rather than decided unilaterally.
2. **CHK012 — `w-28` on the review label column.** Not a 4px-scale spacing token but a column
   width, which the scale does not cover. The alternative, a two-line stacked label/value, reads
   worse at every width. `w-28` is 112px, which is `space-12` in the 4px scale, so it lands on
   the scale even though it is not spelled as a token.

**Conventions** ([constitution](../../../.specify/memory/constitution.md) gate 5): `.html` / `.css` /
`.ts` stay separate for both the rewritten wizard and the new `invite-search` component.

**Not verified here**: the rendered result in a real browser at 375px. The checks above are
against the markup and the token system; `quickstart.md` §Gate 7 lists what a visual pass should
confirm, and that pass has not been run in this environment.
