import { Component, HostListener, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  AdminSubjectAwards,
  AdminSubjectType,
  RecognitionDefinition,
} from '../../../core/models/recognition.models';
import { RecognitionAdminService } from '../../../core/services/recognition-admin.service';
import { problemDetail } from '../../../core/utils/problem';

type Tab = 'badge' | 'achievement';

/**
 * The Assign picker — grant a badge or achievement to one subject (a player or a team). Shared by
 * the admin player detail (feature 013) and the admin team detail (feature 014); the only
 * difference is the subject key sent to the grant endpoint. Offers the active, applicable
 * catalogue, marks already-held types, and reuses feature-012's grant endpoints. The host renders
 * it while open and reloads the subject's awards on the `granted` event.
 */
@Component({
  selector: 'jh-assign-picker',
  imports: [FormsModule],
  templateUrl: './assign-picker.component.html',
  styleUrl: './assign-picker.component.css',
})
export class AssignPickerComponent {
  private readonly recognition = inject(RecognitionAdminService);

  readonly subjectType = input.required<AdminSubjectType>();
  readonly subjectRef = input.required<string>();
  readonly subjectLabel = input<string>('');
  /** The subject's current awards, so already-held types can be marked "Given". */
  readonly held = input<AdminSubjectAwards | null>(null);

  readonly granted = output<void>();
  readonly closed = output<void>();

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
    const held = this.tab() === 'badge' ? this.held()?.badges : this.held()?.achievements;
    return new Set((held ?? []).map((a) => a.definitionId));
  });

  /** The active tab's catalogue, filtered to the types that apply to this subject. */
  protected readonly pickerItems = computed(() => {
    const source = this.tab() === 'badge' ? this.badgeCatalogue() : this.achievementCatalogue();
    const forTeam = this.subjectType() === 'team';
    return source.filter((d) => (forTeam ? d.appliesToTeams : d.appliesToPlayers));
  });

  constructor() {
    // Active-only catalogues (retired types are excluded by the default include flag).
    this.recognition.listBadges().subscribe((b) => this.badgeCatalogue.set(b));
    this.recognition.listAchievements().subscribe((a) => this.achievementCatalogue.set(a));
  }

  protected isHeld(defId: string): boolean {
    return this.heldIds().has(defId);
  }

  protected iconUrl(kind: Tab, id: string): string {
    return `/api/v1/${kind === 'badge' ? 'badges' : 'achievements'}/${id}/icon`;
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

  protected close(): void {
    this.closed.emit();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.close();
  }

  protected grant(): void {
    const defId = this.selectedDefId();
    if (!defId || this.granting()) {
      return;
    }
    this.granting.set(true);
    this.grantError.set(null);

    const subjectKey =
      this.subjectType() === 'team' ? { teamSlug: this.subjectRef() } : { playerHandle: this.subjectRef() };
    const body = { ...subjectKey, note: this.note().trim() || null };
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
        this.granted.emit();
      },
      error: (e) => {
        this.granting.set(false);
        this.grantError.set(problemDetail(e, 'Could not grant that.'));
      },
    });
  }
}
