import { Component, computed, effect, inject, input, output, signal, ChangeDetectionStrategy } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { PasswordPolicy } from '../../../core/models/auth.models';
import { AuthService } from '../../../core/services/auth.service';

interface Rule {
  /** Translation key + params, resolved in the template so labels follow the active language. */
  key: string;
  params?: Record<string, unknown>;
  met: boolean;
}

/**
 * Live password-policy indicator. Fetches the published policy from the backend
 * and shows which rules the current password satisfies, emitting overall validity
 * so a parent form can gate submit. The server still enforces the policy.
 */
@Component({
  selector: 'jh-password-rules',
  imports: [TranslocoPipe],
  templateUrl: './password-rules.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './password-rules.component.css',
})
export class PasswordRulesComponent {
  private readonly auth = inject(AuthService);

  readonly password = input('');
  readonly validChange = output<boolean>();

  protected readonly policy = signal<PasswordPolicy | null>(null);

  protected readonly rules = computed<Rule[]>(() => {
    const policy = this.policy();
    const value = this.password();
    if (!policy) {
      return [];
    }

    const rules: Rule[] = [
      { key: 'auth.passwordRules.minLength', params: { count: policy.minLength }, met: value.length >= policy.minLength },
    ];
    if (policy.requireUppercase) {
      rules.push({ key: 'auth.passwordRules.uppercase', met: /[A-Z]/.test(value) });
    }
    if (policy.requireLowercase) {
      rules.push({ key: 'auth.passwordRules.lowercase', met: /[a-z]/.test(value) });
    }
    if (policy.requireDigit) {
      rules.push({ key: 'auth.passwordRules.digit', met: /[0-9]/.test(value) });
    }
    if (policy.requireNonAlphanumeric) {
      rules.push({ key: 'auth.passwordRules.symbol', met: /[^a-zA-Z0-9]/.test(value) });
    }
    if (policy.requiredUniqueChars > 1) {
      rules.push({
        key: 'auth.passwordRules.uniqueChars',
        params: { count: policy.requiredUniqueChars },
        met: new Set(value).size >= policy.requiredUniqueChars,
      });
    }
    return rules;
  });

  protected readonly allValid = computed(() => {
    const rules = this.rules();
    return rules.length > 0 && rules.every((rule) => rule.met);
  });

  constructor() {
    this.auth.getPasswordPolicy().subscribe((policy) => this.policy.set(policy));
    effect(() => this.validChange.emit(this.allValid()));
  }
}
