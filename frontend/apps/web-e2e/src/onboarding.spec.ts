import { expect, test } from '@playwright/test';
import { pickCity } from './support/city';
import { E2E_PASSWORD, registerVerifySignIn } from './support/auth';

/**
 * Feature 004 end-to-end: a freshly-verified user's first sign-in is routed into
 * the guided onboarding flow; completing it lands them in the app; and a later
 * sign-in goes straight to the app (shown once). Runs at desktop + mobile projects.
 */

test('first login opens onboarding; completing it lands in the app and it is shown only once', async ({
  page,
  request,
}) => {
  // 1. Register + verify + first sign-in, which is routed into onboarding (not the app).
  const { email, handle } = await registerVerifySignIn(page, request, 'onb');
  await expect(page.getByTestId('onboarding')).toContainText(/Welcome to Jugger/i);

  // 3. Walk the flow: name (prefilled with the handle) → city → pompfen → team stub → photo+bio.
  await page.getByTestId('onboarding-start').click();
  await expect(page.getByTestId('onboarding-name')).toHaveValue(handle);
  await page.getByTestId('onboarding-name').fill('E2E Player');
  await page.getByTestId('onboarding-continue').click(); // → city

  await pickCity(page, 'onboarding-city', 'Berlin');
  await page.getByTestId('onboarding-continue').click(); // → pompfen

  await page.getByTestId('pompfe-Stab').click();
  await page.getByTestId('onboarding-continue').click(); // → team

  await page.getByTestId('onboarding-continue').click(); // team stub → photo

  await page.getByTestId('onboarding-bio').fill('Here for the Jugger.');
  await page.getByTestId('onboarding-finish').click();

  // 4. Done → enter the app.
  await expect(page.getByTestId('onboarding')).toContainText(/all set/i);
  await page.getByTestId('onboarding-enter').click();
  await expect(page).not.toHaveURL(/onboarding/);

  // 5. Values persisted — visible on the public share page.
  await page.goto(`/u/${handle}`);
  await expect(page.locator('body')).toContainText('E2E Player');

  // 6. Sign out and back in → straight to the app, onboarding does NOT reappear.
  //    Since feature 008 sign-out lives inside the avatar-menu dropdown.
  await page.goto('/account');
  await page.getByTestId('avatar-menu-button').click();
  await page.getByTestId('sign-out').click();
  await expect(page).toHaveURL(/sign-in/);

  await page.getByTestId('sign-in-email').fill(email);
  await page.getByTestId('sign-in-password').fill(E2E_PASSWORD);
  await page.getByTestId('sign-in-submit').click();
  // Wait for the login to actually land in the app before navigating on. Asserting only
  // `not /onboarding` would pass instantly while still on /sign-in and race the session cookie,
  // bouncing the next goto back to sign-in (see auth.spec.ts). Feature 026 makes that bounce a
  // /sign-in?returnUrl=/onboarding URL, which the loose regex would then also match.
  await expect(page).not.toHaveURL(/sign-in|onboarding/);

  // Directly opening the flow after onboarding bounces to the app.
  await page.goto('/onboarding');
  await expect(page).not.toHaveURL(/onboarding/);
});
