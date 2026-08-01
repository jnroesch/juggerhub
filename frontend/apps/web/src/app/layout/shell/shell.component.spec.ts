import { ComponentFixture, TestBed } from '@angular/core/testing';
import { computed, signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ShellComponent } from './shell.component';
import { AuthService } from '../../core/services/auth.service';
import { MembershipService } from '../../core/services/membership.service';
import { translocoTestingModule } from '../../../testing/transloco-testing';

/**
 * Where the legal links live (feature 036, owner decision 2026-08-01).
 *
 * A signed-out visitor gets them on every screen, because they are the reader a privacy policy
 * exists for and the one still deciding whether to hand over an email address. A signed-in member
 * has already made that call, so carrying the links on every screen is clutter — for them the same
 * links sit on the account page instead (see `account.component.spec.ts`).
 */
describe('ShellComponent — legal footer placement', () => {
  const user = signal<unknown>(undefined);

  function create(): ComponentFixture<ShellComponent> {
    const fixture = TestBed.createComponent(ShellComponent);
    fixture.detectChanges();
    return fixture;
  }

  function footer(fixture: ComponentFixture<ShellComponent>): HTMLElement | null {
    return fixture.nativeElement.querySelector('[data-testid="app-footer"]');
  }

  beforeEach(() => {
    user.set(undefined);

    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            userState: user,
            currentUser: computed(() => user() ?? null),
            isAuthenticated: computed(() => !!user()),
            isAdmin: computed(() => false),
            loadSession: () => of(null),
          },
        },
        {
          provide: MembershipService,
          useValue: {
            load: () => undefined,
            teams: signal([]),
            loaded: signal(true),
            hasTeam: signal(false),
            // top-nav / bottom-nav render on the signed-in path and read this.
            myTeamTarget: signal('/my-team'),
          },
        },
      ],
    });
  });

  it('shows the legal footer to a signed-out visitor', () => {
    user.set(null);
    const fixture = create();

    expect(footer(fixture)).not.toBeNull();
    expect(footer(fixture)!.querySelector('[data-testid="legal-link-privacy"]')).not.toBeNull();
    expect(footer(fixture)!.querySelector('[data-testid="legal-link-imprint"]')).not.toBeNull();
  });

  it('does not show it to a signed-in member', () => {
    user.set({ handle: 'alex', email: 'alex@example.com' });
    const fixture = create();

    expect(footer(fixture)).toBeNull();
  });

  /**
   * `anonymous()` is "probed and null", not "not yet probed" — undefined keeps the full nav to
   * avoid a flash on load. The footer must follow the same rule rather than flashing in.
   */
  it('does not show it before the session has been probed', () => {
    user.set(undefined);
    const fixture = create();

    expect(footer(fixture)).toBeNull();
  });
});
