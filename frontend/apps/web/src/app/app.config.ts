import {
  ApplicationConfig,
  ErrorHandler,
  isDevMode,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import {
  provideHttpClient,
  withFetch,
  withInterceptors,
} from '@angular/common/http';
import { provideTransloco } from '@jsverse/transloco';
import { provideTranslocoLocale } from '@jsverse/transloco-locale';
import { appRoutes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { retryInterceptor } from './core/interceptors/retry.interceptor';
import { languageInterceptor } from './core/interceptors/language.interceptor';
import { ChunkLoadErrorHandler } from './core/chunk-load-error.handler';
import { TranslocoHttpLoader } from './core/i18n/transloco-http.loader';
import { DEFAULT_LANGUAGE, LANG_TO_LOCALE, SUPPORTED_LANGUAGES } from './core/i18n/supported-languages';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Self-heal a stale lazy-chunk after a frontend redeploy (reload once, then give up) so an open
    // tab isn't stranded on "Failed to fetch dynamically imported module".
    { provide: ErrorHandler, useClass: ChunkLoadErrorHandler },
    // withComponentInputBinding lets route params bind straight to component inputs — chat's
    // /chat/:conversationId feeds ChatConversationComponent's `conversationId` input this way,
    // so the open conversation is driven by the URL rather than a manual subscription.
    // anchorScrolling makes the router actually scroll to the element named by a URL fragment
    // (feature 036 — the privacy policy's table of contents). It only takes effect on navigations
    // that carry a fragment, and nothing else in the app uses one, so it changes no existing
    // behaviour. Without it the fragment lands in the URL and the page stays put.
    provideRouter(
      appRoutes,
      withComponentInputBinding(),
      withInMemoryScrolling({ anchorScrolling: 'enabled' }),
    ),
    // All API calls are relative ("/api/v1/...") and same-origin via the nginx
    // proxy, so httpOnly auth cookies stay first-party. The auth interceptor
    // attaches credentials and routes 401s toward sign-in.
    //
    // ORDER IS A CONTRACT, not a style choice (feature 028). Angular chains interceptors in array
    // order, so retryInterceptor is the INNER of the two:
    //   - a 401 is not retryable, so it passes through retry untouched and the auth interceptor
    //     handles it exactly once — refresh once, retry once;
    //   - a transient fault is absorbed by retry and never reaches the auth interceptor.
    // Reversing them puts retry OUTSIDE the refresh, letting one expired session drive several
    // refresh cycles. Do not swap these.
    // languageInterceptor is appended last (feature 031): it only adds an Accept-Language header and
    // never short-circuits, so the auth→retry ordering contract (feature 028) is preserved.
    provideHttpClient(withFetch(), withInterceptors([authInterceptor, retryInterceptor, languageInterceptor])),
    // Runtime i18n (feature 031). Transloco switches the active catalog in place — no per-language
    // build, no reload (FR-004). English is the source AND the fallback: `fallbackLang` +
    // `useFallbackTranslation` means any key missing from de/es resolves to the English text rather
    // than rendering blank or a raw key (FR-008/FR-018).
    provideTransloco({
      config: {
        availableLangs: [...SUPPORTED_LANGUAGES],
        defaultLang: DEFAULT_LANGUAGE,
        fallbackLang: DEFAULT_LANGUAGE,
        reRenderOnLangChange: true,
        missingHandler: { useFallbackTranslation: true, logMissingKey: isDevMode() },
        prodMode: !isDevMode(),
      },
      loader: TranslocoHttpLoader,
    }),
    // Locale-aware date/number formatting that follows the active language at runtime (FR-009),
    // unlike Angular's LOCALE_ID which is fixed at bootstrap.
    provideTranslocoLocale({
      langToLocaleMapping: LANG_TO_LOCALE,
    }),
  ],
};
