import { Component, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

interface NavItem {
  readonly label: string;
  readonly path: string;
}

/**
 * Primary navigation. On desktop it sits alongside the content; below the
 * breakpoint it becomes an off-canvas drawer toggled from the top nav, with a
 * backdrop. Navigation stays reachable at every viewport (FR-025).
 */
@Component({
  selector: 'jh-sidebar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.css',
})
export class SidebarComponent {
  /** Whether the mobile drawer is open. Ignored at desktop width. */
  readonly open = input(false);

  /** Emitted when the drawer should close (backdrop click / link nav). */
  readonly closed = output<void>();

  protected readonly items: readonly NavItem[] = [
    { label: 'Dashboard', path: '/' },
    { label: 'Create team', path: '/teams/new' },
    { label: 'Account', path: '/account' },
  ];
}
