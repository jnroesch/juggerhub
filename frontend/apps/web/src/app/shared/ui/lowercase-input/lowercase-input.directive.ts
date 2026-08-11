import { Directive, ElementRef, HostListener, inject } from '@angular/core';
import { NgControl } from '@angular/forms';

/**
 * Folds typed (and pasted) input to lowercase as it is entered, for the fields whose
 * stored value is a lowercase-only identifier — the `@handle` and the team address.
 *
 * Those formats reject uppercase outright, so without this the first capital a person
 * types turns the field red for a rule they cannot see until they break it. Folding is
 * strictly narrower than that rule: it only removes the *case* failure, so spaces,
 * umlauts and punctuation — the things a pasted team name actually contains — still
 * surface the format warning.
 *
 * The caret is restored after the rewrite; without it, correcting a capital in the
 * middle of an existing value would throw the cursor to the end on every keystroke.
 */
@Directive({
  selector: 'input[jhLowercase]',
})
export class LowercaseInputDirective {
  private readonly el = inject<ElementRef<HTMLInputElement>>(ElementRef);
  /** Optional: the directive is useful on a plain input too, it then only rewrites the DOM. */
  private readonly control = inject(NgControl, { optional: true, self: true });

  @HostListener('input')
  protected onInput(): void {
    const input = this.el.nativeElement;
    const lowered = input.value.toLowerCase();
    if (lowered === input.value) {
      return;
    }

    // Case folding never changes the length, so the caret offset carries over unchanged.
    const caret = input.selectionStart;
    input.value = lowered;
    if (caret !== null) {
      input.setSelectionRange(caret, caret);
    }

    // Keep the form model in step. The value accessor's own listener may run either side
    // of this one; setting both the DOM and the control leaves the same result regardless.
    this.control?.control?.setValue(lowered);
  }
}
