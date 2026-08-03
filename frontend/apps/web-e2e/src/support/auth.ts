import { APIRequestContext, Page, expect } from '@playwright/test';

/**
 * Shared e2e auth helper — the single home for registering, verifying and signing in.
 *
 * Since feature 026 made teams/events/browse authenticated-only, nearly every spec has to
 * create an account before it can test anything, so this sequence used to be copy-pasted into
 * each one. That duplication had a cost: when feature 041 added the Terms of Use checkbox to
 * /register, one product change broke seven spec files at once, and the fix had to be applied
 * eight times. Reach for these helpers rather than re-deriving the flow.
 */

const MAILPIT = process.env['MAILPIT_URL'] || 'http://mailpit:8025';

export const E2E_PASSWORD = 'Str0ng!Passw0rd';

/** The stack's ADMIN_EMAILS — designated admin at registration/startup by the role sync. */
export const ADMIN_EMAIL = 'admin@test.de';

export interface Account {
  email: string;
  handle: string;
}

/**
 * A fresh account identity. The whole suite shares one database and runs at two viewport
 * projects, so both fields must be unique per call; `prefix` only makes a failing test easier
 * to spot in the Mailpit inbox. Kept within the handle rules registration enforces: lowercase
 * alphanumerics joined by single hyphens, 3–30 characters.
 */
export function newAccount(prefix = 'e2e'): Account {
  const suffix = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  return { email: `${prefix}-${suffix}@example.com`, handle: `${prefix}-${suffix}` };
}

/** Strips the origin off an absolute URL — email links and `page.url()` both carry one. */
export function toPath(url: string): string {
  const parsed = new URL(url);
  return parsed.pathname + parsed.search;
}

/**
 * Polls Mailpit for a message to `to` containing a `/{path}?…` link, returning its path+query.
 * Emails point at the configured SPA origin, which is not necessarily the test baseURL, so the
 * caller navigates by path.
 */
export async function linkFromEmail(request: APIRequestContext, to: string, path: string): Promise<string> {
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
        if (match) return toPath(match[0]);
      }
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error(`No '${path}' email for ${to} appeared in Mailpit.`);
}

/** The verification link from the sign-up email. */
export function verifyLinkPath(request: APIRequestContext, to: string): Promise<string> {
  return linkFromEmail(request, to, 'verify-email');
}

/**
 * Fills and submits /register, leaving the page on the neutral "check your email" state.
 *
 * Feature 041: accepting the Terms of Use is `Validators.requiredTrue` and the box is never
 * pre-ticked — agreement has to be an act (FR-015) — so submit stays disabled until it is
 * checked here. Submit is additionally gated on the legal catalogue having loaded
 * (`termsReady()`), which the enabled-assertion below waits out.
 */
export async function register(page: Page, account: Account, password = E2E_PASSWORD): Promise<void> {
  await page.goto('/register');
  await page.getByTestId('register-email').fill(account.email);
  // Feature 003: a unique, immutable handle is claimed at registration.
  await page.getByTestId('register-handle').fill(account.handle);
  await expect(page.getByTestId('handle-available')).toBeVisible();
  await page.getByTestId('register-password').fill(password);
  await page.getByTestId('register-confirm-password').fill(password);
  await page.getByTestId('register-accept-terms').check();
  await expect(page.getByTestId('register-submit')).toBeEnabled();
  await page.getByTestId('register-submit').click();
  await expect(page.getByTestId('register')).toContainText(/check your email/i);
}

/** Register, then follow the emailed verification link. Leaves the account signed out. */
export async function registerVerify(
  page: Page,
  request: APIRequestContext,
  account: Account,
  password = E2E_PASSWORD,
): Promise<void> {
  await register(page, account, password);
  await page.goto(await verifyLinkPath(request, account.email));
  // Assert the outcome rather than just the navigation: every caller depends on the account
  // actually being verified, and a silent failure here would surface much later as a confusing
  // "cannot sign in" further down whichever spec called us.
  await expect(page.getByTestId('verify-email')).toContainText(/verified/i);
}

/**
 * Sign in an existing account. Asserts only that sign-in was left behind — the destination
 * depends on whether onboarding has been completed, so a stricter assertion would be wrong for
 * one of the two cases.
 */
export async function signIn(page: Page, email: string, password = E2E_PASSWORD): Promise<void> {
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(email);
  await page.getByTestId('sign-in-password').fill(password);
  await page.getByTestId('sign-in-submit').click();
  await expect(page).toHaveURL((u) => !u.pathname.includes('/sign-in'));
}

/** Register → verify → sign in. Returns the account's email + handle. */
export async function registerVerifySignIn(
  page: Page,
  request: APIRequestContext,
  prefix = 'e2e',
): Promise<Account> {
  const account = newAccount(prefix);
  await registerVerify(page, request, account);
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(account.email);
  await page.getByTestId('sign-in-password').fill(E2E_PASSWORD);
  await page.getByTestId('sign-in-submit').click();
  // A brand-new account always lands in onboarding (feature 004). Waiting for that redirect —
  // rather than just "not /sign-in" — also guarantees the session cookie is set before the
  // caller navigates on, otherwise the authGuard bounces it straight back to /sign-in.
  await expect(page).toHaveURL(/onboarding/);
  return account;
}

/** Register → verify → sign in → dismiss onboarding, leaving `page` inside the app. */
export async function registerAndEnter(
  page: Page,
  request: APIRequestContext,
  prefix = 'e2e',
): Promise<Account> {
  const account = await registerVerifySignIn(page, request, prefix);
  await page.getByTestId('onboarding-dismiss').click();
  await expect(page).not.toHaveURL(/onboarding/);
  return account;
}

/** Sign in as the configured admin, registering + verifying it once if it doesn't exist yet. */
export async function ensureAdminSignedIn(page: Page, request: APIRequestContext): Promise<void> {
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(ADMIN_EMAIL);
  await page.getByTestId('sign-in-password').fill(E2E_PASSWORD);
  await page.getByTestId('sign-in-submit').click();
  await page.waitForTimeout(1500);
  if (page.url().includes('/sign-in')) {
    await registerVerify(page, request, {
      email: ADMIN_EMAIL,
      handle: `admin-${Date.now().toString(36)}`,
    });
    await signIn(page, ADMIN_EMAIL);
  }
}
