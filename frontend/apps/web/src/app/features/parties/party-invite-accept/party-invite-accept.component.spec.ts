import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { translocoLocaleTestingProviders, translocoTestingModule } from '../../../../testing/transloco-testing';
import { PartyInvitePreview } from '../../../core/models/party.models';
import { PartyService } from '../../../core/services/party.service';
import { PartyInviteAcceptComponent } from './party-invite-accept.component';

const preview: PartyInvitePreview = {
  partyId: 'party-1',
  teamName: 'Rheinfeuer',
  eventName: 'Tempelhof Summer Slam',
  startsAt: '2026-10-30T09:00:00Z',
  inviterDisplayName: 'Mara',
  state: 'Usable',
};

/**
 * GH #401 — a targeted party co-admin invitation answers every account but its recipient's as if
 * it did not exist. The preview still loads for anyone holding the link, so the page explains the
 * refusal in the player's language instead of showing the server's "not valid" wording.
 */
describe('PartyInviteAcceptComponent — an invitation for another account (GH #401)', () => {
  let fixture: ComponentFixture<PartyInviteAcceptComponent>;
  let parties: { previewInvite: jest.Mock; acceptInvite: jest.Mock; declineInvite: jest.Mock };
  let navigate: jest.SpyInstance;

  const notFound = () =>
    throwError(() => new HttpErrorResponse({ status: 404, error: { detail: 'Server wording, never shown' } }));

  beforeEach(() => {
    parties = {
      previewInvite: jest.fn().mockReturnValue(of(preview)),
      acceptInvite: jest.fn(),
      declineInvite: jest.fn(),
    };
    TestBed.configureTestingModule({
      imports: [PartyInviteAcceptComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        ...translocoLocaleTestingProviders(),
        { provide: PartyService, useValue: parties },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ token: 'tok-1' }) } } },
      ],
    });
    navigate = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fixture = TestBed.createComponent(PartyInviteAcceptComponent);
    fixture.detectChanges();
  });

  const note = () => fixture.nativeElement.querySelector('[data-testid="invite-other-account"]') as HTMLElement | null;
  /** The card's two actions, in order: accept, decline. */
  const buttons = () => Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
  const otherAccount = "This invite can't be used with this account. If it was sent to another account of yours, sign in with that one.";

  it('accepting says which account to use and stays on the invitation', () => {
    parties.acceptInvite.mockReturnValue(notFound());

    buttons()[0].click();
    fixture.detectChanges();

    expect(note()?.textContent?.trim()).toBe(otherAccount);
    expect(fixture.nativeElement.textContent).not.toContain('Server wording');
    expect(buttons()[0].disabled).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('declining says the same and does not leave', () => {
    parties.declineInvite.mockReturnValue(notFound());

    buttons()[1].click();
    fixture.detectChanges();

    expect(note()?.textContent?.trim()).toBe(otherAccount);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('a refusal that is about the team, not the account, keeps its own message', () => {
    parties.acceptInvite.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 403, error: { detail: "Only a member of the party's team can co-run it." } })),
    );

    buttons()[0].click();
    fixture.detectChanges();

    expect(note()).toBeNull();
    expect(fixture.nativeElement.textContent).toContain("Only a member of the party's team can co-run it.");
  });

  it('clears the note when the next press succeeds', () => {
    parties.acceptInvite.mockReturnValueOnce(notFound()).mockReturnValueOnce(of({ partyId: 'party-1' }));

    buttons()[0].click();
    fixture.detectChanges();
    buttons()[0].click();
    fixture.detectChanges();

    expect(note()).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/parties', 'party-1']);
  });
});
