import { expect, test } from '@playwright/test';

/**
 * PWA shell (feature 054) — the two static files the browser depends on and the live worker
 * registration. Runs at both the desktop and mobile projects (playwright.config.mts): the file
 * assertions are origin-level, the registration one proves the worker is claimed at both
 * viewports.
 *
 * Header assertions run ONLY when BASE_URL is set — that is the nginx container, which is
 * where the Cache-Control and Content-Type contract lives. The local Vite dev server serves the
 * same files with its own headers, and asserting on those would test Vite, not the product.
 */
const servedByNginx = Boolean(process.env['BASE_URL']);

test('the installable description and its icons are served', async ({ request }) => {
  const res = await request.get('/manifest.webmanifest');
  expect(res.ok()).toBeTruthy();
  if (servedByNginx) {
    expect(res.headers()['content-type']).toContain('manifest+json');
  }

  const manifest = (await res.json()) as { display: string; icons: { src: string }[] };
  expect(manifest.display).toBe('standalone');
  expect(manifest.icons.length).toBeGreaterThan(0);

  for (const icon of manifest.icons) {
    const img = await request.get('/' + icon.src);
    expect(img.ok(), icon.src).toBeTruthy();
    expect(img.headers()['content-type'], icon.src).toContain('image/png');
  }
});

test('the worker script is served as a script, never as the app page', async ({ request }) => {
  // The failure this guards is silent: `location /` serving index.html for a missing worker
  // makes the browser refuse the registration, and the app swallows that by design (FR-008).
  const res = await request.get('/sw.js');
  expect(res.ok()).toBeTruthy();
  expect(res.headers()['content-type']).toContain('javascript');

  const body = await res.text();
  expect(body).not.toContain('<html');
  expect(body).toContain('addEventListener');
});

test('the worker is registered for the whole site after the app starts', async ({ page }) => {
  // An off-shell public route, so this proves FR-006 on the pages that render outside the
  // signed-in shell too.
  await page.goto('/sign-in');

  const registration = await page.evaluate(() =>
    navigator.serviceWorker.ready.then((r) => ({
      scope: r.scope,
      script: r.active?.scriptURL ?? null,
    })),
  );

  expect(registration.scope).toBe(new URL(page.url()).origin + '/');
  expect(registration.script).toMatch(/\/sw\.js$/);
});

test('the worker and the manifest are served fresh', async ({ request }) => {
  test.skip(!servedByNginx, "Cache-Control is nginx's, not the dev server's");

  for (const file of ['/sw.js', '/manifest.webmanifest']) {
    const res = await request.get(file);
    expect(res.ok(), file).toBeTruthy();
    expect(res.headers()['cache-control'], file).toContain('no-cache');
  }
});
