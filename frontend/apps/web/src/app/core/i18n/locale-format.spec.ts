import { Injector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { translocoTestingModule } from '../../../testing/transloco-testing';
import { injectDateFormats, injectLocale, injectRelativeTime } from './locale-format';
import { LANG_TO_LOCALE } from './supported-languages';

/**
 * These bind the pure `format.ts` helpers to the viewer's chosen language. The wiring is the whole
 * point: the helpers used to receive `undefined` (the browser's locale) and, before that, English
 * hard-coded into the strings themselves.
 */
describe('locale-format', () => {
  let injector: Injector;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [translocoTestingModule()] });
    injector = TestBed.inject(Injector);
  });

  const setLang = (lang: string) => TestBed.inject(TranslocoService).setActiveLang(lang);

  it('resolves the locale through the same mapping app.config gives provideTranslocoLocale', () => {
    const locale = runInInjectionContext(injector, () => injectLocale());

    setLang('de');
    expect(locale()).toBe(LANG_TO_LOCALE.de);

    setLang('es');
    expect(locale()).toBe(LANG_TO_LOCALE.es);
  });

  it('falls back to the default language for an unsupported active lang', () => {
    const locale = runInInjectionContext(injector, () => injectLocale());

    setLang('fr');

    // Never `undefined` — that is the browser locale, which is exactly what this replaced.
    expect(locale()).toBe(LANG_TO_LOCALE.en);
  });

  it('formats dates and relative times in the active language', () => {
    const fmt = runInInjectionContext(injector, () => injectDateFormats());
    const rel = runInInjectionContext(injector, () => injectRelativeTime());
    const twoHoursAgo = new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString();

    setLang('en');
    expect(fmt.shortWeekday('2026-07-11')).toBe('Sat');
    expect(rel(twoHoursAgo)).toBe('2 hr. ago');

    setLang('de');
    expect(fmt.shortWeekday('2026-07-11')).toBe('Sa');
    expect(rel(twoHoursAgo)).toBe('vor 2 Std.');
  });
});
