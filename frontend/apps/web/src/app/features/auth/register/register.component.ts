import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { debounceTime, distinctUntilChanged, switchMap, of } from 'rxjs';
import { LegalContentService } from '../../legal/legal-content.service';
import { AuthService } from '../../../core/services/auth.service';
import { ProfileService } from '../../../core/services/profile.service';
import { passwordsMatch } from '../../../core/utils/passwords-match.validator';
import { problemDetail } from '../../../core/utils/problem';
import { safeReturnUrl } from '../../../core/utils/return-url';
import { PasswordRulesComponent } from '../password-policy/password-rules.component';
import { TranslocoPipe } from '@jsverse/transloco';
import { LegalLinksComponent, ButtonDirective, AlertComponent, CardComponent } from '../../../shared/ui';
import { LanguageSwitcherComponent } from '../../settings/language/language-switcher.component';

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
  imports: [LegalLinksComponent, ReactiveFormsModule, RouterLink, PasswordRulesComponent, ButtonDirective, AlertComponent, CardComponent, LanguageSwitcherComponent, TranslocoPipe],
  templateUrl: './register.component.html',
  styleUrl: './register.component.css',
  // Feature 041: the acceptance control needs the version of the document it is asking about.
  // Provided here rather than app-wide so the fetch is tied to this page's lifetime.
  providers: [LegalContentService],
})
export class RegisterComponent {
  private readonly auth = inject(AuthService);
  private readonly profiles = inject(ProfileService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly legal = inject(LegalContentService);
  private readonly transloco = inject(TranslocoService);
  private readonly destroyRef = inject(DestroyRef);

  /**
   * A pending returnUrl (e.g. an invite opened while signed out) arrives here via the
   * sign-in → register link. It's forwarded onto the "sign in" links so the intended
   * action survives the register → verify → sign-in hop instead of being dropped.
   * Only internal paths survive the open-redirect guard.
   */
  /**
   * Host for the permalink preview. Read from the browser rather than hardcoded so the
   * preview matches the origin the account is actually being created on (juggerhub.com,
   * dev.juggerhub.com, localhost) instead of naming a domain the profile isn't served from.
   */
  protected readonly appHost = location.host;

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
      // Feature 041 FR-015: starts false and is never set programmatically. A pre-ticked box is
      // not agreement, and this is the one control on the form where that is a legal point rather
      // than a usability one.
      acceptsTerms: [false, [Validators.requiredTrue]],
    },
    { validators: passwordsMatch },
  );

  /**
   * The version of the Terms of Use this page is actually showing, read from the same catalogue
   * the `/terms` page renders. Deliberately NOT hard-coded and NOT fetched from the API: the
   * server refuses any version that is not current, so sending the version we displayed is what
   * proves the reader saw the current text (specs/041 research R1).
   */
  protected readonly termsVersion = computed(() => this.legal.content()?.terms.version ?? null);

  /** True once the document is loaded and a version is known. Until then, agreeing is blocked. */
  protected readonly termsReady = computed(() => this.termsVersion() !== null);

  /**
   * The catalogue could not be fetched. Submission stays blocked rather than degrading to a
   * silent default: nobody may be pushed into agreeing to a document the app could not load.
   */
  protected readonly termsUnavailable = this.legal.failed;

  protected readonly password = toSignal(this.form.controls.password.valueChanges, { initialValue: '' });

  protected readonly passwordValid = signal(false);
  protected readonly submitting = signal(false);
  protected readonly sent = signal(false);
  protected readonly error = signal<string | null>(null);

  // Live handle availability (UX only — the server is the real uniqueness boundary).
  protected readonly handleState = signal<HandleState>('idle');
  protected readonly handleReason = signal<string | null>(null);

  constructor() {
    // Follows the active language, so the version recorded matches the text that was on screen.
    this.legal.load().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();

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

  /**
   * `form.valid` already covers the ticked box via `Validators.requiredTrue`; `termsReady` is the
   * separate condition that the document was actually loaded. Both are usability only — the
   * server refuses an unaccepted registration regardless of what this button does (FR-018).
   */
  protected get canSubmit(): boolean {
    return (
      this.form.valid &&
      this.passwordValid() &&
      this.handleState() === 'available' &&
      this.termsReady() &&
      !this.submitting()
    );
  }

  submit(): void {
    if (!this.canSubmit) {
      return;
    }

    const termsVersion = this.termsVersion();
    if (!termsVersion) {
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    // Confirmation is a client-side UX check; only the fields the API needs are sent.
    const { email, password, handle, acceptsTerms } = this.form.getRawValue();
    this.auth
      .register({
        email,
        password,
        handle: handle.trim().toLowerCase(),
        acceptsTerms,
        termsVersion,
        termsLanguage: this.transloco.getActiveLang(),
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.sent.set(true);
        },
        error: (err) => {
          this.submitting.set(false);
          // A 409 means the document changed under an open tab. Saying "something went wrong"
          // there is useless — the reader needs to know the text they agreed to is no longer the
          // current one, and that reloading is the fix.
          this.error.set(
            err?.status === 409 ? this.transloco.translate('auth.register.termsChanged') : problemDetail(err),
          );
        },
      });
  }
}
