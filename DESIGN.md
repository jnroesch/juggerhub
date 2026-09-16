---
version: alpha
name: JuggerHub Design System
description: >-
  Warm, welcoming, community-run visual identity for JuggerHub — a webapp where
  Jugger players find teams, book training, follow matches, and start local
  groups. Friendly and community-oriented, clean and modern, a touch playful,
  mobile-first by default. This file is the single source of truth for UI/visual
  work; the frontend implements it via CSS custom properties in
  frontend/apps/web/src/styles.css mapped onto Tailwind utilities.
voice:
  person: talk to the reader as "you"; the community is "we"
  tone: friendly, direct, encouraging, lightly playful; short sentences, verbs over nouns
  casing: sentence case everywhere (headings, buttons, labels, nav); UPPERCASE only as a styled eyebrow
  emoji: never in product UI — personality comes from color, rounded shapes, warm copy
  numbers: scores/stats/times set in the mono typeface (tabular, sporty)
colors:
  # Warm sand neutrals (page → ink) — the biggest departure from cool grays
  sand-0: "#FBF8F3"
  sand-1: "#F4EEE3"
  sand-2: "#EAE1D2"
  sand-3: "#DBCEB9"
  sand-4: "#C7B79C"
  sand-5: "#A6957A"
  sand-6: "#82725B"
  sand-7: "#5F5343"
  sand-8: "#3E362B"
  sand-9: "#241F18"
  white: "#FFFFFF"
  black: "#1A160F"
  # Coral — primary brand
  coral-0: "#FFF1EC"
  coral-1: "#FFD8C9"
  coral-2: "#FFB69C"
  coral-3: "#FF8E68"
  coral-4: "#F5623A"
  coral-5: "#DB4A22"
  coral-6: "#B93A17"
  coral-7: "#8F2C12"
  coral-8: "#6A2210"
  coral-9: "#401307"
  # Sage / teal — secondary
  teal-0: "#EEF3EF"
  teal-1: "#DBE6DE"
  teal-2: "#C1D3C6"
  teal-3: "#9FBAA7"
  teal-4: "#7A9B87"
  teal-5: "#5F8070"
  teal-6: "#4A6558"
  teal-7: "#3B5145"
  teal-8: "#2C3B33"
  teal-9: "#1F2924"
  # Lemon — playful highlight
  lemon-0: "#FFFBE0"
  lemon-1: "#FFF0A8"
  lemon-2: "#FFE066"
  lemon-3: "#F7CE33"
  lemon-4: "#E0B211"
  lemon-5: "#B88C05"
  lemon-6: "#8F6B03"
  lemon-7: "#7D5D02"
  # Semantic status scales
  green-0: "#E6F7ED"
  green-1: "#A7E9C1"
  green-4: "#1FA860"
  green-5: "#16824A"
  green-6: "#0F6438"
  red-0: "#FFECEA"
  red-1: "#FFC7C2"
  red-4: "#F0463F"
  red-5: "#CC2E28"
  red-6: "#A11F1B"
  blue-0: "#E9F1FF"
  blue-1: "#BFD6FF"
  blue-4: "#3B7DF0"
  blue-5: "#2660CC"
  blue-6: "#1B489C"
semantic:
  surface-page: "{colors.sand-0}"
  surface-raised: "{colors.white}"
  surface-card: "{colors.white}"
  surface-sunken: "{colors.sand-1}"
  surface-muted: "{colors.sand-2}"
  surface-inverse: "{colors.sand-9}"
  surface-accent-soft: "{colors.coral-0}"
  surface-secondary-soft: "{colors.teal-0}"
  text-heading: "{colors.sand-9}"
  text-body: "{colors.sand-8}"
  text-muted: "{colors.sand-7}"
  text-on-accent: "{colors.white}"
  text-on-inverse: "{colors.sand-1}"
  text-link: "{semantic.brand-primary-strong}"
  text-link-hover: "{semantic.brand-primary-strong-hover}"
  brand-primary: "{colors.coral-4}"
  brand-primary-strong: "{colors.coral-6}"
  brand-primary-strong-hover: "{colors.coral-7}"
  brand-secondary: "{colors.teal-4}"
  brand-secondary-strong: "{colors.teal-6}"
  brand-secondary-strong-hover: "{colors.teal-7}"
  brand-highlight: "{colors.lemon-2}"
  border-default: "{colors.sand-3}"
  border-muted: "{colors.sand-2}"
  border-strong: "{colors.sand-6}"
  border-accent: "{colors.coral-3}"
  border-focus: "{colors.coral-5}"
  success-fg: "{colors.green-6}"
  success-bg: "{colors.green-0}"
  success-border: "{colors.green-1}"
  danger-fg: "{colors.red-6}"
  danger-bg: "{colors.red-0}"
  danger-border: "{colors.red-1}"
  warning-fg: "{colors.lemon-7}"
  warning-bg: "{colors.lemon-0}"
  warning-border: "{colors.lemon-1}"
  info-fg: "{colors.blue-6}"
  info-bg: "{colors.blue-0}"
  info-border: "{colors.blue-1}"
