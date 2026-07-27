import { HttpInterceptorFn } from '@angular/common/http';

/**
 * Stamps `Accept-Language` on API requests with the app's active language (feature 031, FR-012a).
 *
 * The value is read from `document.documentElement.lang`, which `LanguageService` keeps in sync with
 * the effective (post-override) language — so this carries the user's real choice, not merely the
 * browser default. Reading the DOM (rather than injecting `LanguageService`) keeps this interceptor
 * dependency-free and avoids a DI cycle with `HttpClient`.
 *
 * Only API calls are stamped; static catalog/asset requests are left untouched. Header-only and
 * non-short-circuiting, so it does not affect the auth/retry ordering contract (feature 028).
 */
export const languageInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.includes('/api/')) {
    return next(req);
  }

  const lang = (typeof document !== 'undefined' && document.documentElement.lang) || 'en';
  return next(req.clone({ setHeaders: { 'Accept-Language': lang } }));
};
