import { expect, test } from '@playwright/test';
import { resolveCityExternalId } from './support/city';
import { E2E_PASSWORD, ensureAdminSignedIn, newAccount, registerVerify, signIn } from './support/auth';

/**
 * Feature 013 end-to-end: the gated admin area. Covers the lock-marked entry (admins
 * only), the overview → users → player-detail path, the account-help lifecycle
 * (suspend blocks sign-in with a clear message; reinstate restores it), and the
 * Assign picker on the player detail. The admin account is `admin@test.de` (the
 * stack's ADMIN_EMAILS — designated at registration/startup by the role sync).
 */

const uniquePlayer = () => newAccount('e2eadm');

test('non-admins never see the admin entry and /admin bounces them home', async ({ page, request }) => {
  const player = uniquePlayer();
  const { email, handle } = player;
  await registerVerify(page, request, player);
  await signIn(page, email);

  await page.goto('/');
  await page.getByTestId('avatar-menu-button').click();
  await expect(page.getByTestId('avatar-menu')).toBeVisible();
  await expect(page.getByTestId('admin-link')).toHaveCount(0);

  await page.goto('/admin');
  await expect(page).toHaveURL((u) => !u.pathname.startsWith('/admin'));
});

test('admin: gated entry → overview → find player → suspend blocks sign-in → reinstate restores', async ({ page, request }) => {
  const player = uniquePlayer();
  const { email, handle } = player;
  await registerVerify(page, request, player);

  // The gated entry lives in the avatar menu on every form factor (owner decision:
  // the top-nav item was dropped in favor of the single account-menu row).
  await ensureAdminSignedIn(page, request);
  await page.goto('/');
  await page.getByTestId('avatar-menu-button').click();
  await expect(page.getByTestId('admin-link')).toBeVisible();
  await page.getByTestId('admin-link').click();
  await expect(page.getByTestId('admin-overview-stats')).toBeVisible();

  // Search leads into user management with the query applied; the row opens the detail.
  await page.getByTestId('admin-overview-search').fill(handle);
  await page.getByTestId('admin-overview-search').press('Enter');
  await expect(page).toHaveURL((u) => u.pathname.endsWith('/admin/users'));
  // Desktop renders table rows, mobile renders cards — both carry the testid;
  // click whichever this viewport actually shows.
  await page.getByTestId('admin-users-row').filter({ visible: true }).first().click();
  await expect(page).toHaveURL((u) => u.pathname.endsWith(`/admin/users/${handle}`));
  await expect(page.getByTestId('admin-user-status')).toHaveText('Active');

  // Suspend (confirmed) → status flips.
  page.on('dialog', (d) => d.accept());
  await page.getByTestId('admin-action-suspend').click();
  await expect(page.getByTestId('admin-user-status')).toHaveText('Suspended');

  // The suspended player is refused sign-in with a clear message.
  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(email);
  await page.getByTestId('sign-in-password').fill(E2E_PASSWORD);
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

// The player assign/revoke round trip (grant from the player detail with a note → shows on
// the public profile → revoke removes it) lives in recognition.spec.ts, which drives the
// same shared Assign picker.

test('admin: catalogue — create, edit, retire, then reinstate a badge type (feature 014)', async ({ page, request }) => {
  await ensureAdminSignedIn(page, request);
  await page.goto('/admin/catalogue');
  await expect(page.getByTestId('catalogue-new')).toBeVisible();

  const name = `e2e badge ${Date.now().toString(36)}`;
  const row = () => page.getByTestId('catalogue-row').filter({ hasText: name }).filter({ visible: true }).first();

  // Create a players badge.
  await page.getByTestId('catalogue-new').click();
  await expect(page.getByTestId('catalogue-form')).toBeVisible();
  await page.getByTestId('catalogue-form-name').fill(name);
  await page.getByTestId('catalogue-form-description').fill('Created by e2e.');
  await page.getByTestId('catalogue-form-save').click();
  await expect(page.getByTestId('catalogue-form')).toHaveCount(0);
  // The list renders a desktop table and a mobile card list at once (only CSS hides
  // one), so the name appears twice in the DOM — scope to the visible row.
  await expect(row()).toBeVisible();

  // Edit its description.
  await row().getByTestId('catalogue-edit').click();
  await expect(page.getByTestId('catalogue-form')).toBeVisible();
  await page.getByTestId('catalogue-form-description').fill('Edited by e2e.');
  await page.getByTestId('catalogue-form-save').click();
  await expect(page.getByTestId('catalogue-form')).toHaveCount(0);

  // Retire (amber confirm, reversible).
  await row().getByTestId('catalogue-retire').click();
  await expect(page.getByTestId('catalogue-retire-modal')).toBeVisible();
  await page.getByTestId('catalogue-retire-confirm').click();
  await expect(page.getByTestId('catalogue-retire-modal')).toHaveCount(0);

  // Under the Retired filter it appears with Reinstate; reinstate it.
  await page.getByTestId('catalogue-filter-retired').click();
  await expect(row()).toBeVisible();
  await row().getByTestId('catalogue-reinstate').click();

  // It is active again.
  await page.getByTestId('catalogue-filter-active').click();
  await expect(row()).toBeVisible();
});

test('admin: assign a badge to a team, see it on the public page, then revoke it (feature 014)', async ({ page, request }) => {
  // A player creates a team (creator is an auto-member).
  const player = uniquePlayer();
  const { email, handle } = player;
  await registerVerify(page, request, player);
  await signIn(page, email);
  const slug = `e2eteam${Date.now().toString(36)}`.slice(0, 18);
  // Feature 030: teams take a structured city selection, not a freeform string. Resolve a real
  // reference id via the search endpoint (using the signed-in session's request context).
  const cityExternalId = await resolveCityExternalId(page.request, 'Berlin');
  const created = await page.request.post('/api/v1/teams', {
    data: { name: `E2E Team ${slug}`, slug, type: 'CityTeam', location: { cityExternalId, name: 'Berlin' } },
  });
  expect(created.ok()).toBeTruthy();

  // Admin creates a team-applicable badge.
  await ensureAdminSignedIn(page, request);
  await page.goto('/admin/catalogue');
  const badgeName = `e2e team badge ${Date.now().toString(36)}`;
  await page.getByTestId('catalogue-new').click();
  await page.getByTestId('catalogue-form-name').fill(badgeName);
  await page.getByTestId('catalogue-form-description').fill('Team award e2e.');
  await page.getByTestId('catalogue-form-teams').check();
  await page.getByTestId('catalogue-form-save').click();
  await expect(page.getByTestId('catalogue-form')).toHaveCount(0);

  // Teams → search → open the team → assign the badge.
  await page.getByTestId('admin-nav-teams').filter({ visible: true }).first().click();
  await expect(page).toHaveURL((u) => u.pathname.endsWith('/admin/teams'));
  await page.getByTestId('admin-teams-search').fill(slug);
  // Search is debounced and the DB holds teams from other specs, so scope the click to the
  // row for this team (name contains the slug) — this also waits out the debounce/reload
  // instead of clicking whatever row was showing before results narrowed.
  await page.getByTestId('admin-teams-row').filter({ hasText: slug }).filter({ visible: true }).first().click();
  await expect(page).toHaveURL((u) => u.pathname.endsWith(`/admin/teams/${slug}`));

  await page.getByTestId('assign').click();
  await expect(page.getByTestId('assign-modal')).toBeVisible();
  await page.getByTestId('assign-modal').getByRole('button', { name: badgeName }).click();
  await page.getByTestId('grant-submit').click();
  await expect(page.getByTestId('assign-modal')).toHaveCount(0);
  await expect(page.getByTestId('team-awards')).toContainText(badgeName);

  // Visible on the public team page.
  await page.goto(`/t/${slug}`);
  await expect(page.getByText(badgeName)).toBeVisible();

  // Revoke it from the admin team detail (revoke uses a confirm dialog).
  page.on('dialog', (d) => d.accept());
  await page.goto(`/admin/teams/${slug}`);
  await page.getByTestId('team-awards').getByRole('button', { name: 'Revoke' }).first().click();
  await expect(page.getByTestId('team-awards')).not.toContainText(badgeName);
});
