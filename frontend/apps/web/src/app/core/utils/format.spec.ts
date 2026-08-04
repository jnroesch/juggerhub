import { relativeTime } from './format';

/**
 * `relativeTime` used to return hard-coded English ("just now", "15m ago"), which rendered
 * untranslated next to German copy on the dashboard and in the alerts inbox. It now takes the
 * app's active language and delegates the wording to `Intl.RelativeTimeFormat`, so each bucket is
 * asserted in more than one language — an English-only suite passed against the old behaviour.
 */
describe('relativeTime', () => {
  const now = new Date('2026-07-08T12:00:00Z');

  it('reads "now" under a minute', () => {
    expect(relativeTime('2026-07-08T11:59:30Z', 'en', now)).toBe('now');
    expect(relativeTime('2026-07-08T11:59:30Z', 'de', now)).toBe('jetzt');
  });

  it('reads minutes', () => {
    expect(relativeTime('2026-07-08T11:45:00Z', 'en', now)).toBe('15 min. ago');
    expect(relativeTime('2026-07-08T11:45:00Z', 'de', now)).toBe('vor 15 Min.');
  });

  it('reads hours', () => {
    expect(relativeTime('2026-07-08T10:00:00Z', 'en', now)).toBe('2 hr. ago');
    expect(relativeTime('2026-07-08T10:00:00Z', 'de', now)).toBe('vor 2 Std.');
  });

  it('reads days under a week', () => {
    expect(relativeTime('2026-07-05T12:00:00Z', 'en', now)).toBe('3 days ago');
    expect(relativeTime('2026-07-05T12:00:00Z', 'es', now)).toBe('hace 3 d');
  });

  it('names yesterday rather than counting it', () => {
    expect(relativeTime('2026-07-07T11:00:00Z', 'en', now)).toBe('yesterday');
    expect(relativeTime('2026-07-07T11:00:00Z', 'de', now)).toBe('gestern');
  });

  it('falls back to a date beyond a week, in the app language', () => {
    // Older than 7 days → a formatted date, not a relative count.
    expect(relativeTime('2026-06-01T12:00:00Z', 'en', now)).not.toMatch(/ago/);
    expect(relativeTime('2026-06-01T12:00:00Z', 'de', now)).toContain('Juni');
  });
});
