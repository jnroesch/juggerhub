import { expect, test } from '@playwright/test';
import { registerVerifySignIn } from './support/auth';

/**
 * Feature 026 (US1/US3) — signed-out visitors cannot reach teams, events, or any browse view;
 * every attempt lands on the sign-in screen. This is the in-browser proof of the client guard;
 * the server-side 401 boundary is proven by the backend integration tests.
 */

const gatedPaths = [
  '/browse/teams',
  '/browse/events',
  '/browse/players',
  '/t/some-team',
  '/events/00000000-0000-0000-0000-000000000000',
];

test.describe('signed-out access to gated routes redirects to sign-in with a returnUrl', () => {
  for (const path of gatedPaths) {
    test(`${path} → sign-in carrying returnUrl`, async ({ page }) => {
      await page.context().clearCookies();
      await page.goto(path);
      await expect(page).toHaveURL(/sign-in/);
      await expect(page.getByTestId('sign-in-submit')).toBeVisible();
      // The originally-requested path is preserved so login can return the user there.
      const returnUrl = new URL(page.url()).searchParams.get('returnUrl');
      expect(returnUrl).toBe(path);
    });
  }
});

test('a private/unknown profile redirects a signed-out visitor to sign-in with a returnUrl', async ({ page }) => {
  await page.context().clearCookies();
  const handle = 'definitely-not-a-real-handle-xyz';
  // A handle that does not resolve publicly (unknown or private) must not reveal a profile; the
  // read-only view sends signed-out visitors to sign-in so they can view it after logging in.
  await page.goto(`/u/${handle}`);
  await expect(page).toHaveURL(/sign-in/);
  expect(new URL(page.url()).searchParams.get('returnUrl')).toBe(`/u/${handle}`);
  await expect(page.getByTestId('profile-public')).toHaveCount(0);
});

/**
 * Feature 036 — the INVERSE of everything above. The privacy policy and imprint are the two
 * documented exceptions to the authenticated-only rule (alongside the opt-in public profile):
 * they must render for a visitor with no session, because the reader who most needs a privacy
 * policy is the one still deciding whether to hand over an email address.
 *
 * These assertions live in this file on purpose. The rule and its exceptions belong together —
 * a future change that guards the legal routes should break a test that sits next to the tests
 * proving everything else IS guarded.
 */
test.describe('legal pages are reachable without a session (feature 036)', () => {
  const legalPaths = ['/privacy', '/imprint'];

  for (const path of legalPaths) {
    test(`${path} renders for a signed-out visitor, with no redirect`, async ({ page }) => {
      await page.context().clearCookies();
      await page.goto(path);

      // The URL must be untouched: no bounce to sign-in, no returnUrl (RC-3, SC-008).
      await expect(page).toHaveURL(new RegExp(`${path}$`));
      expect(new URL(page.url()).searchParams.get('returnUrl')).toBeNull();

      const doc = page.getByTestId(`legal-${path.slice(1)}`);
      await expect(doc).toBeVisible();
      // A rendered-but-empty legal page would pass a naive visibility check.
      await expect(doc.locator('section')).not.toHaveCount(0);
    });

    test(`${path} loads without issuing any backend request`, async ({ page }) => {
      // RC-2: a 401 from a session probe would trigger the refresh path and could redirect a
      // reader away from a page they are legally entitled to see. The pages call nothing.
      const apiCalls: string[] = [];
      page.on('request', (req) => {
        const url = new URL(req.url());
        if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/')) {
          apiCalls.push(url.pathname);
        }
      });

      await page.context().clearCookies();
      await page.goto(path);
      await expect(page.getByTestId(`legal-${path.slice(1)}`)).toBeVisible();

      expect(apiCalls).toEqual([]);
    });
  }

  test('both are one click away from the sign-in screen', async ({ page }) => {
    // FR-002 / SC-001. The auth screens sit outside the app shell, so they carry their own
    // inline links — and /register, where an email address is actually handed over, is the
    // placement that matters most.
    await page.context().clearCookies();

    for (const [testId, expected] of [
      ['legal-link-privacy', '/privacy'],
      ['legal-link-imprint', '/imprint'],
    ] as const) {
      await page.goto('/sign-in');
      await page.getByTestId(testId).click();
      await expect(page).toHaveURL(new RegExp(`${expected}$`));
    }

    await page.goto('/register');
    await expect(page.getByTestId('legal-link-privacy')).toBeVisible();
  });

  test('the table of contents anchors within the page instead of bouncing to sign-in', async ({ page }) => {
    // Regression. The app sets <base href="/">, so a bare href="#section" resolved against the
    // BASE url, not the current one — every contents entry navigated to `/`, which is auth-guarded,
    // and threw a signed-out reader onto the sign-in screen from a public page.
    await page.context().clearCookies();
    await page.goto('/privacy');

    const toc = page.getByTestId('legal-toc');
    await expect(toc).toBeVisible();
    await toc.getByRole('link').first().click();

    await expect(page).toHaveURL(/\/privacy#privacy-controller$/);
    await expect(page.getByTestId('legal-privacy')).toBeVisible();
    // The section it names must actually be scrolled to, not merely present in the URL.
    await expect(page.locator('#privacy-controller')).toBeInViewport();
  });
});

/**
 * Feature 036, owner decision 2026-08-01: the legal links follow a signed-out visitor everywhere,
 * because they are the reader a privacy policy exists for. A signed-in member has already made
 * that call, so the links move to the account page rather than sitting on every screen.
 */
test.describe('legal links move to the account page once signed in', () => {
  test('the shell footer is gone and the account page carries the links', async ({ page, request }) => {
    await registerVerifySignIn(page, request);
    await page.goto('/browse/teams');

    await expect(page.getByTestId('app-footer')).toHaveCount(0);

    await page.goto('/account');
    const links = page.getByTestId('account').getByTestId('legal-links');
    await expect(links.getByTestId('legal-link-privacy')).toBeVisible();

    await links.getByTestId('legal-link-imprint').click();
    await expect(page).toHaveURL(/\/imprint$/);
    await expect(page.getByTestId('legal-imprint')).toBeVisible();
  });
});

/**
 * SC-007 / PC-2 — long-form text at the narrowest width we support. A document page that scrolls
 * sideways is unreadable exactly where reading matters most.
 */
test.describe('legal pages at 320px', () => {
  test.use({ viewport: { width: 320, height: 640 } });

  for (const path of ['/privacy', '/imprint']) {
    test(`${path} does not scroll horizontally`, async ({ page }) => {
      await page.context().clearCookies();
      await page.goto(path);
      await expect(page.getByTestId(`legal-${path.slice(1)}`)).toBeVisible();

      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      );
      expect(overflow).toBeLessThanOrEqual(0);
    });
  }
});
