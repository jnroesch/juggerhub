import { Component, OnInit, inject, signal } from '@angular/core';
import { Health, HealthService } from '../../core/services/health.service';

/**
 * US1 — the walking skeleton's dashboard. Fetches backend health on load and
 * renders the live status, proving the frontend → API → database round trip.
 */
@Component({
  selector: 'jh-dashboard',
  imports: [],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css',
})
export class DashboardComponent implements OnInit {
  private readonly healthService = inject(HealthService);

  protected readonly health = signal<Health | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  ngOnInit(): void {
    this.healthService.getHealth().subscribe({
      next: (health) => {
        this.health.set(health);
        this.loading.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      },
    });
  }
}
