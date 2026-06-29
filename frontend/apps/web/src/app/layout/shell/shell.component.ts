import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TopNavComponent } from '../top-nav/top-nav.component';
import { SidebarComponent } from '../sidebar/sidebar.component';

/**
 * Application shell — top navigation + sidebar around the routed page content.
 * Responsive: the sidebar is static on desktop and an off-canvas drawer on
 * mobile, toggled from the top nav.
 */
@Component({
  selector: 'jh-shell',
  imports: [RouterOutlet, TopNavComponent, SidebarComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.css',
})
export class ShellComponent {
  protected readonly sidebarOpen = signal(false);

  toggleSidebar(): void {
    this.sidebarOpen.update((open) => !open);
  }

  closeSidebar(): void {
    this.sidebarOpen.set(false);
  }
}
