import { APIRequestContext, Page, expect, test } from '@playwright/test';

/**
 * Feature 012/013 end-to-end: an admin grants a badge to a player from the player's
 * admin detail (the one place grants happen since 013), the badge appears on the
 * player's public profile, and revoking it removes it. The admin account is
 * `admin@test.de` (the stack's ADMIN_EMAILS, designated by the role sync).
 */

const MAILPIT = process.env['MAILPIT_URL'] || 'http://mailpit:8025';
const PASSWORD = 'Str0ng!Passw0rd';
const ADMIN_EMAIL = 'admin@test.de';

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

async function registerVerify(page: Page, request: APIRequestContext, email: string, handle: string): Promise<void> {
  await page.goto('/register');
  await page.getByTestId('register-email').fill(email);
  await page.getByTestId('register-handle').fill(handle);
  await expect(page.getByTestId('handle-available')).toBeVisible();
  await page.getByTestId('register-password').fill(PASSWORD);
  await page.getByTestId('register-confirm-password').fill(PASSWORD);
  await expect(page.getByTestId('register-submit')).toBeEnabled();
  await page.getByTestId('register-submit').click();
  await expect(page.getByTestId('register')).toContainText(/check your email/i);
  await page.goto(await verifyLinkPath(request, email));
}

async function signIn(page: Page, email: string): Promise<void> {
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(email);
  await page.getByTestId('sign-in-password').fill(PASSWORD);
  await page.getByTestId('sign-in-submit').click();
  await expect(page).toHaveURL((u) => !u.pathname.includes('/sign-in'));
}

/** Sign in as the seeded admin, registering+verifying once if it doesn't exist yet. */
async function ensureAdminSignedIn(page: Page, request: APIRequestContext): Promise<void> {
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(ADMIN_EMAIL);
  await page.getByTestId('sign-in-password').fill(PASSWORD);
  await page.getByTestId('sign-in-submit').click();
  await page.waitForTimeout(1500);
  if (page.url().includes('/sign-in')) {
    // Not registered yet — create the admin account, then sign in.
    await registerVerify(page, request, ADMIN_EMAIL, `admin-${Date.now().toString(36)}`);
    await signIn(page, ADMIN_EMAIL);
  }
}

test('admin grants a badge → it shows on the player profile → revoke removes it', async ({ page, request }) => {
  const suffix = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  const playerEmail = `e2e-recip-${suffix}@example.com`;
  const handle = `e2erec${suffix}`.replace(/[^a-z0-9]/g, '').slice(0, 20);

  // 1. A target player exists.
  await registerVerify(page, request, playerEmail, handle);

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
