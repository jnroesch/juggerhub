import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TopNavComponent } from '../top-nav/top-nav.component';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { AuthService } from '../../core/services/auth.service';

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
export class ShellComponent implements OnInit {
  private readonly auth = inject(AuthService);

  protected readonly sidebarOpen = signal(false);

  ngOnInit(): void {
    // Hydrate auth state once so the nav reflects a real session (incl. silent refresh).
    this.auth.loadSession().subscribe();
  }

  toggleSidebar(): void {
    this.sidebarOpen.update((open) => !open);
  }

  closeSidebar(): void {
    this.sidebarOpen.set(false);
  }
}
