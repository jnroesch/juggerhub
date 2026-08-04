# UI Review Checklist: Browse Public Trainings

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before a feature is considered done.
**Created**: 2026-08-04
**Feature**: [spec.md](../spec.md)

**How to use**: This is an *implementation-quality* gate, run **after** UI is built and
**before** verification — not a spec-quality gate like `requirements.md`. Check each item
against the diff, recording `file:line` for anything that fails.
[DESIGN.md](../../../DESIGN.md) is the source of truth: if a check ever conflicts with it,
DESIGN.md wins and the conflict is reported rather than silently resolved.

## Color & tokens

- [x] CHK001 Components reference **semantic aliases** (`surface-card`, `text-body`, `brand-primary`, `border-default`…), never raw scale steps (`sand-4`, `coral-5`)
- [x] CHK002 **Exactly one coral `brand-primary` CTA per view**; supporting actions use sage `brand-secondary`
- [x] CHK003 Lemon `brand-highlight` is used only for small pops ("New" badges, streaks, dots) — never large fields
- [x] CHK004 Status (success/danger/warning/info) uses the paired `*-bg` / `*-border` / `*-fg` tokens, not ad-hoc colors
- [x] CHK005 No new colors introduced ad hoc — any new value was added to DESIGN.md tokens first

## Typography, numbers & voice

- [x] CHK006 Headings/hero use the **Hubot Sans** display face; body and UI text use **Mona Sans**
- [x] CHK007 Scores, stats, times, and counts are set in the **mono** face (tabular)
- [x] CHK008 **Sentence case everywhere** (headings, buttons, labels, nav); UPPERCASE only as a styled eyebrow
- [x] CHK009 Nothing meaningful drops below 12px (`caption`); body is 16px (`body-md`)
- [x] CHK010 Copy addresses the reader as **"you"** / the community as **"we"**; CTAs invite, never shout; no emoji in product UI

## Layout & spacing

- [x] CHK011 Interactive controls (buttons, inputs) have a **touch target ≥ 44px**
- [x] CHK012 Spacing composes from the 4px scale tokens (`space-1`…`space-13`) — no arbitrary pixel values
- [x] CHK013 Content sits in a centered column capped at `container-lg` (1100px); layout is mobile-first and reflows down
- [x] CHK014 Section rhythm uses `section-gap` (`clamp(48px, 8vw, 112px)`)

## Shape & elevation

- [x] CHK015 **No sharp corners** — radius matches element type (controls `sm`, buttons/inputs `md`, cards `lg`, media `xl`, chips/avatars `pill`)
- [x] CHK016 Shadows are the warm-tinted `xs`…`xl` tokens (`rgba(64,46,24,…)`) — never pure black, never harsh
- [x] CHK017 Cards are a white `surface-card` with a 1px muted border and soft `sm` shadow; they **lift 3px + deepen shadow on hover**
- [x] CHK018 Larger shadows are reserved for elements that genuinely float above the page

## Motion & states

- [x] CHK019 Transitions use the `fast`/`base`/`slow` durations (120/200/320ms) and token easings (`ease-out` entrances, `ease-bounce` for toggles)
- [x] CHK020 Focus is always visible: 2px coral border + coral `focus-ring`
- [x] CHK021 Buttons darken a brand step + gain a colored glow on hover, and nudge down 1px / scale 0.99 on press
- [x] CHK022 No infinite decorative animation loops in content

## Iconography

- [x] CHK023 Icons are **Lucide line icons** only (no filled/duotone), 16–22px, colored via `currentColor` or a token
- [x] CHK024 No emoji used as UI icons

## Accessibility

- [x] CHK025 Body text meets **WCAG AA contrast (≥ 4.5:1)** against its surface
- [x] CHK026 Status is **never conveyed by color alone** — paired with text or an icon
- [x] CHK027 Interactive elements are keyboard-reachable with a visible focus state and appropriate labels/roles

## Empty, loading & error states

- [x] CHK028 Empty states offer a warm, low-pressure next step (e.g. "Be the first to…")
- [x] CHK029 Loading and error states exist and are styled to the system (not raw/unstyled)

## Feature-specific UI

