import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed, ComponentFixture } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { WritableSignal, Signal } from '@angular/core';
import { OnboardingComponent } from './onboarding.component';
import { OwnerProfile, UpdateProfileRequest } from '../../core/models/profile.models';
import { Pompfe } from '../../shared/pompfen.catalog';

const PROFILE: OwnerProfile = {
  handle: 'nik',
  displayName: 'nik',
  hometown: null,
  description: null,
  hasAvatar: false,
  pompfen: [],
  recentActivity: [],
};

/** Protected surface we drive directly in tests (signals are callable + .set). */
interface OnboardingApi {
  step: Signal<string>;
  displayName: WritableSignal<string>;
  hometown: WritableSignal<string>;
  description: WritableSignal<string>;
  selectedPompfen: WritableSignal<Pompfe[]>;
  nameEmpty: Signal<boolean>;
  next(): void;
  back(): void;
  finish(): void;
  dismiss(): void;
}

describe('OnboardingComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    httpMock = TestBed.inject(HttpTestingController);
    const router = TestBed.inject(Router);
    jest.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => httpMock.verify());

  function createComponent(prefill: OwnerProfile = PROFILE) {
    const fixture = TestBed.createComponent(OnboardingComponent);
    fixture.detectChanges(); // ngOnInit → getMine()
    httpMock.expectOne('/api/v1/profiles/me').flush(prefill);
    fixture.detectChanges();
    return fixture;
  }

  function api(fixture: ComponentFixture<OnboardingComponent>): OnboardingApi {
    return fixture.componentInstance as unknown as OnboardingApi;
  }

  it('prefills the display name from the profile and blocks Continue when empty', () => {
    const fixture = createComponent();
    const comp = api(fixture);

    expect(comp.displayName()).toBe('nik'); // prefilled (defaults to the handle)
    expect(comp.nameEmpty()).toBe(false);

    comp.displayName.set('   ');
    expect(comp.nameEmpty()).toBe(true);

    // The name-step Continue button reflects the gate.
    comp.next(); // welcome → name
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector(
      '[data-testid="onboarding-continue"]',
    ) as HTMLButtonElement;
    expect(button.disabled).toBe(true);
  });

  it('finish() sends one profile update then marks onboarding complete (no avatar)', () => {
    const fixture = createComponent();
    const comp = api(fixture);

    comp.displayName.set('Nik Berlin');
    comp.hometown.set('Berlin');
    comp.description.set('Läufer at heart.');
    comp.selectedPompfen.set(['Stab', 'Laeufer']);
    comp.finish();

    const update = httpMock.expectOne('/api/v1/profiles/me');
    expect(update.request.method).toBe('PUT');
    const body = update.request.body as UpdateProfileRequest;
    expect(body).toEqual({
      displayName: 'Nik Berlin',
      hometown: 'Berlin',
      description: 'Läufer at heart.',
      pompfen: ['Stab', 'Laeufer'],
    });
    update.flush(PROFILE);

    // No avatar was picked → no avatar upload, straight to complete.
    const complete = httpMock.expectOne('/api/v1/profiles/me/onboarding/complete');
    expect(complete.request.method).toBe('POST');
    complete.flush(null);

    expect(comp.step()).toBe('done');
  });

  it('a name-only finish sends null optional fields (skipped steps are not written as blanks)', () => {
    const fixture = createComponent();
    const comp = api(fixture);

    comp.displayName.set('Solo'); // everything else left at its prefilled (empty) default
    comp.finish();

    const update = httpMock.expectOne('/api/v1/profiles/me');
    expect(update.request.body).toEqual({
      displayName: 'Solo',
      hometown: null,
      description: null,
      pompfen: [],
    });
    update.flush(PROFILE);
    httpMock.expectOne('/api/v1/profiles/me/onboarding/complete').flush(null);

    expect(comp.step()).toBe('done');
  });

  it('dismiss() marks onboarding complete without writing any profile update', () => {
    const fixture = createComponent();
    const comp = api(fixture);

    comp.dismiss();

    // No profile PUT — just complete, then a session refresh on the way out.
    const complete = httpMock.expectOne('/api/v1/profiles/me/onboarding/complete');
    expect(complete.request.method).toBe('POST');
    complete.flush(null);

    // enterApp() re-hydrates the session so the guard sees the completed flag.
    httpMock
      .expectOne('/api/v1/auth/me')
      .flush({ id: 'u1', email: 'a@example.com', emailConfirmed: true, onboardingCompleted: true });
  });
});
