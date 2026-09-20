import { expect, test } from '@playwright/test';
import { registerVerifySignIn } from './support/auth';

/**
 * US3 — the shell stays usable at both the desktop and mobile projects
 * (playwright.config.mts): primary navigation is reachable and there is no
 * unintended horizontal scrolling / clipped content (FR-025, SC-009).
 *
 * `/browse` is inside the app shell and authenticated-only since feature 026, so each test signs
 * in first, then the shell renders the real navigation chrome. Feature 008 replaced the pre-001
 * sidebar + off-canvas `menu-toggle` with a persistent top-nav (desktop) and a fixed
 * bottom tab bar (mobile), so navigation is anchored on those instead.
 */
test.beforeEach(async ({ page, request }) => {
  await registerVerifySignIn(page, request);
});
test('the shell renders with no horizontal overflow', async ({ page }) => {
  await page.goto('/browse');

  // The shell chrome renders (the account button sits in the top strip at every
  // viewport; the desktop nav links are hidden below md).
  await expect(page.getByTestId('avatar-menu-button')).toBeVisible();

  // No unintended horizontal scrolling at this viewport.
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(1);
});

test('primary navigation is reachable', async ({ page }, testInfo) => {
  await page.goto('/browse');

  if (testInfo.project.name === 'mobile-chrome') {
    // On mobile the primary destinations live in the fixed bottom tab bar; the
    // top-nav destinations are hidden (md:flex).
    const bottomNav = page.getByTestId('bottom-nav');
    await expect(bottomNav).toBeVisible();
    await expect(bottomNav.getByTestId('tab-home')).toBeVisible();
    await expect(bottomNav.getByTestId('tab-browse')).toBeVisible();
  } else {
    // On desktop the primary destinations live in the persistent top nav.
    await expect(page.getByTestId('nav-home')).toBeVisible();
    await expect(page.getByTestId('nav-browse')).toBeVisible();
  }
});

/**
 * The same no-overflow assertion, in German, across the app's main authenticated routes.
 *
 * German runs 30–40% past the English for a sentence and can double a short label, so it is
 * where this product's overflows actually appear — every one shipped so far was found in German
 * at 375px, by hand, after the code was written. The test above proved the shell at one route in
 * English, which is the one combination that was never going to fail.
 *
 * `jh.lang` is seeded before the app boots rather than clicked through the switcher: the
 * precedence in `LanguageService` is account preference → stored choice → browser, and a fresh
 * e2e account has no preference, so the stored choice wins. The `lang` assertion is load-bearing
 * — without it a seeding regression would leave every route quietly passing in English.
 *
 * The widths are set here rather than taken from the project, because the two project viewports
 * (1280, and the mobile device preset) are not the two that DESIGN.md calls binding. 375 is the
 * narrow phone, and 768 is `md` — the breakpoint where stacked cards become a grid and a German
 * column header has the least room it will ever have. So the block runs once, on the desktop
 * project, and drives the viewport itself; running it again under device emulation would repeat
 * the same layout assertion for the same widths.
 *
 * This is the mechanical half only. Truncation and clipped-but-not-scrolling text stay with the
 * owner's walk, because telling a deliberate ellipsis from a broken label needs eyes.
 */
const GERMAN_ROUTES = ['/', '/browse/teams', '/browse/players', '/browse/trainings', '/account'];

/** 375 = narrow phone, 768 = `md`, 1280 = desktop. The first two are DESIGN.md's binding cases. */
const GERMAN_WIDTHS = [375, 768, 1280];

test.describe('German text expansion', () => {
  test.beforeEach(async ({ page }, testInfo) => {
    // This block drives its own viewports, so running it again under device emulation would
    // repeat the same three widths. Skipped in the hook rather than with a `test.skip(fn)`
    // callback, whose `({}, testInfo)` signature is an empty destructuring pattern that lint
    // rejects outright.
    test.skip(
      testInfo.project.name !== 'desktop-chromium',
      'drives its own viewports; device emulation would repeat the same widths',
    );

    await page.addInitScript(() => window.localStorage.setItem('jh.lang', 'de'));
  });

  for (const width of GERMAN_WIDTHS) {
    test(`no horizontal overflow in German at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });

      for (const route of GERMAN_ROUTES) {
        await test.step(route, async () => {
          await page.goto(route);

          // The app really is in German — otherwise this is the English test with more steps.
          await expect(page.locator('html')).toHaveAttribute('lang', 'de');

          const overflow = await page.evaluate(
            () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
          );
          expect(overflow, `${route} overflows horizontally in German at ${width}px`)
            .toBeLessThanOrEqual(1);
        });
      }
    });
  }
});
