import { Signal, linkedSignal } from '@angular/core';

/**
 * The motion vocabulary's `step` for a wizard (DESIGN.md "Motion vocabulary"): the class a step's
 * root enters with, bound as `[animate.enter]="stepMotion()"`.
 *
 * Direction comes from the step order, not from which button was pressed, so a jump — the review
 * step's "edit" link back to the first step — travels the right way too. The step the wizard opens
 * on (including one restored from a draft) enters with no class: there was no step before it.
 */
export function stepMotion<T>(step: Signal<T>, order: readonly T[]): Signal<string> {
  return linkedSignal<T, string>({
    source: step,
    computation: (current, previous) => {
      if (previous === undefined) return '';
      return order.indexOf(current) >= order.indexOf(previous.source) ? 'jh-step-forward' : 'jh-step-back';
    },
  });
}
