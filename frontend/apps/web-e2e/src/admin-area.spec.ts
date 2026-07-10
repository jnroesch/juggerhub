import { APIRequestContext, Page, expect, test } from '@playwright/test';

/**
 * Feature 013 end-to-end: the gated admin area. Covers the lock-marked entry (admins
 * only), the overview → users → player-detail path, the account-help lifecycle
 * (suspend blocks sign-in with a clear message; reinstate restores it), and the
 * Assign picker on the player detail. The admin account is `admin@test.de` (the
 * stack's ADMIN_EMAILS — designated at registration/startup by the role sync).
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

/** Sign in as the configured admin, registering+verifying once if it doesn't exist yet. */
async function ensureAdminSignedIn(page: Page, request: APIRequestContext): Promise<void> {
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(ADMIN_EMAIL);
  await page.getByTestId('sign-in-password').fill(PASSWORD);
  await page.getByTestId('sign-in-submit').click();
  await page.waitForTimeout(1500);
  if (page.url().includes('/sign-in')) {
    await registerVerify(page, request, ADMIN_EMAIL, `admin-${Date.now().toString(36)}`);
    await signIn(page, ADMIN_EMAIL);
  }
}

function uniquePlayer(): { email: string; handle: string } {
  const suffix = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  return {
    email: `e2e-adm-${suffix}@example.com`,
    handle: `e2eadm${suffix}`.replace(/[^a-z0-9]/g, '').slice(0, 20),
  };
}

test('non-admins never see the admin entry and /admin bounces them home', async ({ page, request }) => {
  const { email, handle } = uniquePlayer();
  await registerVerify(page, request, email, handle);
  await signIn(page, email);

  await page.goto('/');
  await expect(page.getByTestId('nav-admin')).toHaveCount(0);
  await expect(page.getByTestId('admin-link')).toHaveCount(0);

  await page.goto('/admin');
  await expect(page).toHaveURL((u) => !u.pathname.startsWith('/admin'));
});

test('admin: gated entry → overview → find player → suspend blocks sign-in → reinstate restores', async ({ page, request }) => {
  const { email, handle } = uniquePlayer();
  await registerVerify(page, request, email, handle);

  // The gated entry exists for the admin and opens the overview. Per wireframe 1a the
  // entry differs by form factor: a lock-marked top-nav item on desktop, an account-menu
  // row on mobile (no fifth tab) — so the test enters the way that viewport does.
  await ensureAdminSignedIn(page, request);
  await page.goto('/');
  const isMobile = (page.viewportSize()?.width ?? 1280) < 768;
  if (isMobile) {
    await page.getByTestId('avatar-menu-button').click();
    await expect(page.getByTestId('admin-link')).toBeVisible();
    await page.getByTestId('admin-link').click();
  } else {
    await expect(page.getByTestId('nav-admin')).toBeVisible();
    await page.getByTestId('nav-admin').click();
  }
  await expect(page.getByTestId('admin-overview-stats')).toBeVisible();

  // Search leads into user management with the query applied; the row opens the detail.
  await page.getByTestId('admin-overview-search').fill(handle);
  await page.getByTestId('admin-overview-search').press('Enter');
  await expect(page).toHaveURL((u) => u.pathname.endsWith('/admin/users'));
  await page.getByTestId('admin-users-row').first().click();
  await expect(page).toHaveURL((u) => u.pathname.endsWith(`/admin/users/${handle}`));
  await expect(page.getByTestId('admin-user-status')).toHaveText('Active');

  // Suspend (confirmed) → status flips.
  page.on('dialog', (d) => d.accept());
  await page.getByTestId('admin-action-suspend').click();
  await expect(page.getByTestId('admin-user-status')).toHaveText('Suspended');

  // The suspended player is refused sign-in with a clear message.
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(email);
  await page.getByTestId('sign-in-password').fill(PASSWORD);
  await page.getByTestId('sign-in-submit').click();
  await expect(page.getByTestId('sign-in')).toContainText(/suspended/i);

  // Reinstate → sign-in works again.
  await ensureAdminSignedIn(page, request);
  await page.goto(`/admin/users/${handle}`);
  await page.getByTestId('admin-action-reinstate').click();
  await expect(page.getByTestId('admin-user-status')).toHaveText('Active');
  await signIn(page, email);

  // Ban → the public profile disappears entirely; unban → it returns intact.
  await ensureAdminSignedIn(page, request);
  await page.goto(`/admin/users/${handle}`);
  await page.getByTestId('admin-action-ban').click();
  await expect(page.getByTestId('admin-user-status')).toHaveText('Banned');
  await page.goto(`/u/${handle}`);
  await expect(page.getByText(`@${handle}`)).toHaveCount(0);

  await page.goto(`/admin/users/${handle}`);
  await page.getByTestId('admin-action-unban').click();
  await expect(page.getByTestId('admin-user-status')).toHaveText('Active');
  await page.goto(`/u/${handle}`);
  await expect(page.getByText(`@${handle}`).first()).toBeVisible();
});

test('admin assigns a badge from the player detail with a note, then revokes it', async ({ page, request }) => {
  const { email, handle } = uniquePlayer();
  await registerVerify(page, request, email, handle);
  await ensureAdminSignedIn(page, request);

  await page.goto(`/admin/users/${handle}`);
  await page.getByTestId('admin-assign-open').click();
  await expect(page.getByTestId('admin-assign-picker')).toBeVisible();

  // Pick the first grantable catalogue item (dev seed provides the catalogue).
  await page.locator('[data-testid="admin-assign-items"] button:not([disabled])').first().click();
  await page.getByTestId('admin-assign-note').fill('e2e: for great fair play');
  await page.getByTestId('admin-assign-grant').click();
  await expect(page.getByTestId('admin-assign-picker')).toBeHidden();

  // It lands on the detail's award list, then revoke removes it.
  await expect(page.getByTestId('admin-award-list').locator('li')).toHaveCount(1);
  page.on('dialog', (d) => d.accept());
  await page.getByRole('button', { name: 'Revoke' }).first().click();
  await expect(page.getByTestId('admin-award-list')).toHaveCount(0);
});
