import { FormControl, FormGroup } from '@angular/forms';
import { passwordsMatch } from './passwords-match.validator';

describe('passwordsMatch', () => {
  function group(password: string, confirmPassword: string) {
    return new FormGroup({
      password: new FormControl(password),
      confirmPassword: new FormControl(confirmPassword),
    });
  }

  it('returns null when the passwords match', () => {
    expect(passwordsMatch(group('Secret1!', 'Secret1!'))).toBeNull();
  });

  it('returns a passwordMismatch error when they differ', () => {
    expect(passwordsMatch(group('Secret1!', 'Different1!'))).toEqual({ passwordMismatch: true });
  });

  it('treats two empty values as matching (the required validators handle emptiness)', () => {
    expect(passwordsMatch(group('', ''))).toBeNull();
  });
});
