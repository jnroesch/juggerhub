import { APIRequestContext, Page, expect } from '@playwright/test';

/**
 * Shared e2e auth helper. Since feature 026 made teams/events/browse authenticated-only, most
 * flows must sign in first. Registers a fresh account, verifies it via Mailpit, and signs in —
 * leaving the page on the post-login destination with a valid session cookie.
 */

const MAILPIT = process.env['MAILPIT_URL'] || 'http://mailpit:8025';
export const E2E_PASSWORD = 'Str0ng!Passw0rd';

export async function verifyLinkPath(request: APIRequestContext, to: string): Promise<string> {
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

export interface SignedInUser {
  email: string;
  handle: string;
}

/** Register → verify → sign in. Returns the account's email + handle. */
export async function registerVerifySignIn(page: Page, request: APIRequestContext): Promise<SignedInUser> {
  const suffix = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  const email = `e2e-${suffix}@example.com`;
  const handle = `e2e-${suffix}`;

  await page.goto('/register');
  await page.getByTestId('register-email').fill(email);
  await page.getByTestId('register-handle').fill(handle);
  await expect(page.getByTestId('handle-available')).toBeVisible();
  await page.getByTestId('register-password').fill(E2E_PASSWORD);
  await page.getByTestId('register-confirm-password').fill(E2E_PASSWORD);
  await expect(page.getByTestId('register-submit')).toBeEnabled();
  await page.getByTestId('register-submit').click();
  await expect(page.getByTestId('register')).toContainText(/check your email/i);

  await page.goto(await verifyLinkPath(request, email));
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(email);
  await page.getByTestId('sign-in-password').fill(E2E_PASSWORD);
  await page.getByTestId('sign-in-submit').click();
  // First sign-in redirects into onboarding; wait so the session cookie is set before navigating on.
  await expect(page).toHaveURL(/onboarding/);

  return { email, handle };
}
