import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Translation, TranslocoLoader } from '@jsverse/transloco';

/**
 * Loads translation catalogs from the statically-served `/i18n/` folder (feature 031).
 *
 * Catalogs live in `apps/web/public/i18n/` — the `@angular/build:application` build copies
 * `public/` to the site root, so they are served at `/i18n/*.json`. Transloco passes the root
 * language (`"en"`) or a scoped path (`"auth/en"`) as `path`, which maps 1:1 onto the folder
 * layout (`public/i18n/en.json`, `public/i18n/auth/en.json`).
 */
@Injectable({ providedIn: 'root' })
export class TranslocoHttpLoader implements TranslocoLoader {
  private readonly http = inject(HttpClient);

  getTranslation(path: string) {
    return this.http.get<Translation>(`/i18n/${path}.json`);
  }
}
