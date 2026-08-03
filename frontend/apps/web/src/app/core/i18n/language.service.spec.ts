import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { LanguageService } from './language.service';
import { translocoTestingModule } from '../../../testing/transloco-testing';

describe('LanguageService', () => {
  let httpMock: HttpTestingController;
  const userState = signal<{ preferredLanguage: string | null } | null | undefined>(null);
  const authenticated = signal(false);
  const setPreferredLanguage = jest.fn();

  const fakeAuth = {
    userState,
    isAuthenticated: () => authenticated(),
    setPreferredLanguage,
  };

  beforeEach(() => {
    localStorage.clear();
    setPreferredLanguage.mockReset();
    userState.set(null);
    authenticated.set(false);

    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: fakeAuth },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('select() applies immediately and persists locally (FR-004/FR-006)', () => {
    const service = TestBed.inject(LanguageService);

    service.select('de');

    expect(service.language()).toBe('de');
    expect(document.documentElement.lang).toBe('de');
    expect(localStorage.getItem('jh.lang')).toBe('de');
    httpMock.expectNone('/api/v1/account/language'); // anonymous → no persistence call
  });

  it('select() persists to the account and syncs the session when signed in (FR-005)', () => {
    authenticated.set(true);
    userState.set({ preferredLanguage: null });
    const service = TestBed.inject(LanguageService);

    service.select('es');

    const req = httpMock.expectOne('/api/v1/account/language');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ language: 'es' });
    req.flush({});

    expect(setPreferredLanguage).toHaveBeenCalledWith('es');
  });
});
