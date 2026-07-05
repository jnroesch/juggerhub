import { APIRequestContext, Page, expect, test } from '@playwright/test';

/**
 * Feature 006 end-to-end: an organiser creates an event through the guided wizard,
 * a second user signs up, the organiser posts news and cancels — proving the core
 * loop and the admin surface across two browser contexts. Runs at desktop + mobile
 * projects. Requires the local docker stack (backend + Mailpit + frontend).
 */

const MAILPIT = process.env['MAILPIT_URL'] || 'http://mailpit:8025';
const PASSWORD = 'Str0ng!Passw0rd';

/** Polls Mailpit for the newest message to `to` whose body contains a `/{path}?…` link. */
async function linkFromEmail(request: APIRequestContext, to: string, path: string): Promise<string> {
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
        if (match) return match[0];
      }
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error(`No '${path}' email for ${to} appeared in Mailpit.`);
}

function toPath(url: string): string {
  const parsed = new URL(url);
  return parsed.pathname + parsed.search;
}

/** Register → verify (via Mailpit) → sign in → dismiss onboarding, leaving `page` in the app. */
async function registerAndEnter(page: Page, request: APIRequestContext): Promise<string> {
  const unique = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  const email = `e2e-ev-${unique}@example.com`;

  await page.goto('/register');
  await page.getByTestId('register-email').fill(email);
  await page.getByTestId('register-handle').fill(`evp-${unique}`);
  await expect(page.getByTestId('handle-available')).toBeVisible();
  await page.getByTestId('register-password').fill(PASSWORD);
  await page.getByTestId('register-confirm-password').fill(PASSWORD);
  await page.getByTestId('register-submit').click();
  await expect(page.getByTestId('register')).toContainText(/check your email/i);

  const verify = await linkFromEmail(request, email, 'verify-email');
  await page.goto(toPath(verify));

  await page.goto('/sign-in');
  await page.getByTestId('sign-in-email').fill(email);
  await page.getByTestId('sign-in-password').fill(PASSWORD);
  await page.getByTestId('sign-in-submit').click();

  await expect(page).toHaveURL(/onboarding/);
  await page.getByTestId('onboarding-dismiss').click();
  await expect(page).not.toHaveURL(/onboarding/);
  return email;
}

test('create via wizard → view → sign up (2nd user) → post news → cancel', async ({ browser, request }) => {
  const organiser = await browser.newContext();
  const orgPage = await organiser.newPage();
  await registerAndEnter(orgPage, request);

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
  await registerAndEnter(partPage, request);
  await partPage.goto(toPath(eventUrl));
  await expect(partPage.getByTestId('event-detail')).toContainText('E2E Open Day');
  await partPage.getByTestId('join').click();
  await expect(partPage.getByTestId('my-status')).toBeVisible();

  // 5. Organiser cancels the event from the manage danger zone.
  await orgPage.goto(`${toPath(eventUrl)}/manage`);
  await orgPage.getByTestId('cancel-open').click();
  await orgPage.getByTestId('cancel-confirm').click();
  await expect(orgPage.getByTestId('event-cancelled')).toBeVisible();

  // 6. The participant now sees the cancelled state (no join action).
  await partPage.reload();
  await expect(partPage.getByTestId('event-cancelled')).toBeVisible();

  await organiser.close();
  await participant.close();
});
