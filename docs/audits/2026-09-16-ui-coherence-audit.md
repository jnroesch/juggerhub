# UI coherence audit — "the AI vibe" (GH #278)

Date: 2026-09-16 · Scope: `frontend/apps/web` (106 templates), `DESIGN.md`, the three
i18n catalogues · Status: research only, no code changed.

## Why this audit exists

[#278](https://github.com/jnroesch/juggerhub/issues/278) is feedback from a player, not a
bug report. The substance:

> …dass die Website ähnlich wie EM-Dashes und "Smoking Gun" Formulierungen, Elemente
> enthält die sich slightly inkongruent oder bland/samey anfühlen.

and two screenshots: a list whose bullets are not the same size, and a box whose text sits
too close to its edge. The reporter says up front that he is a dev, not a designer, and
that it is all subjective.

It is not, mostly. Almost every specific he points at is measurable, and most of it is the
same root cause: **`DESIGN.md` is a good, opinionated system, and the implementation has
drifted from it in ~40 small ways.** Drift is what "slightly incongruent" feels like from
the outside — the same element rendered five slightly different ways across five screens.

What follows is what is actually in the code. Each finding names files and counts so it can
be checked, and the fix is stated. Nothing here is a matter of taste unless it says so.

---

## Part 1 — The two screenshots

### The bullets that are not the same size

The app has **no list-marker convention at all**. Markers are typed as literal text
characters, in at least five different ways:

| Where | Marker | File |
| --- | --- | --- |
| Password rules on register | `✓` / `○` | `auth/password-policy/password-rules.component.html:10` |
| Party readiness list | `✓` / `·` | `parties/party-manage/party-manage.component.html:50-52` |
| "What retiring does" list | `•` | `admin/catalogue/admin-catalogue.component.html:357-359` |
| Table row affordance | `›` | `admin/users/admin-users.component.html:102` |
| Brand mark, unread dot | `<span class="h-1.5 w-1.5 rounded-full">` | `layout/top-nav/top-nav.component.html:7` |

The password-rules list is the most likely subject of the screenshot: a vertical list where
each row is prefixed by either `✓` (U+2713) or `○` (U+25CB), boxed in a 16×16 grid cell.
Those two characters have different cap heights and advance widths in any typeface, so the
markers cannot optically align no matter what the box does — and `✓` and `○` are outside the
latin subset that `@fontsource/mona-sans` ships, so they are very likely being drawn by a
*fallback system font* while the label next to them is Mona Sans. Two typefaces, two optical
sizes, one list. (Worth confirming in a browser with devtools → Rendered Fonts; the fix is
the same either way.)

The fix is already in the repo and unused. `jh-icon` exists precisely for this — its own doc
comment says it exists "so screens never inline ad-hoc SVG or use a text glyph (e.g. a
literal '+') as an icon (DESIGN.md, FR-012)" — and `check` is one of the seven icons it
ships. The rule exists; six places break it.

### The box whose text touches the edge

**21 pills and badges have zero vertical padding**, most of them with a visible border. The
best match for the screenshot is the "beginners welcome" chip on team cards:

```html
<!-- browse/browse-teams/browse-teams.component.html:41 -->
<span class="rounded-pill border border-brand px-sm py-0 text-caption text-brand">…</span>
```

12px horizontal padding, **0px vertical**, inside a 999px-radius border. The glyphs have
only the font's own leading between them and the curve, and because the radius is a pill the
border closes in on the first and last letter as well. Same construction at
`browse-events:36`, `browse-trainings:59`, `browse-players:67`, `browse-shell:163`,
`onboarding:211` and `onboarding:213`; the chat kind-tags (`chat-inbox:93,99`,
`chat-conversation:24,26`) omit `py-*` entirely.

This one is half sanctioned by the system: `DESIGN.md` reserves the 2px half-step for
"the vertical padding of pills". `py-0` is not that, and `py-0` on a *bordered* pill is not
that either. See also finding B2 — the same chip is built with eleven different padding
combinations across the app.

---

## Part 2 — Findings

### A. Shipped defects that no test catches: utility classes that do not exist

Tailwind emits **nothing** for an undefined scale key. The repo already learned this once —
`core/design/spacing-scale.spec.ts` was written after GH #137, when `py-3xs` silently
rendered as no padding. That guard covers margin/padding/gap only. The same bug class is
live right now in font size and letter spacing, where nothing guards.

