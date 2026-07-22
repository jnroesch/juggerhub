import { Component, computed, inject, signal } from '@angular/core';
import { ButtonDirective, LoadingComponent } from '../../../shared/ui';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { AdminTeamDetail } from '../../../core/models/admin.models';
import { AdminAward, AdminSubjectAwards } from '../../../core/models/recognition.models';
import { AdminService } from '../../../core/services/admin.service';
import { RecognitionAdminService } from '../../../core/services/recognition-admin.service';
import { problemDetail } from '../../../core/utils/problem';
import { AssignPickerComponent } from '../shared/assign-picker.component';

type AwardKind = 'badge' | 'achievement';

/**
 * One team, for award assignment (feature 014 US6): identity + its current badges & achievements
 * with revoke, and the shared Assign picker (team-applicable, non-retired types). Reuses feature
 * 012's team-awards read and `teamSlug` grant/revoke; the server enforces everything.
 */
@Component({
  selector: 'jh-admin-team-detail',
  imports: [DatePipe, RouterLink, AssignPickerComponent, ButtonDirective, LoadingComponent],
  templateUrl: './admin-team-detail.component.html',
  styleUrl: './admin-team-detail.component.css',
})
export class AdminTeamDetailComponent {
  private readonly api = inject(AdminService);
  private readonly recognition = inject(RecognitionAdminService);
  private readonly route = inject(ActivatedRoute);

  private readonly slug = toSignal(this.route.paramMap.pipe(map((p) => p.get('slug') ?? '')), {
    initialValue: this.route.snapshot.paramMap.get('slug') ?? '',
  });

  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly detail = signal<AdminTeamDetail | null>(null);

  protected readonly awards = signal<AdminSubjectAwards | null>(null);
  protected readonly awardCount = computed(
    () => (this.awards()?.badges.length ?? 0) + (this.awards()?.achievements.length ?? 0),
  );

  protected readonly assignOpen = signal(false);

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe(() => this.load());
  }

  protected load(): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.error.set(null);
    this.api.getTeamDetail(this.slug()).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.loading.set(false);
        this.reloadAwards();
      },
      error: (e) => {
        this.loading.set(false);
        if (e?.status === 404) {
          this.notFound.set(true);
        } else {
          this.error.set(problemDetail(e, 'Could not load that team.'));
        }
      },
    });
  }

  private reloadAwards(): void {
    this.recognition.subjectAwards('team', this.slug()).subscribe({
      next: (a) => this.awards.set(a),
      error: () => this.awards.set(null),
    });
  }

  protected openAssign(): void {
    this.assignOpen.set(true);
  }

  protected closeAssign(): void {
    this.assignOpen.set(false);
  }

  protected onGranted(): void {
    this.assignOpen.set(false);
    this.reloadAwards();
  }

  protected revoke(award: AdminAward, kind: AwardKind): void {
    if (!confirm(`Revoke “${award.name}” from ${this.detail()?.name}? It can be re-granted later.`)) {
      return;
    }
    const call =
      kind === 'badge'
        ? this.recognition.revokeBadge(award.awardId)
        : this.recognition.revokeAchievement(award.awardId);
    call.subscribe({ next: () => this.reloadAwards() });
  }
}
