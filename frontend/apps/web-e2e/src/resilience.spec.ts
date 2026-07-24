import { expect, test } from '@playwright/test';
import { registerVerifySignIn } from './support/auth';

/**
 * Browser-hop resilience (feature 028, US1) end to end.
 *
 * These assert the two behaviours that were *absent* before this feature and that no unit test can
 * prove, because both depend on the real browser stack: a request that never answers used to leave
 * the screen loading forever, and a transient failure used to become an error card on the first
 * blip. Faults are injected with Playwright route interception rather than by breaking the backend,
 * so the run stays deterministic and parallel-safe.
 *
 * Interception is deliberately **endpoint-agnostic** — it targets whatever data call the screen
 * happens to make, not a hard-coded URL. An earlier version pinned a specific path, and when that
 * path turned out to be wrong the tests passed while intercepting nothing at all. Each test now
 * asserts it actually intercepted something, so it cannot pass vacuously.
 */

/** Data reads the app makes for content — excludes the auth/session probes. */
const DATA_READ = /\/api\/v1\/(?!auth\/)/;

test.describe('a hung request does not spin forever (FR-001)', () => {
  test('a hung read is abandoned after a bounded number of attempts', async ({ page, request }) => {
    // Inherently slow: proving the bound means waiting out the 15s per-attempt limit three times.
    // That duration IS the feature — before it, this wait would never have ended.
    test.setTimeout(180_000);

    await registerVerifySignIn(page, request);

    const attemptsByUrl = new Map<string, number>();
    await page.route(
      (url) => DATA_READ.test(url.pathname),
      async (route) => {
        if (route.request().method() !== 'GET') {
          await route.continue();
          return;
        }
        const url = route.request().url();
        attemptsByUrl.set(url, (attemptsByUrl.get(url) ?? 0) + 1);
        // Never fulfil, never abort: the request just hangs, exactly like a wedged upstream.
        await new Promise(() => undefined);
      },
    );

    // 'commit' rather than the default 'load': every data read is being held open, so waiting for
    // the load event would hang on the very condition under test.
    await page.goto('/browse/teams', { waitUntil: 'commit' });

    // Confirm the fault actually landed before asserting anything about it.
    await expect.poll(() => attemptsByUrl.size, { timeout: 30_000 }).toBeGreaterThan(0);

    // The guarantee under test is that a hung request is ABANDONED and bounded — 1 attempt plus at
    // most 2 retries, each cut off at the 15s per-attempt limit. Asserted at the request level
    // rather than by watching a spinner: a screen fires several independent loads, each bounded
    // separately, so "the page stopped loading" is a weaker and much slower proxy for the same
    // thing. Before this feature the count here would have been 1, forever, with no timeout at all.
    // THE assertion: the hung attempt was abandoned and a second one was made. Before this feature
    // there was no client timeout at all, so a wedged upstream produced exactly one request that
    // never completed and a screen that loaded forever — this count could never have exceeded 1.
    await expect
      .poll(() => Math.max(...attemptsByUrl.values()), { timeout: 60_000 })
      .toBeGreaterThan(1);

    // And retrying STOPS — the whole page goes quiet rather than hammering a dead backend.
    //
    // Measured behaviour: this screen issues six logical reads across five endpoints (browse teams,
    // the notification and chat unread badges, my-teams, and an admin-access check made twice).
    // Each is retried exactly 1 + 2 times and then gives up, so the total settles at 18 and never
    // moves again. Allow the full 3 × 15s sequence to finish before sampling — measuring too early
    // reads "not finished yet" as "still growing".
    await page.waitForTimeout(60_000);
    const settled = [...attemptsByUrl.values()].reduce((a, b) => a + b, 0);

    await page.waitForTimeout(30_000);
    const afterWaiting = [...attemptsByUrl.values()].reduce((a, b) => a + b, 0);

    expect(afterWaiting).toBe(settled);
  });
});

test.describe('a transient failure recovers silently (FR-002)', () => {
  test('a read that fails once is retried without the person seeing an error', async ({
    page,
    request,
  }) => {
    await registerVerifySignIn(page, request);

    const attemptsByUrl = new Map<string, number>();
    await page.route(
      (url) => DATA_READ.test(url.pathname),
      async (route) => {
        if (route.request().method() !== 'GET') {
          await route.continue();
          return;
        }

        const url = route.request().url();
        const seen = (attemptsByUrl.get(url) ?? 0) + 1;
        attemptsByUrl.set(url, seen);

        if (seen === 1) {
          // One transient blip — the kind a rolling deploy produces.
          await route.fulfill({ status: 503, body: '' });
          return;
        }
        await route.continue();
      },
    );

    await page.goto('/browse/teams');
    await page.waitForTimeout(5_000);

    expect(attemptsByUrl.size).toBeGreaterThan(0);

    // At least one read was asked again after its 503 — the retry happened, silently.
    const retried = [...attemptsByUrl.values()].filter((n) => n > 1);
    expect(retried.length).toBeGreaterThan(0);

    // And the person was never shown a dead end.
    await expect(page.getByRole('button', { name: /try again/i })).toBeHidden();
  });
});

test.describe('mutations are never retried (FR-004)', () => {
  test('a failed write is attempted exactly once', async ({ page, request }) => {
    // The guard against silently performing an action twice. If this ever fails, a person could
    // create two teams, send two messages, or RSVP twice from a single click.
    await registerVerifySignIn(page, request);

    let writes = 0;
    await page.route('**/api/v1/teams', async (route) => {
      if (route.request().method() === 'POST') {
        writes += 1;
        await route.fulfill({ status: 503, body: '' });
        return;
      }
      await route.continue();
    });

    const suffix = `${Date.now()}`;
    await page.goto('/teams/new');

    await page.getByTestId('team-name').fill(`Resilience E2E ${suffix}`);
    await page.getByTestId('team-slug').fill(`resilience-e2e-${suffix}`);
    await page.getByTestId('type-city').click();
    await page.getByTestId('team-city').fill('Berlin');
    await page.getByTestId('team-create-submit').click();

    // Give any (incorrect) retry ample time to fire — the assertion is that none does.
    await page.waitForTimeout(3_000);
    expect(writes).toBe(1);
  });
});
