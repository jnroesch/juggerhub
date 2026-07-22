import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { ButtonDirective, LoadingComponent, EmptyStateComponent } from '../../../shared/ui';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { RecognitionDefinition, RecognitionKind } from '../../../core/models/recognition.models';
import { RecognitionAdminService } from '../../../core/services/recognition-admin.service';
import { problemDetail } from '../../../core/utils/problem';

type StatusFilter = 'all' | 'active' | 'retired';

const NAME_MAX = 60;
const DESCRIPTION_MAX = 280;
const ICON_MAX_BYTES = 512 * 1024;
const ICON_TYPES = ['image/png', 'image/jpeg', 'image/webp'];

/**
 * The catalogue management surface (GitHub issue #24 → feature 014). One page for both fixed
 * catalogues (badges = recognition, achievements = milestones): browse with a grant count and
 * status, create/edit types (one form; kind is locked on edit), give a type an icon, and
 * retire/reinstate — retire never deletes and never touches already-granted awards. The server
 * `PlatformAdmin` policy enforces everything; this UI is convenience only.
 */
@Component({
  selector: 'jh-admin-catalogue',
  imports: [DatePipe, FormsModule, ButtonDirective, LoadingComponent, EmptyStateComponent],
  templateUrl: './admin-catalogue.component.html',
  styleUrl: './admin-catalogue.component.css',
})
export class AdminCatalogueComponent {
  private readonly recognition = inject(RecognitionAdminService);
  private readonly sanitizer = inject(DomSanitizer);

  protected readonly nameMax = NAME_MAX;
  protected readonly descriptionMax = DESCRIPTION_MAX;

  // Which catalogue + status filter is shown.
  protected readonly kind = signal<RecognitionKind>('badge');
  protected readonly filter = signal<StatusFilter>('all');

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  private readonly items = signal<RecognitionDefinition[]>([]);

  protected readonly visible = computed(() => {
    const f = this.filter();
    return this.items().filter((d) => (f === 'all' ? true : f === 'active' ? !d.isRetired : d.isRetired));
  });
  protected readonly total = computed(() => this.items().length);
  protected readonly retiredCount = computed(() => this.items().filter((d) => d.isRetired).length);

  // Create / edit form (one form for both).
  protected readonly formOpen = signal(false);
  protected readonly editing = signal<RecognitionDefinition | null>(null);
  protected readonly formKind = signal<RecognitionKind>('badge');
  protected readonly formName = signal('');
  protected readonly formDescription = signal('');
  protected readonly formPlayers = signal(false);
  protected readonly formTeams = signal(false);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly formValid = computed(
    () =>
      this.formName().trim().length > 0 &&
      this.formName().trim().length <= NAME_MAX &&
      this.formDescription().trim().length > 0 &&
      this.formDescription().trim().length <= DESCRIPTION_MAX &&
      (this.formPlayers() || this.formTeams()),
  );

  // Icon editor.
  protected readonly iconOpen = signal(false);
  protected readonly iconTarget = signal<RecognitionDefinition | null>(null);
  protected readonly iconFile = signal<File | null>(null);
  protected readonly iconPreview = signal<SafeUrl | null>(null);
  private previewObjectUrl: string | null = null;
  protected readonly iconBusy = signal(false);
  protected readonly iconError = signal<string | null>(null);

  // Retire confirm (amber, reversible).
  protected readonly retireTarget = signal<RecognitionDefinition | null>(null);
  protected readonly actionBusy = signal(false);

  constructor() {
    this.load();
  }

  // --- List ------------------------------------------------------------------

  protected setKind(kind: RecognitionKind): void {
    if (kind !== this.kind()) {
      this.kind.set(kind);
      this.load();
    }
  }

  protected setFilter(f: StatusFilter): void {
    this.filter.set(f);
  }

  /** Icon URL for a definition in the currently-shown catalogue. */
  protected iconUrl(id: string): string {
    return `/api/v1/${this.catalogue()}/${id}/icon`;
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    // Fetch everything (small set) and filter status client-side.
    this.recognition.listDefinitions(this.kind(), true).subscribe({
      next: (defs) => {
        this.items.set(defs);
        this.loading.set(false);
      },
      error: (e) => {
        this.error.set(problemDetail(e, 'Could not load the catalogue.'));
        this.loading.set(false);
      },
    });
  }

  private catalogue(): string {
    return this.kind() === 'badge' ? 'badges' : 'achievements';
  }

  // --- Create / edit ---------------------------------------------------------

  protected openCreate(): void {
    this.editing.set(null);
    this.formKind.set(this.kind());
    this.formName.set('');
    this.formDescription.set('');
    this.formPlayers.set(true);
    this.formTeams.set(false);
    this.formError.set(null);
    this.formOpen.set(true);
  }

  protected openEdit(def: RecognitionDefinition): void {
    this.editing.set(def);
    this.formKind.set(this.kind()); // kind is fixed on edit — the current catalogue
    this.formName.set(def.name);
    this.formDescription.set(def.description);
    this.formPlayers.set(def.appliesToPlayers);
    this.formTeams.set(def.appliesToTeams);
    this.formError.set(null);
    this.formOpen.set(true);
  }

