import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PasswordPolicy } from '../../../core/models/auth.models';
import { RegisterComponent } from './register.component';

const POLICY: PasswordPolicy = {
  minLength: 8,
  requireDigit: true,
  requireLowercase: true,
  requireUppercase: true,
  requireNonAlphanumeric: true,
  requiredUniqueChars: 3,
};

describe('RegisterComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function createComponent() {
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/auth/password-policy').flush(POLICY); // password-rules child fetch
    fixture.detectChanges();
    return fixture;
  }

  it('flags a password mismatch and clears it once they match', () => {
    const fixture = createComponent();
    const form = (fixture.componentInstance as unknown as { form: import('@angular/forms').FormGroup }).form;

    form.setValue({ email: 'a@example.com', password: 'Str0ng!Pass', confirmPassword: 'nope' });
    expect(form.hasError('passwordMismatch')).toBe(true);

    form.get('confirmPassword')!.setValue('Str0ng!Pass');
    expect(form.hasError('passwordMismatch')).toBe(false);
  });

  it('keeps the submit button disabled while the passwords do not match', () => {
    const fixture = createComponent();
    const form = (fixture.componentInstance as unknown as { form: import('@angular/forms').FormGroup }).form;

    form.setValue({ email: 'a@example.com', password: 'Str0ng!Pass', confirmPassword: 'mismatch' });
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('[data-testid="register-submit"]') as HTMLButtonElement;
    expect(button.disabled).toBe(true);
  });
});
