import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EventDetail } from '../../../../core/models/event.models';
import { PartyContext } from '../../../../core/models/party.models';
import { translocoTestingModule } from '../../../../../testing/transloco-testing';
import { EventJoinActionsComponent } from './join-actions.component';

function detail(overrides: Partial<EventDetail> = {}): EventDetail {
  return {
    id: 'e1',
    name: 'Hanse Cup',
    type: 'Tournament',
    customTypeLabel: null,
    description: '',
    startsAt: '2999-05-01T09:00:00Z',
    endsAt: '2999-05-02T18:00:00Z',
    locationKind: 'Virtual',
    venueName: null,
    street: null,
    postalCode: null,
    location: null,
    virtualLink: null,
    participantMode: 'Individuals',
    participationLimit: 16,
    occupiedSpots: 0,
    isFull: false,
    isPaid: false,
    feeAmount: null,
    feeCurrency: null,
    feeRecipientName: null,
    feeIban: null,
    feePaymentDeadline: null,
    status: 'Published',
    viewer: { isAuthenticated: true, isAdmin: false, mySignupStatus: null, mySignupId: null, teamsICanEnter: [] },
    ...overrides,
  };
}

const ENDED = { startsAt: '2020-05-01T09:00:00Z', endsAt: '2020-05-02T18:00:00Z' };

describe('EventJoinActionsComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [translocoTestingModule()], providers: [provideRouter([])] });
  });

  function mount(d: EventDetail, party: PartyContext | null = null): ComponentFixture<EventJoinActionsComponent> {
    const fixture = TestBed.createComponent(EventJoinActionsComponent);
    fixture.componentRef.setInput('detail', d);
    fixture.componentRef.setInput('partyContext', party);
    fixture.detectChanges();
    return fixture;
  }

  const q = (f: ComponentFixture<EventJoinActionsComponent>, testId: string) =>
    f.nativeElement.querySelector(`[data-testid="${testId}"]`) as HTMLElement | null;

  it('offers joining an upcoming event', () => {
    expect(q(mount(detail()), 'join')).not.toBeNull();
  });

  it('does not offer joining once the event is over — the server would refuse it', () => {
    const f = mount(detail(ENDED));

    expect(q(f, 'join')).toBeNull();
    expect(q(f, 'event-over')).not.toBeNull();
  });

  it('does not offer entering a party once a teams event is over, but still shows an existing party', () => {
    const party: PartyContext = {
      mode: 'Teams',
      rosterCap: 8,
      teams: [
        { teamId: 't1', teamName: 'Alpha', teamSlug: 'alpha', isAdmin: true, partyId: null, canForm: true, myState: 'None', inCount: null, rosterCap: null, partyStatus: null },
        { teamId: 't2', teamName: 'Bravo', teamSlug: 'bravo', isAdmin: true, partyId: 'p2', canForm: false, myState: 'Admin', inCount: 6, rosterCap: 8, partyStatus: 'Applied' },
      ],
    };
    const f = mount(detail({ ...ENDED, participantMode: 'Teams' }), party);

    expect(q(f, 'enter-party')).toBeNull();
    expect(q(f, 'view-party')).not.toBeNull();
  });
});
