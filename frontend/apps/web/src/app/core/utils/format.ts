/**
 * Small, pure date/number formatting helpers for the dashboard (feature 008).
 *
 * Every helper takes an explicit `locale` and none of them defaults it. These used to pass
 * `undefined` to `toLocaleDateString`, which formats against the **browser's** locale — so a viewer
 * who set the app to German in a browser configured for English got German copy around English date
 * chips. The app language is the viewer's stated choice and is what should win; a default parameter
 * is how the browser locale got in, so there isn't one. Prefer `injectDateFormats` over calling
 * these directly, so the strings also re-render when the viewer switches language.
 */

/**
 * A date-only string (`YYYY-MM-DD`) parses as UTC midnight, which renders as the *previous* day for
 * any viewer west of Greenwich. Pin it to local midnight; full ISO timestamps pass through.
 */
function toDate(value: string): Date {
  return /^\d{4}-\d{2}-\d{2}$/.test(value) ? new Date(`${value}T00:00:00`) : new Date(value);
}

/** Short weekday for a date chip, e.g. "Sat" / "Sa". */
export function shortWeekday(value: string, locale: string): string {
  return toDate(value).toLocaleDateString(locale, { weekday: 'short' });
}

/**
 * Day of the month for a date chip, e.g. "12". Takes no locale on purpose: it is a bare number, and
 * every language the app supports (en/de/es) writes it in Latin digits.
 */
export function dayOfMonth(value: string): string {
  return String(toDate(value).getDate());
}

/** Short month for a date chip, e.g. "Jul" / "Jul." */
export function shortMonth(value: string, locale: string): string {
  return toDate(value).toLocaleDateString(locale, { month: 'short' });
}

/** 24-hour time, e.g. "14:00". `hour12: false` is a product choice, not a locale one. */
export function timeHm(value: string, locale: string): string {
  return toDate(value).toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit', hour12: false });
}

/** A compact "Sat 12 Jul" style date, e.g. for training sessions and fixture rows. */
export function shortDate(value: string, locale: string): string {
  return toDate(value).toLocaleDateString(locale, { weekday: 'short', day: 'numeric', month: 'short' });
}

/**
 * A coarse relative time, e.g. "now", "2h ago", "3d ago", or a date for older items.
 * Timezone-agnostic (uses the viewer's clock).
 *
 * `locale` is REQUIRED and deliberately has no default: this used to return hard-coded English
 * ("just now", "15m ago") and rendered untranslated beside German copy. A default would let a new
 * call site silently reintroduce that. Pass the app's active language — prefer `injectRelativeTime`
 * over calling this directly, so the string also re-renders when the viewer switches language.
 *
 * `Intl.RelativeTimeFormat` supplies the wording, so there is nothing here for a catalogue to hold.
 * `numeric: 'auto'` yields "now"/"yesterday" (de "jetzt"/"gestern") instead of "in 0 seconds".
 * `style: 'short'` rather than `narrow`: narrow reproduces today's English exactly ("15m ago") but
 * abbreviates German to "vor 15 m", which doesn't read as minutes. Short costs ~3 characters of
 * English width in a `shrink-0` caption and is unambiguous in all three languages.
 */
export function relativeTime(iso: string, locale: string, now: Date = new Date()): string {
  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: 'auto', style: 'short' });
  const then = new Date(iso).getTime();
  const diffMs = now.getTime() - then;
  const mins = Math.floor(diffMs / 60000);
  if (mins < 1) {
    return rtf.format(0, 'second');
  }
  if (mins < 60) {
    return rtf.format(-mins, 'minute');
  }
  const hours = Math.floor(mins / 60);
  if (hours < 24) {
    return rtf.format(-hours, 'hour');
  }
  const days = Math.floor(hours / 24);
  if (days < 7) {
    return rtf.format(-days, 'day');
  }
  return new Date(iso).toLocaleDateString(locale, { day: 'numeric', month: 'short' });
}
