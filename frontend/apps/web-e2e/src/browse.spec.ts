import { expect, test } from '@playwright/test';
import { registerVerifySignIn } from './support/auth';

/**
 * Search / Browse (feature 007). The three pages share one shell, so these checks assert the
 * common controls on all three, then exercise the filter panel, the no-results state, and the
 * players list note. Browse is authenticated-only since feature 026, so each test signs in first.
 * Runs at the desktop and mobile projects from playwright.config.mts (sheet on mobile, drawer on desktop).
 */

// Browse requires a session (feature 026) — sign in before each test.
test.beforeEach(async ({ page, request }) => {
  await registerVerifySignIn(page, request);
});

const pages = [
  { path: '/browse/teams', title: 'Teams' },
  { path: '/browse/events', title: 'Events' },
  { path: '/browse/players', title: 'Players' },
];

test.describe('browse shell is consistent across entities', () => {
  for (const { path, title } of pages) {
    test(`${title} page shows the shared shell controls`, async ({ page }) => {
      await page.goto(path);

      await expect(page.getByRole('heading', { name: title })).toBeVisible();
      await expect(page.getByTestId('browse-search')).toBeVisible();
      await expect(page.getByTestId('browse-filters-button')).toBeVisible();

      // Results, one of the four states, resolve (not a blank page).
      await expect(
        page.getByTestId('browse-results')
          .or(page.getByTestId('browse-empty'))
          .or(page.getByTestId('browse-no-results'))
          .or(page.getByTestId('browse-loading'))
          .first(),
      ).toBeVisible();
    });
  }
});

test('filters open on demand and can be applied', async ({ page }) => {
  await page.goto('/browse/teams');

  await page.getByTestId('browse-filters-button').click();
  const panel = page.getByTestId('filter-panel');
  await expect(panel).toBeVisible();

  // The locked near-me hook is present and non-interactive.
  await expect(page.getByTestId('filter-near-me')).toBeVisible();

  await page.getByTestId('filter-apply').click();
  await expect(panel).toBeHidden();
});

test('a nonsense query shows the no-results state with a clear action', async ({ page }) => {
  await page.goto('/browse/teams');

  await page.getByTestId('browse-search').fill('zzz-no-such-team-qqq-xyz');
  const noResults = page.getByTestId('browse-no-results');
  await expect(noResults).toBeVisible();

  // Clearing restores results (or the empty state) — never a blank page.
  await noResults.getByRole('button').click();
  await expect(page.getByTestId('browse-no-results')).toBeHidden();
});

test('players page lists every player', async ({ page }) => {
  await page.goto('/browse/players');
  await expect(page.getByText(/every player on JuggerHub is listed/i)).toBeVisible();
});
