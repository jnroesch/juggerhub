import { Component } from '@angular/core';
import { LegalPageComponent } from '../legal-page.component';

/** Reading order for the imprint (feature 036). Short enough that it needs no table of contents. */
export const IMPRINT_SECTIONS = ['operator', 'contact', 'responsibility', 'disputes'] as const;

/** The imprint (Impressum) at `/imprint` — public, unguarded, and outside the app shell. */
@Component({
  selector: 'jh-imprint',
  imports: [LegalPageComponent],
  templateUrl: './imprint.component.html',
})
export class ImprintComponent {
  protected readonly sections = IMPRINT_SECTIONS;
}