gradients:
  brand: "linear-gradient(105deg, {colors.coral-4}, {colors.teal-4})"
  brand-soft: "linear-gradient(135deg, {colors.coral-1}, {colors.teal-1})"
  hero: "linear-gradient(135deg, {colors.coral-0}, {colors.teal-0})"
typography:
  fontFamilies:
    display: "'Hubot Sans', 'Mona Sans', ui-sans-serif, system-ui, sans-serif"
    body: "'Mona Sans', ui-sans-serif, system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif"
    mono: "'Mona Sans Mono', ui-monospace, 'SF Mono', Menlo, monospace"
  weights:
    regular: 400
    medium: 500
    semibold: 600
    bold: 700
    heavy: 800
  scale:
    display: 4rem      # 2.75rem on mobile
    h1: 3rem           # 2.125rem on mobile
    h2: 2.25rem        # 1.625rem on mobile
    h3: 1.75rem        # 1.375rem on mobile
    h4: 1.375rem       # 1.1875rem on mobile
    lead: 1.25rem
    body-lg: 1.125rem
    body-md: 1rem
    body-sm: 0.875rem
    caption: 0.75rem
    eyebrow: 0.8125rem
  leading:
    tight: 1.1
    snug: 1.25
    normal: 1.5
    relaxed: 1.65
  tracking:
    tight: -0.02em
    normal: 0
    wide: 0.02em
    eyebrow: 0.06em
spacing:
  "0.5": 2px
  "1": 4px
  "2": 8px
  "3": 12px
  "4": 16px
  "5": 20px
  "6": 24px
  "7": 32px
  "8": 40px
  "9": 48px
  "10": 64px
  "11": 80px
  "12": 96px
  "13": 128px
  section-gap: "clamp(48px, 8vw, 112px)"
containers:
  sm: 640px
  md: 860px
  lg: 1100px
  xl: 1320px
rounded:
  xs: 6px
  sm: 10px
  md: 14px
  lg: 20px
  xl: 28px
  "2xl": 36px
  pill: 999px
shadows:
  xs: "0 1px 2px rgba(64, 46, 24, 0.06)"
  sm: "0 2px 6px rgba(64, 46, 24, 0.08)"
  md: "0 6px 16px rgba(64, 46, 24, 0.10)"
  lg: "0 14px 32px rgba(64, 46, 24, 0.12)"
  xl: "0 24px 56px rgba(64, 46, 24, 0.16)"
  coral: "0 8px 20px rgba(245, 98, 58, 0.28)"
  teal: "0 8px 20px rgba(122, 155, 135, 0.28)"
motion:
  duration:
    fast: 120ms
    base: 200ms
    slow: 320ms
  ease:
    standard: "cubic-bezier(0.4, 0, 0.2, 1)"
    out: "cubic-bezier(0.16, 1, 0.3, 1)"
    bounce: "cubic-bezier(0.34, 1.56, 0.64, 1)"
