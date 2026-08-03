import { Component, input, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';

/** `footer` for the app footer strip; `inline` for the compact form on off-shell screens. */
export type LegalLinksVariant = 'footer' | 'inline';

/**
 * The privacy / imprint link cluster (feature 036).
 *
 * One component, three placements — the app footer inside the shell (covering signed-out and
 * signed-in, desktop and mobile), and inline at the bottom of the nine screens that render
 * outside the shell. That is what makes FR-002's "≤2 clicks from any screen" hold everywhere
 * rather than only where someone remembered to add a link. See
 * specs/036-privacy-policy-imprint/contracts/routes.md §2.
 *
 * Presentation only: no injected service, no state, no API call. The labels come from the MAIN
 * translation catalog (`legal.*`), not the lazy `legal` scope — the footer renders on every
 * screen and cannot wait for a scope that only loads on the legal routes.
 */
@Component({
  selector: 'jh-legal-links',
  imports: [RouterLink, TranslocoDirective],
  templateUrl: './legal-links.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './legal-links.component.css',
})
export class LegalLinksComponent {
  readonly variant = input<LegalLinksVariant>('footer');
}
