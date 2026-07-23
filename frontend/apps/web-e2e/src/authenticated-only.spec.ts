import { expect, test } from '@playwright/test';

/**
 * Feature 026 (US1/US3) — signed-out visitors cannot reach teams, events, or any browse view;
 * every attempt lands on the sign-in screen. This is the in-browser proof of the client guard;
 * the server-side 401 boundary is proven by the backend integration tests.
 */

const gatedPaths = [
  '/browse/teams',
  '/browse/events',
  '/browse/players',
  '/t/some-team',
  '/events/00000000-0000-0000-0000-000000000000',
];

test.describe('signed-out access to gated routes redirects to sign-in with a returnUrl', () => {
  for (const path of gatedPaths) {
    test(`${path} → sign-in carrying returnUrl`, async ({ page }) => {
      await page.context().clearCookies();
      await page.goto(path);
      await expect(page).toHaveURL(/sign-in/);
      await expect(page.getByTestId('sign-in-submit')).toBeVisible();
      // The originally-requested path is preserved so login can return the user there.
      const returnUrl = new URL(page.url()).searchParams.get('returnUrl');
      expect(returnUrl).toBe(path);
    });
  }
});

test('a private/unknown profile redirects a signed-out visitor to sign-in with a returnUrl', async ({ page }) => {
  await page.context().clearCookies();
  const handle = 'definitely-not-a-real-handle-xyz';
  // A handle that does not resolve publicly (unknown or private) must not reveal a profile; the
  // read-only view sends signed-out visitors to sign-in so they can view it after logging in.
  await page.goto(`/u/${handle}`);
  await expect(page).toHaveURL(/sign-in/);
  expect(new URL(page.url()).searchParams.get('returnUrl')).toBe(`/u/${handle}`);
  await expect(page.getByTestId('profile-public')).toHaveCount(0);
});