components:
  button:
    backgroundColor: "{semantic.brand-primary-strong}"
    textColor: "{semantic.text-on-accent}"
    typography: "{typography.scale.body-md}"
    fontWeight: 600
    rounded: "{rounded.md}"
    minHeight: 44px
    padding: 12px 20px
    hover: "background {semantic.brand-primary-strong-hover} + {shadows.coral}"
    focus: "2px {semantic.border-focus} ring, offset 2px"
  button-secondary:
    backgroundColor: "{semantic.surface-card}"
    textColor: "{semantic.text-body}"
    border: "1px {semantic.border-strong}"
    rounded: "{rounded.md}"
    minHeight: 44px
  card:
    backgroundColor: "{semantic.surface-card}"
    border: "1px {semantic.border-muted}"
    rounded: "{rounded.lg}"
    padding: "{spacing.6}"
    paddingDense: "{spacing.4} (compact repeated rows only)"
    shadow: "{shadows.sm}"
    accentStrip: "{gradients.brand} (4px top strip; the focal card of a page, see Components)"
    hover: "lift 3px + {shadows.md} — only when the card is itself a link or button"
  input:
    backgroundColor: "{semantic.surface-card}"
    textColor: "{semantic.text-body}"
    border: "1px {semantic.border-strong}"
    rounded: "{rounded.md}"
    minHeight: 44px
    focus: "{semantic.border-focus} border + a 2px {semantic.border-focus} ring"
---

## Overview

JuggerHub feels like **warm daylight on a park pitch, not a boardroom**. The
identity is friendly, community-owned, and mobile-first: warm sand neutrals,
generous rounding, soft warm-tinted shadows, and two energetic-but-friendly
accents — coral and sage — used with restraint on a calm cream background.

This system uses [Primer Brand](https://github.com/primer/brand) (GitHub's
marketing design system) as a *structural* reference for layout, accessible
form/nav patterns, spacing discipline, and its open fonts — but deliberately
moves **away** from Primer's cool enterprise palette toward warm sand neutrals
and soft coral/teal accents. Colors, spacing, radii, shadows, and components are
original to JuggerHub.

Treat this file as the source of truth for all UI/visual decisions. When product
UI lands, refine the tokens here first and let the implementation follow.

## Voice & content

We write like **a welcoming teammate, not a brand**. The reader might be brand
new to the sport — never make them feel dumb.

