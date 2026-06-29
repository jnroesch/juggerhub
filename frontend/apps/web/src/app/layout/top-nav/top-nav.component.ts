import { Component, output } from '@angular/core';

/**
 * Top navigation bar. Shows the brand and, below the sidebar breakpoint, a menu
 * button that toggles the off-canvas sidebar drawer.
 */
@Component({
  selector: 'jh-top-nav',
  imports: [],
  templateUrl: './top-nav.component.html',
  styleUrl: './top-nav.component.css',
})
export class TopNavComponent {
  readonly menuToggle = output<void>();
}
