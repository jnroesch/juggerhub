import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { WritableSignal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { TrainingCreateComponent } from './training-create.component';
import { translocoTestingModule } from '../../../../testing/transloco-testing';
import { LocationKind, TrainingInterval, TrainingVisibility } from '../../../core/models/trainings.models';
import { CityOption } from '../../../core/models/city.models';
import { DRAFT_VERSION, TrainingDraft } from '../../../core/drafts/wizard-draft.models';
import { WizardDraftStore } from '../../../core/drafts/wizard-draft.store';

/**
 * Feature 045 (GH #182) — the wizard's state survives leaving the page.
 *
 * The reported failure was mid-wizard: an admin filling in a training left the app (or a
 * backgrounded mobile tab was discarded) and came back to a blank step 1. These tests cover the
 * restore, and the three ways a draft is meant to disappear.
 */

const SLUG = 'koeln-jugger';

/** Protected surface driven directly (signals are callable + `.set`). */
interface WizardApi {
  step: WritableSignal<1 | 2 | 3 | 4 | 5>;
  isRecurring: WritableSignal<boolean>;
  name: WritableSignal<string>;
  weekday: WritableSignal<string>;
  interval: WritableSignal<TrainingInterval>;
  startTime: WritableSignal<string>;
  endTime: WritableSignal<string>;
  startDate: WritableSignal<string>;
  endDate: WritableSignal<string>;
  locationKind: WritableSignal<LocationKind>;
  venueName: WritableSignal<string>;
  street: WritableSignal<string>;
  postalCode: WritableSignal<string>;
  selectedCity: WritableSignal<CityOption | null>;
  virtualLink: WritableSignal<string>;
  description: WritableSignal<string>;
  visibility: WritableSignal<TrainingVisibility>;
  created: () => { name: string; sessionCount: number; isOneOff: boolean; firstSessionId: string } | null;
  cancel(): void;
  create(): void;
  createAnother(): void;
}

const KOELN: CityOption = {
  externalId: 'TEST:koeln',
  name: 'Köln',
  region: null,
  countryName: 'Germany',
  countryCode: 'DE',
  label: 'Köln, Germany',
  latitude: 50.938,
  longitude: 6.96,
};

/** A draft in which every one of the 16 answers differs from its default. */
const FULL_DRAFT: TrainingDraft = {
  v: DRAFT_VERSION,
  step: 3,
  isRecurring: false,
  name: 'Tuesday practice',
  weekday: 'Thursday',
  interval: 'BiWeekly',
  startTime: '18:30',
  endTime: '20:30',
  startDate: '2026-09-01',
  endDate: '2026-12-01',
  locationKind: 'InPerson',
  venueName: 'Sporthalle Nord',
  street: 'Hauptstr. 12',
  postalCode: '50667',
  city: KOELN,
  virtualLink: 'https://meet.example.com/abc',
  description: 'Bring water and a spare pompfe.',
  visibility: 'Public',
};

describe('TrainingCreateComponent draft persistence', () => {
  let fixture: ComponentFixture<TrainingCreateComponent>;
  let api: WizardApi;
  let store: WizardDraftStore;
  let httpMock: HttpTestingController;

  function configure(): void {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: (key: string) => (key === 'slug' ? SLUG : null) } } },
        },
      ],
    });
    store = TestBed.inject(WizardDraftStore);
    httpMock = TestBed.inject(HttpTestingController);
    // Where the wizard navigates on cancel/success is not under test here, and the testing router
    // has no routes to match against.
    jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  }

  function createComponent(): void {
    fixture = TestBed.createComponent(TrainingCreateComponent);
    api = fixture.componentInstance as unknown as WizardApi;
    fixture.detectChanges();
  }

  beforeEach(() => {
    sessionStorage.clear();
    jest.restoreAllMocks();
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  describe('restoring', () => {
    beforeEach(() => {
      configure();
      store.writeTraining(SLUG, FULL_DRAFT);
      createComponent();
    });

    /**
     * SC-003. Asserted field by field on purpose: a test that samples three fields passes happily
     * while one of the others is silently dropped, which is the whole failure mode being fixed.
     */
    it('restores all 16 answers and the step', () => {
      expect(api.step()).toBe(3);
      expect(api.isRecurring()).toBe(false);
      expect(api.name()).toBe('Tuesday practice');
      expect(api.weekday()).toBe('Thursday');
      expect(api.interval()).toBe('BiWeekly');
      expect(api.startTime()).toBe('18:30');
      expect(api.endTime()).toBe('20:30');
      expect(api.startDate()).toBe('2026-09-01');
      expect(api.endDate()).toBe('2026-12-01');
      expect(api.locationKind()).toBe('InPerson');
      expect(api.venueName()).toBe('Sporthalle Nord');
      expect(api.street()).toBe('Hauptstr. 12');
      expect(api.postalCode()).toBe('50667');
      expect(api.selectedCity()).toEqual(KOELN);
      expect(api.virtualLink()).toBe('https://meet.example.com/abc');
      expect(api.description()).toBe('Bring water and a spare pompfe.');
      expect(api.visibility()).toBe('Public');
    });

    /**
     * The trap this feature is most likely to ship half-fixed (plan, risk 1). `CityPickerComponent`
     * reads its `initial` input in `ngOnInit` and never looks again, so restoring the parent signal
     * alone leaves the chip EMPTY while the review step prints the city. Asserting the rendered
     * chip — not the parent state — is what makes this test worth having.
     */
    it('restores the picked city into the picker chip, not just the parent state', () => {
      const chip = fixture.nativeElement.querySelector('[data-testid="city-picker-chip"]');

      expect(chip).not.toBeNull();
      expect(chip.textContent).toContain('Köln, Germany');
    });

    it('opens on the step the user left rather than step 1', () => {
      expect(api.step()).toBe(3);
    });
  });

  describe('writing', () => {
    beforeEach(() => {
      configure();
      createComponent();
    });

    /**
     * FR-013. Without this, merely opening the wizard leaves an entry behind and the next visit
     * "restores" input the user never gave.
     */
    it('writes nothing when the wizard is opened and left untouched', () => {
      expect(store.readTraining(SLUG)).toBeNull();
    });

    /**
     * FR-005 — the heart of the fix. Saving only on step change would lose whichever step is open
     * when the tab is discarded, which is exactly what was reported.
     */
    it('persists an answer immediately, without advancing a step', () => {
      api.description.set('Bring water.');
      fixture.detectChanges();

      expect(store.readTraining(SLUG)?.description).toBe('Bring water.');
      expect(store.readTraining(SLUG)?.step).toBe(1);
    });

    it('persists the step as the user moves through the wizard', () => {
      api.name.set('Tuesday practice');
      api.step.set(4);
      fixture.detectChanges();

      expect(store.readTraining(SLUG)?.step).toBe(4);
    });

    /** Editing back to the pristine state should not leave a stale draft behind either. */
    it('drops the draft when every answer is returned to its default', () => {
      api.name.set('Something');
      fixture.detectChanges();
      expect(store.readTraining(SLUG)).not.toBeNull();

      api.name.set('');
      fixture.detectChanges();

      expect(store.readTraining(SLUG)).toBeNull();
    });
  });

  describe('discarding', () => {
    it('opens blank when the stored draft is from an older release', () => {
      configure();
      sessionStorage.setItem(
        `jh-draft:training:${SLUG}`,
        JSON.stringify({ ...FULL_DRAFT, v: DRAFT_VERSION + 1 }),
      );

      createComponent();

      expect(api.name()).toBe('');
      expect(api.step()).toBe(1);
    });

    it('clears the draft on cancel (FR-008)', () => {
      configure();
      store.writeTraining(SLUG, FULL_DRAFT);
      createComponent();

      api.cancel();

      expect(store.readTraining(SLUG)).toBeNull();
    });

    /** FR-007 — cleared once the server has accepted, so a reopened wizard is blank. */
    it('clears the draft after the server accepts the create', () => {
      configure();
      store.writeTraining(SLUG, { ...FULL_DRAFT, step: 5 });
      createComponent();

      api.create();
      httpMock
        .expectOne(`/api/v1/teams/${SLUG}/trainings`)
        .flush({ id: 't1', firstSessionId: 's1' });

      expect(store.readTraining(SLUG)).toBeNull();
    });

    /**
     * The other half of FR-007, and the reason clearing is wired to the server's acceptance rather
     * than to the button: a rejected create must not throw the user's answers away.
     */
    it('keeps the draft when the create is rejected', () => {
      configure();
      store.writeTraining(SLUG, { ...FULL_DRAFT, step: 5 });
      createComponent();

      api.create();
      httpMock
        .expectOne(`/api/v1/teams/${SLUG}/trainings`)
        .flush({ detail: 'End date is before the start date.' }, { status: 400, statusText: 'Bad Request' });

      expect(store.readTraining(SLUG)?.name).toBe('Tuesday practice');
    });
  });

  /**
   * SC-007 / FR-015. Private browsing, a full quota or storage disabled by policy must leave the
   * wizard exactly as usable as it is today — persistence is an enhancement, never a precondition.
   */
  describe('when browser storage is unavailable', () => {
    it('still lets the wizard be filled in and submitted', () => {
      jest.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
        throw new DOMException('QuotaExceededError');
      });
      jest.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
        throw new DOMException('SecurityError');
      });
      configure();

      expect(() => {
        createComponent();
        api.name.set('Tuesday practice');
        api.step.set(5);
        fixture.detectChanges();
      }).not.toThrow();

      expect(api.name()).toBe('Tuesday practice');
    });
  });

  /**
   * GH #187 — the review step must show the series end date in the active language, never the raw
   * `YYYY-MM-DD` the date input holds. Mirrors the recognition-display regression spec: the de/en
   * difference plus a mid-review language switch that re-renders. The shared locale-bound helper
   * follows the active language; Angular's `| date` pipe would be pinned to `LOCALE_ID` at bootstrap.
   */
  describe('review step end date (GH #187)', () => {
    it('shows the end date in the active language and re-renders on a switch', () => {
      configure();
      createComponent();

      api.isRecurring.set(true);
      api.endDate.set('2026-12-31');
      api.step.set(5);
      fixture.detectChanges();

      const text = () => fixture.nativeElement.textContent as string;
      expect(text()).not.toContain('2026-12-31');
      expect(text()).toContain('Dec'); // en short month

      TestBed.inject(TranslocoService).setActiveLang('de');
      fixture.detectChanges();

      expect(text()).toContain('Dez'); // de short month
      expect(text()).not.toContain('Dec');
    });
  });

  /**
   * GH #188 — after a successful create the wizard shows a success step confirming the series,
   * instead of redirecting to one arbitrary session. The subtle part is the interaction with the
   * draft-persistence effect: the just-submitted answers must NOT be re-saved as a fresh draft, and
   * "create another" must open a genuinely blank wizard.
   */
  describe('success step (GH #188)', () => {
    function submitRecurring(): void {
      configure();
      store.writeTraining(SLUG, { ...FULL_DRAFT, isRecurring: true, step: 5 });
      createComponent();

      api.create();
      httpMock
        .expectOne(`/api/v1/teams/${SLUG}/trainings`)
        .flush({ trainingId: 't1', sessionCount: 12, firstSessionId: 's1' });
      fixture.detectChanges();
    }

    it('shows the success step with the session count and clears the draft', () => {
      submitRecurring();

      expect(api.created()).toEqual(
        expect.objectContaining({ name: 'Tuesday practice', sessionCount: 12, firstSessionId: 's1' }),
      );
      expect(store.readTraining(SLUG)).toBeNull();

      const success = fixture.nativeElement.querySelector('[data-testid="training-success"]');
      expect(success).not.toBeNull();
      expect(fixture.nativeElement.querySelector('[data-testid="training-success-count"]').textContent).toContain('12');
    });

    /**
     * The heart of the fix: returning to pristine on success is what stops the effect from writing
     * the submitted answers back. Without it, a reopened wizard would "restore" the finished draft.
     */
    it('resets the wizard to a blank state and persists nothing after success', () => {
      submitRecurring();

      expect(api.name()).toBe('');
      expect(api.step()).toBe(1);
      expect(store.readTraining(SLUG)).toBeNull();
    });

    it('create another returns to a blank wizard, not the success step', () => {
      submitRecurring();

      api.createAnother();
      fixture.detectChanges();

      expect(api.created()).toBeNull();
      expect(api.name()).toBe('');
      expect(api.step()).toBe(1);
      expect(store.readTraining(SLUG)).toBeNull();
      expect(fixture.nativeElement.querySelector('[data-testid="training-success"]')).toBeNull();
    });
  });
});
