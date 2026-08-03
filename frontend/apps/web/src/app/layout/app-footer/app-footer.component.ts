import { Component } from '@angular/core';
import { LegalLinksComponent } from '../../shared/ui';

/**
 * The app-wide footer strip (feature 036).
 *
 * Its only job is to make the privacy policy and imprint reachable from anywhere inside the
 * shell, in one click, signed in or out (FR-002). It renders in BOTH states — it sits outside
 * the shell's `@if (anonymous())` branch — because a signed-out visitor is precisely the reader
 * a privacy policy exists for.
 *
 * Mount position is load-bearing: it goes after `<main>`, which already carries `pb-[76px]` to
 * reserve room for the fixed mobile bottom bar, so the footer clears that bar instead of
 * disappearing underneath it. See specs/036-privacy-policy-imprint/contracts/routes.md §2.2.
 */
@Component({
  selector: 'jh-app-footer',
  imports: [LegalLinksComponent],
  templateUrl: './app-footer.component.html',
  styleUrl: './app-footer.component.css',
})
export class AppFooterComponent {}
