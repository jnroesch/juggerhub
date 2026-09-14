import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { computed } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Translation, TranslocoService } from '@jsverse/transloco';
import { of } from 'rxjs';
import { translocoTestingModule } from '../../../testing/transloco-testing';
import { AuthUser } from '../../core/models/auth.models';
import { AuthService } from '../../core/services/auth.service';
import { ProfileService } from '../../core/services/profile.service';
import { RecognitionAdminService } from '../../core/services/recognition-admin.service';
import { AvatarMenuComponent } from './avatar-menu.component';

// JSON via require: a default import resolves to `undefined` under this Jest config (see transloco-testing).
const en: Translation = require('../../../../public/i18n/en.json');
const de: Translation = require('../../../../public/i18n/de.json');

const USER: AuthUser = {
  id: 'u1',
  email: 'nik@example.com',
  emailConfirmed: true,
  onboardingCompleted: true,
  handle: 'nik-berlin',
  hasAvatar: false,
  preferredLanguage: null,
};

/**
 * GH #283 — the top-nav avatar kept showing the initial after the player uploaded a picture. It now
 * shows the same image as their profile, and an upload from any screen replaces it in place. Runs the
 * real AuthService + ProfileService so the upload → header path is the one the app uses.
 */
describe('AvatarMenuComponent — own avatar (GH #283)', () => {
  let httpMock: HttpTestingController;
  let profiles: ProfileService;

  function signIn(user: AuthUser): ComponentFixture<AvatarMenuComponent> {
    TestBed.inject(AuthService).loadSession().subscribe();
    httpMock.expectOne('/api/v1/auth/me').flush(user);
    const fixture = TestBed.createComponent(AvatarMenuComponent);
    fixture.detectChanges();
    return fixture;
  }

  function image(fixture: ComponentFixture<AvatarMenuComponent>): HTMLImageElement | null {
    return fixture.nativeElement.querySelector('[data-testid="avatar-menu-image"]');
  }

  function button(fixture: ComponentFixture<AvatarMenuComponent>): HTMLElement {
    return fixture.nativeElement.querySelector('[data-testid="avatar-menu-button"]');
  }

  function upload(): void {
    profiles.uploadAvatar(new File(['x'], 'a.png', { type: 'image/png' })).subscribe();
    httpMock.expectOne('/api/v1/profiles/me/avatar').flush(null);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AvatarMenuComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        {
          provide: RecognitionAdminService,
          useValue: { isAdmin: computed(() => false), checkAccess: () => of(false) },
        },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    profiles = TestBed.inject(ProfileService);
  });

  afterEach(() => httpMock.verify());

  it('shows the initial when the player has no avatar', () => {
    const fixture = signIn(USER);

    expect(image(fixture)).toBeNull();
    expect(button(fixture).textContent?.trim()).toBe('N');
  });

  it('shows the same image as the profile when the player has one', () => {
    const fixture = signIn({ ...USER, hasAvatar: true });

    expect(image(fixture)?.getAttribute('src')).toBe('/api/v1/profiles/nik-berlin/avatar?v=0');
  });

  it('swaps the initial for the image once a first avatar is uploaded', () => {
    const fixture = signIn(USER);

    upload();
    fixture.detectChanges();

    expect(image(fixture)?.getAttribute('src')).toBe('/api/v1/profiles/nik-berlin/avatar?v=1');
  });

  it('re-fetches the image when an existing avatar is replaced', () => {
    const fixture = signIn({ ...USER, hasAvatar: true });

    upload();
    fixture.detectChanges();

    // A new URL is what makes the browser load the new bytes rather than reuse its cached image.
    expect(image(fixture)?.getAttribute('src')).toBe('/api/v1/profiles/nik-berlin/avatar?v=1');
  });

  it('falls back to the initial when the image cannot be loaded, and retries after the next upload', () => {
    const fixture = signIn({ ...USER, hasAvatar: true });

    image(fixture)!.dispatchEvent(new Event('error'));
    fixture.detectChanges();
    expect(image(fixture)).toBeNull();
    expect(button(fixture).textContent?.trim()).toBe('N');

    upload();
    fixture.detectChanges();
    expect(image(fixture)?.getAttribute('src')).toBe('/api/v1/profiles/nik-berlin/avatar?v=1');
  });

  it('keeps the initial when an upload fails', () => {
    const fixture = signIn(USER);

    profiles.uploadAvatar(new File(['x'], 'a.png', { type: 'image/png' })).subscribe({ error: () => undefined });
    httpMock.expectOne('/api/v1/profiles/me/avatar').flush('nope', { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(image(fixture)).toBeNull();
  });
});

/**
 * GH #287 — the menu was hard-coded in English, so it stayed English under German or Spanish.
 * Renders it in German and checks every label against the catalogue, admin entry included.
 */
describe('AvatarMenuComponent — translated (GH #287)', () => {
  const MENU_KEYS = [
    'create',
    'createEvent',
    'createTeam',
    'profile',
    'account',
    'notificationSettings',
    'sendFeedback',
    'adminPanel',
    'signOut',
  ];
  const nav = (catalog: Translation, key: string): string => (catalog['nav'] as Translation)[key] as string;

  it('renders every label and aria-label in the active language', () => {
    TestBed.configureTestingModule({
      imports: [AvatarMenuComponent, translocoTestingModule({ en, de })],
      providers: [
        provideRouter([]),
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        {
          provide: RecognitionAdminService,
          useValue: { isAdmin: computed(() => true), checkAccess: () => of(true) },
        },
      ],
    });
    TestBed.inject(TranslocoService).setActiveLang('de');
    const httpMock = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).loadSession().subscribe();
    httpMock.expectOne('/api/v1/auth/me').flush(USER);

    const fixture = TestBed.createComponent(AvatarMenuComponent);
    fixture.detectChanges();
    const button: HTMLElement = fixture.nativeElement.querySelector('[data-testid="avatar-menu-button"]');
    button.click();
    fixture.detectChanges();

    const menu: HTMLElement = fixture.nativeElement.querySelector('[data-testid="avatar-menu"]');
    expect(button.getAttribute('aria-label')).toBe(nav(de, 'accountMenu'));
    expect(menu.getAttribute('aria-label')).toBe(nav(de, 'account'));
    const text = menu.textContent ?? '';
    for (const key of MENU_KEYS) {
      expect(text).toContain(nav(de, key));
      expect(text).not.toContain(nav(en, key));
    }
    httpMock.verify();
  });
});
