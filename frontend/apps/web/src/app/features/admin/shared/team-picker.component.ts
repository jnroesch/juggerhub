import { Component, HostListener, OnInit, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { Subject, debounceTime } from 'rxjs';
import { AdminTeamListItem } from '../../../core/models/admin.models';
import { AdminService } from '../../../core/services/admin.service';
import { problemDetail } from '../../../core/utils/problem';
import { IconComponent, LoadingComponent } from '../../../shared/ui';

const PAGE_SIZE = 10;

/**
 * Pick one existing team (feature 050): a debounced search over the admin teams list, in the same
 * bottom-sheet / dialog as the award picker. Emits the chosen team; the host does the connecting.
 * The server `PlatformAdmin` policy is the boundary.
 */
@Component({
  selector: 'jh-admin-team-picker',
  imports: [TranslocoPipe, LoadingComponent, IconComponent],
  templateUrl: './team-picker.component.html',
  styleUrl: './team-picker.component.css',
})
export class AdminTeamPickerComponent implements OnInit {
  private readonly api = inject(AdminService);
  private readonly transloco = inject(TranslocoService);

  /** What is being connected, shown in the heading — e.g. the name as recorded. */
  readonly subjectLabel = input.required<string>();
  /** Pre-filled search, e.g. the recorded name. A suggestion to search, never a choice. */
  readonly initialQuery = input('');

  readonly picked = output<{ slug: string; name: string }>();
  readonly closed = output<void>();

  protected readonly q = signal('');
  protected readonly items = signal<AdminTeamListItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly typing = new Subject<string>();

  constructor() {
    this.typing.pipe(debounceTime(250), takeUntilDestroyed()).subscribe((value) => {
      this.q.set(value);
      this.search();
    });
  }

  ngOnInit(): void {
    this.q.set(this.initialQuery());
    this.search();
  }

  protected onType(value: string): void {
    this.typing.next(value);
  }

  private search(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.searchTeams(this.q(), 0, PAGE_SIZE).subscribe({
      next: (page) => {
        this.items.set([...page.items]);
        this.loading.set(false);
      },
      error: (e) => {
        this.error.set(problemDetail(e, this.transloco.translate('admin.teams.loadError')));
        this.loading.set(false);
      },
    });
  }

  protected pick(team: AdminTeamListItem): void {
    this.picked.emit({ slug: team.slug, name: team.name });
  }

  @HostListener('document:keydown.escape')
  protected close(): void {
    this.closed.emit();
  }
}
