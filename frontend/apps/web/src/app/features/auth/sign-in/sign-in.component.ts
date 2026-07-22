import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { safeReturnUrl } from '../../../core/utils/return-url';
import { ButtonDirective, AlertComponent, CardComponent } from '../../../shared/ui';

/**
 * US2 — sign in. Generic failure for bad credentials; a 403 (correct password but
 * unverified email) surfaces a "verify your email" path with resend. "Remember me"
 * selects a persistent vs session cookie server-side.
 */
@Component({
  selector: 'jh-sign-in',
  imports: [ReactiveFormsModule, RouterLink, ButtonDirective, AlertComponent, CardComponent],
  templateUrl: './sign-in.component.html',
  styleUrl: './sign-in.component.css',
})
export class SignInComponent {
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
    rememberMe: [false],
  });

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly needsVerification = signal(false);
  protected readonly resent = signal(false);

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    this.needsVerification.set(false);
    this.resent.set(false);

    this.auth.login(this.form.getRawValue()).subscribe({
      next: (user) => {
        this.submitting.set(false);
        // First-login gate: new (not-yet-onboarded) users start the guided flow;
        // everyone else goes to the app — or back to a pending returnUrl (e.g. an
        // invite opened while signed out). The server is the authority for the flag.
        // A pending returnUrl is carried *through* onboarding so the intended action
        // (e.g. accepting an invite) still resumes after the wizard, rather than being
        // dropped at the first-login gate.
        const returnUrl = this.returnUrl();
        if (!user.onboardingCompleted) {
          this.router.navigate(['/onboarding'], returnUrl ? { queryParams: { returnUrl } } : {});
          return;
        }
        this.router.navigateByUrl(returnUrl ?? '/');
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        // Two coded 403s exist (both only after a correct password): unverified email
        // and a suspended account (feature 013). Banned accounts get the generic 401.
        if (err.status === 403 && err.error?.status === 'account_suspended') {
          this.error.set(err.error?.message ?? 'This account is suspended.');
        } else if (err.status === 403) {
          this.needsVerification.set(true);
        } else {
          this.error.set('Invalid email or password.');
        }
      },
    });
  }

  /** The pending returnUrl, if any — only internal paths survive (open-redirect guard). */
  protected returnUrl(): string | null {
    return safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
  }

  resendVerification(): void {
    const email = this.form.controls.email.value;
    if (!email) {
      return;
    }

    // Neutral either way.
    this.auth.resendVerification({ email }).subscribe({
      next: () => this.resent.set(true),
      error: () => this.resent.set(true),
    });
  }
}
