import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { AccountComponent } from './account.component';
import { AuthService } from '../../core/services/auth.service';
import { translocoTestingModule } from '../../../testing/transloco-testing';

/**
 * The signed-in home for the legal links (feature 036, owner decision 2026-08-01). The shell
 * footer only renders for signed-out visitors, so if these links were ever dropped from here a
 * member would have no in-app route to the privacy policy at all.
 */
describe('AccountComponent — legal links', () => {
  let fixture: ComponentFixture<AccountComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            currentUser: signal({ handle: 'alex', email: 'alex@example.com' }),
            // The language switcher on this page reads the session state too.
            userState: signal({ handle: 'alex', email: 'alex@example.com' }),
          },
        },
      ],
    });

    fixture = TestBed.createComponent(AccountComponent);
    fixture.detectChanges();
  });

  function link(which: 'privacy' | 'imprint'): HTMLAnchorElement | null {
    return fixture.nativeElement.querySelector(`[data-testid="legal-link-${which}"]`);
  }

  it('links to the privacy policy', () => {
    expect(link('privacy')?.getAttribute('href')).toBe('/privacy');
  });

  it('links to the imprint', () => {
    expect(link('imprint')?.getAttribute('href')).toBe('/imprint');
  });
});
