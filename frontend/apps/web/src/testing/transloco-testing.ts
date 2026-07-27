import { TranslocoTestingModule, TranslocoTestingOptions, Translation } from '@jsverse/transloco';
import enCatalog from '../../public/i18n/en.json';

/**
 * Test harness for components/services that use Transloco (feature 031). Provides an in-memory
 * catalog set with no HTTP, English default + fallback, matching the app's config.
 *
 * By default it preloads the **real English root catalog**, so a component whose template was
 * converted to translation keys renders the same English strings in tests as at runtime — existing
 * text assertions keep passing after extraction. Pass explicit `langs` to override.
 *
 * Usage:
 *   TestBed.configureTestingModule({ imports: [translocoTestingModule()] });
 */
export function translocoTestingModule(
  langs: Record<string, Translation> = { en: enCatalog as Translation },
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
