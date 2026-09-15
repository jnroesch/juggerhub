import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { Observable } from 'rxjs';
import { AlertComponent, ButtonDirective, CardComponent, LoadingComponent } from '../../../shared/ui';
import { ImportConnection, ResultEditor, TugenyImportPreview } from '../../../core/models/results.models';
import { ResultsService } from '../../../core/services/results.service';

/**
 * Link this event to its Tugeny tournament, and import the results once the organizer has
 * finalized it there (feature 050, US4).
 *
 * Linking and importing are the only actions that make the server contact Tugeny. The preview is
 * a draft — nothing is saved until the admin confirms — and every connection to a JuggerHub team
 * starts empty: which team a Tugeny name means is the admin's choice (FR-011), and only teams with
 * a confirmed sign-up for this event are offered.
 */
@Component({
  selector: 'jh-tugeny-link-card',
  imports: [TranslocoPipe, TranslocoDatePipe, AlertComponent, ButtonDirective, CardComponent, LoadingComponent],
  templateUrl: './tugeny-link-card.component.html',
  styleUrl: './tugeny-link-card.component.css',
})
export class TugenyLinkCardComponent {
  private readonly api = inject(ResultsService);
  private readonly transloco = inject(TranslocoService);

  readonly editor = input.required<ResultEditor>();
  /** Emitted after a link, unlink or import, so the page re-reads its state. */
  readonly changed = output<void>();

  protected readonly address = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notFinalized = signal(false);

  protected readonly preview = signal<TugenyImportPreview | null>(null);
  /** Tugeny team id → chosen JuggerHub team id. Absent means "keep the name". */
  protected readonly choices = signal<ReadonlyMap<number, string>>(new Map());

  protected readonly link = computed(() => this.editor().tugeny);
  protected readonly canImport = computed(() => !!this.link() && this.editor().canRecord);

  /** Teams already chosen for another Tugeny team — one team, one placement (FR-005). */
  protected readonly chosen = computed(() => new Set(this.choices().values()));

  protected connectLink(): void {
    const address = this.address().trim();
    if (!address) {
      return;
    }

    this.run(this.api.linkTugeny(this.editor().eventId, address), () => this.address.set(''));
  }

  protected removeLink(): void {
    this.run(this.api.unlinkTugeny(this.editor().eventId));
  }

  protected openImport(): void {
    this.busy.set(true);
    this.error.set(null);
    this.notFinalized.set(false);
    this.api.previewTugenyImport(this.editor().eventId).subscribe({
      next: (p) => {
        this.preview.set(p);
        this.choices.set(new Map());
        this.busy.set(false);
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.fail(err);
      },
    });
  }

  protected choose(tugenyTeamId: number, teamId: string): void {
    const next = new Map(this.choices());
    if (teamId) {
      next.set(tugenyTeamId, teamId);
    } else {
      next.delete(tugenyTeamId);
    }
    this.choices.set(next);
  }

  protected cancelImport(): void {
    this.preview.set(null);
    this.choices.set(new Map());
  }

  protected commitImport(): void {
    const connections: ImportConnection[] = [...this.choices()].map(([tugenyTeamId, teamId]) => ({ tugenyTeamId, teamId }));
    this.run(this.api.commitTugenyImport(this.editor().eventId, connections), () => this.cancelImport());
  }

  private run(call: Observable<unknown>, after?: () => void): void {
    this.busy.set(true);
    this.error.set(null);
    this.notFinalized.set(false);
    call.subscribe({
      next: () => {
        this.busy.set(false);
        after?.();
        this.changed.emit();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.fail(err);
      },
    });
  }

  /** Explain a refusal in the viewer's language, from its status (never the server's English). */
  private fail(err: unknown): void {
    const t = (key: string) => this.transloco.translate(key);
    if (!(err instanceof HttpErrorResponse)) {
      this.error.set(t('events.results.tugeny.failed'));
      return;
    }

    const type = typeof err.error?.type === 'string' ? err.error.type : '';
    switch (err.status) {
      case 400:
        this.error.set(t('events.results.tugeny.badAddress'));
        return;
      case 404:
        this.error.set(t('events.results.tugeny.unknownTournament'));
        return;
      case 409:
        this.error.set(t('events.results.editor.notOpen'));
        return;
      case 422:
        if (type.endsWith('not-finalized')) {
          this.notFinalized.set(true);
          this.preview.set(null);
          return;
        }
        this.error.set(t('events.results.editor.teamNotAllowedShort'));
        return;
      case 429:
        // Our own rate limiter: never retried, just a pause.
        this.error.set(t('events.results.tugeny.slowDown'));
        return;
      case 503:
        this.error.set(t('events.results.tugeny.unreachable'));
        return;
      default:
        this.error.set(t('events.results.tugeny.failed'));
    }
  }
}
