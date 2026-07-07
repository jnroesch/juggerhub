import { relativeTime } from './format';

describe('relativeTime', () => {
  const now = new Date('2026-07-08T12:00:00Z');

  it('reads "just now" under a minute', () => {
    expect(relativeTime('2026-07-08T11:59:30Z', now)).toBe('just now');
  });

  it('reads minutes', () => {
    expect(relativeTime('2026-07-08T11:45:00Z', now)).toBe('15m ago');
  });

  it('reads hours', () => {
    expect(relativeTime('2026-07-08T10:00:00Z', now)).toBe('2h ago');
  });

  it('reads days under a week', () => {
    expect(relativeTime('2026-07-05T12:00:00Z', now)).toBe('3d ago');
  });

  it('falls back to a date beyond a week', () => {
    // Older than 7 days → a formatted date, not "Nd ago".
    expect(relativeTime('2026-06-01T12:00:00Z', now)).not.toMatch(/ago/);
  });
});
