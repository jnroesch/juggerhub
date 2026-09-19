import { expect, test } from '@playwright/test';
import { pickCity } from './support/city';
import { E2E_PASSWORD, newAccount, registerAndEnter, registerVerifySignIn, toPath, verifyLinkPath } from './support/auth';
import { createTeam } from './support/team';

/**
 * Feature 004 end-to-end: a freshly-verified user's first sign-in is routed into
 * the guided onboarding flow; completing it lands them in the app; and a later
 * sign-in goes straight to the app (shown once). Runs at desktop + mobile projects.
 */

test('first login opens onboarding; completing it lands in the app and it is shown only once', async ({
  page,
  request,
}) => {
  // 1. Register + verify + first sign-in, which is routed into onboarding (not the app).
  const { email, handle } = await registerVerifySignIn(page, request, 'onb');
  await expect(page.getByTestId('onboarding')).toContainText(/Welcome to Jugger/i);

  // 3. Walk the flow: name (prefilled with the handle) → city → pompfen → team stub → photo+bio.
  await page.getByTestId('onboarding-start').click();
  await expect(page.getByTestId('onboarding-name')).toHaveValue(handle);
  await page.getByTestId('onboarding-name').fill('E2E Player');
  await page.getByTestId('onboarding-continue').click(); // → city

  await pickCity(page, 'onboarding-city', 'Berlin');
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
  await page.getByTestId('sign-in-password').fill(E2E_PASSWORD);
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

/**
 * Feature 053: a shared invite link survives registration, the verification email and sign-in,
 * and the onboarding team step leads with it. The invitee runs in a second browser context —
 * their own browser, as far as cookies go — and the verification link is read from Mailpit,
 * which is exactly the hop that used to drop the invite.
 */
test('an invite link survives registration and the team step offers it', async ({ page, browser, request }) => {
  // 1. A team admin creates a team (through the 052 wizard) and its shared invite link.
  await registerAndEnter(page, request, 'inv-admin');
  const suffix = `${Date.now()}`;
  const slug = `invite-e2e-${suffix}`;
  const teamName = `Invite E2E ${suffix}`;
  await createTeam(page, { name: teamName, slug, city: 'Köln' });

  await page.goto(`/t/${slug}/invitations`);
  await page.getByTestId('create-link').click();
  const inviteUrl = (await page.getByTestId('invite-link').textContent())?.trim() ?? '';
  const invitePath = toPath(inviteUrl);
  expect(invitePath).toMatch(new RegExp(`^/join/${slug}/`));

  // 2. Someone with no account opens the link, presses Accept, and is sent to register.
  const context = await browser.newContext();
  const invitee = await context.newPage();
  const account = newAccount('invitee');
  await invitee.goto(invitePath);
  await expect(invitee.getByTestId('invite-accept')).toContainText(teamName);
  await invitee.getByTestId('accept-join').click();
  await expect(invitee).toHaveURL(/sign-in\?returnUrl=/);
  await invitee.getByRole('link', { name: /create an account/i }).click();
  await expect(invitee).toHaveURL(/register\?returnUrl=/);
  await invitee.getByTestId('register-email').fill(account.email);
  await invitee.getByTestId('register-handle').fill(account.handle);
  await expect(invitee.getByTestId('handle-available')).toBeVisible();
  await invitee.getByTestId('register-password').fill(E2E_PASSWORD);
  await invitee.getByTestId('register-confirm-password').fill(E2E_PASSWORD);
  await invitee.getByTestId('register-accept-terms').check();
  await expect(invitee.getByTestId('register-submit')).toBeEnabled();
  await invitee.getByTestId('register-submit').click();
  await expect(invitee.getByTestId('register')).toContainText(/check your email/i);

  // 3. The emailed verification link carries the invite, and the verify page's Sign in carries
  //    it on as the returnUrl sign-in already knows how to honour.
  const verifyPath = await verifyLinkPath(request, account.email);
  expect(verifyPath).toContain(`inviteSlug=${slug}`);
  expect(verifyPath).toContain('inviteToken=');
  await invitee.goto(verifyPath);
  await expect(invitee.getByTestId('verify-email')).toContainText(/verified/i);
  await invitee.getByTestId('verify-success-signin').click();
  await expect(invitee).toHaveURL(/sign-in\?returnUrl=/);
  await invitee.getByTestId('sign-in-email').fill(account.email);
  await invitee.getByTestId('sign-in-password').fill(E2E_PASSWORD);
  await invitee.getByTestId('sign-in-submit').click();
  await expect(invitee).toHaveURL(/onboarding\?returnUrl=/);

  // 4. The team step leads with the invitation; Accept joins immediately; the search stays.
  await invitee.getByTestId('onboarding-start').click();
  await invitee.getByTestId('onboarding-continue').click(); // name (prefilled) → city
  await invitee.getByTestId('onboarding-skip').click(); // city → pompfen
  await invitee.getByTestId('onboarding-skip').click(); // pompfen → team
  await expect(invitee.getByTestId('onboarding-invite')).toContainText(teamName);
  await expect(invitee.getByTestId('onboarding-team-search')).toBeVisible();
  await invitee.getByTestId('onboarding-invite-accept').click();
  await expect(invitee.getByTestId('onboarding-invite-joined')).toContainText(teamName);
  await expect(invitee.getByTestId('onboarding-invite-accept')).toHaveCount(0);

  // 5. Finishing onboarding lands on the joined team, not the dashboard.
  await invitee.getByTestId('onboarding-continue').click(); // team → photo
  await invitee.getByTestId('onboarding-finish').click();
  await expect(invitee.getByTestId('onboarding')).toContainText(/all set/i);
  await invitee.getByTestId('onboarding-enter').click();
  await expect(invitee).toHaveURL(new RegExp(`/t/${slug}$`));
  await context.close();
});
