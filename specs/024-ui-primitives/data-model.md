# Phase 1 Data Model: UI primitive & variant inventory

This feature has no persisted data. The "model" is the inventory of presentation
primitives and their variants — the stable vocabulary screens compose from. All
visual values resolve to DESIGN.md tokens (via `styles.css` / Tailwind); none are
hard-coded.

## Primitive: Button (`jhButton` directive)

Applied to a native `<button>` or `<a>`. Sets host classes only; preserves the
element's own `type`, `disabled`, `routerLink`, `aria-*`, `data-testid`, `(click)`.

| Input | Type | Default | Notes |
|-------|------|---------|-------|
| `variant` | `'primary' \| 'secondary' \| 'danger' \| 'ghost'` | `'primary'` | primary = coral; secondary = white + border-strong; danger = danger tokens; ghost = subtle text button |
| `size` | `'md' \| 'sm'` | `'md'` | `md` = `min-h-11` (44px); `sm` = dense/inline exception |
| `full` | `boolean` | `false` | `w-full` |

**Invariants** (all variants unless noted):
- `rounded-md`, weight 600, sentence-case label.
- Focus: always-visible coral ring.
- Hover: primary → `bg-brand-hover` + `shadow-coral`; secondary/ghost → warm bg.
- Press: `active:translate-y-px`.
- Primary label color: `text-on-accent` (never raw `text-white`).
- At most one `variant="primary"` per view (DESIGN.md; author-enforced, checklist item).

## Primitive: Card (`jh-card` component)

| Input | Type | Default | Notes |
|-------|------|---------|-------|
| `accent` | `boolean` | `false` | renders the 4px `bg-brand-gradient` top strip |
| `interactive` | `boolean` | `false` | adds hover lift (`-translate-y-[3px]` + deeper shadow) for clickable cards |

Content via `<ng-content>`. Invariants: `surface-card`, `border-border-muted`,
`rounded-lg`, `shadow-sm`, `p-6`(spacing-6).

## Primitive: Empty state (`jh-empty-state` component)

| Input | Type | Default | Notes |
|-------|------|---------|-------|
| `heading` | `string \| null` | `null` | optional `h4` heading |
| `inline` | `boolean` | `false` | compact variant for an `@empty` row inside an existing list (no outer card) |

Slots: default = message (warm, sentence-cased); `[action]` = optional next-step
control (usually a `jhButton`). Invariants: centered, `text-muted`, one container
(`rounded-lg border border-border-muted bg-surface-card p-lg` unless `inline`).

## Primitive: Loading (`jh-loading` component)

| Input | Type | Default | Notes |
|-------|------|---------|-------|
| `label` | `string` | `'Loading…'` | contextual labels allowed (`'Loading your profile…'`) |
| `align` | `'left' \| 'center'` | `'left'` | |

Invariants: single text line, `text-body-sm text-muted`, component-owned top
margin. Skeletons are **not** part of this primitive (dashboard keeps its own as a
documented exception).

## Primitive: Alert / error (`jh-alert` component)

| Input | Type | Default | Notes |
|-------|------|---------|-------|
| `tone` | `'danger' \| 'success' \| 'warning' \| 'info'` | `'danger'` | maps to the `*-fg/*-bg/*-border` token triples |

Content via `<ng-content>`. Invariants: boxed
(`rounded-md border px-md py-sm text-body-sm`), `role="alert"` always set, single
danger color = `danger-fg` (red-6). Retires bare `text-danger` (red-5) error text.

## Primitive: Page container (`jh-page-container` component)

| Input | Type | Default | Notes |
|-------|------|---------|-------|
| `width` | `'sm' \| 'md' \| 'lg' \| 'xl'` | `'md'` | page-type → container token (see research R6) |

Content via `<ng-content>`. Owns `mx-auto`, the `max-w-container-*` cap, and
horizontal page padding. Page-type mapping is the taxonomy in research R6.

## Primitive: Icon (`jh-icon` component)

| Input | Type | Default | Notes |
|-------|------|---------|-------|
| `name` | curated union (e.g. `'plus' \| 'search' \| 'bell' \| …`) | — (required) | keys of the curated Lucide map |
| `size` | `number` | `18` | px; 16–22 inline with text |

Invariants: inline SVG, 2px stroke, `currentColor`, `aria-hidden="true"` (decorative).
No runtime icon-library dependency.

## Variant coverage vs. audit findings

| Audit finding | Primitive / variant that resolves it |
|---------------|--------------------------------------|
| Button radius/height/hover/press/focus/`text-white` drift | `jhButton` invariants + `md` 44px + `text-on-accent` |
| Two error visual languages & two reds; inconsistent `role="alert"` | `jh-alert` (one box, `danger-fg`, always `role="alert"`) |
| Four empty-state treatments; bare dead-ends | `jh-empty-state` + `[action]` slot |
| Loading color/size/margin/copy scatter | `jh-loading` standardized text line |
| Arbitrary container widths | `jh-page-container` width taxonomy |
| Literal `+` glyph as icon | `jh-icon name="plus"` |
| Terminology "invite"/"invitation" | copy sweep to canonical **"invite"** (not a primitive) |
