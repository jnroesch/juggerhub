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
