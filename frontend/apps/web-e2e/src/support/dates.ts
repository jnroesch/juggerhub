/**
 * Dates the specs type into the app's date inputs, always relative to the day the suite runs.
 *
 * Hard-coded calendar dates rot. The event specs carried `2026-10-01`, and from the afternoon of
 * that day every run went red on every branch, including diffs that touched nothing the suite
 * exercises: an event whose end has passed offers no Join action (feature 050), so the second
 * user's sign-up step waited for a button the page no longer renders. A relative date cannot
 * expire.
 *
 * Local time on purpose: the browser interprets `<input type="date">` and `datetime-local` values
 * in its own time zone, and the Playwright process and the browser share one clock here (the same
 * container, or the same developer machine).
 */
export function daysFromNow(days: number): Date {
  const d = new Date();
  d.setDate(d.getDate() + days);
  return d;
}

const two = (n: number) => String(n).padStart(2, '0');

/** `YYYY-MM-DD` for an `<input type="date">`, `days` days from today. */
export function dateInput(days: number): string {
  const d = daysFromNow(days);
  return `${d.getFullYear()}-${two(d.getMonth() + 1)}-${two(d.getDate())}`;
}

/** `YYYY-MM-DDTHH:mm` for an `<input type="datetime-local">`, `days` days from today at `time` (`HH:mm`). */
export function dateTimeInput(days: number, time: string): string {
  return `${dateInput(days)}T${time}`;
}