  protected closeForm(): void {
    this.formOpen.set(false);
  }

  protected save(): void {
    if (!this.formValid() || this.saving()) {
      return;
    }
    this.saving.set(true);
    this.formError.set(null);

    const body = {
      name: this.formName().trim(),
      description: this.formDescription().trim(),
      appliesToPlayers: this.formPlayers(),
      appliesToTeams: this.formTeams(),
    };
    const editing = this.editing();
    const kind = this.formKind();
    const call = editing
      ? this.recognition.updateDefinition(kind, editing.id, body)
      : this.recognition.createDefinition(kind, body);

    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.formOpen.set(false);
        // Show the catalogue the type lives in (create can target the other catalogue).
        if (kind !== this.kind()) {
          this.kind.set(kind);
        }
        this.load();
      },
      error: (e) => {
        this.saving.set(false);
        this.formError.set(problemDetail(e, 'Could not save that type.'));
      },
    });
  }

  // --- Icon ------------------------------------------------------------------

  protected openIcon(def: RecognitionDefinition): void {
    this.iconTarget.set(def);
    this.clearIconSelection();
    this.iconError.set(null);
    this.iconOpen.set(true);
  }

  protected closeIcon(): void {
    this.iconOpen.set(false);
    this.clearIconSelection();
  }

  protected onIconFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      this.selectIconFile(file);
    }
  }

  protected onIconDrop(event: DragEvent): void {
    event.preventDefault();
    const file = event.dataTransfer?.files?.[0];
    if (file) {
      this.selectIconFile(file);
    }
  }

  private selectIconFile(file: File): void {
    // Client-side hint only — the server sniffs the bytes and enforces the real limit.
    if (!ICON_TYPES.includes(file.type)) {
      this.iconError.set('Use a PNG, JPEG, or WebP image.');
      return;
    }
    if (file.size > ICON_MAX_BYTES) {
      this.iconError.set('That image is too large (max 512 KB).');
      return;
    }
    this.iconError.set(null);
    this.revokePreview();
    this.previewObjectUrl = URL.createObjectURL(file);
    this.iconPreview.set(this.sanitizer.bypassSecurityTrustUrl(this.previewObjectUrl));
    this.iconFile.set(file);
  }

  protected saveIcon(): void {
    const target = this.iconTarget();
    const file = this.iconFile();
    if (!target || !file || this.iconBusy()) {
      return;
    }
    this.iconBusy.set(true);
    this.iconError.set(null);
    this.recognition.setIcon(this.kind(), target.id, file).subscribe({
      next: () => this.afterIconChange(),
      error: (e) => {
        this.iconBusy.set(false);
        this.iconError.set(problemDetail(e, 'Could not save that image.'));
      },
    });
  }

  protected removeIcon(): void {
    const target = this.iconTarget();
    if (!target || this.iconBusy()) {
      return;
    }
    this.iconBusy.set(true);
    this.iconError.set(null);
    this.recognition.removeIcon(this.kind(), target.id).subscribe({
      next: () => this.afterIconChange(),
      error: (e) => {
        this.iconBusy.set(false);
        this.iconError.set(problemDetail(e, 'Could not remove that image.'));
      },
    });
  }

  private afterIconChange(): void {
    this.iconBusy.set(false);
    this.iconOpen.set(false);
    this.clearIconSelection();
    this.load();
  }

  private clearIconSelection(): void {
    this.revokePreview();
    this.iconFile.set(null);
    this.iconPreview.set(null);
  }

  private revokePreview(): void {
    if (this.previewObjectUrl) {
      URL.revokeObjectURL(this.previewObjectUrl);
      this.previewObjectUrl = null;
    }
  }

  // --- Retire / reinstate ----------------------------------------------------

  protected askRetire(def: RecognitionDefinition): void {
    this.retireTarget.set(def);
  }

  protected cancelRetire(): void {
    this.retireTarget.set(null);
  }

  protected confirmRetire(): void {
    const target = this.retireTarget();
    if (!target || this.actionBusy()) {
      return;
    }
    this.actionBusy.set(true);
    this.recognition.retireDefinition(this.kind(), target.id).subscribe({
      next: () => {
        this.actionBusy.set(false);
        this.retireTarget.set(null);
        this.load();
      },
      error: (e) => {
        this.actionBusy.set(false);
        this.retireTarget.set(null);
        this.error.set(problemDetail(e, 'Could not retire that type.'));
      },
    });
  }

  protected reinstate(def: RecognitionDefinition): void {
    if (this.actionBusy()) {
      return;
    }
    this.actionBusy.set(true);
    this.recognition.reinstateDefinition(this.kind(), def.id).subscribe({
      next: () => {
        this.actionBusy.set(false);
        this.load();
      },
      error: (e) => {
        this.actionBusy.set(false);
        this.error.set(problemDetail(e, 'Could not reinstate that type.'));
      },
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.retireTarget()) {
      this.cancelRetire();
    } else if (this.iconOpen()) {
      this.closeIcon();
    } else if (this.formOpen()) {
      this.closeForm();
    }
  }
}
