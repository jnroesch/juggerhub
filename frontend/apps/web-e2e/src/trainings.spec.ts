import { expect, test } from '@playwright/test';
import { registerAndEnter } from './support/auth';
import { pickCity } from './support/city';

/**
 * Feature 042 end-to-end: a team admin schedules an in-person training with a structured address.
 *
 * The point of the test is the "Where" step — it must refuse to continue until a street, a postal
 * code AND a resolved city are present (a venue name alone is not an address), and the created
 * training must render a city-anchored location afterwards.
 *
 * Requires the local docker stack (backend + Mailpit + frontend).
 */

test('create an in-person training with a structured address', async ({ page, request }) => {
  await registerAndEnter(page, request, 'tradm');

  // A team to own the training; the creator becomes its first admin.
  const suffix = `${Date.now()}`;
  await page.goto('/teams/new');
  await page.getByTestId('team-name').fill(`Trainings E2E ${suffix}`);
  await page.getByTestId('team-slug').fill(`trainings-e2e-${suffix}`);
  await page.getByTestId('type-city').click();
  await pickCity(page, 'team-city', 'Köln');
  await page.getByTestId('team-create-submit').click();
  await expect(page).toHaveURL(new RegExp(`/t/trainings-e2e-${suffix}`));

  // 1. Step 1 — a one-off, so the schedule step needs only a single date.
  await page.goto(`/t/trainings-e2e-${suffix}/trainings/new`);
  await page.getByTestId('training-oneoff').click();
  await page.getByTestId('training-name').fill('E2E structured location');
  await page.getByTestId('training-next-1').click();

  // 2. Step 2 — when.
  await page.getByTestId('training-start-date').fill('2026-11-14');
  await page.getByTestId('training-next-2').click();

  // 3. Step 3 — where. In person is the default; Continue stays blocked until the whole
  //    address is there. A venue name on its own must NOT unlock it (spec edge case).
  const next3 = page.getByTestId('training-next-3');
  await expect(next3).toBeDisabled();

  await page.getByTestId('training-venue').fill('Sportpark Müngersdorf');
  await expect(next3).toBeDisabled();

  await page.getByTestId('training-street').fill('Aachener Str. 999');
  await page.getByTestId('training-postal').fill('50933');
  // Street + postal but still no city — the last thing standing between here and Continue.
  await expect(next3).toBeDisabled();

  // Pick explicitly: the search ranks "Kolno, Poland" above Köln for this query, and the whole
  // point of a canonical city is that the admin chooses which one they meant.
  await pickCity(page, 'training-city', 'Köln', /Köln, Germany/);
  await expect(next3).toBeEnabled();
  await next3.click();

  // 4. Step 4 — visibility (team-only default), then review.
  await page.getByTestId('training-next-4').click();

  // 5. Review shows the address back before anything is committed.
  await expect(page.getByTestId('review-city')).toContainText('Köln');
  await expect(page.getByTestId('review-address')).toContainText('Sportpark Müngersdorf');
  await expect(page.getByTestId('review-address')).toContainText('Aachener Str. 999');
  await expect(page.getByTestId('review-address')).toContainText('50933');

  await page.getByTestId('training-create-submit').click();

  // 6. Lands on the session page, showing the city-anchored label plus the full address.
  await expect(page.getByTestId('session-location')).toHaveText('Köln');
  await expect(page.getByTestId('session-address')).toContainText('Sportpark Müngersdorf');
  await expect(page.getByTestId('session-address')).toContainText('50933');
});

test('a virtual training asks for no address at all', async ({ page, request }) => {
  await registerAndEnter(page, request, 'trvirt');

  const suffix = `${Date.now()}`;
  await page.goto('/teams/new');
  await page.getByTestId('team-name').fill(`Virtual E2E ${suffix}`);
  await page.getByTestId('team-slug').fill(`virtual-e2e-${suffix}`);
  await page.getByTestId('type-city').click();
  await pickCity(page, 'team-city', 'Berlin');
  await page.getByTestId('team-create-submit').click();

  await page.goto(`/t/virtual-e2e-${suffix}/trainings/new`);
  await page.getByTestId('training-oneoff').click();
  await page.getByTestId('training-name').fill('E2E virtual training');
  await page.getByTestId('training-next-1').click();
  await page.getByTestId('training-start-date').fill('2026-11-21');
  await page.getByTestId('training-next-2').click();

  // Switching to virtual removes the address group entirely — it is not merely disabled (FR-003).
  await expect(page.getByTestId('training-street')).toBeVisible();
  await page.getByText('Virtual', { exact: true }).click();
  await expect(page.getByTestId('training-venue')).toHaveCount(0);
  await expect(page.getByTestId('training-street')).toHaveCount(0);
  await expect(page.getByTestId('training-postal')).toHaveCount(0);
  await expect(page.getByTestId('training-city')).toHaveCount(0);

  // A join link is the only thing "Where" needs for a virtual training: Continue stays blocked
  // until one is typed, then unlocks (regression — the field is a signal so the computed re-runs).
  const next3 = page.getByTestId('training-next-3');
  await expect(next3).toBeDisabled();
  await page.getByTestId('training-join-link').fill('https://www.meet.com/e2e');
  await expect(next3).toBeEnabled();
  await next3.click();

  // Visibility → review shows the training as online (no address), then create it.
  await page.getByTestId('training-next-4').click();
  await expect(page.getByTestId('review-address')).toHaveCount(0);
  await page.getByTestId('training-create-submit').click();

  // Lands on the session page for the created virtual training.
  await expect(page).toHaveURL(/\/trainings\/sessions\//);
});
