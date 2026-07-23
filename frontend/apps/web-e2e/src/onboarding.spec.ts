import { APIRequestContext, expect, test } from '@playwright/test';

/**
 * Feature 004 end-to-end: a freshly-verified user's first sign-in is routed into
 * the guided onboarding flow; completing it lands them in the app; and a later
 * sign-in goes straight to the app (shown once). Runs at desktop + mobile projects.
 */

const MAILPIT = process.env['MAILPIT_URL'] || 'http://mailpit:8025';
const PASSWORD = 'Str0ng!Passw0rd';

/** Polls Mailpit for the newest message to `to` whose body contains a `/{path}?…` link. */
async function linkFromEmail(request: APIRequestContext, to: string, path: string): Promise<string> {
  for (let attempt = 0; attempt < 30; attempt++) {
    const res = await request.get(`${MAILPIT}/api/v1/search`, { params: { query: `to:${to}` } });
    if (res.ok()) {
      const data = (await res.json()) as { messages?: { ID: string }[] };
      for (const message of data.messages ?? []) {
        const full = await request.get(`${MAILPIT}/api/v1/message/${message.ID}`);
        if (!full.ok()) continue;
        const body = (await full.json()) as { HTML?: string; Text?: string };
        const html = body.HTML || body.Text || '';
        const match = html.match(new RegExp(`https?://[^"'\\s]*/${path}\\?[^"'\\s<]+`));
        if (match) return match[0];
      }
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error(`No '${path}' email for ${to} appeared in Mailpit.`);
}

function toPath(url: string): string {
  const parsed = new URL(url);
  return parsed.pathname + parsed.search;
}

test('first login opens onboarding; completing it lands in the app and it is shown only once', async ({
  page,
  request,
}) => {
  const stamp = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  const email = `e2e-onb-${stamp}@example.com`;
  const handle = `onb${stamp}`.toLowerCase();

  // 1. Register + verify.
  await page.goto('/register');
  await page.getByTestId('register-email').fill(email);
  await page.getByTestId('register-handle').fill(handle);
  await expect(page.getByTestId('handle-available')).toBeVisible();
  await page.getByTestId('register-password').fill(PASSWORD);
  await page.getByTestId('register-confirm-password').fill(PASSWORD);
  await expect(page.getByTestId('register-submit')).toBeEnabled();
  await page.getByTestId('register-submit').click();
  await expect(page.getByTestId('register')).toContainText(/check your email/i);

  const verifyLink = await linkFromEmail(request, email, 'verify-email');
  await page.goto(toPath(verifyLink));
  await expect(page.getByTestId('verify-email')).toContainText(/verified/i);

  // 2. First sign-in → routed into onboarding (not the app).
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(email);
  await page.getByTestId('sign-in-password').fill(PASSWORD);
  await page.getByTestId('sign-in-submit').click();
  await expect(page).toHaveURL(/onboarding/);
  await expect(page.getByTestId('onboarding')).toContainText(/Welcome to Jugger/i);

  // 3. Walk the flow: name (prefilled with the handle) → city → pompfen → team stub → photo+bio.
  await page.getByTestId('onboarding-start').click();
  await expect(page.getByTestId('onboarding-name')).toHaveValue(handle);
  await page.getByTestId('onboarding-name').fill('E2E Player');
  await page.getByTestId('onboarding-continue').click(); // → city

  await page.getByTestId('onboarding-city').fill('Berlin');
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
  await page.getByTestId('sign-in-password').fill(PASSWORD);
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
