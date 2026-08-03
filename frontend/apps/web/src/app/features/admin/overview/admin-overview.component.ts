import { Component, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { LoadingComponent, CardComponent } from '../../../shared/ui';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AdminOverview } from '../../../core/models/admin.models';
import { AdminService } from '../../../core/services/admin.service';
import { problemDetail } from '../../../core/utils/problem';

/**
 * The admin landing (feature 013 US2, wireframe 1b): four counts that matter, then two
 * live lists that lead into the two real jobs — new players (→ find users) and recent
 * grants. The search box up top jumps straight into user management.
 */
@Component({
  selector: 'jh-admin-overview',
  imports: [DatePipe, RouterLink, FormsModule, LoadingComponent, CardComponent, TranslocoPipe],
  templateUrl: './admin-overview.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './admin-overview.component.css',
})
export class AdminOverviewComponent {
  private readonly api = inject(AdminService);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly overview = signal<AdminOverview | null>(null);
  protected readonly search = signal('');

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getOverview().subscribe({
      next: (o) => {
        this.overview.set(o);
        this.loading.set(false);
      },
      error: (e) => {
        this.error.set(problemDetail(e, this.transloco.translate('admin.overview.loadError')));
        this.loading.set(false);
      },
    });
  }

  /** The search box leads into user management with the query applied (wireframe 1b). */
  protected goSearch(): void {
    const q = this.search().trim();
    this.router.navigate(['/admin/users'], q ? { queryParams: { q } } : undefined);
  }
}
