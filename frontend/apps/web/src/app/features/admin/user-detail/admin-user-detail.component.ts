import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { AdminUserDetail } from '../../../core/models/admin.models';
import { AdminAward, AdminSubjectAwards } from '../../../core/models/recognition.models';
import { AdminService } from '../../../core/services/admin.service';
import { RecognitionAdminService } from '../../../core/services/recognition-admin.service';
import { problemDetail } from '../../../core/utils/problem';
import { AssignPickerComponent } from '../shared/assign-picker.component';

type AwardKind = 'badge' | 'achievement';
type AccountAction = 'suspend' | 'reinstate' | 'ban' | 'unban' | 'reset';

/**
 * One player, everything an admin needs (feature 013 US4/US5, wireframe 1d/1e):
 * identity + activity, the recorded & reversible account actions (suspend/reinstate,
 * send reset link, ban/unban — each behind a confirm), and badges & achievements with
 * the shared Assign picker (fixed catalogues, already-held marked "Given", optional
 * note) — reusing feature 012's grant/revoke endpoints. The server enforces everything.
 */
@Component({
  selector: 'jh-admin-user-detail',
  imports: [DatePipe, RouterLink, AssignPickerComponent],
  templateUrl: './admin-user-detail.component.html',
  styleUrl: './admin-user-detail.component.css',
})
export class AdminUserDetailComponent {
  private readonly api = inject(AdminService);
  private readonly recognition = inject(RecognitionAdminService);
  private readonly route = inject(ActivatedRoute);

  private readonly handle = toSignal(this.route.paramMap.pipe(map((p) => p.get('handle') ?? '')), {
    initialValue: this.route.snapshot.paramMap.get('handle') ?? '',
  });

  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly detail = signal<AdminUserDetail | null>(null);

  protected readonly awards = signal<AdminSubjectAwards | null>(null);
  protected readonly awardCount = computed(
    () => (this.awards()?.badges.length ?? 0) + (this.awards()?.achievements.length ?? 0),
  );

  // Account actions.
  protected readonly acting = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly resetSent = signal(false);

  // Assign picker (shared component; opens over the player).
  protected readonly assignOpen = signal(false);

  constructor() {
    // Fires on entry AND when navigating detail→detail (e.g. from the overview lists),
    // where Angular reuses this component instance with a new :handle.
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe(() => this.load());
  }

  protected load(): void {
    const handle = this.handle();
    this.loading.set(true);
    this.notFound.set(false);
    this.error.set(null);
    this.actionError.set(null);
    this.resetSent.set(false);

    this.api.getUserDetail(handle).subscribe({
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
          this.error.set(problemDetail(e, 'Could not load that player.'));
        }
      },
    });
  }

  private reloadAwards(): void {
    this.recognition.subjectAwards('player', this.handle()).subscribe({
      next: (a) => this.awards.set(a),
      // A banned player's awards may not resolve; the page still works without them.
      error: () => this.awards.set(null),
    });
  }

  // --- Account actions (all confirmed, all recorded server-side) ------------

  protected act(action: AccountAction): void {
    const d = this.detail();
    if (!d || this.acting()) {
      return;
    }

    const prompts: Record<AccountAction, string> = {
      suspend: `Suspend @${d.handle}? They can't sign in until an admin reinstates them — everything they've built stays visible. Reversible any time.`,
      reinstate: `Reinstate @${d.handle}? They can sign in again right away.`,
      ban: `Ban @${d.handle}? They disappear from the app entirely, can't sign in, and their email can't register again. An admin can undo this later.`,
      unban: `Lift the ban on @${d.handle}? Their profile, teams, and badges return, and they can sign in again.`,
      reset: `Send @${d.handle} a password reset link? You never see or set their password.`,
    };
    if (!confirm(prompts[action])) {
      return;
    }

    const calls = {
      suspend: () => this.api.suspend(d.handle),
      reinstate: () => this.api.reinstate(d.handle),
      ban: () => this.api.ban(d.handle),
      unban: () => this.api.unban(d.handle),
      reset: () => this.api.sendPasswordReset(d.handle),
    };

    this.acting.set(true);
    this.actionError.set(null);
    this.resetSent.set(false);
    calls[action]().subscribe({
      next: () => {
        this.acting.set(false);
        if (action === 'reset') {
          this.resetSent.set(true);
        } else {
          this.load();
        }
      },
      error: (e) => {
        this.acting.set(false);
        this.actionError.set(problemDetail(e, 'That didn’t work. Try again.'));
      },
    });
  }

  // --- Assign picker ---------------------------------------------------------

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
    if (!confirm(`Revoke “${award.name}” from @${this.detail()?.handle}? It can be re-granted later.`)) {
      return;
    }
    const call =
      kind === 'badge'
        ? this.recognition.revokeBadge(award.awardId)
        : this.recognition.revokeAchievement(award.awardId);
    call.subscribe({ next: () => this.reloadAwards() });
  }
}
