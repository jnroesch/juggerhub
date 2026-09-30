import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { translocoTestingModule } from '../../../../testing/transloco-testing';
import { InvitePreview } from '../../../core/models/team.models';
import { AuthService } from '../../../core/services/auth.service';
import { MembershipService } from '../../../core/services/membership.service';
import { TeamService } from '../../../core/services/team.service';
import { InviteAcceptComponent } from './invite-accept.component';

const preview: InvitePreview = {
  teamName: 'Rheinfeuer',
  teamSlug: 'rheinfeuer',
  type: 'Mixteam',
  location: null,
  memberCount: 12,
  inviterDisplayName: 'Mara',
  state: 'Usable',
};

/**
 * Feature 064 — accepting on the invite page can meet our own limit on joining by invitation. The
 * page says so in the player's language (never the server's wording), stays on the invitation, and
 * does not retry by itself.
 */
describe('InviteAcceptComponent — the joining limit (feature 064)', () => {
  let fixture: ComponentFixture<InviteAcceptComponent>;
  let teams: { getInvitePreview: jest.Mock; acceptInvite: jest.Mock; declineInvite: jest.Mock };
  let navigate: jest.SpyInstance;

  beforeEach(() => {
    teams = {
      getInvitePreview: jest.fn().mockReturnValue(of(preview)),
      acceptInvite: jest.fn(),
      declineInvite: jest.fn(),
    };
    TestBed.configureTestingModule({
      imports: [InviteAcceptComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        { provide: TeamService, useValue: teams },
        { provide: AuthService, useValue: { ensureSession: () => of({ id: 'me' }) } },
        { provide: MembershipService, useValue: { load: jest.fn() } },
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({ slug: 'rheinfeuer', token: 'tok-1' })),
            snapshot: { queryParamMap: convertToParamMap({}) },
          },
        },
      ],
    });
    navigate = jest.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    fixture = TestBed.createComponent(InviteAcceptComponent);
    fixture.detectChanges();
  });

  const el = (testId: string) => fixture.nativeElement.querySelector(`[data-testid="${testId}"]`) as HTMLElement | null;

  it('says to try again later, stays on the invitation, and asks only once', () => {
    teams.acceptInvite.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 429, error: { detail: 'Server wording, never shown' } })),
    );

    el('accept-join')?.click();
    fixture.detectChanges();

    expect(teams.acceptInvite).toHaveBeenCalledTimes(1);
    expect(el('accept-limited')?.textContent?.trim()).toBe("You've joined a lot of teams in a short time. Try again in a while.");
    expect(el('accept-error')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Server wording');
    expect((el('accept-join') as HTMLButtonElement).disabled).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('clears the note when the next press succeeds', () => {
    teams.acceptInvite
      .mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 429 })))
      .mockReturnValueOnce(of({ teamSlug: 'rheinfeuer' }));

    el('accept-join')?.click();
    fixture.detectChanges();
    el('accept-join')?.click();
    fixture.detectChanges();

    expect(el('accept-limited')).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/t/rheinfeuer');
  });
});

/**
 * GH #401 — a targeted invitation answers every account but its recipient's as if it did not exist.
 * The preview still loads for anyone holding the link, so the page has to explain the refusal: in
 * the player's language, never the server's wording, and without leaving the invitation.
 */
describe('InviteAcceptComponent — an invitation for another account (GH #401)', () => {
  let fixture: ComponentFixture<InviteAcceptComponent>;
  let teams: { getInvitePreview: jest.Mock; acceptInvite: jest.Mock; declineInvite: jest.Mock };
  let navigateByUrl: jest.SpyInstance;
  let navigate: jest.SpyInstance;

  const notFound = () =>
    throwError(() => new HttpErrorResponse({ status: 404, error: { detail: 'Server wording, never shown' } }));

  beforeEach(() => {
    teams = {
      getInvitePreview: jest.fn().mockReturnValue(of(preview)),
      acceptInvite: jest.fn(),
      declineInvite: jest.fn(),
    };
    TestBed.configureTestingModule({
      imports: [InviteAcceptComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        { provide: TeamService, useValue: teams },
        { provide: AuthService, useValue: { ensureSession: () => of({ id: 'me' }) } },
        { provide: MembershipService, useValue: { load: jest.fn() } },
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({ slug: 'rheinfeuer', token: 'tok-1' })),
            snapshot: { queryParamMap: convertToParamMap({}) },
          },
        },
      ],
    });
    const router = TestBed.inject(Router);
    navigateByUrl = jest.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    navigate = jest.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture = TestBed.createComponent(InviteAcceptComponent);
    fixture.detectChanges();
  });

  const el = (testId: string) => fixture.nativeElement.querySelector(`[data-testid="${testId}"]`) as HTMLElement | null;
  const otherAccount = "This invite can't be used with this account. If it was sent to another account of yours, sign in with that one.";

  it('accepting says which account to use and stays on the invitation', () => {
    teams.acceptInvite.mockReturnValue(notFound());

    el('accept-join')?.click();
    fixture.detectChanges();

    expect(el('invite-other-account')?.textContent?.trim()).toBe(otherAccount);
    expect(el('accept-error')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Server wording');
    expect((el('accept-join') as HTMLButtonElement).disabled).toBe(false);
    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('declining says the same instead of leaving as if it had been declined', () => {
    teams.declineInvite.mockReturnValue(notFound());

    el('decline')?.click();
    fixture.detectChanges();

    expect(el('invite-other-account')?.textContent?.trim()).toBe(otherAccount);
    expect((el('decline') as HTMLButtonElement).disabled).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('any other failed decline still leaves for home', () => {
    teams.declineInvite.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 })));

    el('decline')?.click();
    fixture.detectChanges();

    expect(el('invite-other-account')).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/']);
  });

  it('clears the note when the next press succeeds', () => {
    teams.acceptInvite.mockReturnValueOnce(notFound()).mockReturnValueOnce(of({ teamSlug: 'rheinfeuer' }));

    el('accept-join')?.click();
    fixture.detectChanges();
    el('accept-join')?.click();
    fixture.detectChanges();

    expect(el('invite-other-account')).toBeNull();
    expect(navigateByUrl).toHaveBeenCalledWith('/t/rheinfeuer');
  });
});
