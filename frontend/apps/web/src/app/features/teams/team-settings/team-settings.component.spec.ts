import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { TeamDetail } from '../../../core/models/team.models';
import { AuthService } from '../../../core/services/auth.service';
import { CityService } from '../../../core/services/city.service';
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
  description: null,
  links: [],
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
        // Feature 061 — the details section renders the city picker for a City team.
        { provide: CityService, useValue: { search: jest.fn().mockReturnValue(of([])) } },
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

/**
 * GH #361 — a member leaves the team from "Manage team". The server owns the rule
 * (`MutateMembershipAsync`: self-removal is allowed, the last admin is refused); these pin what
 * only the page can get wrong: that a plain member can open the page and is offered leave, that
 * the sole admin gets the disabled control + warning rather than a request that would only 409,
 * and that leaving refreshes the cached memberships before landing on "My team".
 */
describe('TeamSettingsComponent — leave team (GH #361)', () => {
  const ME = '00000000-0000-7000-8000-000000000001';
  const OTHER = '00000000-0000-7000-8000-000000000002';

  let teams: { getDetail: jest.Mock; getMembers: jest.Mock; removeMember: jest.Mock; logoUrl: jest.Mock };
  let membership: { load: jest.Mock };

  function member(userId: string, role: 'Admin' | 'Member') {
    return { userId, handle: `u${userId.slice(-1)}`, displayName: `Player ${userId.slice(-1)}`, role, hasAvatar: false, pompfen: [] };
  }

  function render(detail: TeamDetail, roster: ReturnType<typeof member>[]): ComponentFixture<TeamSettingsComponent> {
    teams = {
      getDetail: jest.fn().mockReturnValue(of(detail)),
      getMembers: jest.fn().mockReturnValue(of({ items: roster, totalCount: roster.length, skip: 0, take: 50 })),
      removeMember: jest.fn().mockReturnValue(of(undefined)),
      logoUrl: jest.fn().mockReturnValue('/api/v1/teams/rheinfeuer/logo?v=1'),
    };
    membership = { load: jest.fn() };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [TeamSettingsComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        { provide: TeamService, useValue: teams },
        { provide: AuthService, useValue: { currentUser: () => ({ id: ME }) } },
        { provide: MembershipService, useValue: membership },
        { provide: CityService, useValue: { search: jest.fn().mockReturnValue(of([])) } },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ slug: 'rheinfeuer' })) } },
      ],
    });
    const fixture = TestBed.createComponent(TeamSettingsComponent);
    fixture.detectChanges();
    return fixture;
  }

  function query(fixture: ComponentFixture<TeamSettingsComponent>, testId: string): HTMLElement | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
  }

  it('opens for a plain member and offers leave, with no admin controls', () => {
    const fixture = render({ ...ADMIN_DETAIL, myRole: 'Member' }, []);
    expect(query(fixture, 'membership')).not.toBeNull();
    expect((query(fixture, 'leave-team') as HTMLButtonElement).disabled).toBe(false);
    expect(query(fixture, 'step-down')).toBeNull();
    expect(query(fixture, 'delete-team')).toBeNull();
    expect(query(fixture, 'team-logo-pick')).toBeNull();
  });

  it('offers an admin both step down and leave while another admin remains', () => {
    const fixture = render(ADMIN_DETAIL, [member(ME, 'Admin'), member(OTHER, 'Admin')]);
    expect((query(fixture, 'step-down') as HTMLButtonElement).disabled).toBe(false);
    expect((query(fixture, 'leave-team') as HTMLButtonElement).disabled).toBe(false);
    expect(query(fixture, 'sole-admin-warning')).toBeNull();
  });

  it('disables both for the sole admin and says why', () => {
    const fixture = render(ADMIN_DETAIL, [member(ME, 'Admin'), member(OTHER, 'Member')]);
    expect((query(fixture, 'step-down') as HTMLButtonElement).disabled).toBe(true);
    expect((query(fixture, 'leave-team') as HTMLButtonElement).disabled).toBe(true);
    expect(query(fixture, 'sole-admin-warning')).not.toBeNull();
  });

  it('asks first, and cancelling leaves the membership untouched', () => {
    const fixture = render({ ...ADMIN_DETAIL, myRole: 'Member' }, []);
    (query(fixture, 'leave-team') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(query(fixture, 'leave-confirm')).not.toBeNull();

    (query(fixture, 'leave-confirm')?.querySelectorAll('button')[1] as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(query(fixture, 'leave-confirm')).toBeNull();
    expect(query(fixture, 'leave-team')).not.toBeNull();
    expect(teams.removeMember).not.toHaveBeenCalled();
  });

  it('removes the viewer themselves, refreshes the cached memberships and lands on My team', () => {
    const fixture = render({ ...ADMIN_DETAIL, myRole: 'Member' }, []);
    const navigate = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    (query(fixture, 'leave-team') as HTMLButtonElement).click();
    fixture.detectChanges();
    (query(fixture, 'leave-confirm-yes') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(teams.removeMember).toHaveBeenCalledWith('rheinfeuer', ME);
    expect(membership.load).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/my-team']);
  });

  it('shows the server reason and stays on the page when the leave is refused', () => {
    const fixture = render(ADMIN_DETAIL, [member(ME, 'Admin'), member(OTHER, 'Admin')]);
    const navigate = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    teams.removeMember.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: { detail: 'Make someone else an admin before you step down or leave.' },
          }),
      ),
    );

    (query(fixture, 'leave-team') as HTMLButtonElement).click();
    fixture.detectChanges();
    (query(fixture, 'leave-confirm-yes') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(navigate).not.toHaveBeenCalled();
    expect(membership.load).not.toHaveBeenCalled();
    expect(query(fixture, 'leave-confirm')).toBeNull();
    expect(query(fixture, 'settings-error')?.textContent).toContain('Make someone else an admin');
  });
});

