import { expect, test } from '@playwright/test';
import { E2E_PASSWORD, linkFromEmail, newAccount, register } from './support/auth';

/**
 * US1–US3 end-to-end: register → verify (via the Mailpit inbox) → sign in → sign
 * out → forgot/reset → sign in with the new password. Runs at both the desktop and
 * mobile projects (playwright.config.mts), proving the full local cycle and
 * responsive auth screens (SC-001, SC-009).
 *
 * The steps are spelled out rather than delegated to the support helper's
 * `registerVerifySignIn`: this is the spec that owns proving each hop works, so
 * collapsing them would leave the shared helper untested.
 */

const NEW_PASSWORD = 'N3w!Passw0rd#';

test('register → verify → sign in → sign out → reset password', async ({ page, request }) => {
  const account = newAccount();
  const { email } = account;

  // 1. Register
  await register(page, account);

  // 2. Cannot sign in before verifying.
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(email);
  await page.getByTestId('sign-in-password').fill(E2E_PASSWORD);
  await page.getByTestId('sign-in-submit').click();
  await expect(page.getByTestId('sign-in-verify')).toBeVisible();

  // 3. Verify via the emailed link.
  await page.goto(await linkFromEmail(request, email, 'verify-email'));
  await expect(page.getByTestId('verify-email')).toContainText(/verified/i);

  // 4. Sign in → first-login onboarding (feature 004); dismiss it, then reach the protected area.
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(email);
  await page.getByTestId('sign-in-password').fill(E2E_PASSWORD);
  await page.getByTestId('sign-in-remember').check();
  await page.getByTestId('sign-in-submit').click();
  await expect(page).toHaveURL(/onboarding/);
  await page.getByTestId('onboarding-dismiss').click();
  await expect(page).not.toHaveURL(/onboarding/);
  await page.goto('/account');
  await expect(page.getByTestId('account-email')).toContainText(email);

  // 5. Sign out → protected area no longer reachable. Since feature 008 the control
  //    lives inside the avatar-menu dropdown, so open it first.
  await page.getByTestId('avatar-menu-button').click();
  await page.getByTestId('sign-out').click();
  await expect(page).toHaveURL(/sign-in/);
  await page.goto('/account');
  await expect(page).toHaveURL(/sign-in/);

  // 6. Forgot password.
  await page.goto('/forgot-password');
  await page.getByTestId('forgot-email').fill(email);
  await page.getByTestId('forgot-submit').click();
  await expect(page.getByTestId('forgot-password')).toContainText(/check your email/i);

  // 7. Reset via the emailed link.
  await page.goto(await linkFromEmail(request, email, 'reset-password'));
  await page.getByTestId('reset-password-input').fill(NEW_PASSWORD);
  await page.getByTestId('reset-confirm-password').fill(NEW_PASSWORD);
  await expect(page.getByTestId('reset-submit')).toBeEnabled();
  await page.getByTestId('reset-submit').click();
  await expect(page.getByTestId('reset-password')).toContainText(/password reset/i);

  // 8. The new password works; the old one would not (it was changed server-side).
  //    Onboarding was already dismissed in step 4 → sign-in goes straight to the app.
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(email);
  await page.getByTestId('sign-in-password').fill(NEW_PASSWORD);
  await page.getByTestId('sign-in-submit').click();
  // Wait for the login to actually land before navigating on: an onboarded user
  // is redirected to the app (neither /sign-in nor /onboarding). Asserting only
  // `not /onboarding` would pass instantly while still on /sign-in and race the
  // session cookie, bouncing the next goto back to sign-in.
  await expect(page).not.toHaveURL(/sign-in|onboarding/);
  await page.goto('/account');
  await expect(page.getByTestId('account-email')).toContainText(email);
});
