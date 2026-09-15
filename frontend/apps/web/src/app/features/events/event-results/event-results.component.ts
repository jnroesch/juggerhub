import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { AlertComponent, ButtonDirective, CardComponent, EmptyStateComponent, LoadingComponent } from '../../../shared/ui';
import { EditorPlacement, RankingRow, ResultEditor, SignedUpTeam } from '../../../core/models/results.models';
import { ResultsService } from '../../../core/services/results.service';
import { MAX_EXPORT_ROWS, MAX_TEAM_NAME, parseTugenyRankingExport } from './tugeny-export.parser';
import { TugenyLinkCardComponent } from './tugeny-link-card.component';
import { TugenyTeamListCardComponent } from './tugeny-team-list-card.component';

/** One row of the ranking editor. */
export interface EditorRow {
  /** Stable key for the list, independent of the server id. */
  key: number;
  /** The existing placement this row keeps, or null for a new row. */
  id: string | null;
  position: number;
  /** A signed-up team, or null for a typed name. */
  teamId: string | null;
  /** The typed name (used when `teamId` is null). */
  name: string;
  /**
   * Connected to a team outside this event's sign-ups — a platform admin's check. The event admin
   * keeps it by saving the row back unchanged, but cannot re-point it (research R8).
   */
  locked: { teamName: string; connectedBy: string | null } | null;
}

/**
 * The results page of a tournament (feature 050), for the event's admins: record the ranking by
 * hand or from Tugeny's export, hand the team list to Tugeny, and link / import a Tugeny tournament.
 *
 * The page is UX only — every rule (who may, when, which teams) is enforced by the server, and a
 * refusal is explained here from its status code in the viewer's language.
 */
@Component({
  selector: 'jh-event-results-page',
  imports: [
    RouterLink,
    TranslocoPipe,
    TranslocoDatePipe,
    AlertComponent,
    ButtonDirective,
    CardComponent,
    EmptyStateComponent,
    LoadingComponent,
    TugenyLinkCardComponent,
    TugenyTeamListCardComponent,
  ],
  templateUrl: './event-results.component.html',
  styleUrl: './event-results.component.css',
})
export class EventResultsPageComponent implements OnInit {
  private readonly api = inject(ResultsService);
  private readonly route = inject(ActivatedRoute);
  private readonly transloco = inject(TranslocoService);

  protected readonly maxName = MAX_TEAM_NAME;

  protected readonly editor = signal<ResultEditor | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly forbidden = signal(false);

  protected readonly rows = signal<EditorRow[]>([]);
  /** Set when the rows came from a paste — saving them replaces a saved ranking (FR-012). */
  protected readonly draftFromPaste = signal(false);
  protected readonly confirmReplace = signal(false);
  protected readonly confirmClear = signal(false);
  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly saveError = signal<string | null>(null);

  protected readonly pasteOpen = signal(false);
  protected readonly pasteText = signal('');
  protected readonly pasteError = signal(false);

  private nextKey = 0;
  private id = '';

  protected readonly signedUpTeams = computed<SignedUpTeam[]>(() => this.editor()?.signedUpTeams ?? []);
  protected readonly savedCount = computed(() => this.editor()?.placements.length ?? 0);
  protected readonly canAddRow = computed(() => this.rows().length < MAX_EXPORT_ROWS);

