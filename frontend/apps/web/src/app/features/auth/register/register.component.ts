import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { passwordsMatch } from '../../../core/utils/passwords-match.validator';
import { problemDetail } from '../../../core/utils/problem';
import { PasswordRulesComponent } from '../password-policy/password-rules.component';

/**
 * US1 — registration. Live password-policy feedback plus a confirm-password field
 * gate submit; on success shows a neutral "check your email" state (the response is
 * identical whether or not the address already exists).
 */
@Component({
  selector: 'jh-register',
  imports: [ReactiveFormsModule, RouterLink, PasswordRulesComponent],
  templateUrl: './register.component.html',
  styleUrl: './register.component.css',
})
export class RegisterComponent {
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);

  protected readonly form = this.fb.nonNullable.group(
    {
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

  protected readonly password = toSignal(this.form.controls.password.valueChanges, { initialValue: '' });

  protected readonly passwordValid = signal(false);
  protected readonly submitting = signal(false);
  protected readonly sent = signal(false);
  protected readonly error = signal<string | null>(null);

  protected get canSubmit(): boolean {
    return this.form.valid && this.passwordValid() && !this.submitting();
  }

  submit(): void {
    if (!this.canSubmit) {
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    // Only the password is sent — the confirmation is a client-side UX check.
    const { email, password } = this.form.getRawValue();
    this.auth.register({ email, password }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.sent.set(true);
      },
      error: (err) => {
        this.submitting.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }
}
