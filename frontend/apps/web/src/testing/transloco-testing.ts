import { TranslocoTestingModule, TranslocoTestingOptions, Translation } from '@jsverse/transloco';

/**
 * Test harness for components/services that use Transloco (feature 031). Provides an in-memory
 * catalog set with no HTTP, English default + fallback, matching the app's config. Pass real
 * catalog fragments per language to assert rendered translations; omit to get empty catalogs
 * (keys then render via the English fallback / key).
 *
 * Usage:
 *   TestBed.configureTestingModule({
 *     imports: [translocoTestingModule({ en: { common: { save: 'Save' } } })],
 *   });
 */
export function translocoTestingModule(
  langs: Record<string, Translation> = { en: {}, de: {}, es: {} },
  options: TranslocoTestingOptions = {},
) {
  return TranslocoTestingModule.forRoot({
    langs,
    translocoConfig: {
      availableLangs: ['en', 'de', 'es'],
      defaultLang: 'en',
      fallbackLang: 'en',
      reRenderOnLangChange: true,
    },
    preloadLangs: true,
    ...options,
  });
}
