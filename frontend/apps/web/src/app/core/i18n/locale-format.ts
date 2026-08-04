import { Signal, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoService } from '@jsverse/transloco';
import { dayOfMonth, relativeTime, shortDate, shortMonth, shortWeekday, timeHm } from '../utils/format';
import { DEFAULT_LANGUAGE, LANG_TO_LOCALE, isSupportedLanguage } from './supported-languages';

/**
 * The `format.ts` helpers bound to the app's active language (feature 031).
 *
 * Those helpers are pure and take a locale they cannot obtain themselves; this is the one place
 * that supplies it. Each returned closure reads a signal, so calling it inside a `computed()` or a
 * template binding — which is every call site — also re-renders the string when the viewer switches
 * language, matching the copy around it.
 *
 * The locale comes from `LANG_TO_LOCALE`, the same mapping `provideTranslocoLocale` is configured
 * with in `app.config.ts` (FR-009), so raw `Intl` calls and the `translocoDate` pipe cannot drift
 * apart. Read from that constant rather than from `TranslocoLocaleService`: the service needs
 * `provideTranslocoLocale()` in the injector, which would have to be added to every TestBed that
 * mounts a component using a date.
 *
 * Must be called in an injection context (field initializer / constructor).
 */

/** The active BCP-47 locale, e.g. `de-DE`. Re-emits on a language switch. */
export function injectLocale(): Signal<string> {
  const transloco = inject(TranslocoService);
  const lang = toSignal(transloco.langChanges$, { initialValue: transloco.getActiveLang() });
  return computed(() => {
    const active = lang();
    return LANG_TO_LOCALE[isSupportedLanguage(active) ? active : DEFAULT_LANGUAGE];
  });
}

/**
 * A `relativeTime` bound to the active locale.
 *
 *   private readonly rel = injectRelativeTime();
 *   protected readonly time = computed(() => this.rel(this.item().createdDate));
 */
export function injectRelativeTime(): (iso: string) => string {
  const locale = injectLocale();
  return (iso: string) => relativeTime(iso, locale());
}

/**
 * The date/time helpers bound to the active locale. Accepts both full ISO timestamps and date-only
 * (`YYYY-MM-DD`) values, so training sessions no longer need their own local-midnight copies.
 *
 *   private readonly fmt = injectDateFormats();
 *   protected readonly weekday = computed(() => this.fmt.shortWeekday(this.item().startsAt));
 */
export function injectDateFormats(): {
  shortWeekday: (value: string) => string;
  dayOfMonth: (value: string) => string;
  shortMonth: (value: string) => string;
  timeHm: (value: string) => string;
  shortDate: (value: string) => string;
} {
  const locale = injectLocale();
  return {
    shortWeekday: (value) => shortWeekday(value, locale()),
    dayOfMonth, // locale-independent — see format.ts
    shortMonth: (value) => shortMonth(value, locale()),
    timeHm: (value) => timeHm(value, locale()),
    shortDate: (value) => shortDate(value, locale()),
  };
}
