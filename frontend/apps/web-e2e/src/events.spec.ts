import { expect, test } from '@playwright/test';
import { registerAndEnter, toPath } from './support/auth';

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

  // 5. Organiser cancels the event from the settings danger zone.
  await orgPage.goto(`${toPath(eventUrl)}/edit`);
  await orgPage.getByTestId('cancel-open').click();
  await orgPage.getByTestId('cancel-confirm').click();
  await expect(orgPage.getByTestId('event-cancelled')).toBeVisible();

  // 6. The participant now sees the cancelled state (no join action).
  await partPage.reload();
  await expect(partPage.getByTestId('event-cancelled')).toBeVisible();

  await organiser.close();
  await participant.close();
});
