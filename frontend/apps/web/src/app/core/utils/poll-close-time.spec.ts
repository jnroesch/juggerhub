import { toLocalInputValue, toUtcInstant } from './poll-close-time';

describe('poll close time', () => {
  it('turns an empty input into no close time', () => {
    expect(toUtcInstant('')).toBeNull();
    expect(toUtcInstant('   ')).toBeNull();
    expect(toUtcInstant(null)).toBeNull();
    expect(toUtcInstant('not a date')).toBeNull();
  });

  it('reads the input in the viewer’s own zone and sends an instant', () => {
    const local = '2026-10-04T20:00';
    const expected = new Date(2026, 9, 4, 20, 0).toISOString();
    expect(toUtcInstant(local)).toBe(expected);
    expect(toUtcInstant(local)).toMatch(/Z$/);
  });

  it('shows an instant as the viewer’s wall-clock time', () => {
    const instant = new Date(2026, 9, 4, 20, 5).toISOString();
    expect(toLocalInputValue(instant)).toBe('2026-10-04T20:05');
  });

  it('round-trips', () => {
    const local = '2027-01-15T07:30';
    expect(toLocalInputValue(toUtcInstant(local))).toBe(local);
  });

  it('shows nothing for no close time', () => {
    expect(toLocalInputValue(null)).toBe('');
    expect(toLocalInputValue('garbage')).toBe('');
  });
});
