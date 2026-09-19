import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { VerifyEmailComponent } from './verify-email.component';
import { translocoTestingModule } from '../../../../testing/transloco-testing';

const USER_ID = '018f5b6e-7c1a-7d2e-9a1b-3c4d5e6f7a8b';
const TOKEN = 'Xy9_abcDEF-ghiJKL012mnoPQR345stuVWX678yzAB_';

/**
 * Feature 053 — the verification link may carry the invite the person registered from. When it
 * does, the success state's sign-in button carries `returnUrl=/join/{slug}/{token}?action=accept`
 * and a resend forwards the pair; when it doesn't, or the pair is malformed, the page is exactly
 * what it was.
 */
describe('VerifyEmailComponent', () => {
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

  function create(query: Record<string, string>, verify: 'ok' | 'fail' = 'ok'): ComponentFixture<VerifyEmailComponent> {
    routeStub = { snapshot: { queryParamMap: convertToParamMap(query) } };
    const fixture = TestBed.createComponent(VerifyEmailComponent);
    fixture.detectChanges();
    const req = httpMock.expectOne('/api/v1/auth/verify-email');
    if (verify === 'ok') {
      req.flush({ message: 'verified' });
    } else {
      req.flush({ title: 'Verification failed' }, { status: 400, statusText: 'Bad Request' });
    }
    fixture.detectChanges();
    return fixture;
  }

  function signInHref(fixture: ComponentFixture<VerifyEmailComponent>): string {
    const a = fixture.nativeElement.querySelector('[data-testid="verify-success-signin"]') as HTMLAnchorElement;
    return a.getAttribute('href') ?? '';
  }

  it('carries the invite page as returnUrl on the sign-in button when the link carried a well-formed pair', () => {
    const fixture = create({ userId: USER_ID, token: 'verify-tok', inviteSlug: 'berlin-jugger', inviteToken: TOKEN });

    const href = signInHref(fixture);
    expect(href).toContain('/sign-in?returnUrl=');
    expect(decodeURIComponent(href)).toContain(`/join/berlin-jugger/${TOKEN}?action=accept`);
  });

  it('links to a plain sign-in when the link carried no pair', () => {
    const fixture = create({ userId: USER_ID, token: 'verify-tok' });
    expect(signInHref(fixture)).toBe('/sign-in');
  });

  it('links to a plain sign-in when the pair is incomplete or malformed', () => {
    for (const query of [
      { inviteSlug: 'berlin-jugger' },
      { inviteToken: TOKEN },
      { inviteSlug: 'Bad_Slug', inviteToken: TOKEN },
      { inviteSlug: 'berlin-jugger', inviteToken: 'not a token' },
      { inviteSlug: 'https://evil.example.com', inviteToken: TOKEN },
    ]) {
      const fixture = create({ userId: USER_ID, token: 'verify-tok', ...query });
      expect(signInHref(fixture)).toBe('/sign-in');
    }
  });

  it('forwards the pair on a resend from the failed state', () => {
    const fixture = create(
      { userId: USER_ID, token: 'stale', inviteSlug: 'berlin-jugger', inviteToken: TOKEN },
      'fail',
    );
    const instance = fixture.componentInstance as unknown as {
      resendForm: { setValue: (v: { email: string }) => void };
      resend: () => void;
    };
    instance.resendForm.setValue({ email: 'a@example.com' });

    instance.resend();

    const req = httpMock.expectOne('/api/v1/auth/resend-verification');
    expect(req.request.body).toEqual({ email: 'a@example.com', inviteSlug: 'berlin-jugger', inviteToken: TOKEN });
    req.flush({ message: 'ok' });
  });

  it('resends without the pair when the link carried none', () => {
    const fixture = create({ userId: USER_ID, token: 'stale' }, 'fail');
    const instance = fixture.componentInstance as unknown as {
      resendForm: { setValue: (v: { email: string }) => void };
      resend: () => void;
    };
    instance.resendForm.setValue({ email: 'a@example.com' });

    instance.resend();

    const req = httpMock.expectOne('/api/v1/auth/resend-verification');
    expect(req.request.body).toEqual({ email: 'a@example.com' });
    req.flush({ message: 'ok' });
  });

  it('still fails plainly when userId or token is missing', () => {
    routeStub = { snapshot: { queryParamMap: convertToParamMap({ inviteSlug: 'berlin-jugger', inviteToken: TOKEN }) } };
    const fixture = TestBed.createComponent(VerifyEmailComponent);
    fixture.detectChanges();
    httpMock.expectNone('/api/v1/auth/verify-email');
    expect(fixture.nativeElement.querySelector('[data-testid="verify-resend-email"]')).not.toBeNull();
  });
});
