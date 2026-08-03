import { Component, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AccountDeletionService } from '../../core/services/account-deletion.service';
import { AuthService } from '../../core/services/auth.service';
import { AccountDeletionPreview, DeletionBlocker } from '../../core/models/account-deletion.models';
import { AlertComponent, ButtonDirective } from '../../shared/ui';
import { problemDetail } from '../../core/utils/problem';

/**
 * The danger zone on /account: delete your own account (feature 037).
 *
 * Three states, in order — closed, disclosure, confirmation. The disclosure is deliberately a step
 * of its own rather than a line of small print, because the thing it has to say is the thing nobody
 * expects: **your messages and posts stay**, attributed to no one (spec FR-025). A member who reads
 * this and concludes their messages will disappear has been misled by us, in a flow the privacy
 * policy describes.
 *
 * There is no grace period, so re-authentication and the typed confirmation are the only protection
 * against a regretted click (FR-037) — which is why both are required and neither is skippable.
 */
@Component({
  selector: 'jh-delete-account',
  imports: [FormsModule, TranslocoPipe, AlertComponent, ButtonDirective],
  templateUrl: './delete-account.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './delete-account.component.css',
})
export class DeleteAccountComponent {
  private readonly api = inject(AccountDeletionService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);

  protected readonly open = signal(false);
  protected readonly loading = signal(false);
  protected readonly working = signal(false);
  protected readonly preview = signal<AccountDeletionPreview | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly password = signal('');
  protected readonly confirmation = signal('');

  /** Blockers from the preview, or from a 409 the confirmation ran into (FR-011). */
  protected readonly blockers = signal<DeletionBlocker[]>([]);

  protected readonly canDelete = computed(() => this.blockers().length === 0);

  /**
   * The literal the member must type, localized (T064). The server accepts the whole en/de/es set,
   * so whichever language they are reading in, the word they see is a word that works.
   */
  protected readonly confirmationWord = computed(() =>
    this.transloco.translate('account.delete.confirmationWord'),
  );

  protected readonly ready = computed(
    () =>
      this.canDelete() &&
      this.password().length > 0 &&
      this.confirmation().trim().toUpperCase() === this.confirmationWord().toUpperCase(),
  );

  /**
   * Opening always re-fetches. A blocker the member resolved since last time must show as resolved,
   * and one they picked up must show as new — never a cached refusal (FR-013).
   */
  protected start(): void {
    this.open.set(true);
    this.error.set(null);
    this.password.set('');
    this.confirmation.set('');
    this.loading.set(true);

    this.api.preview().subscribe({
      next: (preview) => {
        this.preview.set(preview);
        this.blockers.set(preview.blockers);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(problemDetail(err, this.transloco.translate('account.delete.previewFailed')));
        this.loading.set(false);
      },
    });
  }

  /** Abandoning changes nothing, and leaves no typed password behind. */
  protected cancel(): void {
    this.open.set(false);
    this.password.set('');
    this.confirmation.set('');
    this.error.set(null);
  }

  protected submit(): void {
    if (!this.ready() || this.working()) {
      return;
    }

    this.working.set(true);
    this.error.set(null);

    this.api.deleteAccount(this.password(), this.confirmation().trim()).subscribe({
      next: () => {
        // The account is gone; the session must not outlive it. Clear local state and leave for a
        // public route — there is nothing signed-in left to render.
        this.auth.clearSession();
        void this.router.navigate(['/'], { queryParams: { deleted: '1' } });
      },
      error: (err) => {
        this.working.set(false);

        // A 409 means nothing happened and the server found obligations we did not know about —
        // show all of them at once rather than one refusal at a time (FR-011).
        const conflict = err?.status === 409 ? (err.error?.blockers as DeletionBlocker[]) : null;
        if (conflict?.length) {
          this.blockers.set(conflict);
          this.error.set(this.transloco.translate('account.delete.blockedNow'));
          return;
        }

        this.error.set(problemDetail(err, this.transloco.translate('account.delete.failed')));
      },
    });
  }
}
