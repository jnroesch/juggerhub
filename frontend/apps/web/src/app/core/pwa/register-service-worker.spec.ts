import { registerServiceWorker } from './register-service-worker';

/**
 * No TestBed: the helper is a plain function whose whole contract is "one register call with
 * these arguments, after load, or none at all — and never a visible failure" (FR-006, FR-008).
 */
type LoadListener = () => void;

function fakeWindow(readyState: DocumentReadyState): { win: Window; fireLoad: () => void } {
  const listeners: LoadListener[] = [];
  const win = {
    document: { readyState },
    addEventListener: (type: string, listener: LoadListener) => {
      if (type === 'load') listeners.push(listener);
    },
  } as unknown as Window;
  return { win, fireLoad: () => listeners.splice(0).forEach((l) => l()) };
}

function fakeNavigator(register: jest.Mock): Navigator {
  return { serviceWorker: { register } } as unknown as Navigator;
}

describe('registerServiceWorker', () => {
  it('does nothing in a browser without service workers', () => {
    const register = jest.fn();
    const nav = {} as Navigator;
    const { win } = fakeWindow('complete');

    registerServiceWorker(nav, win);

    expect(register).not.toHaveBeenCalled();
  });

  it('registers /sw.js at root scope, bypassing the HTTP cache, once the document is complete', () => {
    const register = jest.fn().mockResolvedValue({});
    const { win } = fakeWindow('complete');

    registerServiceWorker(fakeNavigator(register), win);

    expect(register).toHaveBeenCalledTimes(1);
    expect(register).toHaveBeenCalledWith('/sw.js', { scope: '/', updateViaCache: 'none' });
  });

  it('waits for load when the document is still loading, then registers exactly once', () => {
    const register = jest.fn().mockResolvedValue({});
    const { win, fireLoad } = fakeWindow('loading');

    registerServiceWorker(fakeNavigator(register), win);
    expect(register).not.toHaveBeenCalled();

    fireLoad();
    fireLoad();
    expect(register).toHaveBeenCalledTimes(1);
    expect(register).toHaveBeenCalledWith('/sw.js', { scope: '/', updateViaCache: 'none' });
  });

  it('swallows a failed registration — no throw, no unhandled rejection (FR-008)', async () => {
    const unhandled: unknown[] = [];
    const onUnhandled = (reason: unknown): void => {
      unhandled.push(reason);
    };
    process.on('unhandledRejection', onUnhandled);
    try {
      const register = jest.fn().mockRejectedValue(new Error('refused'));
      const { win } = fakeWindow('complete');

      expect(() => registerServiceWorker(fakeNavigator(register), win)).not.toThrow();

      // Let the rejection propagate through the helper's catch.
      await new Promise((resolve) => setTimeout(resolve, 0));
      expect(register).toHaveBeenCalledTimes(1);
      expect(unhandled).toEqual([]);
    } finally {
      process.off('unhandledRejection', onUnhandled);
    }
  });
});
