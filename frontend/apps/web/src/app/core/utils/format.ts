/** Small, pure date/number formatting helpers for the dashboard (feature 008). */

/** Short weekday for a date chip, e.g. "Sat". */
export function shortWeekday(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { weekday: 'short' });
}

/** Day of the month for a date chip, e.g. "12". */
export function dayOfMonth(iso: string): string {
  return String(new Date(iso).getDate());
}

/** Short month for a date chip, e.g. "Jul". */
export function shortMonth(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { month: 'short' });
}

/** 24-hour time, e.g. "14:00". */
export function timeHm(iso: string): string {
  return new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit', hour12: false });
}

/** A compact "Sat 12 Jul" style date, e.g. for tournament/fixture rows. */
export function shortDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short' });
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
