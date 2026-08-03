import { Component, input } from '@angular/core';

/**
 * Shared card primitive (feature 024). A white `surface-card` panel with a 1px muted
 * border, `lg` radius, and a soft `sm` shadow — the DESIGN.md card. `accent` adds the
 * signature 4px coral→sage gradient strip along the top; `interactive` adds the 3px
 * hover lift + deeper shadow for clickable cards. Content is projected. The surface
 * itself is styled on the host (see card.component.css) so callers just wrap content.
 */
@Component({
  selector: 'jh-card',
  templateUrl: './card.component.html',
  styleUrl: './card.component.css',
  host: {
    '[class.jh-card--interactive]': 'interactive()',
  },
})
export class CardComponent {
  /** Render the 4px brand-gradient accent strip at the top. */
  readonly accent = input(false, { transform: booleanish });
  /** Add the hover lift + deeper shadow (for clickable cards). */
  readonly interactive = input(false, { transform: booleanish });
}

function booleanish(value: boolean | '' | null | undefined): boolean {
  return value === '' || value === true;
}
