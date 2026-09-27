import { Directive, ElementRef, Injector, afterNextRender, effect, inject, input } from '@angular/core';

/**
 * The motion vocabulary's `bump` (DESIGN.md "Motion vocabulary"): when the bound count goes up,
 * the `[data-bump]` element inside the host scales up once and settles.
 *
 * It sits on an element that is always rendered — the icon wrapper, not the badge — because the
 * badge only exists while the count is above zero, and the 0 → 1 rise is the one the reader most
 * needs to notice. The first value it sees is the page loading, not news, so it never bumps.
 *
 * The replay waits for `afterNextRender`: the badge that shows the new count may not be in the
 * DOM yet when the count changes (it is created by the same change), and in a zoneless app a
 * microtask is still too early to see it.
 */
@Directive({
  selector: '[jhBump]',
})
export class BumpDirective {
  readonly jhBump = input.required<number>();

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  private last: number | undefined;

  constructor() {
    effect(() => {
      const count = this.jhBump();
      const rose = this.last !== undefined && count > this.last;
      this.last = count;
      if (rose) {
        afterNextRender(() => this.play(), { injector: this.injector });
      }
    });
  }

  private play(): void {
    const target = this.host.nativeElement.querySelector<HTMLElement>('[data-bump]');
    if (!target) return;
    // Removing the class and forcing a layout read is what lets a second rise replay the
    // keyframe; re-adding a class that is already there does nothing.
    target.classList.remove('jh-bump');
    void target.offsetWidth;
    target.classList.add('jh-bump');
  }
}
