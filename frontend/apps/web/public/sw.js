/*
 * JuggerHub's push-only service worker (feature 054).
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
 * pwa-shell.spec.ts reads this file and fails the build if any of those appear. push and
 * notificationclick handlers arrive with #308, together with the subscription that makes them
 * reachable.
 *
 * The two listeners below are lifecycle only: a changed worker takes over on the visit that
 * fetched it instead of waiting for every tab of the site to close.
 */
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));
