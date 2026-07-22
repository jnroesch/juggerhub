import { Directive, ElementRef, Renderer2, effect, inject, input } from '@angular/core';

/** Visual variant of a button/link. */
export type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'ghost';
/** Control size. `md` (default) meets the DESIGN.md 44px touch target. */
export type ButtonSize = 'md' | 'sm';

/**
 * Shared button primitive (feature 024). An **attribute directive** applied to a
 * native `<button>` / `<a>` so the element keeps its own semantics — `type`,
 * `disabled`, `routerLink`, focus order, `aria-*`, `data-testid`, click handlers —
 * while the directive owns only the DESIGN.md-conformant look and behaviour
 * (44px height on `md`, `rounded-md`, coral hover glow, 1px press nudge, an
 * always-visible coral focus ring, and the canonical on-accent token).
 *
 * Classes are applied additively via Renderer2 so any layout utilities already on
 * the host (e.g. `w-full`, `mt-lg`) are preserved. Only design-system tokens are
 * used — never raw hex or scale steps (spec FR-008).
 */
@Directive({
  selector: 'button[jhButton], a[jhButton]',
})
export class ButtonDirective {
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly renderer = inject(Renderer2);

  readonly variant = input<ButtonVariant>('primary');
  readonly size = input<ButtonSize>('md');
  /** Stretch to the full width of the container. */
  readonly full = input(false, { transform: booleanish });

  /** Classes this directive currently owns, so a variant/size change removes them cleanly. */
  private applied: string[] = [];

  constructor() {
    effect(() => {
      const next = this.compose(this.variant(), this.size(), this.full());
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

  private compose(variant: ButtonVariant, size: ButtonSize, full: boolean): string[] {
    const base = [
      'inline-flex',
      'items-center',
      'justify-center',
      'gap-xs',
      'rounded-md',
      'font-semibold',
      'text-center',
      'transition-all',
      'duration-fast',
      'ease-standard',
      'active:translate-y-px',
      'focus-visible:outline-none',
      'focus-visible:ring-2',
      'focus-visible:ring-focus',
      'disabled:opacity-50',
      'disabled:pointer-events-none',
    ];

    const sizing =
      size === 'md'
        ? ['min-h-11', 'px-lg', 'py-sm', 'text-body-md']
        : ['min-h-9', 'px-md', 'py-xs', 'text-body-sm'];

    const width = full ? ['w-full'] : [];

    const variants: Record<ButtonVariant, string[]> = {
      primary: ['bg-brand', 'text-on-accent', 'hover:bg-brand-hover', 'hover:shadow-coral'],
      secondary: [
        'bg-surface-card',
        'text-body',
        'border',
        'border-border-strong',
        'hover:bg-surface-sunken',
      ],
      danger: [
        'bg-surface-card',
        'text-danger-fg',
        'border',
        'border-danger-border',
        'hover:bg-danger-bg',
      ],
      ghost: ['bg-transparent', 'text-body', 'hover:bg-surface-sunken'],
    };

    return [...base, ...sizing, ...width, ...variants[variant]];
  }
}

/** Accept both a bare attribute (`full`) and a bound boolean (`[full]="x"`). */
function booleanish(value: boolean | '' | null | undefined): boolean {
  return value === '' || value === true;
}