/**
 * Feature 061 — the Team details section. The server owns every rule (TeamDetailsTests); these
 * pin what only the page can get wrong: who sees the section, that a save sends the whole thing
 * (the current city resent untouched, none for a Mixteam), that the page and the cached
 * memberships follow a success, and that a refusal is shown in the reader's words — never the
 * server's English `detail` (GH #179) — pointing at the right link row.
 */
describe('TeamSettingsComponent — team details (feature 061)', () => {
  const DETAIL: TeamDetail = {
    ...ADMIN_DETAIL,
    location: { externalId: 'TEST:berlin', name: 'Berlin', region: null, countryName: 'Germany', countryCode: 'DE', label: 'Berlin, Germany' },
    description: 'Wir trainieren dienstags.',
    links: [{ label: 'Website', url: 'https://rheinfeuer.de/' }],
  };

  let teams: {
    getDetail: jest.Mock;
    getMembers: jest.Mock;
    updateDetails: jest.Mock;
    logoUrl: jest.Mock;
  };
  let membership: { load: jest.Mock };

  function render(detail: TeamDetail = DETAIL): ComponentFixture<TeamSettingsComponent> {
    teams = {
      getDetail: jest.fn().mockReturnValue(of(detail)),
      getMembers: jest.fn().mockReturnValue(of({ items: [], totalCount: 0, skip: 0, take: 50 })),
      updateDetails: jest.fn().mockImplementation((_slug: string, body: { name: string }) => of({ ...detail, name: body.name })),
      logoUrl: jest.fn().mockReturnValue('/api/v1/teams/rheinfeuer/logo?v=1'),
    };
    membership = { load: jest.fn() };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [TeamSettingsComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        { provide: TeamService, useValue: teams },
        { provide: MembershipService, useValue: membership },
        { provide: CityService, useValue: { search: jest.fn().mockReturnValue(of([])) } },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ slug: 'rheinfeuer' })) } },
      ],
    });
    const fixture = TestBed.createComponent(TeamSettingsComponent);
    fixture.detectChanges();
    return fixture;
  }

  function query(fixture: ComponentFixture<TeamSettingsComponent>, testId: string): HTMLElement | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
  }

  function queryAll(fixture: ComponentFixture<TeamSettingsComponent>, testId: string): HTMLElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll(`[data-testid="${testId}"]`));
  }

  function type(fixture: ComponentFixture<TeamSettingsComponent>, el: HTMLElement | null, value: string): void {
    const input = el as HTMLInputElement | HTMLTextAreaElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function save(fixture: ComponentFixture<TeamSettingsComponent>): void {
    (query(fixture, 'details-save') as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  function refuse(status: number, error: unknown): void {
    teams.updateDetails.mockReturnValue(throwError(() => new HttpErrorResponse({ status, error })));
  }

  it('is the first section, prefilled, for an admin — and absent for a member', () => {
    const fixture = render();
    const section = query(fixture, 'team-details');
    expect(section).not.toBeNull();
    // First on the page: nothing admin-only comes before it.
    expect(section?.compareDocumentPosition(query(fixture, 'team-logo-pick') as Node)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect((query(fixture, 'details-name') as HTMLInputElement).value).toBe('Rheinfeuer');
    expect((query(fixture, 'details-description') as HTMLTextAreaElement).value).toBe('Wir trainieren dienstags.');
    expect((query(fixture, 'details-link-url') as HTMLInputElement).value).toBe('https://rheinfeuer.de/');
    expect(query(fixture, 'details-city')).not.toBeNull();

    expect(query(render({ ...DETAIL, myRole: 'Member' }), 'team-details')).toBeNull();
  });

  it('sends the whole section, resending the current city when it was not touched', () => {
    const fixture = render();
    type(fixture, query(fixture, 'details-name'), '  Rheinfeuer Köln ');

    save(fixture);

    expect(teams.updateDetails).toHaveBeenCalledWith('rheinfeuer', {
      name: 'Rheinfeuer Köln',
      type: 'CityTeam',
      location: { cityExternalId: 'TEST:berlin', name: 'Berlin' },
      description: 'Wir trainieren dienstags.',
      links: [{ label: 'Website', url: 'https://rheinfeuer.de/' }],
    });
  });

  it('sends no city once the team becomes a Mixteam, and says why there is none', () => {
    const fixture = render();
    (query(fixture, 'details-type-mix') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(query(fixture, 'details-city')).toBeNull();
    expect(query(fixture, 'details-mixteam-note')).not.toBeNull();

    save(fixture);

    expect(teams.updateDetails).toHaveBeenCalledWith('rheinfeuer', expect.objectContaining({ type: 'Mixteam', location: null }));
  });

  it('does not send a City team without a city, or a name too short to be one', () => {
    const fixture = render({ ...DETAIL, type: 'Mixteam', location: null });
    (query(fixture, 'details-type-city') as HTMLButtonElement).click();
    fixture.detectChanges();

    save(fixture);
    expect(query(fixture, 'details-error')?.textContent).toContain('A city team needs a city');

    type(fixture, query(fixture, 'details-name'), 'X');
    save(fixture);
    expect(query(fixture, 'details-error')?.textContent).toContain('2–50 characters');

    expect(teams.updateDetails).not.toHaveBeenCalled();
  });

  it('follows a successful save: the page, the cached memberships and a saved line', () => {
    const fixture = render();
    type(fixture, query(fixture, 'details-name'), 'Rheinfeuer Neu');

    save(fixture);

    expect(membership.load).toHaveBeenCalled();
    expect(query(fixture, 'details-saved')).not.toBeNull();
    expect((query(fixture, 'details-name') as HTMLInputElement).value).toBe('Rheinfeuer Neu');

    // The next edit retires the saved line.
    type(fixture, query(fixture, 'details-description'), 'Neu.');
    expect(query(fixture, 'details-saved')).toBeNull();
  });

  it('shows a refusal in the reader’s words, never the server’s, and marks the link it is about', () => {
    const fixture = render({ ...DETAIL, links: [{ label: 'Website', url: 'https://rheinfeuer.de/' }, { label: 'Blog', url: 'http://blog.example' }] });
    refuse(400, { code: 'linkUrlInvalid', link: 1, detail: 'SERVER ENGLISH' });

    save(fixture);

    const error = query(fixture, 'details-error')?.textContent ?? '';
    expect(error).toContain('Link 2 needs a secure web address');
    expect(error).not.toContain('SERVER ENGLISH');
    const urls = queryAll(fixture, 'details-link-url');
    expect(urls[1].getAttribute('aria-invalid')).toBe('true');
    expect(urls[0].getAttribute('aria-invalid')).toBeNull();
    expect(membership.load).not.toHaveBeenCalled();
  });

  it('falls back to a general message for a refusal without a code', () => {
    const fixture = render();
    refuse(500, { detail: 'SERVER ENGLISH' });

    save(fixture);

    expect(query(fixture, 'details-error')?.textContent).toContain("We couldn't save that just now");
  });

  it('reloads the page when the admin is no longer an admin, with a notice outside the section', () => {
    const fixture = render();
    teams.getDetail.mockReturnValue(of({ ...DETAIL, myRole: 'Member' }));
    refuse(403, { detail: 'Only admins can change the team’s details.' });

    save(fixture);

    expect(teams.getDetail).toHaveBeenCalledTimes(2);
    expect(query(fixture, 'team-details')).toBeNull();
    expect(query(fixture, 'settings-notice')?.textContent).toContain('not one anymore');
  });

  it('adds link rows up to five, drops empty rows, and removes a row', () => {
    const fixture = render({ ...DETAIL, links: [] });

    for (let i = 0; i < 5; i++) {
      (query(fixture, 'details-add-link') as HTMLButtonElement).click();
      fixture.detectChanges();
    }
    expect(queryAll(fixture, 'details-link')).toHaveLength(5);
    expect((query(fixture, 'details-add-link') as HTMLButtonElement).disabled).toBe(true);
    expect(query(fixture, 'details-links-max')).not.toBeNull();

    type(fixture, queryAll(fixture, 'details-link-label')[1], 'Instagram');
    type(fixture, queryAll(fixture, 'details-link-url')[1], 'instagram.com/rheinfeuer');
    (queryAll(fixture, 'details-link-remove')[4] as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(queryAll(fixture, 'details-link')).toHaveLength(4);

    save(fixture);

    expect(teams.updateDetails).toHaveBeenCalledWith('rheinfeuer', expect.objectContaining({
      links: [{ label: 'Instagram', url: 'instagram.com/rheinfeuer' }],
    }));
  });

  it('counts the description against its limit', () => {
    const fixture = render();
    type(fixture, query(fixture, 'details-description'), 'Hallo');
    expect(query(fixture, 'details-description-count')?.textContent?.trim()).toBe('5/1000');
  });
});
