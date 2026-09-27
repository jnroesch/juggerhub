import { Directive, Injectable, Injector, afterNextRender, inject, input } from '@angular/core';

/**
 * Decides which items get to `rise` on a page (DESIGN.md "Motion vocabulary"): the first batch
 * that renders on a visit, and nothing after it. A page provides one in its `providers`, so the
 * scope lives exactly as long as the visit — a reload, a new filter, a search result or another
 * page of "load more" renders into a closed scope and stays still, and coming back to the page
 * builds a new one.
 *
 * It closes one render after the first item claimed it, so every item created in that same pass
 * rises together (staggered by index) and nothing later does.
 */
@Injectable()
export class RiseScope {
  private readonly injector = inject(Injector);
  private open = true;
  private closing = false;

  claim(): boolean {
    if (!this.open) return false;
    if (!this.closing) {
      this.closing = true;
      afterNextRender(() => (this.open = false), { injector: this.injector });
    }
    return true;
  }
}

/**
 * `[jhRise]="$index"` on an item of a first-load list, or a fixed position on a page's modules.
 * Without a `RiseScope` above it, it never rises — the safe default for a list nobody decided about.
 */
@Directive({
  selector: '[jhRise]',
  host: {
    '[class.jh-rise]': 'rises',
    '[style.--jh-stagger]': 'jhRise()',
  },
})
export class RiseDirective {
  readonly jhRise = input.required<number>();

  /** Decided once, when the item is created — an item never starts rising later. */
  protected readonly rises = inject(RiseScope, { optional: true })?.claim() ?? false;
}
