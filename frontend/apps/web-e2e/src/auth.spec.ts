import { APIRequestContext, expect, test } from '@playwright/test';

/**
 * US1–US3 end-to-end: register → verify (via the Mailpit inbox) → sign in → sign
 * out → forgot/reset → sign in with the new password. Runs at both the desktop and
 * mobile projects (playwright.config.mts), proving the full local cycle and
 * responsive auth screens (SC-001, SC-009).
 */

const MAILPIT = process.env['MAILPIT_URL'] || 'http://mailpit:8025';
const PASSWORD = 'Str0ng!Passw0rd';
const NEW_PASSWORD = 'N3w!Passw0rd#';

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

/** Email links point at the configured SPA origin; navigate by path against the test baseURL. */
function toPath(url: string): string {
  const parsed = new URL(url);
  return parsed.pathname + parsed.search;
}

test('register → verify → sign in → sign out → reset password', async ({ page, request }) => {
  const email = `e2e-${Date.now()}-${Math.random().toString(36).slice(2)}@example.com`;

  // 1. Register
  await page.goto('/register');
  await page.getByTestId('register-email').fill(email);
  await page.getByTestId('register-password').fill(PASSWORD);
  await page.getByTestId('register-confirm-password').fill(PASSWORD);
  await expect(page.getByTestId('register-submit')).toBeEnabled();
  await page.getByTestId('register-submit').click();
  await expect(page.getByTestId('register')).toContainText(/check your email/i);

  // 2. Cannot sign in before verifying.
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(email);
  await page.getByTestId('sign-in-password').fill(PASSWORD);
  await page.getByTestId('sign-in-submit').click();
  await expect(page.getByTestId('sign-in-verify')).toBeVisible();

  // 3. Verify via the emailed link.
  const verifyLink = await linkFromEmail(request, email, 'verify-email');
  await page.goto(toPath(verifyLink));
  await expect(page.getByTestId('verify-email')).toContainText(/verified/i);

  // 4. Sign in → reach the protected area.
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(email);
  await page.getByTestId('sign-in-password').fill(PASSWORD);
  await page.getByTestId('sign-in-remember').check();
  await page.getByTestId('sign-in-submit').click();
  await expect(page.getByTestId('account-email')).toContainText(email);

  // 5. Sign out → protected area no longer reachable.
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
  const resetLink = await linkFromEmail(request, email, 'reset-password');
  await page.goto(toPath(resetLink));
  await page.getByTestId('reset-password-input').fill(NEW_PASSWORD);
  await page.getByTestId('reset-confirm-password').fill(NEW_PASSWORD);
  await expect(page.getByTestId('reset-submit')).toBeEnabled();
  await page.getByTestId('reset-submit').click();
  await expect(page.getByTestId('reset-password')).toContainText(/password reset/i);

  // 8. The new password works; the old one would not (it was changed server-side).
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(email);
  await page.getByTestId('sign-in-password').fill(NEW_PASSWORD);
  await page.getByTestId('sign-in-submit').click();
  await expect(page.getByTestId('account-email')).toContainText(email);
});
