import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PasswordPolicy } from '../../../core/models/auth.models';
import { PasswordRulesComponent } from './password-rules.component';

const POLICY: PasswordPolicy = {
  minLength: 8,
  requireDigit: true,
  requireLowercase: true,
  requireUppercase: true,
  requireNonAlphanumeric: true,
  requiredUniqueChars: 3,
};

describe('PasswordRulesComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('emits invalid for a weak password and valid once every rule is met', () => {
    const emissions: boolean[] = [];
    const fixture = TestBed.createComponent(PasswordRulesComponent);
    fixture.componentRef.instance.validChange.subscribe((v) => emissions.push(v));

    fixture.componentRef.setInput('password', 'weak');
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/auth/password-policy').flush(POLICY);
    fixture.detectChanges();

    expect(emissions.at(-1)).toBe(false);

    fixture.componentRef.setInput('password', 'Str0ng!Pass');
    fixture.detectChanges();

    expect(emissions.at(-1)).toBe(true);
  });
});