**A1 — Two page titles render at body size.** `text-heading-xl` is not a key in
`tailwind.config.js` (`fontSize` defines `display, h1, h2, h3, h4, lead, body-lg, body-md,
body-sm, caption, eyebrow, heading-lg, heading-md, code`). Tailwind preflight resets
headings to `font-size: inherit`, and `styles.css` sets font-family/colour/tracking on
`h1`–`h4` but never a size. So:

- `events/event-detail/event-detail.component.html:24` — `<h1 class="text-heading-xl">{{ d.name }}</h1>` → **16px bold**, the same size as the paragraph under it.
- `trainings/training-session/training-session.component.html:22` — same.

The event name and the training session name — two of the most important titles in the
product — have no visual hierarchy at all.

**A2 — `text-heading-sm` (×8) is also undefined**, including the event price
(`event-detail:155`) and every "you can't do that" title across the events feature
(`event-manage/edit/admins/contacts/invite-accept:6`, `marketplace/recruiting:15`).

The name collision is what hid both: `text-heading` is a *colour* token, `text-heading-lg`
and `-md` are *sizes*, `-xl` and `-sm` are nothing. All four read as members of one family.

**A3 — `tracking-eyebrow` (×7) is undefined.** `tailwind.config.js` extends colours,
spacing, maxWidth, borderRadius, fontFamily, fontSize, boxShadow, backgroundImage,
transitions and ringColor — but never `letterSpacing`. `DESIGN.md`'s `tracking` tokens
(`tight -0.02em`, `wide 0.02em`, `eyebrow 0.06em`) were never mapped, so the whole
`tracking-*` namespace falls through to Tailwind's defaults. All seven sites are in chat
(`chat-conversation:24,26,78,232`, `chat-inbox:93,99`, `chat-details:56`).

**Fix for all three:** extend `spacing-scale.spec.ts`'s technique to `text-*` and
`tracking-*`, and map `DESIGN.md`'s tracking tokens into the config. The guard is the fix —
these three would not have survived a week with it.

### B. One style, many spellings

**B1 — The eyebrow/section label ships five ways.** 112 uppercase elements, and the
letter-spacing on them takes five distinct values:

| Spelling | Sites | Actual tracking |
| --- | --- | --- |
| `text-eyebrow … tracking-[0.06em]` | 47 | 0.06em (correct, but the token already sets it — the class is redundant) |
| `text-eyebrow … tracking-wide` | 17 | **0.025em** — Tailwind's default, overrides the token |
| `text-eyebrow … tracking-eyebrow` | 7 | **none** (A3) |
| `tracking-[0.08em]` / `tracking-[0.04em]` | 1 + 1 | one-offs |
| `text-body-sm`/`text-caption` + `uppercase` | ~10 | wrong step entirely (14px / 12px instead of 13px) |

Marketplace eyebrows are tighter than the rest of the app, chat's have no tracking, settings'
are a size smaller, and the legal TOC heading is a size larger. Nobody would name this if
asked, and everybody feels it.

**B2 — The chip/pill ships eleven ways.** Same visual element, eleven padding combinations:
`px-xs py-0.5`(18), `px-sm py-0.5`(17), `px-sm py-1`(8), `px-sm py-0`(7), `px-xs py-3xs`(6),
`px-md py-1.5`(4), `px-md py-xs`(4), `px-1`(5), `px-sm py-xs`(2), `px-sm py-3xs`(2),
`px-lg py-sm`(1). Note `py-0.5` and `py-3xs` are **the same 2px in two vocabularies** —
24 chips split across two spellings of one value.

**B3 — Two spacing vocabularies.** 2366 spacing utilities use the named scale
(`px-sm`, `gap-md`); 195 use Tailwind's numeric one, clustered exactly where the named
scale is least memorable: `py-0.5`(36), `mt-1`(25), `py-1`(18), `mt-0.5`(14), `p-1`(12),
`gap-1`(11), `gap-0.5`(11). Most duplicate a named token (`p-1` == `p-2xs`); `py-1.5`(6),
`mt-1.5`(1) and `mt-[3px]`(1) are off the 4px base entirely, against `DESIGN.md`'s own
warning that "if a gap is being tuned by 2px, the wrong step was chosen".

