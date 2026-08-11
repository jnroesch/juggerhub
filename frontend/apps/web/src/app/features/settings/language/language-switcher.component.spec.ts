import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../../../core/services/auth.service';
import { LanguageService } from '../../../core/i18n/language.service';
import { translocoTestingModule } from '../../../../testing/transloco-testing';
import { LanguageSwitcherComponent } from './language-switcher.component';

describe('LanguageSwitcherComponent', () => {
  const userState = signal<{ preferredLanguage: string | null } | null | undefined>(null);

  const fakeAuth = {
    userState,
    isAuthenticated: () => false,
    setPreferredLanguage: jest.fn(),
  };

  beforeEach(() => {
    localStorage.clear();
    userState.set(null);

    TestBed.configureTestingModule({
      imports: [translocoTestingModule(), LanguageSwitcherComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: fakeAuth },
      ],
    });
  });

  it('shows the resolved language as the selected option on first render', () => {
    localStorage.setItem('jh.lang', 'de');

    const fixture = TestBed.createComponent(LanguageSwitcherComponent);
    fixture.detectChanges();

    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    expect(select.value).toBe('de');
    expect(select.options[select.selectedIndex].textContent?.trim()).toBe('Deutsch');
  });

  it('follows a language resolved after the first render', () => {
    const fixture = TestBed.createComponent(LanguageSwitcherComponent);
    fixture.detectChanges();
    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    expect(select.value).toBe('en');

    TestBed.inject(LanguageService).select('de');
    fixture.detectChanges();

    expect(select.value).toBe('de');
  });

  it('resolves from the browser language when nothing is stored', () => {
    const spy = jest.spyOn(navigator, 'language', 'get').mockReturnValue('de-DE');
    try {
      const fixture = TestBed.createComponent(LanguageSwitcherComponent);
      fixture.detectChanges();

      const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
      expect(select.value).toBe('de');
    } finally {
      spy.mockRestore();
    }
  });
});
