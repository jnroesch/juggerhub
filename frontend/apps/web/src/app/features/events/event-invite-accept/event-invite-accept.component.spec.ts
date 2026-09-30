import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { translocoLocaleTestingProviders, translocoTestingModule } from '../../../../testing/transloco-testing';
import { InvitePreview } from '../../../core/models/event.models';
import { EventService } from '../../../core/services/event.service';
import { EventInviteAcceptComponent } from './event-invite-accept.component';

const preview: InvitePreview = {
  eventId: 'ev-1',
  eventName: 'Tempelhof Summer Slam',
  startsAt: '2026-10-30T09:00:00Z',
  inviterDisplayName: 'Mara',
  state: 'Usable',
};

/**
 * GH #401 — a targeted co-admin invitation answers every account but its recipient's as if it did
 * not exist. The preview still loads for anyone holding the link, so the page has to explain the
 * refusal rather than go quiet, and it must not read as an accepted or declined invitation.
 */
describe('EventInviteAcceptComponent — an invitation for another account (GH #401)', () => {
  let fixture: ComponentFixture<EventInviteAcceptComponent>;
  let events: { getInvitePreview: jest.Mock; acceptInvite: jest.Mock; declineInvite: jest.Mock };
  let navigate: jest.SpyInstance;

  const notFound = () =>
    throwError(() => new HttpErrorResponse({ status: 404, error: { detail: 'Server wording, never shown' } }));

  beforeEach(() => {
    events = {
      getInvitePreview: jest.fn().mockReturnValue(of(preview)),
      acceptInvite: jest.fn(),
      declineInvite: jest.fn(),
    };
    TestBed.configureTestingModule({
      imports: [EventInviteAcceptComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        ...translocoLocaleTestingProviders(),
        { provide: EventService, useValue: events },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ token: 'tok-1' }) } } },
      ],
    });
    navigate = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fixture = TestBed.createComponent(EventInviteAcceptComponent);
    fixture.detectChanges();
  });

  const el = (testId: string) => fixture.nativeElement.querySelector(`[data-testid="${testId}"]`) as HTMLElement | null;
  const otherAccount = "This invite can't be used with this account. If it was sent to another account of yours, sign in with that one.";

  it('accepting says which account to use and stays on the invitation', () => {
    events.acceptInvite.mockReturnValue(notFound());

    el('invite-accept')?.click();
    fixture.detectChanges();

    expect(el('invite-other-account')?.textContent?.trim()).toBe(otherAccount);
    expect(fixture.nativeElement.textContent).not.toContain('Server wording');
    expect((el('invite-accept') as HTMLButtonElement).disabled).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('declining says the same and does not leave', () => {
    events.declineInvite.mockReturnValue(notFound());

    el('invite-decline')?.click();
    fixture.detectChanges();

    expect(el('invite-other-account')?.textContent?.trim()).toBe(otherAccount);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('a signed-out visitor is still sent to sign in, with no note', () => {
    events.acceptInvite.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 401 })));

    el('invite-accept')?.click();
    fixture.detectChanges();

    expect(el('invite-other-account')).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/sign-in'], { queryParams: { returnUrl: '/event-invite/tok-1' } });
  });

  it('clears the note when the next press succeeds', () => {
    events.acceptInvite.mockReturnValueOnce(notFound()).mockReturnValueOnce(of({ eventId: 'ev-1' }));

    el('invite-accept')?.click();
    fixture.detectChanges();
    el('invite-accept')?.click();
    fixture.detectChanges();

    expect(el('invite-other-account')).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/events', 'ev-1']);
  });
});
