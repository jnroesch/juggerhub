/**
 * Registers the product's push-only service worker (feature 054) for the whole origin.
 *
 * Called from `main.ts` once `bootstrapApplication` has resolved, and it waits for `load` on top
 * of that, so first paint never waits on it (FR-008). ONE attempt per visit, no retry, no timeout,
 * no logging: the failure is invisible by requirement (a browser without workers, a private
 * window that refuses them, a fetch that fails — the product works exactly as before in each
 * case). Principle VII is not engaged here — this is the browser's own same-origin static-file
 * request, not an app network call — and a retry/timeout/breaker wrapper around it is
 * review-rejectable.
 *
 * `updateViaCache: 'none'` makes the browser bypass its HTTP cache when it checks the script for
 * an update; the nginx `Cache-Control: no-cache` on `/sw.js` is the other half of FR-009.
 *
 * `nav` and `win` are parameters only so the spec can pass fakes — jsdom has no
 * `navigator.serviceWorker` at all.
 */
export function registerServiceWorker(nav: Navigator = navigator, win: Window = window): void {
  if (!('serviceWorker' in nav)) {
    return;
  }

  const register = (): void => {
    void nav.serviceWorker.register('/sw.js', { scope: '/', updateViaCache: 'none' }).catch(() => undefined);
  };

  if (win.document.readyState === 'complete') {
    register();
  } else {
    win.addEventListener('load', register, { once: true });
  }
}
