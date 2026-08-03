import { provideHttpClient, withXhr } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { PasswordPolicy } from '../../../core/models/auth.models';
import { RegisterComponent } from './register.component';
import { translocoTestingModule } from '../../../../testing/transloco-testing';

const legalEn = require('../../../../../public/i18n/legal/en.json');

const POLICY: PasswordPolicy = {
  minLength: 8,
  requireDigit: true,
  requireLowercase: true,
  requireUppercase: true,
  requireNonAlphanumeric: true,
  requiredUniqueChars: 3,
};

/** A complete, valid form value. `acceptsTerms` is spelled out at every call site on purpose. */
const VALID_FORM = {
  email: 'a@example.com',
  handle: 'nik',
  password: 'Str0ng!Pass',
  confirmPassword: 'Str0ng!Pass',
  acceptsTerms: true,
};

describe('RegisterComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  /**
   * Renders the page and answers both of its fetches: the password policy (feature 003) and the
   * legal catalogue, which feature 041 added so the acceptance control knows which version of the
   * Terms of Use it is asking about.
   */
  function createComponent(options: { legalFails?: boolean } = {}) {
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();

    httpMock.expectOne('/api/v1/auth/password-policy').flush(POLICY);

    const legal = httpMock.expectOne('/i18n/legal/en.json');
    if (options.legalFails) {
      legal.error(new ProgressEvent('network error'));
    } else {
      legal.flush(legalEn);
    }

    fixture.detectChanges();
    return fixture;
  }

  function form(fixture: ComponentFixture<RegisterComponent>): FormGroup {
    return (fixture.componentInstance as unknown as { form: FormGroup }).form;
  }

  function el(fixture: ComponentFixture<RegisterComponent>, testId: string): HTMLElement | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
  }

  function submitButton(fixture: ComponentFixture<RegisterComponent>): HTMLButtonElement {
    return el(fixture, 'register-submit') as HTMLButtonElement;
  }

  /** Satisfies the two gates that are driven by async checks rather than by the form model. */
  function markHandleAndPasswordReady(fixture: ComponentFixture<RegisterComponent>): void {
    const instance = fixture.componentInstance as unknown as {
      handleState: { set: (v: string) => void };
      passwordValid: { set: (v: boolean) => void };
    };
    instance.handleState.set('available');
    instance.passwordValid.set(true);
  }

  it('flags a password mismatch and clears it once they match', () => {
    const fixture = createComponent();

    form(fixture).setValue({ ...VALID_FORM, confirmPassword: 'nope' });
    expect(form(fixture).hasError('passwordMismatch')).toBe(true);

    form(fixture).get('confirmPassword')!.setValue('Str0ng!Pass');
    expect(form(fixture).hasError('passwordMismatch')).toBe(false);
  });

  it('keeps the submit button disabled while the passwords do not match', () => {
    const fixture = createComponent();

    form(fixture).setValue({ ...VALID_FORM, confirmPassword: 'mismatch' });
    fixture.detectChanges();

    expect(submitButton(fixture).disabled).toBe(true);
  });

  describe('terms acceptance (feature 041)', () => {
    /**
     * FR-015. A pre-ticked box is not agreement — this is the one control on the form where that
     * is a legal point rather than a usability preference, so it is asserted on the rendered
     * input and not only on the form model.
     */
    it('renders the acceptance checkbox unticked', () => {
      const fixture = createComponent();
      const checkbox = el(fixture, 'register-accept-terms') as HTMLInputElement;

      expect(checkbox.checked).toBe(false);
      expect(form(fixture).controls['acceptsTerms'].value).toBe(false);
    });

    /** FR-017: the client-side half of the gate. The server refusal is the real boundary. */
    it('keeps submit disabled until the box is ticked', () => {
      const fixture = createComponent();

      form(fixture).setValue({ ...VALID_FORM, acceptsTerms: false });
      markHandleAndPasswordReady(fixture);
      fixture.detectChanges();
      expect(submitButton(fixture).disabled).toBe(true);

      form(fixture).controls['acceptsTerms'].setValue(true);
      fixture.detectChanges();
      expect(submitButton(fixture).disabled).toBe(false);
    });

    it('says why submission is blocked rather than only disabling the button', () => {
      const fixture = createComponent();

      form(fixture).controls['acceptsTerms'].markAsTouched();
      fixture.detectChanges();

      expect(el(fixture, 'register-terms-required')).not.toBeNull();
    });

    /** FR-016: the document has to be readable before agreeing to it. */
    it('links to the full terms from the acceptance label', () => {
      const fixture = createComponent();

      expect(el(fixture, 'register-terms-link')?.getAttribute('href')).toBe('/terms');
    });

    /**
     * Nobody may be pushed into agreeing to a document the app could not load. The control fails
     * closed: a failed catalogue fetch blocks submission and says so, rather than falling back to
     * a default version and recording agreement to text that was never displayed.
     */
    it('blocks submission when the terms could not be loaded', () => {
      const fixture = createComponent({ legalFails: true });

      form(fixture).setValue(VALID_FORM);
      markHandleAndPasswordReady(fixture);
      fixture.detectChanges();

      expect(submitButton(fixture).disabled).toBe(true);
      expect(el(fixture, 'register-terms-unavailable')).not.toBeNull();
    });

    /**
     * The version sent is the one the page displayed, read from the catalogue — never hard-coded
     * in the component. That is what makes the stored record evidence of what the reader saw
     * (research R1).
     */
    it('sends the displayed version and language with the registration', () => {
      const fixture = createComponent();

      form(fixture).setValue(VALID_FORM);
      markHandleAndPasswordReady(fixture);
      fixture.detectChanges();

      submitButton(fixture).click();

      const request = httpMock.expectOne('/api/v1/auth/register');
      expect(request.request.body).toMatchObject({
        acceptsTerms: true,
        termsVersion: legalEn.terms.version,
        termsLanguage: 'en',
      });
      request.flush({ message: 'ok' });
    });

    /**
     * A 409 means the document changed under an open tab. "Something went wrong" is useless
     * there — the reader has to know the text they agreed to is no longer the current one, and
     * that reloading is the fix.
     */
    it('surfaces a version conflict as a specific, actionable message', () => {
      const fixture = createComponent();

      form(fixture).setValue(VALID_FORM);
      markHandleAndPasswordReady(fixture);
      fixture.detectChanges();

      submitButton(fixture).click();
      httpMock
        .expectOne('/api/v1/auth/register')
        .flush({ title: 'Terms have changed' }, { status: 409, statusText: 'Conflict' });
      fixture.detectChanges();

      const error = el(fixture, 'register-error')!.textContent!.toLowerCase();
      expect(error).toContain('updated');
      expect(error).toContain('reload');
    });
  });
});
