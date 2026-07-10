import { expect, test } from '@playwright/test';

/**
 * US1 — the full frontend → API → database round trip. The SPA origin proxies
 * `/api/` to the backend (nginx.conf), and the backend reports overall status plus
 * database reachability. Feature 008 removed the dashboard health card, so this no
 * longer has a UI surface; the check now targets the public health endpoint directly
 * through the same origin the app is served from. Runs at both the desktop and mobile
 * projects (playwright.config.mts) — the assertion is origin-level, not viewport-level.
 */
test('the API health endpoint reports healthy with a reachable database', async ({ request }) => {
  const res = await request.get('/api/v1/health');
  expect(res.ok()).toBeTruthy();

  const body = (await res.json()) as { status: string; database: string };
  expect(body.status).toMatch(/healthy/i);
  expect(body.database).toMatch(/reachable/i);
});
