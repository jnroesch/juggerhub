# UI Contract: shared primitives (`app/shared/ui/`)

The public "interface" of this frontend feature is the set of Angular
selectors/inputs/slots screens consume. This contract is the stable surface;
internal class strings may change as long as the DESIGN.md-conformant behavior
below holds. All are standalone, `jh-`-prefixed, zoneless-safe, with separate
`.html`/`.css`/`.ts` (constitution VI).

## `jhButton` (attribute directive)

```
selector: 'button[jhButton], a[jhButton]'
inputs:   variant='primary'|'secondary'|'danger'|'ghost' (default 'primary')
          size='md'|'sm' (default 'md')
          full=boolean (default false)
```

Contract:
- Host is a native `<button>`/`<a>`; the directive never intercepts `disabled`,
  `type`, `routerLink`, click handlers, `aria-*`, or `data-testid`.
- `size='md'` guarantees rendered height ≥ 44px.
- A visible focus indicator appears on keyboard focus for every variant.
- `variant='primary'` uses coral brand bg + `text-on-accent`, hover glow, press nudge.

Usage:
```html
<button jhButton (click)="save()" [disabled]="busy()">{{ busy() ? 'Saving…' : 'Save' }}</button>
<a jhButton variant="secondary" routerLink="/browse/events">Browse open events</a>
<button jhButton variant="danger" (click)="remove()">Remove</button>
```

## `<jh-card>`

```
selector: 'jh-card'
inputs:   accent=boolean (default false), interactive=boolean (default false)
content:  default slot
```
Contract: white surface, muted border, `rounded-lg`, soft shadow; `accent` adds the
4px brand gradient strip; `interactive` adds the hover lift.

## `<jh-empty-state>`

```
selector: 'jh-empty-state'
inputs:   heading=string|null (default null), inline=boolean (default false)
content:  default slot (message), [action] slot (optional next step)
```
Contract: one centered, muted, sentence-cased treatment; `[action]` renders a
next-step control when provided.

```html
<jh-empty-state heading="No messages yet">
  Start a conversation with a teammate.
  <a jhButton ngProjectAs="[action]" routerLink="/chat/new">New message</a>
</jh-empty-state>
```

## `<jh-loading>`

```
selector: 'jh-loading'
inputs:   label=string (default 'Loading…'), align='left'|'center' (default 'left')
```
Contract: single `text-body-sm text-muted` line with component-owned spacing.

## `<jh-alert>`

```
selector: 'jh-alert'
inputs:   tone='danger'|'success'|'warning'|'info' (default 'danger')
content:  default slot (message)
```
Contract: boxed treatment, `role="alert"` always present, tone→token triple;
danger uses `danger-fg`.

```html
@if (error()) { <jh-alert>{{ error() }}</jh-alert> }
```

## `<jh-page-container>`

```
selector: 'jh-page-container'
inputs:   width='sm'|'md'|'lg'|'xl' (default 'md')
content:  default slot
```
Contract: centers content, caps at the `max-w-container-<width>` token, owns
horizontal page padding. Width chosen per the page-type taxonomy (research R6).

## `<jh-icon>`

```
selector: 'jh-icon'
inputs:   name=<curated Lucide key> (required), size=number (default 18)
```
Contract: inline 2px-stroke `currentColor` SVG, `aria-hidden="true"`; only curated
names exist (no arbitrary/text-glyph icons).

## Behavior-preservation contract (migration)

For every migrated screen:
- Same actions, same visible label meaning, same `data-testid` values, same routes.
- Same `disabled`/busy/loading gating and the same conditional (`@if`) structure.
- Pre-existing Jest specs and Playwright e2e pass unchanged.
- No user-facing behavior added or removed — visual assembly only.
