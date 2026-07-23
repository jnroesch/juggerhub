import { APIRequestContext, expect, test } from '@playwright/test';

/**
 * Feature 003 end-to-end: register with a handle → verify (Mailpit) → sign in →
 * edit the profile → open the public /u/<handle> page signed-out and assert no
 * email is exposed on the wire (SC-002). Runs at desktop + mobile projects.
 */

const MAILPIT = process.env['MAILPIT_URL'] || 'http://mailpit:8025';
const PASSWORD = 'Str0ng!Passw0rd';

async function verifyLinkPath(request: APIRequestContext, to: string): Promise<string> {
  for (let attempt = 0; attempt < 30; attempt++) {
    const res = await request.get(`${MAILPIT}/api/v1/search`, { params: { query: `to:${to}` } });
    if (res.ok()) {
      const data = (await res.json()) as { messages?: { ID: string }[] };
      for (const message of data.messages ?? []) {
        const full = await request.get(`${MAILPIT}/api/v1/message/${message.ID}`);
        if (!full.ok()) continue;
        const body = (await full.json()) as { HTML?: string; Text?: string };
        const html = body.HTML || body.Text || '';
        const match = html.match(/https?:\/\/[^"'\s]*\/verify-email\?[^"'\s<]+/);
        if (match) {
          const url = new URL(match[0]);
          return url.pathname + url.search;
        }
      }
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error(`No verification email for ${to} appeared in Mailpit.`);
}

test('register with handle → edit profile → public page hides email', async ({ page, request }) => {
  const suffix = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  const email = `e2e-profile-${suffix}@example.com`;
  const handle = `e2e-${suffix}`;

  // 1. Register with a handle, waiting for live availability.
  await page.goto('/register');
  await page.getByTestId('register-email').fill(email);
  await page.getByTestId('register-handle').fill(handle);
  await expect(page.getByTestId('handle-available')).toBeVisible();
  await page.getByTestId('register-password').fill(PASSWORD);
  await page.getByTestId('register-confirm-password').fill(PASSWORD);
  await expect(page.getByTestId('register-submit')).toBeEnabled();
  await page.getByTestId('register-submit').click();
  await expect(page.getByTestId('register')).toContainText(/check your email/i);

  // 2. Verify + sign in.
  await page.goto(await verifyLinkPath(request, email));
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(email);
  await page.getByTestId('sign-in-password').fill(PASSWORD);
  await page.getByTestId('sign-in-submit').click();
  // A fresh, not-yet-onboarded account is redirected into /onboarding on first
  // sign-in (feature 004). Wait for that redirect so the session cookie is set
  // before navigating on; otherwise the goto below races login and the authGuard
  // bounces it back to /sign-in.
  await expect(page).toHaveURL(/onboarding/);

  // 3. Edit the profile — one URL for your own profile is your slug (/u/<handle>), owner view.
  await page.goto(`/u/${handle}`);
  await expect(page.getByTestId('profile-owner')).toBeVisible();
  await page.getByTestId('profile-edit').click();
  await page.getByTestId('profile-displayname').fill('Nik Berlin');
  await page.getByTestId('profile-hometown').fill('Berlin');
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
