import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { AdminUserDetail } from '../../../core/models/admin.models';
import {
  AdminAward,
  AdminSubjectAwards,
  RecognitionDefinition,
} from '../../../core/models/recognition.models';
import { AdminService } from '../../../core/services/admin.service';
import { RecognitionAdminService } from '../../../core/services/recognition-admin.service';
import { problemDetail } from '../../../core/utils/problem';

type Tab = 'badge' | 'achievement';
type AccountAction = 'suspend' | 'reinstate' | 'ban' | 'unban' | 'reset';

/**
 * One player, everything an admin needs (feature 013 US4/US5, wireframe 1d/1e):
 * identity + activity, the recorded & reversible account actions (suspend/reinstate,
 * send reset link, ban/unban — each behind a confirm), and badges & achievements with
 * the Assign picker (fixed catalogues, already-held marked "Given", optional note) —
 * reusing feature 012's grant/revoke endpoints. The server enforces everything.
 */
@Component({
  selector: 'jh-admin-user-detail',
  imports: [DatePipe, RouterLink, FormsModule],
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

  // Assign picker (wireframe 1e; UI migrated from 012's catalogue surface).
  protected readonly assignOpen = signal(false);
  protected readonly tab = signal<Tab>('badge');
  protected readonly selectedDefId = signal<string | null>(null);
  protected readonly note = signal('');
  protected readonly contextYear = signal<number | null>(null);
  protected readonly contextLabel = signal('');
  protected readonly granting = signal(false);
  protected readonly grantError = signal<string | null>(null);

  private readonly badgeCatalogue = signal<RecognitionDefinition[]>([]);
  private readonly achievementCatalogue = signal<RecognitionDefinition[]>([]);

  private readonly heldIds = computed(() => {
    const held = this.tab() === 'badge' ? this.awards()?.badges : this.awards()?.achievements;
    return new Set((held ?? []).map((a) => a.definitionId));
  });

  /** The active tab's catalogue, players-only definitions. */
  protected readonly pickerItems = computed(() =>
    (this.tab() === 'badge' ? this.badgeCatalogue() : this.achievementCatalogue()).filter(
      (d) => d.appliesToPlayers,
    ),
  );

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

  protected isHeld(defId: string): boolean {
    return this.heldIds().has(defId);
  }

  protected iconUrl(kind: Tab, id: string): string {
    return `/api/v1/${kind === 'badge' ? 'badges' : 'achievements'}/${id}/icon`;
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
    this.tab.set('badge');
    this.selectedDefId.set(null);
    this.note.set('');
    this.contextYear.set(null);
    this.contextLabel.set('');
    this.grantError.set(null);
    this.assignOpen.set(true);
    if (this.badgeCatalogue().length === 0) {
      this.recognition.listBadges().subscribe((b) => this.badgeCatalogue.set(b));
      this.recognition.listAchievements().subscribe((a) => this.achievementCatalogue.set(a));
    }
  }

  protected closeAssign(): void {
    this.assignOpen.set(false);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.assignOpen()) {
      this.closeAssign();
    }
  }

  protected switchTab(tab: Tab): void {
    this.tab.set(tab);
    this.selectedDefId.set(null);
  }

  protected select(defId: string): void {
    if (!this.isHeld(defId)) {
      this.selectedDefId.set(defId);
    }
  }

  protected grant(): void {
    const defId = this.selectedDefId();
    const d = this.detail();
    if (!defId || !d || this.granting()) {
      return;
    }

    this.granting.set(true);
    this.grantError.set(null);
    const body = { playerHandle: d.handle, note: this.note().trim() || null };
    const call =
      this.tab() === 'badge'
        ? this.recognition.grantBadge(defId, body)
        : this.recognition.grantAchievement(defId, {
            ...body,
            contextYear: this.contextYear(),
            contextLabel: this.contextLabel().trim() || null,
          });

    call.subscribe({
      next: () => {
        this.granting.set(false);
        this.assignOpen.set(false);
        this.reloadAwards();
      },
      error: (e) => {
        this.granting.set(false);
        this.grantError.set(problemDetail(e, 'Could not grant that.'));
      },
    });
  }

  protected revoke(award: AdminAward, kind: Tab): void {
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
