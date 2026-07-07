/** Small, pure date/number formatting helpers for the dashboard (feature 008). */

/** Short weekday for a date chip, e.g. "Sat". */
export function shortWeekday(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { weekday: 'short' });
}

/** Day of the month for a date chip, e.g. "12". */
export function dayOfMonth(iso: string): string {
  return String(new Date(iso).getDate());
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
 * A coarse relative time, e.g. "just now", "2h ago", "3d ago", or a date for older items.
 * Timezone-agnostic (uses the viewer's locale/clock).
 */
export function relativeTime(iso: string, now: Date = new Date()): string {
  const then = new Date(iso).getTime();
  const diffMs = now.getTime() - then;
  const mins = Math.floor(diffMs / 60000);
  if (mins < 1) {
    return 'just now';
  }
  if (mins < 60) {
    return `${mins}m ago`;
  }
  const hours = Math.floor(mins / 60);
  if (hours < 24) {
    return `${hours}h ago`;
  }
  const days = Math.floor(hours / 24);
  if (days < 7) {
    return `${days}d ago`;
  }
  return new Date(iso).toLocaleDateString(undefined, { day: 'numeric', month: 'short' });
}
