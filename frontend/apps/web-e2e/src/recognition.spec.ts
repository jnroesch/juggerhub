import { expect, test } from '@playwright/test';
import { ensureAdminSignedIn, newAccount, registerVerify } from './support/auth';

/**
 * Feature 012/013 end-to-end: an admin grants a badge to a player from the player's
 * admin detail (the one place grants happen since 013), the badge appears on the
 * player's public profile, and revoking it removes it. The admin account is
 * `admin@test.de` (the stack's ADMIN_EMAILS, designated by the role sync).
 */

test('admin grants a badge → it shows on the player profile → revoke removes it', async ({ page, request }) => {
  // 1. A target player exists.
  const player = newAccount('e2erec');
  const { handle } = player;
  await registerVerify(page, request, player);

  // 2. Sign in as the platform admin.
  await ensureAdminSignedIn(page, request);

  // 3. Open the player's admin detail (users → detail is the grant surface since 013).
  await page.goto(`/admin/users/${handle}`);
  await expect(page.getByTestId('subject-awards')).toBeVisible();

  await page.getByTestId('assign').click();
  await expect(page.getByTestId('assign-modal')).toBeVisible();

  // Grant the first available (not-held) catalogue badge, with a note.
  await page.locator('[data-testid^="pick-"]:not([disabled])').first().click();
  await page.getByTestId('grant-note').fill('e2e: for great fair play');
  await page.getByTestId('grant-submit').click();
  await expect(page.getByTestId('assign-modal')).toBeHidden();

  // 4. The player's public profile shows a badge.
  await page.goto(`/u/${handle}`);
  await expect(page.getByText('Badges', { exact: true })).toBeVisible();

  // 5. Revoke from the admin detail → the badge is gone again.
  await page.goto(`/admin/users/${handle}`);
  await expect(page.getByTestId('subject-awards')).toBeVisible();
  page.on('dialog', (d) => d.accept());
  await page.getByRole('button', { name: 'Revoke' }).first().click();
  await expect(page.getByText('None yet.').first()).toBeVisible();
});
