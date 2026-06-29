import { expect, test } from '@playwright/test';

/**
 * US1 — the dashboard loads and renders the backend health status. Runs at both
 * the desktop and mobile projects defined in playwright.config.mts.
 */
test('dashboard loads and shows the health status', async ({ page }) => {
  await page.goto('/');

  const status = page.getByTestId('health-status');
  await expect(status).toBeVisible();
  await expect(status).toHaveText(/healthy/i);

  await expect(page.getByTestId('health-database')).toHaveText(/reachable/i);
});
