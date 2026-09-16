import { NgTemplateOutlet } from '@angular/common';
import { Component, input } from '@angular/core';
import { CardComponent } from '../card/card.component';

/**
 * Shared empty-state primitive (feature 024). One centered, muted, warm treatment for
 * "nothing here yet" — replacing the four different containers the audit found. The
 * default (card) variant is the shared `jhCard` surface; `inline` is a compact row
 * for an `@empty` block inside an existing list. An optional `heading` sits above the
 * projected message; callers may include a next-step control (e.g. a `jhButton`) in the
 * projected content per DESIGN.md's "offer a next step" voice.
 */
@Component({
  selector: 'jh-empty-state',
  imports: [CardComponent, NgTemplateOutlet],
  templateUrl: './empty-state.component.html',
  styleUrl: './empty-state.component.css',
})
export class EmptyStateComponent {
  /** Optional heading shown above the message. */
  readonly heading = input<string | null>(null);
  /** Compact variant (no outer card) for an `@empty` row inside a list. */
  readonly inline = input(false, { transform: booleanish });
}

function booleanish(value: boolean | '' | null | undefined): boolean {
  return value === '' || value === true;
}
