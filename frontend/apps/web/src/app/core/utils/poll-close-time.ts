/**
 * A poll's close time moves between two forms (feature 062):
 *
 * - what a `datetime-local` input holds — `yyyy-MM-ddTHH:mm`, a wall-clock time with no zone,
 *   which the browser means in the viewer's own zone (FR-004);
 * - what the server stores and returns — an ISO instant in UTC.
 *
 * Unlike an event's start (a wall-clock time at the venue), a poll closes at one instant for
 * everyone, so the conversion happens here, in the viewer's zone, and the server never sees a
 * zone-less string.
 */

/** A `datetime-local` value to the ISO instant the server expects; empty or unparseable ⇒ null. */
export function toUtcInstant(local: string | null | undefined): string | null {
  if (!local || !local.trim()) return null;
  const date = new Date(local);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

/** An ISO instant to the value a `datetime-local` input shows, in the viewer's zone; null ⇒ ''. */
export function toLocalInputValue(iso: string | null | undefined): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
