import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { PasswordPolicy } from '../../../core/models/auth.models';
import { AuthService } from '../../../core/services/auth.service';

interface Rule {
  label: string;
  met: boolean;
}

/**
 * Live password-policy indicator. Fetches the published policy from the backend
 * and shows which rules the current password satisfies, emitting overall validity
 * so a parent form can gate submit. The server still enforces the policy.
 */
@Component({
  selector: 'jh-password-rules',
  imports: [],
  templateUrl: './password-rules.component.html',
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
      { label: `At least ${policy.minLength} characters`, met: value.length >= policy.minLength },
    ];
    if (policy.requireUppercase) {
      rules.push({ label: 'An uppercase letter', met: /[A-Z]/.test(value) });
    }
    if (policy.requireLowercase) {
      rules.push({ label: 'A lowercase letter', met: /[a-z]/.test(value) });
    }
    if (policy.requireDigit) {
      rules.push({ label: 'A number', met: /[0-9]/.test(value) });
    }
    if (policy.requireNonAlphanumeric) {
      rules.push({ label: 'A symbol', met: /[^a-zA-Z0-9]/.test(value) });
    }
    if (policy.requiredUniqueChars > 1) {
      rules.push({
        label: `At least ${policy.requiredUniqueChars} unique characters`,
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
