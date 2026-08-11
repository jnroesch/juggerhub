import { TestBed } from '@angular/core/testing';
import { WizardDraftStore } from './wizard-draft.store';
import { DRAFT_VERSION, EventDraft, TrainingDraft } from './wizard-draft.models';

/**
 * Guards for the wizard draft store (feature 045, GH #182).
 *
 * Two of these are not conventional coverage and should not be weakened to make a build pass:
 * the compatibility rule (a draft from an older release must be discarded, never half-restored)
 * and the never-throws rule (persistence is an enhancement and must never become a precondition
 * for using either wizard — FR-015).
 */

const TRAINING: TrainingDraft = {
  v: DRAFT_VERSION,
  step: 3,
  isRecurring: true,
  name: 'Tuesday practice',
  weekday: 'Tuesday',
  interval: 'Weekly',
  startTime: '19:00',
  endTime: '21:00',
  startDate: '2026-09-01',
  endDate: '2026-12-01',
  locationKind: 'InPerson',
  venueName: 'Sporthalle Nord',
  street: 'Hauptstr. 12',
  postalCode: '50667',
  city: {
    externalId: 'city-1',
    name: 'Köln',
    region: null,
    countryName: 'Germany',
    countryCode: 'DE',
    label: 'Köln, Germany',
    latitude: 50.9,
    longitude: 6.9,
  },
  virtualLink: '',
  description: 'Bring water.',
  visibility: 'TeamOnly',
};

const EVENT: EventDraft = {
  v: DRAFT_VERSION,
  step: 'fee',
  type: 'Tournament',
  locationKind: 'InPerson',
  participantMode: 'Teams',
  isPaid: true,
  venueName: 'Sportpark',
  street: 'Ringstr. 3',
  postalCode: '10115',
  city: null,
  name: 'Autumn Cup',
  customLabel: '',
  description: 'Two days of jugger.',
  startsAt: '2026-10-01T10:00',
  endsAt: '2026-10-02T18:00',
  virtualLink: '',
  participationLimit: 16,
  rosterCap: 8,
  feeAmount: 40,
  feeCurrency: 'EUR',
  feeRecipientName: 'Jugger e.V.',
  feeIban: 'DE89370400440532013000',
  feePaymentDeadline: '2026-09-15',
};

describe('WizardDraftStore', () => {
  let store: WizardDraftStore;

  beforeEach(() => {
    sessionStorage.clear();
    jest.restoreAllMocks();
    TestBed.configureTestingModule({});
    store = TestBed.inject(WizardDraftStore);
  });

  describe('round-trip', () => {
    it('restores a training draft field for field', () => {
      store.writeTraining('koeln-jugger', TRAINING);

      expect(store.readTraining('koeln-jugger')).toEqual(TRAINING);
    });

    it('restores an event draft field for field, fee details included', () => {
      store.writeEvent(EVENT);

      const restored = store.readEvent();

      expect(restored).toEqual(EVENT);
      // The two fields the owner decided to persist deliberately (data-model.md).
      expect(restored?.feeIban).toBe('DE89370400440532013000');
      expect(restored?.feeRecipientName).toBe('Jugger e.V.');
    });

    it('returns null when nothing has been written', () => {
      expect(store.readTraining('koeln-jugger')).toBeNull();
      expect(store.readEvent()).toBeNull();
    });
  });

  describe('scoping', () => {
    /** FR-006: a draft for one team must never appear in another team's wizard. */
    it('keeps training drafts separate per team', () => {
      store.writeTraining('team-a', TRAINING);

      expect(store.readTraining('team-b')).toBeNull();
      expect(store.readTraining('team-a')).toEqual(TRAINING);
    });

    it('clears only the team it was asked to clear', () => {
      store.writeTraining('team-a', TRAINING);
      store.writeTraining('team-b', { ...TRAINING, name: 'Other' });

      store.clearTraining('team-a');

      expect(store.readTraining('team-a')).toBeNull();
      expect(store.readTraining('team-b')).not.toBeNull();
    });

    it('clearEvent leaves training drafts alone', () => {
      store.writeTraining('team-a', TRAINING);
      store.writeEvent(EVENT);

      store.clearEvent();

      expect(store.readEvent()).toBeNull();
      expect(store.readTraining('team-a')).toEqual(TRAINING);
    });
  });

  describe('clearAll (FR-011 — sign-out)', () => {
    it('removes every draft regardless of wizard or team', () => {
      store.writeTraining('team-a', TRAINING);
      store.writeTraining('team-b', TRAINING);
      store.writeEvent(EVENT);

      store.clearAll();

      expect(store.readTraining('team-a')).toBeNull();
      expect(store.readTraining('team-b')).toBeNull();
      expect(store.readEvent()).toBeNull();
    });

    it('leaves storage owned by other features untouched', () => {
      sessionStorage.setItem('jh-chunk-reloaded', '1');
      store.writeEvent(EVENT);

      store.clearAll();

      expect(sessionStorage.getItem('jh-chunk-reloaded')).toBe('1');
    });
  });

  describe('compatibility rule (R5)', () => {
    /**
     * A draft written by an older release must be DISCARDED, not partially applied. Restoring
     * some answers while others silently sit at their defaults is indistinguishable, to the user,
     * from the app losing half their input.
     *
     * Fix the version constant, never this test.
     */
    it('discards a draft whose version differs', () => {
      sessionStorage.setItem(
        'jh-draft:training:team-a',
        JSON.stringify({ ...TRAINING, v: DRAFT_VERSION + 1 }),
      );

      expect(store.readTraining('team-a')).toBeNull();
    });

    it('discards a draft with no version at all', () => {
      const versionless: Record<string, unknown> = { ...TRAINING };
      delete versionless['v'];
      sessionStorage.setItem('jh-draft:training:team-a', JSON.stringify(versionless));

      expect(store.readTraining('team-a')).toBeNull();
    });

    it('discards unparseable JSON instead of throwing', () => {
      sessionStorage.setItem('jh-draft:event', '{not json');

      expect(() => store.readEvent()).not.toThrow();
      expect(store.readEvent()).toBeNull();
    });

    it('discards a value that is not an object', () => {
      sessionStorage.setItem('jh-draft:event', '"a string"');

      expect(store.readEvent()).toBeNull();
    });

    it('removes the incompatible entry so it is not re-read', () => {
      sessionStorage.setItem('jh-draft:event', JSON.stringify({ ...EVENT, v: 999 }));

      store.readEvent();

      expect(sessionStorage.getItem('jh-draft:event')).toBeNull();
    });
  });

  describe('never throws (FR-015)', () => {
    /**
     * Private browsing, a full quota, or storage disabled by policy must degrade to "no draft" —
     * never to a broken wizard. Persistence is an enhancement, not a precondition (SC-007).
     */
    it('survives setItem throwing', () => {
      jest.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
        throw new DOMException('QuotaExceededError');
      });

      expect(() => store.writeTraining('team-a', TRAINING)).not.toThrow();
      expect(() => store.writeEvent(EVENT)).not.toThrow();
    });

    it('survives getItem throwing, returning null', () => {
      jest.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
        throw new DOMException('SecurityError');
      });

      expect(store.readTraining('team-a')).toBeNull();
      expect(store.readEvent()).toBeNull();
    });

    it('survives removeItem throwing', () => {
      jest.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => {
        throw new DOMException('SecurityError');
      });

      expect(() => store.clearTraining('team-a')).not.toThrow();
      expect(() => store.clearEvent()).not.toThrow();
      expect(() => store.clearAll()).not.toThrow();
    });
  });
});