- **Person** — address the reader as **"you"**; the community is **"we"** ("we
  lend gear to newcomers"). Never corporate third-person.
- **Tone** — friendly and direct. Short sentences, verbs over nouns. *"Find a
  team near you,"* not *"Discover team-matching opportunities."*
- **Casing** — **sentence case everywhere**: headings, buttons, labels, nav.
  Never Title Case UI, never ALL-CAPS shouting. Small uppercase is used *only*
  as a styled eyebrow/kicker (e.g. `COMMUNITY-OWNED`) via the eyebrow style.
- **Encouraging & low-pressure** — empty states offer a next step ("Be the first
  to start a team in your city"). CTAs invite ("Find a team near you", "RSVP",
  "Start a team"), never shout ("SIGN UP NOW").
- **Concrete & human** — real places and details ("Saturday at Tempelhofer
  Feld", "we lend gear"), not marketing abstractions.
- **Jugger-native vocabulary** used naturally — team, roster, training, match,
  tournament, chain, pompfen, runner, enforcer, Q-tip. Explain jargon for
  beginners when it first appears.
- **No emoji** in product UI. Personality comes from color, rounded shapes, and
  warm copy — not emoji.
- **Numbers & scores** — set in the mono typeface for a tidy, sporty, tabular
  feel: "5 : 3", "68%", "14:00".

### Dashes and separators

An em dash is a strong piece of punctuation and reads as one only while it stays
rare. Used as the house joint for every hint, subtitle and status line, it stops
marking a turn in the sentence and becomes a rhythm — and a product where every
line cadences the same way reads as machine-set, however warm the words are.

- **English** — a full stop, a comma or a colon carries almost every hint. Reach
  for ` — ` only where the sentence genuinely turns: a sharp aside, a reversal,
  or a parenthetical set off by **two** dashes. A colon introduces a list
  (*"Your permanent link: letters, numbers, hyphens"*); parentheses carry a
  qualifier on a label (*"Description (optional)"*); the middot `·` separates a
  label from its meta (*"New message · @handle"*). If the dash could be a full
  stop without loss, make it one.
- **German** — the Gedankenstrich is a **Halbgeviertstrich `–`**, never `—`.
  Spaced on both sides, and only where the English keeps a dash.
- **Spanish** — the **raya `—`** encloses an incise with **both** dashes and no
  space on the inside (*"… a una conversación —imágenes, PDF, documentos— se
  guardan…"*). A single trailing raya is an English habit; Spanish takes a
  colon, a comma or a full stop.
- **Ranges** are a separate character in every language: an en dash, unspaced —
  "A–Z", "3–5 players", "14:00–16:00".

`catalog-punctuation.spec.ts` enforces the German and Spanish rules and the one
error voice below. The English restraint is a review question, not a test.

## Colors

Always use the **semantic aliases** (`surface-card`, `text-body`,
`brand-primary`, `border-default`…) in components rather than raw scale steps.

- **Sand neutrals** (`sand-0` `#FBF8F3` page → `sand-9` `#241F18` ink) — warm,
  not gray. This is what makes JuggerHub feel welcoming, and the biggest
  departure from cool enterprise grays.
- **Coral — primary**, in two roles. `brand-primary` (`coral-4` `#F5623A`) is
  the identity: the brand gradient, borders, progress fills, the unread dot.
  `brand-primary-strong` (`coral-6` `#B93A17`) is the one that carries text, in
  either direction — a filled button, a filled chip, a count badge, coral text,
  a coral icon. **The identity step is never a label and never sits under one**:
  white on `coral-4` is 3.14:1 and `coral-4` as text is 3.14:1, both below the
  4.5:1 floor. Used with restraint either way — **one coral CTA per view**.
  Hover → `brand-primary-strong-hover` (`coral-7`).
- **Sage — secondary** (`teal-4` `#7A9B87`): a muted warm green, calm and
  low-contrast on cream. Supporting actions, toggles, position chips, secondary
  stats. Split the same way as coral — `brand-secondary-strong` (`teal-6`) is
  the step that carries text. (Kept under the `teal-*` token names.)
- **Lemon — highlight** (`lemon-2` `#FFE066`): small playful pops — "New"
  badges, streaks, the mark's center dot. Never large fields.
- **Status** — success (green), danger (red), warning (lemon/amber), info
  (blue). Each has a soft `*-bg`, a `*-border`, and a readable `*-fg`.
- **Text ramp** — `text-heading` (`sand-9`) for strong headings, `text-body`
  (`sand-8`) for copy, and **one** secondary step, `text-muted` (`sand-7`).
  Links use `text-link` (`coral-6`). There is no third and no fourth: `sand-5`
  and `sand-4` shipped as `text-subtle` and `faint` at 2.92:1 and 1.97:1, and
  neither can be darkened into compliance without landing on top of `sand-6`.
  **Below `text-body`, hierarchy is size and weight — the `caption` and
  `eyebrow` steps — not paler ink** (GH #298).
- **Surfaces** — `surface-card`/`surface-raised` (white) for cards and content,
  `surface-sunken` (`sand-1`) for inset panels, `surface-page` (`sand-0`) for
  the canvas, `surface-inverse` (`sand-9`) for dark moments.
- **Borders** — `border-muted`/`border-default` (light warm sand) for
  decorative separators, where contrast carries no meaning. `border-strong`
  (`sand-6`) draws an input or a secondary button: that is a UI component
  boundary, so it clears 3:1 on every light surface.

## Typography

Two expressive open faces plus a mono, all mobile-first (the scale steps down on
small screens).

- **Hubot Sans** — expressive **display** face for headings and hero text
  (weights 700–800, tight tracking `-0.02em`).
- **Mona Sans** — **body** and all UI text; honest, legible, friendly.
- **Mona Sans Mono** — scores, stats, times, counts (tabular, sporty).

Nothing meaningful drops below 12px (`caption`). The `eyebrow` step
(`0.8125rem`, uppercase, `0.06em` tracking) is the only uppercase usage. Fonts
are GitHub's open-source Mona Sans / Hubot Sans (shipped via `@fontsource`); the
stacks fall back to `system-ui` if a face is unavailable.

### What each step is for

A scale is a set of jobs, not a set of sizes. Pick the step by the job:

| Step | Desktop / mobile | Job |
|------|------------------|-----|
| `display` | 64 / 44 | The brand mark's letter. Not a text step. |
| `h1` | 48 / 34 | **The hero.** One screen in the product earns it: the onboarding welcome. |
| `h2` | 36 / 26 | **A focal page title** — one card and nothing around it: sign in, register, an invite landing, "no such player". Plus the dashboard greeting, which is the front door. |
| `h3` | 28 / 22 | **Every other page title.** |
| `h4` | 22 / 19 | A section heading inside a card. |
| `lead` | 20 | The deck: one sentence under a focal title, introducing the page. Never under an `h3` — 20 under 22 is not a step. |
| `body-lg` | 18 | A bar title (the name in the chat header), and long-form prose. |
| `body-md` | 16 | **Prose** — anything written to be read: a description, an empty state, a paragraph of explanation. |
| `body-sm` | 14 | **The interface** — labels, nav, buttons, table cells, list rows, validation, secondary lines. |
| `caption` | 12 | Metadata under something else: a timestamp, a count, a role. |
| `eyebrow` | 13 | The one uppercase label. |

`body-md` and `body-sm` are two jobs, not a default and an exception. Prose the
reader came for is 16px; the interface around it is 14px. The app is mostly
interface, so `body-sm` is the more common of the two — that is the shape of the
product, not a drift to correct.

**A title carries a step and nothing else.** Weight belongs to the step, and the
display face and heading colour come from the base layer, so `font-bold` on a
heading is either a no-op or a silent override of the scale. Both were live
before GH #299 — 81 titles written 18 ways — and `heading-roles.spec.ts` now
rejects the whole class.

## Layout

- **Mobile-first**, scaling up to a **1100px** (`container-lg`) content column.
  Comfortable padding, `gap`-based flex/grid throughout.
- **Touch targets ≥ 44px** — the default control height for buttons and inputs.
- Sticky top nav; content in a centered column; airy section rhythm
  (`section-gap`, `clamp(48px, 8vw, 112px)`).
- Spacing follows a 4px base (`space-1` 4 → `space-13` 128). Compose from these
  steps rather than arbitrary values. The one step below the base is the **2px
  half-step** (`space-0.5`, `3xs`) — reserved for hairline insets where 4px is
  visibly too loose. It is not a general-purpose step: if a gap is being tuned
  by 2px, the wrong step was chosen. It is **not** the padding of a chip, which
  this file used to say and GH #301 disproved: at caption size 2px makes a 21px
  pill whose glyphs touch its own border. A chip's inset is 12/4 — see
  **Chips**, below.

## Navigation: back links

The small `‹ Parent` link above a page title means **up, not back**. The browser
and the phone already provide *back*, the historical control; an in-page copy of
it breaks exactly where it is needed. On a page opened from an alert or a shared
link it leaves the app, after an edit-and-save it returns to the form, and it can
never say where it goes.

- **It leads to the page's parent, and says which one** — `‹ Trainings`, never a
  bare `‹ Back`. Never `Location.back()`.
- **The parent is the one this viewer can open.** A page with several parents
  picks by audience. A training session's parent is the team's Trainings tab for
  a member and the public trainings list for everyone else; linking a guest to
  the team tab sends them to a page that refuses them.
- **A list comes back as the viewer left it.** Browse lists keep their filters
  and sort in the URL (only non-default values, replacing the history entry
  rather than adding one), and a back link to a list reopens it with those and
  with the typed search. The destination never changes, only its state.
- **Typed text never goes in the URL.** Session recording keeps the query string
  while masking every input, and the privacy policy promises typed text stays on
  the device. The search is remembered in memory and restored when the viewer
  returns to the list (a back link, or the browser's back button), never on an
  ordinary visit — and never after a reload or in a new tab.
- **The nav marks where the viewer is, not what the URL resembles.** "My team"
  is active on the viewer's own teams only; on another team's page no
  destination is.

## Elevation & depth

Shadows are **warm-tinted and soft** — `rgba(64, 46, 24, …)`, never pure black,
never harsh — layered `xs → xl`. Cards rest on a soft `sm` shadow and **lift 3px
with a deeper shadow on hover**. Primary elements get a colored glow on hover
(`shadow-coral`, `shadow-teal`). Most surfaces are a white card with a 1px muted
border; reserve larger shadows for elements that float above the page.

## Shape

**Rounded is core — corners are never sharp.** Small controls and inset boxes
`sm` (10px), buttons/inputs/standard elements `md` (14px), cards `lg` (20px),
feature/media `xl` (28px), chips/avatars/pills `pill` (999px) — written
`rounded-pill`, never `rounded-full`, which was a second name for the same
corner until GH #301 retired it.

## Motion & states

Friendly and gentle. Durations 120 / 200 / 320ms. `ease-out` for entrances, a
subtle `ease-bounce` for toggles and playful moments.

- **Hover** — clickable cards lift 3px + deepen shadow; buttons shift to a darker brand
  step and gain a colored glow; ghost/subtle controls warm their background.
- **Press** — buttons nudge down 1px and scale to 0.99 (tactile, not jumpy).
- **Focus** — a 2px `border-focus` (`coral-5`) ring, always visible, on every
  focusable thing in the product. A filled control adds a 2px offset so the ring
  is not drawn against its own coral fill. It used to be `coral-1` at 1.32:1 —
  present in the markup, invisible on the screen (GH #298).
- Prefer fades/slides; no infinite decorative loops in content.

## Components

- **Button (primary)** — `brand-primary-strong` background, white label, `md`
  radius, ≥44px tall, 600 weight. Hover → `brand-primary-strong-hover` + coral
  glow; press nudges down 1px. One per view.
- **Button (secondary)** — white `surface-card` background, `text-body` label,
  1px `border-strong` outline; warms on hover.
- **Card** — white `surface-card`, 1px `border-muted`, `lg` radius, soft `sm`
  shadow, `spacing.6` (24px) of body padding. A card is one thing, drawn one way:
  it is never re-assembled out of utilities. Two variations, and only these two:
  **dense** (`spacing.4`) for a compact repeated row, where 24px would push the
  list off a phone; and **flush** (none) for a card whose children carry their own
  padding — a divided list, a table, a header strip.
- **The accent strip** — a thin **coral→sage gradient** (`gradient.brand`) along
  the top, kept soft. It marks the **focal card of a page that holds nothing
  else**: sign in, register, reset a password, accept an invite, "no such
  player". One card, one page, one strip — it is a signature, not decoration,
  and on a grid or a list of cards it is noise.
- **The hover lift** — a card that is *itself* a link or a button lifts 3px into
  a deeper shadow under the pointer. A card that merely *contains* links must not
  lift: the movement promises a target the whole box doesn't have.
- **Input** — white `surface-card`, `text-body` color, 1px `border-strong`, `md`
  radius, ≥44px tall; focus uses a `border-focus` border and a `border-focus`
  ring — the same indicator a button gets.
- **Chips / badges / tags** — the small pill that labels a thing, and on a
  `<button>` / `<a>` the pill that filters a list. A chip is one shape, drawn one
  way, and it is never re-assembled out of utilities: **`pill` radius, 12px of
  side padding, 4px above and below, `caption` text**, with the whole colour —
  background, text, border — carried by a **tone**: `muted` (default),
  `secondary` (sage, for position and roster chips), `accent`, `brand`,
  `outline`, and the four status tones `success` / `warning` / `danger` /
  `info`. `lemon` stays the highlight colour of the mark and the "New" dot; no
  chip wears it today.
- **A chip you can press is 44px tall** and takes `jhButton`'s `sm` inset —
  16/8 at `body-sm` — because it is a button that happens to be pill-shaped. A
  filter chip is `accent` when it is on and `outline` when it is off, everywhere
  in the product; a *picker's* selected chip instead wears the tone of the chip
  it produces, so choosing a pompfe previews the sage chip the profile will show.
- **A pill that sets its own box is not a chip** — an avatar, the unread counter
  over a nav icon, a ranking number, a floating action pill. Its height is not
  its padding, and 44px would be wrong for a decoration nobody presses.

## Loading, error & retry states

Networks wobble. These states are where the app either stays calm or feels broken —
so they get the same care as the happy path. **Reassure, don't alarm.**

- **Loading** — one muted text line (`body-sm` / `text-muted`) via `jh-loading`, never a
  spinner or skeleton. Carries `role="status"` so it is announced. The label may be
  contextual: *"Loading your profile…"*.
- **Still loading** — if a load runs long (a slow connection, or a request being quietly
  retried), the *same* line switches to patient copy: *"Still loading…"*. Never a new
  banner, overlay, toast, or spinner, and never a layout shift — the line is already
  there, it just says something kinder. Silence first: a fast load must never flash this.
- **Error** — a short, human sentence plus a **way out**, usually a "Try again" secondary
  button. Say what happened in plain words (*"We couldn't load your teams."*) and never
  surface a status code, stack trace, or internal detail. Page- and form-level status uses
  `jh-alert` with `tone="danger"`, which carries `role="alert"`.
- **Error vs. empty** — they are different and must look different. *Empty* means "nothing
  here yet" and invites a next step (`jh-empty-state`). *Error* means "we couldn't find
  out" and offers a retry. Showing an empty state for a failed load quietly lies to the
  reader.
- **Never colour alone** — every one of these states carries text; tone and icon are
  reinforcement, never the whole message.
- **Voice** — the same warm, sentence-case "you" voice as everywhere else. *"We couldn't
  load that just now. Give it another go."* Not *"ERROR: Request failed"*, and no emoji.
- **Don't** block the whole page for a slow section, stack multiple spinners in one view,
  or replace loaded content with a spinner while refreshing — keep what's there and let
  the quiet line do the talking.

## Long-form content

Most of JuggerHub is cards, lists and forms. A few pages are **documents** —
sustained prose someone reads top to bottom rather than scans: the privacy
policy and the imprint today, anything similar later. They get their own
treatment, built entirely from the tokens above; nothing new is introduced.

- **Measure** — the content column caps at `container-sm` (**640px**), not the
  `container-lg` used for ordinary pages. That is ≈70–75 characters at
  `body-md`, the width at which the eye reliably finds the next line. The wider
  container widths are for scanning layouts; a document is read, not scanned.
- **Body** — `body-md` (16px, line-height 1.5), `text-body`. Paragraph rhythm
  comes from the spacing scale (`sm` between paragraphs, `2xl` between
  sections). **Not `section-gap`** — that token is page-level rhythm between
  major page regions; applied between the sections of a long document it turns
  a readable text into a scroll marathon.
- **Headings** — `h1` for the document title, `h2` per section, `h3` per
  subsection, in the usual display face. **Levels never skip** — the hierarchy is
  the navigation for anyone using a screen reader, and a skipped level breaks it
  silently for exactly the readers who most need it to work.
- **Meta line** — directly under the `h1`, at the `caption` step in
  `text-muted`: the "last updated" date, and for translated legal documents a
  note on which language version is authoritative. Present, never competing.
- **Table of contents** — for a document long enough to need one, anchored `h2`
  links at the top. Section `id`s stay stable so an external deep link keeps
  working.
- **Links in prose are underlined.** This is a deliberate departure from
  navigation links elsewhere in the app, which are colour-only. In a wall of
  text, colour alone is a weak affordance and no affordance at all for a
  colour-blind reader — the same reasoning as "never rely on colour alone" for
  status.
- **Lists** — `disc` / `decimal`, indented on the spacing scale, at the body
  step. Lists carry the load in legal text; they are content, not decoration.
- **Restraint** — no cards, no shadows, no gradient strip, no accent fields. A
  document page is text on the page background. Nothing on it competes for
  attention with the words.
- **Voice** — the warm, sentence-case "you" voice still applies. Legal content
  is precise, not stiff: *"We keep your messages until you ask us to delete your
  account,"* not *"Message data shall be retained for the duration of the
  account lifecycle."* Precision and plain language are not in tension.

## Iconography

- **Icon set: [Lucide](https://lucide.dev)** — friendly, rounded, 2px-stroke
  line icons. Line icons only (no filled/duotone), sized 16–22px inline with
  text, colored via `currentColor` or a token. Common icons: `compass, users,
  calendar-days, search, bell, plus, map-pin, trophy, swords, user-plus,
  arrow-right, check, sparkles`.
- **No emoji** as UI icons. Numbers/scores use the mono face, not glyphs.
- **Brand mark** — gradient rounded square + crossed "pompfen" + lemon center
  dot; original brand asset, not an icon.

## Do's and don'ts

- **Do** maintain WCAG AA contrast (≥ 4.5:1 for text, 3:1 for a focus ring or
  an input's edge). Every token above is measured against every surface it is
  used on by `contrast.spec.ts` — arithmetic over `styles.css`, no browser — so
  this is a checked claim rather than an aspiration. It was an aspiration until
  GH #298, when the ramp this file called "tuned for this" turned out to carry a
  third of the product's text below the floor. **A new colour arrives with its
  measurement**, and a threshold is never lowered to make one fit.
- **Do** reserve coral `brand-primary` for the single primary action per view;
  use sage `brand-secondary` for supporting actions.
- **Do** round everything, keep copy warm and sentence-cased, and build spacing
  from the scale tokens.
- **Do** set scores, stats, times, and counts in the mono face.
- **Don't** introduce new colors, fonts, or radii ad hoc — add a token here
  first so the system stays consistent.
- **Don't** rely on color alone to convey status; pair it with text or an icon.
- **Don't** use emoji, pure-black shadows, sharp corners, or more than one coral
  CTA per view.
- **Don't** invent a new visual style for one screen; extend this system.
