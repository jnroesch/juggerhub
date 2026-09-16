import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { TeamDetail } from '../../../core/models/team.models';
import { MembershipService } from '../../../core/services/membership.service';
import { TeamService } from '../../../core/services/team.service';
import { TeamSettingsComponent } from './team-settings.component';
import { translocoTestingModule } from '../../../../testing/transloco-testing';

const ADMIN_DETAIL: TeamDetail = {
  slug: 'rheinfeuer',
  name: 'Rheinfeuer',
  type: 'CityTeam',
  location: null,
  memberCount: 4,
  myRole: 'Admin',
  beginnersWelcome: false,
  hasLogo: false,
};

/**
 * Feature 051 — the logo control on team settings. The server is the real authority (TeamLogoTests
 * cover it); these pin the parts a reviewer cannot see from the backend: that the control is
 * admin-only, that removing is offered only when there is something to remove, and that the
 * rendered image comes from the service so a replace is not masked by the browser's cache.
 */
describe('TeamSettingsComponent — team logo (feature 051)', () => {
  let teams: {
    getDetail: jest.Mock;
    getMembers: jest.Mock;
    uploadLogo: jest.Mock;
    removeLogo: jest.Mock;
    logoUrl: jest.Mock;
  };

  function configure(detail: TeamDetail): void {
    teams = {
      getDetail: jest.fn().mockReturnValue(of(detail)),
      getMembers: jest.fn().mockReturnValue(of({ items: [], totalCount: 0, skip: 0, take: 50 })),
      uploadLogo: jest.fn().mockReturnValue(of(undefined)),
      removeLogo: jest.fn().mockReturnValue(of(undefined)),
      logoUrl: jest.fn().mockReturnValue('/api/v1/teams/rheinfeuer/logo?v=1'),
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [TeamSettingsComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        { provide: TeamService, useValue: teams },
        { provide: MembershipService, useValue: { load: jest.fn() } },
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ slug: 'rheinfeuer' })) },
        },
      ],
    });
  }

  function render(detail: TeamDetail = ADMIN_DETAIL): ComponentFixture<TeamSettingsComponent> {
    configure(detail);
    const fixture = TestBed.createComponent(TeamSettingsComponent);
    fixture.detectChanges();
    return fixture;
  }

  function query(fixture: ComponentFixture<TeamSettingsComponent>, testId: string): HTMLElement | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
  }

  it('offers the upload control to an admin', () => {
    const fixture = render();
    expect(query(fixture, 'team-logo-pick')).not.toBeNull();
  });

  it('offers no logo control to a member who is not an admin', () => {
    const fixture = render({ ...ADMIN_DETAIL, myRole: 'Member' });
    expect(query(fixture, 'team-logo-pick')).toBeNull();
    expect(query(fixture, 'team-logo-remove')).toBeNull();
  });

  it('offers remove only once the team has a logo', () => {
    expect(query(render(), 'team-logo-remove')).toBeNull();
    expect(query(render({ ...ADMIN_DETAIL, hasLogo: true }), 'team-logo-remove')).not.toBeNull();
  });

  it('renders the logo through the service URL, so a replace is not served from cache', () => {
    const fixture = render({ ...ADMIN_DETAIL, hasLogo: true });
    const img = query(fixture, 'team-logo-preview') as HTMLImageElement | null;
    expect(img?.getAttribute('src')).toBe('/api/v1/teams/rheinfeuer/logo?v=1');
  });

  it('uploads the chosen file and shows the logo without reloading the page', () => {
    const fixture = render();
    const file = new File(['bytes'], 'crest.png', { type: 'image/png' });
    const input = query(fixture, 'team-logo-input') as HTMLInputElement;
    Object.defineProperty(input, 'files', { value: [file] });

    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(teams.uploadLogo).toHaveBeenCalledWith('rheinfeuer', file);
    expect(query(fixture, 'team-logo-preview')).not.toBeNull();
  });

  it('keeps the previous state and reports the reason when an upload is refused', () => {
    const fixture = render({ ...ADMIN_DETAIL, hasLogo: true });
    // A real ProblemDetails response — `problemDetail` trusts nothing else, by design (#293).
    teams.uploadLogo.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            statusText: 'Bad Request',
            error: { detail: 'That file is not an image we can read.' },
          }),
      ),
    );

    const input = query(fixture, 'team-logo-input') as HTMLInputElement;
    Object.defineProperty(input, 'files', { value: [new File(['x'], 'bad.txt')] });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(query(fixture, 'team-logo-preview')).not.toBeNull();
    expect(query(fixture, 'settings-error')?.textContent).toContain('not an image');
  });

  it('removes the logo and falls back to the letter placeholder', () => {
    const fixture = render({ ...ADMIN_DETAIL, hasLogo: true });

    (query(fixture, 'team-logo-remove') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(teams.removeLogo).toHaveBeenCalledWith('rheinfeuer');
    expect(query(fixture, 'team-logo-preview')).toBeNull();
  });
});
