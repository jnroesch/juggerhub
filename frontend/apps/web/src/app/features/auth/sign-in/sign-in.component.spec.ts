import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { SignInComponent } from './sign-in.component';
import { translocoTestingModule } from '../../../../testing/transloco-testing';

const TOKEN = 'Xy9_abcDEF-ghiJKL012mnoPQR345stuVWX678yzAB_';
const JOIN_RETURN = `/join/berlin-jugger/${TOKEN}?action=accept`;

interface SignInApi {
  form: { setValue: (v: { email: string; password: string; rememberMe: boolean }) => void };
  submit: () => void;
  resendVerification: () => void;
}

/**
 * The sign-in page's part in carrying an invite (feature 053): a resend from here forwards the
 * invite pair when the returnUrl is the invite page, and the post-login hop into `/onboarding`
 * carries the returnUrl — the hop the wizard relies on to learn about the invite.
 */
describe('SignInComponent', () => {
  let httpMock: HttpTestingController;
  let routeStub: { snapshot: { queryParamMap: ReturnType<typeof convertToParamMap> } };

  beforeEach(() => {
    routeStub = { snapshot: { queryParamMap: convertToParamMap({}) } };
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useFactory: () => routeStub },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function create(returnUrl: string | null): { fixture: ComponentFixture<SignInComponent>; api: SignInApi } {
    routeStub = { snapshot: { queryParamMap: convertToParamMap(returnUrl ? { returnUrl } : {}) } };
    const fixture = TestBed.createComponent(SignInComponent);
    fixture.detectChanges();
    const api = fixture.componentInstance as unknown as SignInApi;
    api.form.setValue({ email: 'a@example.com', password: 'Str0ng!Pass', rememberMe: false });
    return { fixture, api };
  }

  describe('resend verification', () => {
    it('forwards the invite pair when the returnUrl is the invite page', () => {
      const { api } = create(JOIN_RETURN);

      api.resendVerification();

      const req = httpMock.expectOne('/api/v1/auth/resend-verification');
      expect(req.request.body).toEqual({ email: 'a@example.com', inviteSlug: 'berlin-jugger', inviteToken: TOKEN });
      req.flush({ message: 'ok' });
    });

    it('sends only the email otherwise', () => {
      for (const returnUrl of [null, '/players/nik', `/join/Bad_Slug/${TOKEN}`]) {
        const { api } = create(returnUrl);
        api.resendVerification();
        const req = httpMock.expectOne('/api/v1/auth/resend-verification');
        expect(req.request.body).toEqual({ email: 'a@example.com' });
        req.flush({ message: 'ok' });
      }
    });
  });

  describe('after a successful sign-in', () => {
    function login(api: SignInApi, onboardingCompleted: boolean): void {
      api.submit();
      httpMock.expectOne('/api/v1/auth/login').flush({
        id: 'u1',
        email: 'a@example.com',
        emailConfirmed: true,
        onboardingCompleted,
        handle: 'nik',
        isAdmin: false,
        preferredLanguage: null,
      });
    }

    it('carries the returnUrl into onboarding for a not-yet-onboarded account', () => {
      const { api } = create(JOIN_RETURN);
      const router = TestBed.inject(Router);
      const navigate = jest.spyOn(router, 'navigate').mockResolvedValue(true);

      login(api, false);

      expect(navigate).toHaveBeenCalledWith(['/onboarding'], { queryParams: { returnUrl: JOIN_RETURN } });
    });

    it('goes straight to the returnUrl for an onboarded account', () => {
      const { api } = create(JOIN_RETURN);
      const router = TestBed.inject(Router);
      const navigateByUrl = jest.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

      login(api, true);

      expect(navigateByUrl).toHaveBeenCalledWith(JOIN_RETURN);
    });

    it('drops an external returnUrl on both paths (open-redirect guard)', () => {
      const { api } = create('https://evil.example.com/');
      const router = TestBed.inject(Router);
      const navigate = jest.spyOn(router, 'navigate').mockResolvedValue(true);

      login(api, false);

      expect(navigate).toHaveBeenCalledWith(['/onboarding'], {});
    });
  });
});
