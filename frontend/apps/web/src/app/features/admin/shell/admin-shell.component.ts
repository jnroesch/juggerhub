import { Component, computed, inject } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';

/**
 * The admin area shell (feature 013, wireframe 1a/1b): its own shield header with a
 * clear "Back to app", a sidebar on desktop and a bottom tab bar on mobile
 * (Overview · Users · Catalogue), and the routed admin pages inside. Rendering is
 * UX only — every admin operation is enforced server-side by the PlatformAdmin policy.
 */
@Component({
  selector: 'jh-admin-shell',
  imports: [RouterLink, RouterOutlet],
  templateUrl: './admin-shell.component.html',
  styleUrl: './admin-shell.component.css',
})
export class AdminShellComponent {
  private readonly router = inject(Router);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );

  protected readonly overviewActive = computed(() => {
    const path = this.url().split('?')[0];
    return path === '/admin' || path === '/admin/';
  });

  protected readonly usersActive = computed(() => this.url().split('?')[0].startsWith('/admin/users'));

  protected readonly teamsActive = computed(() => this.url().split('?')[0].startsWith('/admin/teams'));

  protected readonly catalogueActive = computed(() => this.url().split('?')[0].startsWith('/admin/catalogue'));
}
