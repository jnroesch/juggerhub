import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, switchMap, of } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { ProfileService } from '../../../core/services/profile.service';
import { passwordsMatch } from '../../../core/utils/passwords-match.validator';
import { problemDetail } from '../../../core/utils/problem';
import { safeReturnUrl } from '../../../core/utils/return-url';
import { PasswordRulesComponent } from '../password-policy/password-rules.component';

/** URL-safe handle: lowercase alphanumeric segments joined by single hyphens. */
const HANDLE_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

type HandleState = 'idle' | 'checking' | 'available' | 'unavailable';

/**
 * US1 — registration. Live password-policy feedback, a confirm-password field, and
 * a live handle-availability check gate submit; on success shows a neutral "check
 * your email" state. The chosen handle is immutable once the account exists.
 */
@Component({
  selector: 'jh-register',
  imports: [ReactiveFormsModule, RouterLink, PasswordRulesComponent],
  templateUrl: './register.component.html',
  styleUrl: './register.component.css',
})
export class RegisterComponent {
  private readonly auth = inject(AuthService);
  private readonly profiles = inject(ProfileService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);

  /**
   * A pending returnUrl (e.g. an invite opened while signed out) arrives here via the
   * sign-in → register link. It's forwarded onto the "sign in" links so the intended
   * action survives the register → verify → sign-in hop instead of being dropped.
   * Only internal paths survive the open-redirect guard.
   */
  protected readonly signInParams = ((): Record<string, string> => {
    const returnUrl = safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
    return returnUrl ? { returnUrl } : {};
  })();

  protected readonly form = this.fb.nonNullable.group(
    {
      email: ['', [Validators.required, Validators.email]],
      handle: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(30), Validators.pattern(HANDLE_PATTERN)]],
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

  // Live handle availability (UX only — the server is the real uniqueness boundary).
  protected readonly handleState = signal<HandleState>('idle');
  protected readonly handleReason = signal<string | null>(null);

  constructor() {
    const handle = this.form.controls.handle;
    handle.valueChanges
      .pipe(
        debounceTime(350),
        distinctUntilChanged(),
        switchMap((value) => {
          const v = (value ?? '').trim().toLowerCase();
          if (!v || handle.invalid) {
            this.handleState.set(v ? 'unavailable' : 'idle');
            this.handleReason.set(v ? 'Use lowercase letters, numbers, and single hyphens.' : null);
            return of(null);
          }
          this.handleState.set('checking');
          return this.profiles.checkHandle(v);
        }),
      )
      .subscribe((result) => {
        if (!result) {
          return;
        }
        this.handleState.set(result.available ? 'available' : 'unavailable');
        this.handleReason.set(result.reason);
      });
  }

  protected get canSubmit(): boolean {
    return this.form.valid && this.passwordValid() && this.handleState() === 'available' && !this.submitting();
  }

  submit(): void {
    if (!this.canSubmit) {
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    // Confirmation is a client-side UX check; only the fields the API needs are sent.
    const { email, password, handle } = this.form.getRawValue();
    this.auth.register({ email, password, handle: handle.trim().toLowerCase() }).subscribe({
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
