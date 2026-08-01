import { expect, test } from '@playwright/test';
import { E2E_PASSWORD, registerVerifySignIn, verifyLinkPath } from './support/auth';

/**
 * Feature 037 — self-service account deletion, in a real browser.
 *
 * The disposition of every table, the ban/deletion asymmetry and the atomicity guarantees are
 * proven by the backend integration suite. What only a browser can prove is the half those tests
 * cannot reach: that the control is findable, that the disclosure actually says the thing a member
 * will not expect, that the confirmation cannot be given by accident, and that the session really
 * ends. That is what this file covers — it deliberately does not re-assert the data model.
 *
 * Both viewports run (see playwright.config.mts): a destructive action hidden below a broken mobile
 * layout is as bad as one that is missing.
 */

test.describe('account deletion', () => {
  test('a member deletes their own account, and the address works again afterwards', async ({
    page,
    request,
  }) => {
    const { email } = await registerVerifySignIn(page, request);

    await page.goto('/account');
    await expect(page.getByTestId('account')).toBeVisible();

    // 1. The control is present without hunting for it.
    const dangerZone = page.getByTestId('account-danger-zone');
    await expect(dangerZone).toBeVisible();
    await dangerZone.getByTestId('delete-account-open').click();

    const panel = page.getByTestId('delete-account-panel');
    await expect(panel).toBeVisible();

    // 2. The disclosure says the thing nobody expects: the messages STAY (FR-025), and text you
    //    typed about yourself stays with them (FR-027). This is a correctness surface — a member who
    //    reads this and concludes otherwise has been misled in a flow the privacy policy describes.
    await expect(panel).toContainText(/messages you sent stay/i);
    await expect(panel).toContainText(/A former player/i);
    await expect(panel).toContainText(/only one who knows what you wrote/i);
    await expect(panel).toContainText(/permanent/i);

    // 3. Confirmation cannot be given by accident. With no grace period, this and the password are
    //    the ONLY protection against a regretted click (FR-037).
    const confirmButton = page.getByTestId('delete-account-confirm');
    await expect(confirmButton).toBeDisabled();

    await page.getByTestId('delete-account-password').fill(E2E_PASSWORD);
    await expect(confirmButton).toBeDisabled();

    await page.getByTestId('delete-account-confirmation').fill('NOPE');
    await expect(confirmButton).toBeDisabled();

    await page.getByTestId('delete-account-confirmation').fill('DELETE');
    await expect(confirmButton).toBeEnabled();

    // 4. Do it.
    await confirmButton.click();

    // 5. The session ends with the account. Landing anywhere that still renders signed-in chrome
    //    would mean the cookie outlived the row it belonged to.
    await expect(page).not.toHaveURL(/\/account/, { timeout: 15_000 });
    await page.goto('/account');
    await expect(page).toHaveURL(/sign-in/);

    // 6. The old credentials are dead.
    await page.goto('/sign-in');
    await page.getByTestId('sign-in-email').fill(email);
    await page.getByTestId('sign-in-password').fill(E2E_PASSWORD);
    await page.getByTestId('sign-in-submit').click();
    await expect(page).toHaveURL(/sign-in/);

    // 7. …but the ADDRESS is released, so the same person can come back (FR-031). This is the half
    //    that differs from a ban, and the half most likely to regress silently: registration returns
    //    the same neutral acceptance whether or not it created anything, so the proof is signing in.
    const handle = `back-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`;
    await page.goto('/register');
    await page.getByTestId('register-email').fill(email);
    await page.getByTestId('register-handle').fill(handle);
    await expect(page.getByTestId('handle-available')).toBeVisible();
    await page.getByTestId('register-password').fill(E2E_PASSWORD);
    await page.getByTestId('register-confirm-password').fill(E2E_PASSWORD);
    await page.getByTestId('register-submit').click();
    await expect(page.getByTestId('register')).toContainText(/check your email/i);

    await page.goto(await verifyLinkPath(request, email));
    await page.goto('/sign-in');
    await page.getByTestId('sign-in-email').fill(email);
    await page.getByTestId('sign-in-password').fill(E2E_PASSWORD);
    await page.getByTestId('sign-in-submit').click();

    // A genuinely new account: first sign-in, so straight into onboarding.
    await expect(page).toHaveURL(/onboarding/);
  });

  test('cancelling changes nothing and leaves no typed password behind', async ({ page, request }) => {
    await registerVerifySignIn(page, request);

    await page.goto('/account');
    await page.getByTestId('delete-account-open').click();
    await expect(page.getByTestId('delete-account-panel')).toBeVisible();

    await page.getByTestId('delete-account-password').fill(E2E_PASSWORD);
    await page.getByTestId('delete-account-confirmation').fill('DELETE');
    await page.getByTestId('delete-account-cancel').click();

    // Closed, and the account untouched.
    await expect(page.getByTestId('delete-account-panel')).toHaveCount(0);
    await page.reload();
    await expect(page.getByTestId('account')).toBeVisible();

    // Reopening starts clean — a filled-in password must not survive a cancel.
    await page.getByTestId('delete-account-open').click();
    await expect(page.getByTestId('delete-account-password')).toHaveValue('');
    await expect(page.getByTestId('delete-account-confirmation')).toHaveValue('');
  });

  test('a wrong password is refused and the account survives', async ({ page, request }) => {
    await registerVerifySignIn(page, request);

    await page.goto('/account');
    await page.getByTestId('delete-account-open').click();

    await page.getByTestId('delete-account-password').fill('N0t-the-right-one!');
    await page.getByTestId('delete-account-confirmation').fill('DELETE');
    await page.getByTestId('delete-account-confirm').click();

    // A styled error, not a raw status code or an internal detail (DESIGN.md states section).
    await expect(page.getByTestId('delete-account-error')).toBeVisible();

    // Still signed in, still has an account.
    await page.goto('/account');
    await expect(page.getByTestId('account')).toBeVisible();
  });
});
