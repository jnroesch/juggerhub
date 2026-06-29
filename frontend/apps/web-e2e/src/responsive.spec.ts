import { expect, test } from '@playwright/test';

/**
 * US3 — the shell + dashboard stay usable at both the desktop and mobile
 * projects (playwright.config.mts): navigation is reachable and there is no
 * unintended horizontal scrolling / clipped content (FR-025, SC-009).
 */
test('shell and dashboard are usable with no horizontal overflow', async ({ page }) => {
  await page.goto('/');

  // The dashboard content renders inside the shell.
  await expect(page.getByTestId('health-card')).toBeVisible();

  // No unintended horizontal scrolling at this viewport.
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(1);
});

test('primary navigation is reachable', async ({ page }, testInfo) => {
  await page.goto('/');

  const sidebar = page.getByTestId('sidebar');

  if (testInfo.project.name === 'mobile-chrome') {
    // On mobile the sidebar is an off-canvas drawer opened from the top nav.
    const toggle = page.getByTestId('menu-toggle');
    await expect(toggle).toBeVisible();
    await toggle.click();
  }

  // Either statically present (desktop) or revealed by the toggle (mobile).
  await expect(sidebar.getByRole('link', { name: 'Dashboard' })).toBeVisible();
});
