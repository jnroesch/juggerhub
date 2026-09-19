/*
 * JuggerHub's push-only service worker (features 054 and 055).
 *
 * It exists so the browser has somewhere to deliver a push message while no tab is open; on iOS
 * that is only possible for an app added to the Home Screen, and on every platform it needs a
 * registered worker. Nothing else. Deliberately:
 *
 *   - NO fetch listener. A worker with one sits between every page and the server; there is
 *     nothing to serve offline (the whole product is sign-in-only live data, feature 026), and a
 *     no-op handler adds a worker round-trip to every navigation for nothing.
 *   - NO Cache API, no IndexedDB, no storage of any kind — "no offline mode, ever" is an owner
 *     decision (spec 054, Clarification 3). The entry page is rewritten per environment at serve
 *     time (feature 033); a cached copy would freeze that on the device.
 *   - NO importScripts. Nothing is pulled in from anywhere.
 *
 * pwa-shell.spec.ts reads this file and fails the build if any of those appear. It also asserts the
 * four listeners below exist, so deleting one is a failing test rather than a silent regression.
 */

// --- Lifecycle -------------------------------------------------------------
// A changed worker takes over on the visit that fetched it, instead of waiting for every tab of
// the site to close.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));

// --- Push (feature 055) ----------------------------------------------------

/** Shown when a push arrives without a readable body. Rare, and better than showing nothing. */
const FALLBACK = { title: 'JuggerHub', body: 'You have a new notification', url: '/', tag: 'jh' };

/**
 * The server sends exactly four fields: title, body, url and tag. Anything else is ignored, and a
 * malformed payload falls back rather than throwing — a throw here would leave the browser to show
 * its own generic "This site has been updated in the background" notice.
 */
function readPayload(event) {
  if (!event.data) return FALLBACK;
  try {
    const data = event.data.json();
    return {
      title: typeof data.title === 'string' && data.title ? data.title : FALLBACK.title,
      body: typeof data.body === 'string' && data.body ? data.body : FALLBACK.body,
      // App-relative only. The server never sends anything else, and refusing here means a payload
      // that somehow carried an absolute URL still cannot send anyone off-site.
      url: typeof data.url === 'string' && data.url.startsWith('/') && !data.url.startsWith('//')
        ? data.url
        : FALLBACK.url,
      tag: typeof data.tag === 'string' && data.tag ? data.tag : FALLBACK.tag,
    };
  } catch {
    return FALLBACK;
  }
}

self.addEventListener('push', (event) => {
  const payload = readPayload(event);

  // Showing something is not optional: the subscription is userVisibleOnly, so a push that
  // displays nothing costs the site its permission in some browsers.
  event.waitUntil(
    self.registration.showNotification(payload.title, {
      body: payload.body,
      icon: '/icons/icon-192.png',
      badge: '/icons/icon-192.png',
      // The collapse key. A second arrival for the same logical event REPLACES this notification
      // rather than stacking a second one, which is how once-only delivery is achieved without a
      // dedupe store on the server.
      tag: payload.tag,
      data: { url: payload.url },
    }),
  );
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();

  const target = (event.notification.data && event.notification.data.url) || '/';

  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((windows) => {
      // Prefer a window that is already open: focusing beats opening a second copy of the app, and
      // on a phone the installed app is usually the only window there is.
      for (const client of windows) {
        if (client.url.includes(target) && 'focus' in client) {
          return client.focus();
        }
      }
      if (windows.length > 0 && 'navigate' in windows[0]) {
        return windows[0].focus().then((focused) => focused.navigate(target));
      }
      return self.clients.openWindow(target);
    }),
  );
});
