import { IndefiniteHubRetryPolicy } from './hub-reconnect.policy';

/**
 * The property that matters (feature 028, FR-012): this policy NEVER returns null. SignalR treats
 * a null delay as "stop reconnecting forever", which is exactly the silent death the default
 * schedule causes after ~42 seconds.
 */
describe('IndefiniteHubRetryPolicy', () => {
  const policy = new IndefiniteHubRetryPolicy();

  function delayAfter(previousRetryCount: number): number {
    return policy.nextRetryDelayInMilliseconds({
      previousRetryCount,
      elapsedMilliseconds: previousRetryCount * 1_000,
      retryReason: new Error('disconnected'),
    });
  }

  it('reconnects immediately on the first attempt', () => {
    expect(delayAfter(0)).toBe(0);
  });

  it('never gives up, even after a very long outage', () => {
    // The default schedule would have returned null (stop) long before attempt 100.
    for (const attempt of [1, 5, 20, 100, 5_000]) {
      const delay = delayAfter(attempt);
      expect(delay).not.toBeNull();
      expect(Number.isFinite(delay)).toBe(true);
    }
  });

  it('backs off as attempts accumulate', () => {
    jest.spyOn(Math, 'random').mockReturnValue(0.5);

    expect(delayAfter(2)).toBeGreaterThan(delayAfter(1));
    expect(delayAfter(4)).toBeGreaterThan(delayAfter(2));

    jest.restoreAllMocks();
  });

  it('caps the delay so a long outage settles into a steady retry', () => {
    for (const attempt of [10, 50, 1_000]) {
      expect(delayAfter(attempt)).toBeLessThanOrEqual(60_000);
    }
  });

  it('jitters, so clients stranded by one deploy do not stampede together', () => {
    const delays = new Set(Array.from({ length: 25 }, () => delayAfter(6)));

    expect(delays.size).toBeGreaterThan(1);
  });
});
