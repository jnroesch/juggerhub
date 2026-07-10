import { expect, test } from '@playwright/test';

/**
 * US3 — the shell stays usable at both the desktop and mobile projects
 * (playwright.config.mts): primary navigation is reachable and there is no
 * unintended horizontal scrolling / clipped content (FR-025, SC-009).
 *
 * `/browse` is inside the app shell but anonymous (app.routes.ts), so it renders the
 * real navigation chrome without an auth journey. Feature 008 replaced the pre-001
 * sidebar + off-canvas `menu-toggle` with a persistent top-nav (desktop) and a fixed
 * bottom tab bar (mobile), so navigation is anchored on those instead.
 */
test('the shell renders with no horizontal overflow', async ({ page }) => {
  await page.goto('/browse');

  // The shell chrome renders (the account button sits in the top strip at every
  // viewport; the desktop nav links are hidden below md).
  await expect(page.getByTestId('avatar-menu-button')).toBeVisible();

  // No unintended horizontal scrolling at this viewport.
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(1);
});

test('primary navigation is reachable', async ({ page }, testInfo) => {
  await page.goto('/browse');

  if (testInfo.project.name === 'mobile-chrome') {
    // On mobile the primary destinations live in the fixed bottom tab bar; the
    // top-nav destinations are hidden (md:flex).
    const bottomNav = page.getByTestId('bottom-nav');
    await expect(bottomNav).toBeVisible();
    await expect(bottomNav.getByTestId('tab-home')).toBeVisible();
    await expect(bottomNav.getByTestId('tab-browse')).toBeVisible();
  } else {
    // On desktop the primary destinations live in the persistent top nav.
    await expect(page.getByTestId('nav-home')).toBeVisible();
    await expect(page.getByTestId('nav-browse')).toBeVisible();
  }
});
