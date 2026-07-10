import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import {
  AdminAward,
  AdminSubjectAwards,
  AdminSubjectType,
  RecognitionDefinition,
} from '../../../core/models/recognition.models';
import { RecognitionAdminService } from '../../../core/services/recognition-admin.service';
import { problemDetail } from '../../../core/utils/problem';

type Tab = 'badge' | 'achievement';

/**
 * Feature 012 US1 — the platform-admin grant surface (fixed catalogue; admins pick, not create —
 * per the Admin wireframe). Load a player (@handle) or team (slug), see their current badges &
 * achievements, revoke, and open the Assign picker to grant one from the catalogue with an optional
 * note. Already-held items are marked and can't be double-granted. The server is the boundary.
 */
@Component({
  selector: 'jh-admin-recognition',
  imports: [FormsModule, DatePipe],
  templateUrl: './admin-recognition.component.html',
  styleUrl: './admin-recognition.component.css',
})
export class AdminRecognitionComponent {
  private readonly api = inject(RecognitionAdminService);

  protected readonly subjectType = signal<AdminSubjectType>('player');
  protected readonly ref = signal('');
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly subject = signal<AdminSubjectAwards | null>(null);

  private readonly badgeCatalogue = signal<RecognitionDefinition[]>([]);
  private readonly achievementCatalogue = signal<RecognitionDefinition[]>([]);

  // Assign picker state.
  protected readonly assignOpen = signal(false);
  protected readonly tab = signal<Tab>('badge');
  protected readonly selectedDefId = signal<string | null>(null);
  protected readonly note = signal('');
  protected readonly contextYear = signal<number | null>(null);
  protected readonly contextLabel = signal('');
  protected readonly granting = signal(false);
  protected readonly grantError = signal<string | null>(null);

  private readonly heldBadgeIds = computed(() => new Set((this.subject()?.badges ?? []).map((a) => a.definitionId)));
  private readonly heldAchievementIds = computed(
    () => new Set((this.subject()?.achievements ?? []).map((a) => a.definitionId)),
  );

  /** Catalogue for the active tab, filtered to the subject type's applicability. */
  protected readonly pickerItems = computed(() => {
    const isPlayer = this.subjectType() === 'player';
    const source = this.tab() === 'badge' ? this.badgeCatalogue() : this.achievementCatalogue();
    return source.filter((d) => (isPlayer ? d.appliesToPlayers : d.appliesToTeams));
  });

  protected isHeld(defId: string): boolean {
    return (this.tab() === 'badge' ? this.heldBadgeIds() : this.heldAchievementIds()).has(defId);
  }

  protected iconUrl(kind: Tab, id: string): string {
    return `/api/v1/${kind === 'badge' ? 'badges' : 'achievements'}/${id}/icon`;
  }

  protected setType(type: AdminSubjectType): void {
    this.subjectType.set(type);
  }

  protected load(): void {
    const ref = this.ref().trim();
    if (!ref) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.subject.set(null);

    this.api.subjectAwards(this.subjectType(), ref).subscribe({
      next: (s) => {
        this.subject.set(s);
        this.loading.set(false);
        this.loadCatalogue();
      },
      error: (e) => {
        this.error.set(problemDetail(e, 'Could not load that subject.'));
        this.loading.set(false);
      },
    });
  }

  private loadCatalogue(): void {
    this.api.listBadges().subscribe((b) => this.badgeCatalogue.set(b));
    this.api.listAchievements().subscribe((a) => this.achievementCatalogue.set(a));
  }

  protected openAssign(): void {
    this.tab.set('badge');
    this.resetGrantForm();
    this.assignOpen.set(true);
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
    const subject = this.subject();
    if (!defId || !subject) {
      return;
    }
    this.granting.set(true);
    this.grantError.set(null);

    const isPlayer = this.subjectType() === 'player';
    const base = isPlayer ? { playerHandle: subject.subjectRef } : { teamSlug: subject.subjectRef };
    const note = this.note().trim() || null;

    const call =
      this.tab() === 'badge'
        ? this.api.grantBadge(defId, { ...base, note })
        : this.api.grantAchievement(defId, {
            ...base,
            note,
            contextYear: this.contextYear(),
            contextLabel: this.contextLabel().trim() || null,
          });

    call.subscribe({
      next: () => {
        this.granting.set(false);
        this.assignOpen.set(false);
        this.reload();
      },
      error: (e) => {
        this.grantError.set(problemDetail(e, 'Could not grant that.'));
        this.granting.set(false);
      },
    });
  }

  protected revoke(award: AdminAward, kind: Tab): void {
    if (!confirm(`Revoke “${award.name}” from ${this.subject()?.subjectRef ?? 'this subject'}? This can be re-granted later.`)) {
      return;
    }
    const call = kind === 'badge' ? this.api.revokeBadge(award.awardId) : this.api.revokeAchievement(award.awardId);
    call.subscribe({ next: () => this.reload() });
  }

  private reload(): void {
    const subject = this.subject();
    if (subject) {
      this.api.subjectAwards(this.subjectType(), subject.subjectRef).subscribe((s) => this.subject.set(s));
    }
  }

  private resetGrantForm(): void {
    this.selectedDefId.set(null);
    this.note.set('');
    this.contextYear.set(null);
    this.contextLabel.set('');
    this.grantError.set(null);
  }
}
