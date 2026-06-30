import { AbstractControl, ValidationErrors } from '@angular/forms';

/**
 * Cross-field validator: a form group's `password` and `confirmPassword` controls
 * must be equal. Sets a `passwordMismatch` error on the group when they differ.
 * This is a client-side UX aid only — the backend never receives the confirmation.
 */
export function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return password === confirm ? null : { passwordMismatch: true };
}
