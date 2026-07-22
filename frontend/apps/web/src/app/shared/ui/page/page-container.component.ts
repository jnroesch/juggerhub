import { Component, computed, input } from '@angular/core';

/** Page-type → DESIGN.md container token (see specs/024-ui-primitives/research.md R6). */
export type PageWidth = 'sm' | 'md' | 'lg' | 'xl';

/**
 * Shared page container primitive (feature 024). Centers a page's content column and
 * caps it at one of the DESIGN.md container widths, owning the horizontal page padding
 * so screens stop re-declaring `mx-auto max-w-container-* px-*`. Width maps to page
 * type: `sm` forms, `md` standard content (default), `lg` dashboard/admin, `xl` the
 * chat two-pane.
 */
@Component({
  selector: 'jh-page-container',
  templateUrl: './page-container.component.html',
  styleUrl: './page-container.component.css',
})
export class PageContainerComponent {
  readonly width = input<PageWidth>('md');

  protected readonly widthClass = computed(
    () =>
      ({
        sm: 'max-w-container-sm',
        md: 'max-w-container-md',
        lg: 'max-w-container-lg',
        xl: 'max-w-container-xl',
      })[this.width()],
  );
}
