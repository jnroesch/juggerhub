import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { passwordsMatch } from '../../../core/utils/passwords-match.validator';
import { problemDetail } from '../../../core/utils/problem';
import { PasswordRulesComponent } from '../password-policy/password-rules.component';

/**
 * US3 — set a new password from the emailed reset link (userId + token in the
 * query). Live policy gates submit; an invalid/expired link is reported with a path
 * to request a fresh one.
 */
@Component({
  selector: 'jh-reset-password',
  imports: [ReactiveFormsModule, RouterLink, PasswordRulesComponent],
  templateUrl: './reset-password.component.html',
  styleUrl: './reset-password.component.css',
})
export class ResetPasswordComponent {
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);

  private readonly userId = this.route.snapshot.queryParamMap.get('userId') ?? '';
  private readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';
  protected readonly hasLink = Boolean(this.userId) && Boolean(this.token);

  protected readonly form = this.fb.nonNullable.group(
    {
      password: ['', [Validators.required]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

  protected readonly password = toSignal(this.form.controls.password.valueChanges, { initialValue: '' });
  protected readonly passwordValid = signal(false);
  protected readonly submitting = signal(false);
  protected readonly done = signal(false);
  protected readonly error = signal<string | null>(null);

  protected get canSubmit(): boolean {
    return this.hasLink && this.form.valid && this.passwordValid() && !this.submitting();
  }

  submit(): void {
    if (!this.canSubmit) {
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    this.auth
      .resetPassword({ userId: this.userId, token: this.token, newPassword: this.form.controls.password.value })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.done.set(true);
        },
        error: (err) => {
          this.submitting.set(false);
          this.error.set(problemDetail(err));
        },
      });
  }
}
