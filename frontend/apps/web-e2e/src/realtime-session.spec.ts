import { Page, expect, test } from '@playwright/test';
import { registerAndEnter } from './support/auth';

/**
 * The realtime hubs across the end of an access token (GH #402), in a real browser.
 *
 * The server ends a hub connection when the access token it was made with expires, and refuses a
 * handshake made with an expired one. The handshake is SignalR's own request — it never passes
 * through the app's HTTP interceptor — so the client has to renew the session itself, or a signed-in
 * member's live updates stop at the first expiry and stay stopped.
 *
 * The refusal is injected with route interception rather than by waiting out a real token: that
 * wait is the token's whole lifetime. What only a browser can prove is the part asserted here — that
 * the real SignalR client honours the renewing handshake both services hand it, against the real
 * refresh endpoint. That the server closes and refuses is covered by its own integration tests.
 *
 * Each test asserts that it actually refused something, so it cannot pass by intercepting nothing.
 */

const NEGOTIATE = /\/hubs\/(chat|notifications)\/negotiate/;
const HUBS = ['/hubs/chat', '/hubs/notifications'];

const unauthorized = { status: 401, contentType: 'application/problem+json', body: '{"status":401}' };

/**
 * A second tab in the signed-in browser, not yet navigated, so interception can be in place before
 * its first request.
 *
 * Not a reload of the page that signed in: that page may still be making its own first handshake,
 * which would be intercepted instead — and use up the refusal meant for the page under test.
 */
async function newSignedInTab(page: Page): Promise<Page> {
  const tab = await page.context().newPage();
  await page.close();
  return tab;
}

/** The hubs that have completed a handshake: a frame came back on their socket. */
function trackLiveHubs(page: Page): Set<string> {
  const live = new Set<string>();
  page.on('websocket', (socket) => {
    socket.on('framereceived', () => live.add(new URL(socket.url()).pathname));
  });
  return live;
}

test('a refused hub handshake renews the session and connects', async ({ page, request }) => {
  await registerAndEnter(page, request, 'hub');
  const tab = await newSignedInTab(page);

  // Refuse each hub's handshake once, the way the server does for an expired token.
  const refused = new Set<string>();
  await tab.route(NEGOTIATE, async (route) => {
    const hub = new URL(route.request().url()).pathname.replace('/negotiate', '');
    if (refused.has(hub)) {
      await route.continue();
      return;
    }
    refused.add(hub);
    await route.fulfill(unauthorized);
  });

  let renewals = 0;
  tab.on('response', (response) => {
    if (response.url().includes('/api/v1/auth/refresh') && response.status() === 200) {
      renewals++;
    }
  });
  const live = trackLiveHubs(tab);

  await tab.goto('/');

  await expect.poll(() => [...live].sort(), { timeout: 15_000 }).toEqual(HUBS);
  expect([...refused].sort()).toEqual(HUBS);
  expect(renewals).toBeGreaterThan(0);
  // Still signed in: renewing is silent.
  await expect(tab).not.toHaveURL(/sign-in/);
});

test('a session that cannot be renewed ends at sign-in', async ({ page, request }) => {
  await registerAndEnter(page, request, 'hub');
  const tab = await newSignedInTab(page);

  // The session was ended somewhere else — signed out in another tab, a password reset, a ban:
  // the handshake is refused and so is the refresh.
  let refusedHandshakes = 0;
  await tab.route(NEGOTIATE, async (route) => {
    refusedHandshakes++;
    await route.fulfill(unauthorized);
  });
  await tab.route('**/api/v1/auth/refresh', (route) => route.fulfill(unauthorized));
  const live = trackLiveHubs(tab);

  await tab.goto('/');

  // An idle tab must not go on showing a signed-in page it can no longer read.
  await expect(tab).toHaveURL(/sign-in/, { timeout: 15_000 });
  expect(refusedHandshakes).toBeGreaterThan(0);
  expect([...live]).toEqual([]);

  // And it stops trying: no handshake once the session is known to be over. The second hub's own
  // refused attempt may still be landing as the redirect happens, so let that settle first.
  await tab.waitForTimeout(2_000);
  const settled = refusedHandshakes;
  await tab.waitForTimeout(5_000);
  expect(refusedHandshakes).toBe(settled);
});
