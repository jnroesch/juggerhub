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
  text-muted: "{colors.sand-6}"
  text-subtle: "{colors.sand-5}"
  text-on-accent: "{colors.white}"
  text-on-inverse: "{colors.sand-1}"
  text-link: "{colors.coral-6}"
  text-link-hover: "{colors.coral-7}"
  brand-primary: "{colors.coral-4}"
  brand-primary-hover: "{colors.coral-5}"
  brand-primary-active: "{colors.coral-6}"
  brand-secondary: "{colors.teal-4}"
  brand-secondary-hover: "{colors.teal-5}"
  brand-highlight: "{colors.lemon-2}"
  border-default: "{colors.sand-3}"
  border-muted: "{colors.sand-2}"
  border-strong: "{colors.sand-4}"
  border-accent: "{colors.coral-3}"
  border-focus: "{colors.coral-4}"
  success-fg: "{colors.green-6}"
  success-bg: "{colors.green-0}"
  success-border: "{colors.green-1}"
  danger-fg: "{colors.red-6}"
  danger-bg: "{colors.red-0}"
  danger-border: "{colors.red-1}"
  warning-fg: "{colors.lemon-6}"
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
  focus-ring: "0 0 0 3px {colors.coral-1}"
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
    backgroundColor: "{semantic.brand-primary}"
    textColor: "{semantic.text-on-accent}"
    typography: "{typography.scale.body-md}"
    fontWeight: 600
    rounded: "{rounded.md}"
    minHeight: 44px
    padding: 12px 20px
    hover: "background {semantic.brand-primary-hover} + {shadows.coral}"
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
    shadow: "{shadows.sm}"
    accentStrip: "{gradients.brand} (optional 4px top strip)"
  input:
    backgroundColor: "{semantic.surface-card}"
    textColor: "{semantic.text-body}"
    border: "1px {semantic.border-strong}"
    rounded: "{rounded.md}"
    minHeight: 44px
    focus: "{semantic.border-focus} border + {shadows.focus-ring}"
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

## Colors

Always use the **semantic aliases** (`surface-card`, `text-body`,
`brand-primary`, `border-default`…) in components rather than raw scale steps.

- **Sand neutrals** (`sand-0` `#FBF8F3` page → `sand-9` `#241F18` ink) — warm,
  not gray. This is what makes JuggerHub feel welcoming, and the biggest
  departure from cool enterprise grays.
- **Coral — primary** (`coral-4` `#F5623A`): friendly and energetic. The main
  CTA, key highlights, and the brand gradient. Used with restraint — **one coral
  CTA per view**. Hover → `coral-5`, active → `coral-6`.
- **Sage — secondary** (`teal-4` `#7A9B87`): a muted warm green, calm and
  low-contrast on cream. Supporting actions, toggles, position chips, secondary
  stats. (Kept under the `teal-*` token names.)
- **Lemon — highlight** (`lemon-2` `#FFE066`): small playful pops — "New"
  badges, streaks, the mark's center dot. Never large fields.
- **Status** — success (green), danger (red), warning (lemon/amber), info
  (blue). Each has a soft `*-bg`, a `*-border`, and a readable `*-fg`.
- **Text ramp** — `text-heading` (`sand-9`) for strong headings, `text-body`
  (`sand-8`) for copy, `text-muted` (`sand-6`) and `text-subtle` (`sand-5`) for
  secondary/tertiary text. Links use `text-link` (`coral-6`).
- **Surfaces** — `surface-card`/`surface-raised` (white) for cards and content,
  `surface-sunken` (`sand-1`) for inset panels, `surface-page` (`sand-0`) for
  the canvas, `surface-inverse` (`sand-9`) for dark moments.
- **Borders** — `border-muted`/`border-default` (light warm sand) for
  separators, `border-strong` (`sand-4`) for inputs and emphasized edges.

## Typography

Two expressive open faces plus a mono, all mobile-first (the scale steps down on
small screens).

- **Hubot Sans** — expressive **display** face for headings and hero text
  (weights 700–800, tight tracking `-0.02em`).
- **Mona Sans** — **body** and all UI text; honest, legible, friendly.
- **Mona Sans Mono** — scores, stats, times, counts (tabular, sporty).

Body is 16px (`body-md`); nothing meaningful drops below 12px (`caption`). The
`eyebrow` step (`0.8125rem`, uppercase, `0.06em` tracking) is the only uppercase
usage. Fonts are GitHub's open-source Mona Sans / Hubot Sans (shipped via
`@fontsource`); the stacks fall back to `system-ui` if a face is unavailable.

## Layout

- **Mobile-first**, scaling up to a **1100px** (`container-lg`) content column.
  Comfortable padding, `gap`-based flex/grid throughout.
- **Touch targets ≥ 44px** — the default control height for buttons and inputs.
- Sticky top nav; content in a centered column; airy section rhythm
  (`section-gap`, `clamp(48px, 8vw, 112px)`).
- Spacing follows a 4px base (`space-1` 4 → `space-13` 128). Compose from these
  steps rather than arbitrary values.

## Elevation & depth

Shadows are **warm-tinted and soft** — `rgba(64, 46, 24, …)`, never pure black,
never harsh — layered `xs → xl`. Cards rest on a soft `sm` shadow and **lift 3px
with a deeper shadow on hover**. Primary elements get a colored glow on hover
(`shadow-coral`, `shadow-teal`). Most surfaces are a white card with a 1px muted
border; reserve larger shadows for elements that float above the page.

## Shape

**Rounded is core — corners are never sharp.** Small controls and inset boxes
`sm` (10px), buttons/inputs/standard elements `md` (14px), cards `lg` (20px),
feature/media `xl` (28px), chips/avatars/pills `pill` (999px).

## Motion & states

Friendly and gentle. Durations 120 / 200 / 320ms. `ease-out` for entrances, a
subtle `ease-bounce` for toggles and playful moments.

- **Hover** — cards lift 3px + deepen shadow; buttons shift to a darker brand
  step and gain a colored glow; ghost/subtle controls warm their background.
- **Press** — buttons nudge down 1px and scale to 0.99 (tactile, not jumpy).
- **Focus** — 2px coral border + soft coral ring (`focus-ring`), always visible.
- Prefer fades/slides; no infinite decorative loops in content.

## Components

- **Button (primary)** — coral `brand-primary` background, white label, `md`
  radius, ≥44px tall, 600 weight. Hover → `brand-primary-hover` + coral glow;
  press nudges down 1px. One per view.
- **Button (secondary)** — white `surface-card` background, `text-body` label,
  1px `border-strong` outline; warms on hover.
- **Card** — white `surface-card`, 1px `border-muted`, `lg` radius, soft `sm`
  shadow, lifts on hover. Many cards carry a thin **coral→sage gradient strip**
  (`gradient.brand`) at the top as a signature detail — kept soft.
- **Input** — white `surface-card`, `text-body` color, 1px `border-strong`, `md`
  radius, ≥44px tall; focus uses `border-focus` + the coral `focus-ring`.
- **Chips / badges / tags** — `pill` radius; sage for position/roster chips,
  lemon for "New"/highlight badges.

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
  load that just now — give it another go."* Not *"ERROR: Request failed"*, and no emoji.
- **Don't** block the whole page for a slow section, stack multiple spinners in one view,
  or replace loaded content with a spinner while refreshing — keep what's there and let
  the quiet line do the talking.

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

- **Do** maintain WCAG AA contrast (≥ 4.5:1 for body text). The sand text ramp
  on light surfaces is tuned for this.
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
