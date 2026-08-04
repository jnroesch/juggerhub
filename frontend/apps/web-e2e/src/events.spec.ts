import { expect, test } from '@playwright/test';
import { registerAndEnter, toPath } from './support/auth';
import { clearCity, pickCity } from './support/city';

/**
 * Feature 006 end-to-end: an organiser creates an event through the guided wizard,
 * a second user signs up, the organiser posts news and cancels — proving the core
 * loop and the admin surface across two browser contexts. Runs at desktop + mobile
 * projects. Requires the local docker stack (backend + Mailpit + frontend).
 */

test('create via wizard → view → sign up (2nd user) → post news → cancel', async ({ browser, request }) => {
  const organiser = await browser.newContext();
  const orgPage = await organiser.newPage();
  await registerAndEnter(orgPage, request, 'evorg');

  // 1. Create a free individuals-only virtual event through the wizard.
  await orgPage.goto('/events/new');
  await orgPage.getByTestId('event-name').fill('E2E Open Day');
  await orgPage.getByTestId('event-description').fill('An end-to-end test event.');
  await orgPage.getByTestId('event-next').click(); // → when
  await orgPage.getByTestId('event-starts').fill('2026-10-01T10:00');
  await orgPage.getByTestId('event-ends').fill('2026-10-01T16:00');
  await orgPage.getByTestId('event-next').click(); // → where
  await orgPage.getByTestId('loc-virtual').click();
  await orgPage.getByTestId('event-link').fill('https://zoom.us/j/999');
  await orgPage.getByTestId('event-next').click(); // → who
  await orgPage.getByTestId('mode-individuals').click();
  await orgPage.getByTestId('event-limit').fill('5');
  await orgPage.getByTestId('event-next').click(); // → fee (Free is the default)
  await orgPage.getByTestId('event-next').click(); // → review
  await orgPage.getByTestId('event-publish').click();

  // 2. Lands on the event page as admin (the Manage-event menu is present).
  await expect(orgPage.getByTestId('event-detail')).toContainText('E2E Open Day');
  await expect(orgPage.getByTestId('manage-menu')).toBeVisible();
  const eventUrl = orgPage.url();

  // 3. Organiser posts a news update.
  await orgPage.getByTestId('news-input').fill('First whistle 10:00 sharp.');
  await orgPage.getByTestId('news-post').click();
  await expect(orgPage.getByTestId('event-detail')).toContainText('First whistle 10:00 sharp.');

  // 4. A second user signs up.
  const participant = await browser.newContext();
  const partPage = await participant.newPage();
  await registerAndEnter(partPage, request, 'evpart');
  await partPage.goto(toPath(eventUrl));
  await expect(partPage.getByTestId('event-detail')).toContainText('E2E Open Day');
  await partPage.getByTestId('join').click();
  await expect(partPage.getByTestId('my-status')).toBeVisible();

  // 5. Organiser fixes the join link — for a virtual event that link IS the location, and a
  //    replaced one used to mean cancelling and recreating the event (GH #136).
  await orgPage.goto(`${toPath(eventUrl)}/edit`);
  await orgPage.getByTestId('edit-link').fill('https://meet.google.com/abc-defg');
  await orgPage.getByTestId('edit-save').click();
  await expect(orgPage.getByTestId('edit-saved')).toBeVisible();
  await orgPage.goto(toPath(eventUrl));
  await expect(orgPage.getByTestId('event-detail')).toContainText('meet.google.com/abc-defg');

  // 6. Organiser cancels the event from the settings danger zone.
  await orgPage.goto(`${toPath(eventUrl)}/edit`);
  await orgPage.getByTestId('cancel-open').click();
  await orgPage.getByTestId('cancel-confirm').click();
  await expect(orgPage.getByTestId('event-cancelled')).toBeVisible();

  // 7. The participant now sees the cancelled state (no join action).
  await partPage.reload();
  await expect(partPage.getByTestId('event-cancelled')).toBeVisible();

  await organiser.close();
  await participant.close();
});

/**
 * GH #136 — an event's city had no editor at all: the settings form re-sent the stored city and
 * rendered no picker, so an event could be created in the wrong city and never moved. This walks
 * the whole round trip because that is where it broke — the request the form builds is the bug.
 */
test('an organiser moves a published event to another city', async ({ page, request }) => {
  await registerAndEnter(page, request, 'evmove');

  // 1. Create an in-person event in Köln.
  await page.goto('/events/new');
  await page.getByTestId('event-name').fill('E2E Relocation Cup');
  await page.getByTestId('event-description').fill('An event that moves.');
  await page.getByTestId('event-next').click(); // → when
  await page.getByTestId('event-starts').fill('2026-10-01T10:00');
  await page.getByTestId('event-ends').fill('2026-10-01T16:00');
  await page.getByTestId('event-next').click(); // → where (in person is the default)

  // The step stays blocked until the whole address is there — a street alone is not one.
  const next = page.getByTestId('event-next');
  await page.getByTestId('event-street').fill('Aachener Str. 999');
  await page.getByTestId('event-postal').fill('50933');
  await expect(next).toBeDisabled();

  // Pick explicitly: the search ranks "Kolno, Poland" above Köln for this query.
  await pickCity(page, 'event-city', 'Köln', /Köln, Germany/);
  await expect(next).toBeEnabled();
  await next.click(); // → who
  await page.getByTestId('event-next').click(); // → fee
  await page.getByTestId('event-next').click(); // → review
  await page.getByTestId('event-publish').click();

  await expect(page.getByTestId('event-detail')).toContainText('Köln');
  const eventUrl = page.url();

  // 2. Move it to Berlin from the settings form.
  await page.goto(`${toPath(eventUrl)}/edit`);
  await expect(page.getByTestId('edit-street')).toHaveValue('Aachener Str. 999');
  await page.getByTestId('edit-street').fill('Hauptstr 1');
  await page.getByTestId('edit-postal').fill('10115');

  // Changing a city is clear-then-search: the loaded city renders as a chip over the search input.
  await clearCity(page, 'edit-city');
  // An in-person event without a city can't be saved — the guard holds in the real (zoneless) app,
  // not just in the unit test that calls the computed directly.
  await expect(page.getByTestId('edit-save')).toBeDisabled();

  await pickCity(page, 'edit-city', 'Berlin', /Berlin, Germany/);
  await expect(page.getByTestId('edit-save')).toBeEnabled();
  await page.getByTestId('edit-save').click();
  await expect(page.getByTestId('edit-saved')).toBeVisible();

  // 3. The event page reads the new city — not the one it was created in.
  await page.goto(toPath(eventUrl));
  await expect(page.getByTestId('event-detail')).toContainText('Berlin');
  await expect(page.getByTestId('event-detail')).toContainText('Hauptstr 1');
  await expect(page.getByTestId('event-detail')).not.toContainText('Köln');
});
