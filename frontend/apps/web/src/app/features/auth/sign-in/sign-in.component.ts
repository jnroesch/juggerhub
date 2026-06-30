import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

/**
 * US2 — sign in. Generic failure for bad credentials; a 403 (correct password but
 * unverified email) surfaces a "verify your email" path with resend. "Remember me"
 * selects a persistent vs session cookie server-side.
 */
@Component({
  selector: 'jh-sign-in',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './sign-in.component.html',
  styleUrl: './sign-in.component.css',
})
export class SignInComponent {
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

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
      next: () => {
        this.submitting.set(false);
        this.router.navigate(['/account']);
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        if (err.status === 403) {
          this.needsVerification.set(true);
        } else {
          this.error.set('Invalid email or password.');
        }
      },
    });
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
