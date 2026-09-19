import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { InviteRef, inviteFromQuery, inviteReturnUrl } from '../../../core/utils/invite-ref';
import { TranslocoPipe } from '@jsverse/transloco';
import { LegalLinksComponent, ButtonDirective, CardComponent } from '../../../shared/ui';

type VerifyState = 'verifying' | 'success' | 'failed';

/**
 * US1 — consumes the email-verification link (userId + token in the query),
 * auto-confirming on load. On failure, offers to resend a fresh link.
 *
 * Feature 053: the link may also carry the shared invite the person registered from
 * (`inviteSlug` + `inviteToken`). Both are validated here again before anything is composed;
 * when they hold, the success state's "sign in" button carries
 * `returnUrl=/join/{slug}/{token}?action=accept` so sign-in hands the invite to the wizard, and
 * a resend from this page forwards the pair. A malformed pair is simply ignored — this page can
 * only ever compose the invite page's own path, never anywhere else.
 */
@Component({
  selector: 'jh-verify-email',
  imports: [LegalLinksComponent, ReactiveFormsModule, RouterLink, ButtonDirective, CardComponent, TranslocoPipe],
  templateUrl: './verify-email.component.html',
  styleUrl: './verify-email.component.css',
})
export class VerifyEmailComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  protected readonly state = signal<VerifyState>('verifying');
  protected readonly resent = signal(false);
  protected readonly resendForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  /** The invite carried on this link, if any (feature 053). */
  private invite: InviteRef | null = null;

  /** Query params for the success state's sign-in button: the invite page to resume, or nothing. */
  protected signInParams: Record<string, string> = {};

  ngOnInit(): void {
    this.invite = inviteFromQuery(this.route.snapshot.queryParamMap);
    this.signInParams = this.invite ? { returnUrl: inviteReturnUrl(this.invite) } : {};

    const userId = this.route.snapshot.queryParamMap.get('userId');
    const token = this.route.snapshot.queryParamMap.get('token');
    if (!userId || !token) {
      this.state.set('failed');
      return;
    }

    this.auth.verifyEmail({ userId, token }).subscribe({
      next: () => this.state.set('success'),
      error: () => this.state.set('failed'),
    });
  }

  resend(): void {
    if (this.resendForm.invalid) {
      return;
    }

    // Neutral either way. The re-sent link carries the invite this one did (feature 053).
    const invite = this.invite;
    this.auth
      .resendVerification({
        ...this.resendForm.getRawValue(),
        ...(invite ? { inviteSlug: invite.slug, inviteToken: invite.token } : {}),
      })
      .subscribe({
      next: () => this.resent.set(true),
      error: () => this.resent.set(true),
    });
  }
}
