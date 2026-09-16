import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { Subject, debounceTime } from 'rxjs';
import { AdminPlacement } from '../../../core/models/results.models';
import { AdminService } from '../../../core/services/admin.service';
import { problemDetail } from '../../../core/utils/problem';
import { AlertComponent, ButtonDirective, CardComponent, ChipDirective, EmptyStateComponent, IconComponent, LoadingComponent } from '../../../shared/ui';
import { AdminTeamPickerComponent } from '../shared/team-picker.component';

const PAGE_SIZE = 20;

/**
 * Tournament placements (feature 050, US6): the platform admins' queue of placements not yet
 * connected to a team. This is how teams that played without a JuggerHub sign-up — every team of a
 * past tournament, sign-ups run elsewhere, guests added on the day — get their results.
 *
 * One placement at a time, by a person who checked (owner decision): connecting touches exactly the
 * row it was made on. The recorded name is shown next to the connection so the check is possible.
 * The server `PlatformAdmin` policy is the boundary.
 */
@Component({
  selector: 'jh-admin-results',
  imports: [
    CardComponent,
    ChipDirective,
    RouterLink,
    TranslocoPipe,
    TranslocoDatePipe,
    AlertComponent,
    ButtonDirective,
    EmptyStateComponent,
    LoadingComponent,
    AdminTeamPickerComponent,
    IconComponent,
  ],
  templateUrl: './admin-results.component.html',
  styleUrl: './admin-results.component.css',
})
export class AdminResultsComponent {
  private readonly api = inject(AdminService);
  private readonly transloco = inject(TranslocoService);

  protected readonly connected = signal(false);
  protected readonly q = signal('');
  protected readonly items = signal<AdminPlacement[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);
  protected readonly busyId = signal<string | null>(null);

  /** The placement being connected, while the team picker is open. */
  protected readonly picking = signal<AdminPlacement | null>(null);
  /** The placement awaiting a confirmed disconnect. */
  protected readonly confirmingDisconnect = signal<string | null>(null);

  protected readonly hasMore = computed(() => this.items().length < this.total());

  private readonly typing = new Subject<string>();

  constructor() {
    this.typing.pipe(debounceTime(250), takeUntilDestroyed()).subscribe((value) => {
      this.q.set(value);
      this.reload();
    });
    this.reload();
  }

  protected onType(value: string): void {
    this.typing.next(value);
  }

  protected show(connected: boolean): void {
    if (this.connected() !== connected) {
      this.connected.set(connected);
      this.reload();
    }
  }

  protected reload(): void {
    this.items.set([]);
    this.total.set(0);
    this.loadMore();
  }

  protected loadMore(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.listPlacements(this.connected(), this.q(), this.items().length, PAGE_SIZE).subscribe({
      next: (page) => {
        this.items.update((current) => [...current, ...page.items]);
        this.total.set(page.totalCount);
        this.loading.set(false);
      },
      error: (e) => {
        this.error.set(problemDetail(e, this.transloco.translate('admin.results.loadError')));
        this.loading.set(false);
      },
    });
  }

  protected connect(team: { slug: string; name: string }): void {
    const placement = this.picking();
    this.picking.set(null);
    if (!placement) {
      return;
    }

    this.busyId.set(placement.id);
    this.actionError.set(null);
    this.api.connectPlacement(placement.id, team.slug).subscribe({
      next: (updated) => this.replace(updated),
      error: (e: { status?: number }) => {
        this.busyId.set(null);
        this.actionError.set(
          this.transloco.translate(e?.status === 409 ? 'admin.results.alreadyPlaced' : 'admin.results.actionFailed'),
        );
      },
    });
  }

  protected disconnect(placement: AdminPlacement): void {
    if (this.confirmingDisconnect() !== placement.id) {
      this.confirmingDisconnect.set(placement.id);
      return;
    }

    this.confirmingDisconnect.set(null);
    this.busyId.set(placement.id);
    this.actionError.set(null);
    this.api.disconnectPlacement(placement.id).subscribe({
      next: (updated) => this.replace(updated),
      error: () => {
        this.busyId.set(null);
        this.actionError.set(this.transloco.translate('admin.results.actionFailed'));
      },
    });
  }

  /**
   * Show the row's new state in place. It stays in the list until the next reload, so an admin
   * sees what they just did — and can undo it — rather than having it vanish.
   */
  private replace(updated: AdminPlacement): void {
    this.items.update((rows) => rows.map((r) => (r.id === updated.id ? updated : r)));
    this.busyId.set(null);
  }
}