> The tab strip is the item that needs judgement rather than a tick. Everything else on this
> page inherits from the shared browse shell, which three pages already use.

- [x] CHK030 The discovery tab strip renders **four** destinations legibly at **375px** in **en, de and es** — no clipped, wrapped-to-illegible, or overlapping labels (FR-026 / SC-008). Spanish "Entrenamientos" is the binding case
- [x] CHK031 The fix for CHK030 is **not** a reduced font size (would breach CHK009) and **not** a truncation or ellipsis (a tab whose label cannot be read is not a usable control)
- [x] CHK032 Each tab keeps a ≥44px touch target after the fourth is added (CHK011 applied to the strip specifically)
- [x] CHK033 The active tab remains visually distinguishable by more than colour alone (CHK026 applied to the strip)
- [x] CHK034 The Series / One-off badge on a row uses the existing pill chip treatment already used by the events type badge — no new chip style
- [x] CHK035 The row's team name is visually subordinate to the training name, and the whole row is a single tap target rather than nested competing links
- [x] CHK036 The empty state ("no team has opened a training yet") reads warmly and is distinct from the no-results state ("nothing matches your filters"), per CHK028
- [x] CHK037 The city picker in the filter panel matches the existing country picker's field treatment — it is the product's first city *filter*, so it must not look like a new control class

## Notes

**How each item was verified (2026-08-04).** Not every tick carries the same weight, and saying so
is the point of the record.

**Verified directly against this diff, with evidence:**

| Item | Evidence |
|---|---|
| CHK001, CHK005 | `grep` for raw scale steps (`sand-*`, `coral-N`, `lemon-*`) and hex literals across the new templates — no matches |
| CHK009, CHK011, CHK030, CHK031, CHK032 | Playwright measurement in `browse.spec.ts` at both viewport projects: every tab ≥44px high, `scrollWidth <= clientWidth`, no pairwise overlap. Plus a second case substituting the **longest label from each catalogue** ("Entrenamientos", "Veranstaltungen") and re-measuring, so the Spanish case is covered without driving the language switcher. Font size is unchanged from the existing tabs and nothing is truncated |
| CHK010, CHK024 | `grep` for emoji code points across the new templates and all three catalogues — no matches |
| CHK029, CHK036 | Unit test `distinguishes the empty state from no-results`; both states come from the shared shell |
| CHK034, CHK035 | Template inspection: the badge reuses the events row's `rounded-pill border border-border px-sm py-0 text-caption text-subtle`; the team name is `text-body-sm text-subtle` under a `text-body-md text-ink` name, and the whole row is one `<a>` with no nested links |
| CHK037 | Template inspection: `jh-city-picker` sits in the same `flex flex-col gap-xs` + eyebrow-label wrapper as `jh-country-picker` |

**Inherited, not introduced by this page** — these belong to `jh-browse-shell` / `jh-filter-panel`,
which three existing pages already ship, and this feature changed none of them: CHK002–CHK008,
CHK013–CHK023, CHK025–CHK028, CHK033. Ticked as "no regression introduced", **not** as a fresh audit
of the design system.

**⚠ One real caveat, on CHK012 (spacing from the 4px scale, no arbitrary pixel values).** The tab
strip uses `min-h-[44px]`, which is an arbitrary Tailwind value, not a scale token. It is kept
deliberately: the 44px touch minimum in CHK011 is a hard accessibility floor that has no
corresponding spacing token, and the codebase already expresses it this way (for example
`dashboard.component.html:72`). Flagged rather than silently ticked — if DESIGN.md ever gains a
`touch-min` token, this and the existing call sites should move to it.

- Conventions reminder ([constitution](../../../.specify/memory/constitution.md) VI): keep `.html` / `.css` / `.ts` separate per component — this page does.
- If a check conflicts with DESIGN.md, DESIGN.md wins — note the conflict here rather than resolving it silently.
- ⚠ Standing known conflict, not introduced by this feature: DESIGN.md demands ≥4.5:1 (CHK025) while
  primary buttons are specced white-on-coral-4 at 3.14:1. This page introduces no new primary button,
  so it neither fixes nor worsens that; do not "resolve" it here.
