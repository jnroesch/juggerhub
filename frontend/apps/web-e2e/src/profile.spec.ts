import { expect, test } from '@playwright/test';
import { pickCity } from './support/city';
import { registerVerifySignIn } from './support/auth';

/**
 * Feature 003 end-to-end: register with a handle → verify (Mailpit) → sign in →
 * edit the profile → open the public /u/<handle> page signed-out and assert no
 * email is exposed on the wire (SC-002). Runs at desktop + mobile projects.
 */

test('register with handle → edit profile → public page hides email', async ({ page, request }) => {
  // 1+2. Register with a handle, verify, and sign in — which lands in onboarding, since a
  //      fresh account has not been through it (feature 004).
  const { email, handle } = await registerVerifySignIn(page, request, 'prof');

  // 3. Edit the profile — one URL for your own profile is your slug (/u/<handle>), owner view.
  await page.goto(`/u/${handle}`);
  await expect(page.getByTestId('profile-owner')).toBeVisible();
  await page.getByTestId('profile-edit').click();
  await page.getByTestId('profile-displayname').fill('Nik Berlin');
  await pickCity(page, 'profile-hometown', 'Berlin');
  await page.getByTestId('pompfe-Stab').click();
  await page.getByTestId('pompfe-Laeufer').click();
  // Feature 026: opt the profile into public so a signed-out visitor can see it below
  // (profiles are private by default).
  await page.getByTestId('profile-ispublic').check();
  await page.getByTestId('profile-save').click();
  await expect(page.getByTestId('profile-saved')).toBeVisible();

  // 4. Same URL, signed out: shows the public profile but never the email (SC-002).
  await page.context().clearCookies();
  const apiResponse = await request.get(`/api/v1/profiles/${handle}`);
  expect(apiResponse.ok()).toBeTruthy();
  expect(await apiResponse.text()).not.toContain(email);

  await page.goto(`/u/${handle}`);
  await expect(page.getByTestId('profile-public')).toContainText('Nik Berlin');
  await expect(page.getByTestId('profile-public')).toContainText(`@${handle}`);
  await expect(page.locator('body')).not.toContainText(email);
  // Signed-out visitors get the shell's public bar, not the full nav.
  await expect(page.getByTestId('public-top-bar')).toBeVisible();
});