**B4 — 40% of cards use the primitive.** `jh-card` exists and is well built. There are 40
usages — and 59 hand-rolled `rounded-lg + border + bg-surface-card` surfaces beside them.
The primitive deliberately leaves padding to the caller, and its own comment names the
recommended value ("`p-xl` = 24px", matching `DESIGN.md`'s `card.padding: spacing.6`).
32 of 40 call sites pass `p-md` (16px), 6 pass `p-xl`, 1 passes `p-lg`. The system says 24,
the app says 16, and three values ship. **This is the reporter's "more space between the
text and the edge of the box" at card scale.**

**B5 — Two signature details are effectively unused.** `[interactive]` on `jh-card` — the
3px hover lift `DESIGN.md` calls out twice — is used **zero times**. The coral→sage accent
strip ("many cards carry it as a signature detail") is used on 6 of 40. So cards never
respond to the pointer and rarely carry the one detail that makes them JuggerHub's.

**B6 — Buttons: 324 `<button>` elements, 148 carry `jhButton`.** The other 176 are
hand-styled, which is where focus rings, heights and radii drift (see C3). 164 of them carry
no explicit ≥44px height; many reach it through padding, but the filter chips in
`marketplace/market-board` and `browse/*` (bordered, `py-0`/`py-0.5`, 12–14px text) land at
roughly **19–25px tall** — well under the 44px touch target `DESIGN.md` mandates, on the
primary filtering control of four Browse tabs.

**B7 — Minor vocabulary drift:** `rounded-pill`(109) vs `rounded-full`(44) — same 999px,
two names. Bare `rounded` (4px, Tailwind's default) on four checkboxes, below the 6px floor
of the shape scale. `min-h-11` vs `min-h-[44px]`(7) for the same 44px.

### C. Contrast — where "bland" stops being subjective

No contrast test exists anywhere in the repo. Computed against the tokens in `styles.css`
(WCAG 2.1 relative luminance):

| Token | on `surface-card` #FFF | on `surface-sunken` | Verdict |
| --- | --- | --- | --- |
| `text-heading` sand-9 | 16.35 | 14.16 | fine |
| `text-body` sand-8 | 11.88 | 10.29 | fine |
| `text-muted` sand-6 | 4.66 | **4.03** | passes on white, **fails on sunken** |
| **`text-subtle` sand-5** | **2.92** | **2.53** | **fails AA body *and* AA large** — used **325×** |
| **`faint` sand-4** | **1.97** | 1.70 | fails everything — used **58×** |
| **white on `brand` coral-4** | **3.14** | — | **the primary button label fails AA** |
| white on `secondary` teal-4 | 3.06 | — | fails AA |
| `text-link` coral-6 | 5.71 | 4.94 | fine |

**C1 — `text-subtle` at 2.92:1, 325 uses.** This is the app's default secondary text: card
metadata, timestamps, hints, inactive bottom-nav labels. `DESIGN.md` claims "the sand text
ramp on light surfaces is tuned for this (≥4.5:1)". It is not, at the two steps that carry
most of the app's secondary text. **This is the literal, measurable component of "bland":**
a third of the text on screen is washed out, so pages read as uniform grey-on-cream with no
figure/ground.

**C2 — The primary CTA is 3.14:1.** White on coral-4 at 16px semibold fails AA (needs 4.5;
the 3.0 large-text allowance needs ≥18.7px bold). Every primary button in the product.
Darkening the *button background* to coral-5 (#DB4A22, 4.28:1) or coral-6 (#B93A17, 5.71:1)
fixes it without touching the brand colour elsewhere — coral-4 stays the identity colour,
it just stops being a text background.

**C3 — The focus ring is 1.32:1.** `button.directive.ts` applies
`focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus`, and `ring-focus`
is coral-1 (#FFD8C9) — **1.32:1 against white**, against WCAG 2.2's 3:1 for focus
indicators. `DESIGN.md` specifies "2px coral border **+** soft coral ring"; the directive
ships only the ring, i.e. the invisible half. Inputs get it right
(`focus:border-brand focus:ring-2 focus:ring-focus`, 58 sites) — so buttons and inputs
also focus differently from each other.

**C4 — Borders are below the 3:1 non-text bar:** `border-muted` 1.30, `border-default` 1.55,
`border-strong` 1.97, `border-accent` 2.25 — all against white. Input outlines at 1.97:1 are
the reason forms read as floating grey shapes rather than fields.

**C5 — `text-[10px]`** on the four nav unread badges — below the 12px floor `DESIGN.md`
sets, and an arbitrary value off the type scale.

### D. The type scale that never ships

Usage across all templates:

```
text-body-sm  682      text-heading-lg  34      text-h2      1
text-caption  188      text-h3          27      text-display 1
text-body-md  162      text-heading-md  20      text-h1      0
text-eyebrow  103      text-h4          16      text-lead    0
text-body-lg   41
```

- **14px is the app's body text, not 16px.** `text-body-sm` outnumbers `text-body-md` 4:1,
  though `DESIGN.md` states "Body is 16px (`body-md`)".
- **`text-h1` is used zero times; `text-display` once** (as a single letter inside the
  onboarding logo tile); `text-h2` once (the legal page title). There are 81 `<h1>`
  elements and the largest one in the product is `text-heading-lg` = 28px.
- **`text-lead` is never used** — the 20px step that would give any page an intro voice.
- **`font-display`** — Hubot Sans, the expressive face carrying most of the identity —
  appears explicitly 6 times. Headings inherit it from the base layer, but since page titles
  top out at 28px, the display face never gets to be expressive.

So the entire product is rendered in **three sizes between 12 and 16px, plus a 28px
heading**. There is no typographic contrast anywhere: no hero, no lead, no scale jump. That
— far more than the palette — is why every screen looks like every other screen. It is also
the cheapest thing on this list to fix: a page-title step and a lead step, applied to
existing `<h1>`s.

### E. Iconography: one system, 99 exceptions

- `jh-icon` ships **7 icons** (`plus, check, x, search, bell, arrow-right, user-plus`) and is
  used **5 times** in the whole app. `DESIGN.md` names 13 common icons — `compass, users,
  calendar-days, map-pin, trophy, swords, sparkles` and others — **none of which exist in
  the set**.
- Templates contain **99 hand-inlined `<svg>` across 37 files**, at **five stroke widths**
  (2.0 ×54, 1.8 ×39, 1.6 ×8, 3.0 ×2, 2.2 ×1) and **seven sizes** (12, 14, 16, 18, 20, 22,
  24). The bottom nav's five tabs are inline SVG at 1.8 stroke; `jh-icon` renders Lucide at
  2.0.

Icons drawn at 1.6px sitting next to icons drawn at 2.0px is exactly the kind of thing that
registers as "off" without being nameable. It is also the highest-yield *visual* fix here:
consistent icon weight is most of what makes an interface feel drawn rather than assembled.

### F. Copy

**F1 — 101 English strings use the ` — ` clause** (7% of all strings; **26% in the legal
catalogue**). The shape is the app's default for hints, subtitles, empty states and status
lines:

> `You're not on a team yet — join one near you, accept an invite, or start your own.`
> `Full right now — reopens automatically if a spot frees.`
> `Entered — your team is on the event`
> `Bank transfer only — no in-app payment.`

The reporter named this exactly. It is not that em dashes are wrong; it is that **one
sentence shape is used 101 times**, so every hint in the product has the same rhythm. Most
of them want a full stop or a comma; a handful genuinely want the dash.

**F2 — The German em dashes are also typographically wrong.** German uses the
Halbgeviertstrich `–` as a Gedankenstrich, not `—`. `de.json` currently ships **94 strings
with ` — `** and **12 with ` – `** — the newest feature (050, tournament results) used the
correct dash, everything older did not. So the German UI is internally inconsistent *and*
wrong in the majority. To a German reader this reads as machine-set text, which is the
report's first sentence.

**F3 — The German and Spanish catalogues are unreviewed machine translations**, and say so:

```json
"_meta": { "status": "draft", "review": "Machine/AI draft — native-speaker review pending (see #77)" }
```

The reporter read the German. The pointer is stale — #77 (the i18n feature) is closed; the live
native-speaker review issue is **#84**, still open. Anything above about copy "feel" in German is
partly this.

**F4 — Repetition beyond the dash:** 20 strings open "We couldn't…", 15 "Could not…",
7 "Something went…" — three error voices where `DESIGN.md` specifies one.

### G. Identity — the part that is genuinely subjective

The reporter blames the palette, the font and the rounded corners. He is pointing at
something real, but I would locate it slightly differently:

1. **Nothing in the product is drawn.** There are no illustrations, no photographs, no
   textures, no patterns — `public/` contains a favicon and the i18n files, and every
   `<img>` in the app is user-uploaded content (avatars, badge icons). The whole visual
   identity is tokens: colour, radius, shadow. Tokens alone cannot carry personality,
   which is why the result feels *assembled from a config* — because it is.
2. **The one distinctive brand asset is not used.** `favicon.svg` is a gradient square with
   **crossed pompfen** and a lemon centre dot — specific to the sport, and the mark
   `DESIGN.md` describes. The in-app mark drops the pompfen: `top-nav:7`, `shell:13`,
   `legal-page:10` render a gradient rounded square containing **a plain dot**. A gradient
   rounded square with a dot is the single most recognisable "generated app" logo of the
   last three years. The good mark exists; the browser tab gets it and the product does not.
3. **Jugger is a physically distinctive sport** — pompfen, the chain, the skull, the run —
   and none of it appears anywhere in the interface. A Jugger app that looked like Jugger
   would not read as generic regardless of its radii.
4. On the specifics: Mona Sans + Hubot Sans are GitHub's own brand faces, and `DESIGN.md`
   openly names Primer Brand as its structural reference — so "a font I've seen a hundred
   times" is fair. `rounded-md` (14px) appears 338 times and there is not one sharp corner
   in the product; uniform rounding removes shape as a signal entirely. Neither is *wrong*,
   but together with 1–3 there is nothing left to carry an identity.

None of 1–4 is a bug and none should be changed without a decision. They are the real answer
to "why does this feel samey", and they are worth a deliberate call rather than a patch.

---

## Part 3 — Suggested follow-ups, by leverage

Ordered by (visible improvement) ÷ (effort). All eight are filed as #297–#304.

| Issue | Work | Why first | Size |
| --- | --- | --- | --- |
| [#297](https://github.com/jnroesch/juggerhub/issues/297) | Guard `text-*` and `tracking-*` the way `spacing-scale.spec.ts` guards spacing; fix the 17 dead classes it finds (A1–A3) | Two page titles currently render at body size. A test makes it impossible to reintroduce | S |
| [#298](https://github.com/jnroesch/juggerhub/issues/298) | Contrast pass: retire `text-subtle`/`faint` as they stand or darken sand-4/5; darken the primary button background; give the focus ring its coral border. Add a token-level contrast test (C1–C5) | Fixes the literal cause of "washed out", and the accessibility failures are real independent of #278 | M |
| [#299](https://github.com/jnroesch/juggerhub/issues/299) | Typographic hierarchy: introduce the page-title and lead steps and apply them to the 81 `<h1>`s (D) | Biggest single change to "every page looks the same", and it is markup only | M |
| [#300](https://github.com/jnroesch/juggerhub/issues/300) | Icon consolidation: grow `jh-icon`'s set to the icons `DESIGN.md` names, replace the 99 inline SVGs and the 6 text-glyph markers (Part 1, E) | Closes the reporter's screenshot #1 and unifies stroke weight | M–L |
| [#301](https://github.com/jnroesch/juggerhub/issues/301) | Chip/pill primitive (`jh-chip`) with one padding; adopt it at the ~84 chip sites; give interactive chips a 44px target (B2, B6, Part 1) | Closes the reporter's screenshot #2 | M |
| [#302](https://github.com/jnroesch/juggerhub/issues/302) | Card padding decision (16 or 24) written into `jh-card` itself rather than each caller; adopt the primitive at the 59 hand-rolled surfaces; enable `[interactive]` (B4, B5) | Removes the largest remaining source of per-screen variance | M |
| [#303](https://github.com/jnroesch/juggerhub/issues/303) | Copy pass on the 101 ` — ` strings; fix the German dash to `–` throughout; unify the three error voices (F1, F2, F4) | Directly what the reporter named; pairs naturally with #84 | S–M |
| [#304](https://github.com/jnroesch/juggerhub/issues/304) | Identity decision: put the pompfen mark in the product, and decide whether anything gets drawn (G) | Needs the owner, not a patch | — |

#297–#303 are mechanical and verifiable. #304 is a product decision.

---

## Part 4 — What this audit did not cover

- Rendered output. Everything here is read from source and computed; nothing was measured in
  a browser. The font-fallback claim in Part 1 in particular wants a devtools check.
- Responsive behaviour at specific widths (the 5-tab bottom bar at 375px in German is a
  known watch item from `specs/050`).
- Screen-reader flow, keyboard traps, and motion preferences.
- The backend, which #278 does not touch.
- Spanish copy beyond dash counting.

## Sources

`DESIGN.md`; `frontend/apps/web/src/styles.css`; `frontend/apps/web/tailwind.config.js`;
`frontend/apps/web/src/app/core/design/spacing-scale.spec.ts`;
`frontend/apps/web/src/app/shared/ui/{card,button,icon,empty-state}`;
`frontend/apps/web/public/i18n/{en,de,es}.json` and `i18n/legal/*`; 106 component templates.
