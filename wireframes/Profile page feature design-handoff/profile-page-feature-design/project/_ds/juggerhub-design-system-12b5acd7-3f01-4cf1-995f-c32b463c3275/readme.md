# JuggerHub Design System

A warm, welcoming, community-run design system for **JuggerHub** — a webapp where Jugger players find teams, book training, follow matches, and start local groups. It is friendly and community-oriented, clean and modern, a touch playful without being childish, and mobile-first by default.

> **Jugger** is a fast, mixed team sport played with padded weapons ("pompfen") and a chain, on a running clock. JuggerHub is for the people who play it — clubs, tournaments, training sessions, and newcomers.

## Sources & credits
This system uses **[Primer Brand](https://github.com/primer/brand)** (GitHub's marketing design system) as a *structural* reference — for responsive layout, accessible form/nav patterns, spacing discipline, tokenized theming, and its excellent open fonts. We deliberately moved **away** from Primer's cool enterprise palette toward warm sand neutrals and soft coral/teal accents.

- Primer Brand repo: `https://github.com/primer/brand`
  - Fonts (Mona Sans, Hubot Sans, Mona Sans Mono) imported from `packages/fonts/`
  - Token structure referenced from `packages/design-tokens/src/tokens/`
- Explore that repo further if you want to build richer, more faithful patterns on top of this foundation.

The fonts are open-source and shipped in `assets/fonts/`. Colors, spacing, radii, shadows, and all components are original to JuggerHub.

---

## Content fundamentals — how JuggerHub writes

**Voice:** a welcoming teammate, not a brand. Warm, plain-spoken, encouraging, lightly playful. We assume the reader might be brand new to the sport and never make them feel dumb.

- **Person:** talk to the reader as **"you"**; the community is **"we"** ("we lend gear to newcomers"). Never corporate third-person.
- **Tone:** friendly and direct. Short sentences. Verbs over nouns. "Find a team near you," not "Discover team-matching opportunities."
- **Casing:** **Sentence case everywhere** — headings, buttons, labels, nav. Never Title Case UI, never ALL-CAPS shouting. (Small uppercase is used *only* as a styled eyebrow/kicker, e.g. `COMMUNITY-OWNED`, via `.jh-eyebrow`.)
- **Encouraging, low-pressure:** empty states offer a next step ("Be the first to start a team in your city"). CTAs are inviting ("Find a team near you", "RSVP", "Start a team") not aggressive ("SIGN UP NOW").
- **Concrete & human:** real places and details ("Saturday at Tempelhofer Feld", "we lend gear"), not marketing abstractions.
- **Jugger-native vocabulary** used naturally: team, roster, training, match, tournament, chain, pompfen, runner, enforcer, Q-tip. Explain jargon for beginners when it first appears.
- **Emoji:** **not used in product UI.** Personality comes from color, rounded shapes, and warm copy — not emoji. (Real icons: Lucide — see Iconography.)
- **Numbers & scores:** set in the mono typeface (Mona Sans Mono) for a tidy, sporty, tabular feel — "5 : 3", "68%", "14:00".

**Examples**
- Hero: *"Jugger is better with a crew."* / *"Discover local teams, book training, and follow your matches."*
- Empty state: *"No teams here yet — be the first to start one in your city."*
- Success: *"You're in! See you Saturday at Tempelhofer Feld."*
- Button labels: `Find a team near you` · `RSVP` · `Join team` · `Create team`

---

## Visual foundations

**Overall vibe:** warm daylight, rounded, soft. Think a sunny park pitch, not a boardroom. Generous rounding, gentle warm-tinted shadows, and two energetic-but-friendly accents on a calm sand background.

### Color
- **Neutrals are warm sand**, not gray (`--sand-0` `#FBF8F3` page → `--sand-9` `#241F18` ink). This is the single biggest departure from Primer and what makes JuggerHub feel welcoming.
- **Primary = Coral** (`--coral-4` `#F5623A`): friendly, energetic, used for the main CTA, key highlights, and the brand gradient. Used with restraint — one coral CTA per view.
- **Secondary = Sage** (`--teal-4` `#7A9B87`): a muted, warm green — calm and low-contrast against cream. Used for supporting actions, toggles, position chips, secondary stats. (Kept under the `--teal-*` token names.)
- **Highlight = Lemon** (`--lemon-2` `#FFE066`): small playful pops — "New" badges, streaks, the mark's center dot. Never large fields.
- **Semantic:** green (success), red (danger), lemon/amber (warning), blue (info) — each with a soft `*-bg`, `*-border`, and readable `*-fg`.
- Always use the **semantic aliases** (`--surface-card`, `--text-body`, `--brand-primary`, `--border-default`…) in components rather than raw scale steps.

### Type
- **Hubot Sans** — expressive display face for headings & hero text (weights 700–800, tight tracking).
- **Mona Sans** — body & all UI text; honest, legible, friendly.
- **Mona Sans Mono** — scores, stats, times, counts (tabular, sporty).
- Scale is fixed at desktop and **scales down on mobile** via `--text-*` overrides in `tokens/base.css` (mobile-first). Body is 16px; nothing meaningful below 12px.

### Shape & elevation
- **Rounded is core.** Controls `--radius-md` (14px), cards `--radius-lg` (20px), feature/media `--radius-xl` (28px), chips/avatars/pills `--radius-pill`. Corners are never sharp.
- **Shadows are warm-tinted and soft** (`rgba(64,46,24,…)`), layered `--shadow-xs → xl`. Never pure-black, never harsh. Primary elements get a colored glow on hover (`--shadow-coral`, `--shadow-teal`).
- **Cards:** white surface, 1px muted border, soft resting shadow, `--radius-lg`. Many cards carry a thin **gentle coral→sage gradient strip** (`--brand-gradient`) at the top as a signature detail (see `TeamCard`, hero card) — kept soft, never high-contrast.
- **Borders** are light warm sand (`--border-muted`/`--border-default`); dividers are subtle.

### Backgrounds
- Mostly the flat warm `--surface-page`. Heroes use **soft diagonal gradients** between `--coral-0` and `--teal-0` (very light, daylight-like). Team covers use the gentle `--brand-gradient` (coral→muted sage). Gradients are deliberately low-contrast and warm — coral-forward. No photography dependency, no textures, no dark enterprise panels.

### Motion & states
- Friendly and gentle. Durations 120/200/320ms. `--ease-out` for entrances, a subtle **`--ease-bounce`** for toggles/playful moments (e.g. the Switch knob).
- **Hover:** cards lift 3px + deepen shadow; buttons shift to a darker brand step and gain a colored glow; ghost/subtle controls warm their background.
- **Press:** buttons nudge down 1px and scale to 0.99 (tactile, not jumpy).
- **Focus:** 2px coral outline + soft coral ring (`--focus-ring`) — always visible, accessible.
- Prefer fades/slides; no infinite decorative loops in content.

### Layout
- **Mobile-first**, scaling up to a **1100px** content container. Comfortable padding, `gap`-based flex/grid throughout. Touch targets ≥44px (default control height).
- Sticky top nav; content in a centered column; airy section rhythm (`--section-gap`).

---

## Iconography

- **Icon set: [Lucide](https://lucide.dev)** — friendly, rounded, consistent 2px-stroke line icons. Loaded from CDN (`unpkg.com/lucide`). Rendered as `<i data-lucide="name"></i>` then `lucide.createIcons()`, or passed to components as ReactNodes (`leadingVisual`, `icon`, tab `icon`, etc.).
- **⚠️ Substitution:** Primer ships **Octicons**. We deliberately chose **Lucide** because its softer, rounded stroke matches JuggerHub's warm, welcoming, community tone far better than Octicons' tighter developer-tool look. If you require Octicons for brand consistency with GitHub properties, swap the CDN and keep the same `Icon` usage. **Flagging this for your review.**
- **Style rules:** line icons only (no filled/duotone), 2px stroke, size 16–22px inline with text, colored via `currentColor` or a token (`--brand-primary`, `--text-muted`). Common icons: `compass, users, calendar-days, search, bell, plus, map-pin, trophy, swords, user-plus, arrow-right, check, sparkles`.
- **Emoji:** not used as UI icons.
- **Unicode:** the select chevron (`▾`) and the mark's crossed "pompfen" (`✕`) are the only glyph usages; everything else is Lucide.
- **Brand mark:** `assets/juggerhub-mark.svg` (gradient rounded square + crossed pompfen + lemon dot) and `assets/juggerhub-wordmark.svg`. These are original brand assets, not icons.

---

## Components

All components are React (`.jsx`), exported under the `window` namespace reported by `check_design_system`. Import via `const { Button } = window.<Namespace>` after loading `_ds_bundle.js`. Every component references design tokens only (no CSS-in-JS libs, no npm deps).

**Forms & controls** (`components/forms/`): `Button`, `IconButton`, `Input`, `Textarea`, `Select`, `Checkbox`, `Radio`, `Switch`, `FormField`

**Feedback & status** (`components/feedback/`): `Badge`, `Tag`, `Avatar`, `AvatarStack`, `Alert`, `EmptyState`, `Spinner`, `ProgressBar`

**Layout & structure** (`components/layout/`): `Card`, `Stat`, `Tabs`, `Accordion`

**Navigation** (`components/navigation/`): `NavBar`, `Breadcrumbs`, `Pagination`

**Community cards** (`components/community/`): `TeamCard`, `EventCard`, `MatchResult`, `PlayerCard`

Each component directory has a `<Name>.jsx`, `<Name>.d.ts` (props contract), `<Name>.prompt.md` (what/when + example), and a `*.card.html` specimen for the Design System tab.

---

## Index / manifest

**Root**
- `styles.css` — the single entry point (consumers link this). `@import`s all tokens.
- `readme.md` — this file.
- `SKILL.md` — Agent-Skills-compatible skill wrapper.

**`tokens/`** — `fonts.css`, `colors.css`, `typography.css`, `spacing.css`, `effects.css`, `base.css` (reset + mobile type scaling + `.jh-eyebrow`).

**`assets/`** — `juggerhub-mark.svg`, `juggerhub-wordmark.svg`, `fonts/` (Mona Sans, Hubot Sans, Mona Sans Mono `.woff2`).

**`components/`** — `forms/`, `feedback/`, `layout/`, `navigation/`, `community/` (see Components above).

**`guidelines/`** — foundation specimen cards (Colors, Type, Spacing, Brand) shown on the Design System tab.

**`ui_kits/webapp/`** — the interactive JuggerHub community web app (Discover, Teams, Team detail, Onboarding). Open `ui_kits/webapp/index.html`.

**Generated (do not edit):** `_ds_bundle.js`, `_ds_manifest.json`, `_adherence.oxlintrc.json`.

---

## Using this system
1. Link `styles.css` for tokens + fonts.
2. Load `_ds_bundle.js` and read components off the `window` namespace.
3. Reach for community cards (`TeamCard`, `EventCard`, `MatchResult`, `PlayerCard`) first — they encode the brand. Compose primitives for everything else.
4. Keep copy warm and sentence-cased; one coral CTA per view; round everything; keep it mobile-first.