  /** Teams already chosen in another row — a team holds one placement per ranking (FR-005). */
  protected readonly takenTeams = computed(() => new Set(this.rows().map((r) => r.teamId).filter((t): t is string => !!t)));

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.api.getEditor(this.id).subscribe({
      next: (e) => {
        this.editor.set(e);
        this.resetRows(e);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        if (err instanceof HttpErrorResponse && (err.status === 403 || err.status === 404)) {
          this.forbidden.set(true);
        } else {
          this.loadError.set(true);
        }
        this.loading.set(false);
      },
    });
  }

  // --- Rows ----------------------------------------------------------------------------------

  private resetRows(e: ResultEditor): void {
    const signedUp = new Set(e.signedUpTeams.map((t) => t.teamId));
    this.rows.set(e.placements.map((p) => this.fromPlacement(p, signedUp)));
    this.draftFromPaste.set(false);
    this.confirmReplace.set(false);
    if (this.rows().length === 0 && e.canRecord) {
      this.addRow();
    }
  }

  private fromPlacement(p: EditorPlacement, signedUp: Set<string>): EditorRow {
    const locked = p.teamId && !signedUp.has(p.teamId) ? { teamName: p.name, connectedBy: p.connectedBy } : null;
    return { key: this.nextKey++, id: p.id, position: p.position, teamId: p.teamId, name: p.teamId ? p.name : p.sourceName, locked };
  }

  protected addRow(): void {
    if (!this.canAddRow()) {
      return;
    }
    const rows = this.rows();
    const position = rows.length === 0 ? 1 : Math.max(...rows.map((r) => r.position)) + 1;
    this.rows.set([...rows, { key: this.nextKey++, id: null, position, teamId: null, name: '', locked: null }]);
    this.saved.set(false);
  }

  protected removeRow(key: number): void {
    this.rows.update((rows) => rows.filter((r) => r.key !== key));
    this.saved.set(false);
  }

  protected setPosition(key: number, value: string): void {
    const n = Number(value);
    this.patch(key, { position: Number.isInteger(n) ? n : 0 });
  }

  protected setTeam(key: number, teamId: string): void {
    this.patch(key, { teamId: teamId || null });
  }

  protected setName(key: number, name: string): void {
    this.patch(key, { name });
  }

  private patch(key: number, change: Partial<EditorRow>): void {
    this.rows.update((rows) => rows.map((r) => (r.key === key ? { ...r, ...change } : r)));
    this.saved.set(false);
  }

  protected teamName(teamId: string): string {
    return this.signedUpTeams().find((t) => t.teamId === teamId)?.teamName ?? '';
  }

  // --- Save / clear --------------------------------------------------------------------------

  protected save(): void {
    if (this.draftFromPaste() && this.savedCount() > 0 && !this.confirmReplace()) {
      this.confirmReplace.set(true);
      return;
    }

    const placements: RankingRow[] = this.rows().map((r) => ({
      id: r.id,
      position: r.position,
      name: r.locked ? r.locked.teamName : r.teamId ? this.teamName(r.teamId) : r.name.trim(),
      teamId: r.teamId,
    }));

    this.saving.set(true);
    this.saveError.set(null);
    this.api.saveRanking(this.id, placements).subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.set(true);
        this.reloadAfterWrite();
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.confirmReplace.set(false);
        this.saveError.set(this.explain(err));
      },
    });
  }

  protected clear(): void {
    if (!this.confirmClear()) {
      this.confirmClear.set(true);
      return;
    }

    this.saving.set(true);
    this.saveError.set(null);
    this.api.clearRanking(this.id).subscribe({
      next: () => {
        this.saving.set(false);
        this.confirmClear.set(false);
        this.reloadAfterWrite();
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.confirmClear.set(false);
        this.saveError.set(this.explain(err));
      },
    });
  }

  /** Re-read the page after a write, keeping the "saved" note visible. */
  protected reloadAfterWrite(): void {
    const keepSaved = this.saved();
    this.api.getEditor(this.id).subscribe({
      next: (e) => {
        this.editor.set(e);
        this.resetRows(e);
        this.saved.set(keepSaved);
      },
    });
  }

  // --- Paste from Tugeny (US2) ---------------------------------------------------------------

  protected openPaste(): void {
    this.pasteOpen.set(true);
    this.pasteError.set(false);
  }

  protected closePaste(): void {
    this.pasteOpen.set(false);
    this.pasteText.set('');
    this.pasteError.set(false);
  }

  protected readPaste(): void {
    const parsed = parseTugenyRankingExport(this.pasteText());
    if (!parsed.ok) {
      // The draft and the saved ranking stay exactly as they were (FR-010).
      this.pasteError.set(true);
      return;
    }

    // Every pasted row starts unconnected: which team a name means is the admin's choice (FR-011).
    this.rows.set(parsed.rows.map((r) => ({ key: this.nextKey++, id: null, position: r.position, teamId: null, name: r.name, locked: null })));
    this.draftFromPaste.set(true);
    this.confirmReplace.set(false);
    this.saved.set(false);
    this.closePaste();
  }

  // --- Errors --------------------------------------------------------------------------------

  /** A refusal in the viewer's language, from its status — never the server's English detail. */
  private explain(err: unknown): string {
    const t = (key: string, params?: Record<string, unknown>) => this.transloco.translate(key, params);
    if (!(err instanceof HttpErrorResponse)) {
      return t('events.results.editor.saveFailed');
    }

    const row = typeof err.error?.row === 'number' ? err.error.row + 1 : null;
    switch (err.status) {
      case 400:
        return row ? t('events.results.editor.invalidRow', { row }) : t('events.results.editor.invalid');
      case 403:
        return t('events.results.notAdmin');
      case 409:
        return t('events.results.editor.notOpen');
      case 422:
        return t('events.results.editor.teamNotAllowed', { row: row ?? '' });
      default:
        return t('events.results.editor.saveFailed');
    }
  }
}
