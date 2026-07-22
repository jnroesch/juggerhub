import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ButtonDirective, CardComponent } from '../../../shared/ui';

type VerifyState = 'verifying' | 'success' | 'failed';

/**
 * US1 — consumes the email-verification link (userId + token in the query),
 * auto-confirming on load. On failure, offers to resend a fresh link.
 */
@Component({
  selector: 'jh-verify-email',
  imports: [ReactiveFormsModule, RouterLink, ButtonDirective, CardComponent],
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

  ngOnInit(): void {
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

    // Neutral either way.
    this.auth.resendVerification(this.resendForm.getRawValue()).subscribe({
      next: () => this.resent.set(true),
      error: () => this.resent.set(true),
    });
  }
}
