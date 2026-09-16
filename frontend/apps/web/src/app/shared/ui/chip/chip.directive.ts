import { Directive, ElementRef, Renderer2, effect, inject, input } from '@angular/core';

/**
 * The colour family a chip wears. The tone carries the whole appearance — background,
 * text and (where it has one) border — so a call site never writes a colour class of
 * its own and a chip can never be half one tone and half another.
 */
export type ChipTone =
  | 'muted'
  | 'secondary'
  | 'accent'
  | 'brand'
  | 'outline'
  | 'success'
  | 'warning'
  | 'danger'
  | 'info';

/**
 * Shared chip primitive (GH #301). The small pill that labels a thing — "Beginners
 * welcome", "Admin", "Cancelled", "Runner" — and, on a `<button>` or `<a>`, the pill
 * that filters a list.
 *
 * It is an **attribute directive**, like `jhButton` and for the same reason: the chip is
 * always drawn *on* an element the markup already needed — a `<span>` in a sentence, an
 * `<a>` to a team, a `<button>` in a filter row — and applying classes to that element
 * keeps its semantics, its `data-testid` and its place in the DOM exactly as they were.
 *
 * ## Why it exists
 *
 * #278 reported text crowding the edge of a box; the box was the "beginners welcome" chip,
 * `px-sm py-0` inside a 999px border, with nothing but the font's own leading between the
 * glyphs and the curve. Behind that one chip the audit found **the same element built
 * eleven ways across ~84 sites** — `px-xs py-0.5`(18), `px-sm py-0.5`(17), `px-sm py-1`(8),
 * `px-sm py-0`(7), `px-xs py-3xs`(6), `px-md py-1.5`(4), `px-md py-xs`(4), `px-1`(5),
 * `px-sm py-xs`(2), `px-sm py-3xs`(2), `px-lg py-sm`(1) — with 21 of them at no vertical
 * padding at all, `py-0.5` and `py-3xs` spelling the same 2px two ways, and `py-1.5` (6px)
 * off the 4px base entirely.
 *
 * ## The two forms, and why there are two
 *
 * - **A label** (`<span jhChip>`, `<li jhChip>`) — `px-sm py-2xs` (12/4) at `text-caption`.
 *   DESIGN.md reserves the 2px half-step for "the vertical padding of pills", which is
 *   where `py-0.5` came from; at 12px text that is a 21px pill, and inside a pill border
 *   it reads as the crowding #278 reported. 4px is the base step, and the smallest one
 *   that is a padding rather than a hairline.
 * - **A control** (`<button jhChip>`, `<a jhChip>`) — `px-md py-xs` at `text-body-sm`,
 *   **`min-h-11`**. That inset is not a twelfth spelling: it is `jhButton`'s `sm` size,
 *   because a chip you can press is a button that happens to be pill-shaped. The 44px
 *   comes from DESIGN.md's touch-target rule, which the filter chips on four Browse tabs
 *   and two Marketplace boards were failing at roughly 19–25px tall — the primary
 *   filtering control of those pages, too small to hit.
 *
 * The form follows the host element, with no input to set: a `<button>` or an `<a>` is a
 * control, anything else is a label. A chip cannot be given a press target it doesn't
 * answer, or be made pressable without one.
 *
 * ## What is *not* a chip
 *
 * A pill whose box is set by an explicit `h-*` / `size-*` / `min-w-*` — an avatar, the
 * unread counter over a nav icon, a ranking number, a progress track — is not a chip.
 * Its height does not come from its padding, so `py-0` on it is not the crowding this
 * primitive fixes, and 44px would be wrong for a decoration nobody presses.
 */
@Directive({
  selector: '[jhChip]',
})
export class ChipDirective {
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly renderer = inject(Renderer2);

  /** Colour family. Default `muted` — the quiet grey pill. */
  readonly tone = input<ChipTone>('muted');

  /** Classes this directive currently owns, so a tone change removes them cleanly. */
  private applied: string[] = [];

  constructor() {
    effect(() => {
      const next = compose(this.tone(), isControl(this.el.nativeElement));
      const host = this.el.nativeElement;
      for (const cls of this.applied) {
        this.renderer.removeClass(host, cls);
      }
      for (const cls of next) {
        this.renderer.addClass(host, cls);
      }
      this.applied = next;
    });
  }
}

/** A chip you can press. Derived from the element, never from an input — see the class doc. */
function isControl(host: HTMLElement): boolean {
  return host.tagName === 'BUTTON' || host.tagName === 'A';
}

/** Background / text / border per tone, plus the hover a control adds on top of it. */
const TONES: Record<ChipTone, { rest: string[]; hover: string }> = {
  muted: { rest: ['bg-surface-muted', 'text-muted'], hover: 'hover:bg-surface-sunken' },
  secondary: { rest: ['bg-surface-secondary-soft', 'text-secondary'], hover: 'hover:bg-teal-1' },
  accent: {
    rest: ['border', 'border-brand', 'bg-surface-accent-soft', 'text-brand'],
    hover: 'hover:bg-coral-1',
  },
  brand: { rest: ['bg-brand', 'text-on-accent'], hover: 'hover:bg-brand-hover' },
  outline: {
    rest: ['border', 'border-border-strong', 'text-subtle'],
    hover: 'hover:bg-surface-sunken',
  },
  success: { rest: ['bg-success-bg', 'text-success-fg'], hover: 'hover:bg-success-border' },
  warning: { rest: ['bg-warning-bg', 'text-warning-fg'], hover: 'hover:bg-warning-border' },
  danger: { rest: ['bg-danger-bg', 'text-danger-fg'], hover: 'hover:bg-danger-border' },
  info: { rest: ['bg-info-bg', 'text-info-fg'], hover: 'hover:bg-info-border' },
};

function compose(tone: ChipTone, control: boolean): string[] {
  const base = ['inline-flex', 'items-center', 'justify-center', 'gap-2xs', 'rounded-pill'];

  const shape = control
    ? [
        'min-h-11',
        'px-md',
        'py-xs',
        'text-body-sm',
        'font-medium',
        'cursor-pointer',
        'transition-colors',
        'duration-fast',
        'ease-standard',
        'focus-visible:outline-none',
        'focus-visible:ring-2',
        'focus-visible:ring-focus',
        'disabled:opacity-50',
        'disabled:pointer-events-none',
        TONES[tone].hover,
      ]
    : ['px-sm', 'py-2xs', 'text-caption', 'font-medium'];

  return [...base, ...shape, ...TONES[tone].rest];
}
