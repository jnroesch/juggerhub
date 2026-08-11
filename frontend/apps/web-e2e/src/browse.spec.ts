import { expect, test } from '@playwright/test';
import { registerVerifySignIn } from './support/auth';

/**
 * Search / Browse (feature 007). The three pages share one shell, so these checks assert the
 * common controls on all three, then exercise the filter panel, the no-results state, and the
 * players list note. Browse is authenticated-only since feature 026, so each test signs in first.
 * Runs at the desktop and mobile projects from playwright.config.mts (sheet on mobile, drawer on desktop).
 */

// Browse requires a session (feature 026) — sign in before each test.
test.beforeEach(async ({ page, request }) => {
  await registerVerifySignIn(page, request);
});

const pages = [
  { path: '/browse/teams', title: 'Teams' },
  { path: '/browse/events', title: 'Events' },
  { path: '/browse/trainings', title: 'Trainings' },
  { path: '/browse/players', title: 'Players' },
];

test.describe('browse shell is consistent across entities', () => {
  for (const { path, title } of pages) {
    test(`${title} page shows the shared shell controls`, async ({ page }) => {
      await page.goto(path);

      await expect(page.getByRole('heading', { name: title })).toBeVisible();
      await expect(page.getByTestId('browse-search')).toBeVisible();
      await expect(page.getByTestId('browse-filters-button')).toBeVisible();

      // Results, one of the four states, resolve (not a blank page).
      await expect(
        page.getByTestId('browse-results')
          .or(page.getByTestId('browse-empty'))
          .or(page.getByTestId('browse-no-results'))
          .or(page.getByTestId('browse-loading'))
          .first(),
      ).toBeVisible();
    });
  }
});

test('filters open on demand and can be applied', async ({ page }) => {
  await page.goto('/browse/teams');

  await page.getByTestId('browse-filters-button').click();
  const panel = page.getByTestId('filter-panel');
  await expect(panel).toBeVisible();

  await page.getByTestId('filter-apply').click();
  await expect(panel).toBeHidden();
});

test('a nonsense query shows the no-results state with a clear action', async ({ page }) => {
  await page.goto('/browse/teams');

  await page.getByTestId('browse-search').fill('zzz-no-such-team-qqq-xyz');
  const noResults = page.getByTestId('browse-no-results');
  await expect(noResults).toBeVisible();

  // Clearing restores results (or the empty state) — never a blank page.
  await noResults.getByRole('button').click();
  await expect(page.getByTestId('browse-no-results')).toBeHidden();
});

test('players page lists every player', async ({ page }) => {
  await page.goto('/browse/players');
  await expect(page.getByText(/every player on JuggerHub is listed/i)).toBeVisible();
});

/**
 * Public trainings (feature 043). The tab strip carries four destinations from here, and the home
 * empty state's "Browse open trainings" button used to land in the events browser.
 */
test.describe('public trainings are discoverable', () => {
  test('all four tabs are reachable and the trainings tab navigates', async ({ page }) => {
    await page.goto('/browse/teams');

    for (const id of ['browse-tab-teams', 'browse-tab-events', 'browse-tab-trainings', 'browse-tab-players']) {
      await expect(page.getByTestId(id)).toBeVisible();
    }

    // Events leads the strip and is where bare /browse lands — the redirect and the first cell are
    // one decision, so they are asserted together.
    const order = await page.locator('nav[aria-label="Browse"] a').evaluateAll(
      (els) => els.map((el) => el.getAttribute('data-testid')),
    );
    expect(order[0]).toBe('browse-tab-events');
    expect(order[1]).toBe('browse-tab-teams');

    await page.goto('/browse');
    await expect(page).toHaveURL(/\/browse\/events$/);

    await page.getByTestId('browse-tab-trainings').click();
    await expect(page).toHaveURL(/\/browse\/trainings$/);
    await expect(page.getByRole('heading', { name: 'Trainings' })).toBeVisible();
  });

  test('the tab strip stays legible and tappable at every viewport', async ({ page }) => {
    // FR-026 / SC-008. A fourth `flex-1` cell at 375px would leave ~80px per label, which Spanish
    // "Entrenamientos" cannot fill legibly — hence the 2-column grid below `sm`. This asserts the
    // outcome (readable, non-overlapping, ≥44px) rather than the mechanism, so a different DESIGN.md
    // resolution would still satisfy it. Runs on both the desktop and mobile projects.
    await page.goto('/browse/trainings');

    const ids = ['browse-tab-teams', 'browse-tab-events', 'browse-tab-trainings', 'browse-tab-players'];
    const boxes = [];
    for (const id of ids) {
      const tab = page.getByTestId(id);
      await expect(tab).toBeVisible();

      const box = await tab.boundingBox();
      expect(box, `${id} should have a layout box`).not.toBeNull();
      // Touch target minimum (DESIGN.md).
      expect(box!.height).toBeGreaterThanOrEqual(44);
      // The label must actually fit its cell — this is what catches a clipped "Entrenamientos".
      const overflows = await tab.evaluate((el) => el.scrollWidth > el.clientWidth + 1);
      expect(overflows, `${id} label is clipped`).toBe(false);
      boxes.push({ id, ...box! });
    }

    // No two tabs overlap (a wrapped grid must lay out in rows, not on top of each other).
    for (let i = 0; i < boxes.length; i++) {
      for (let j = i + 1; j < boxes.length; j++) {
        const a = boxes[i];
        const b = boxes[j];
        const separated = a.x + a.width <= b.x + 1 || b.x + b.width <= a.x + 1
          || a.y + a.height <= b.y + 1 || b.y + b.height <= a.y + 1;
        expect(separated, `${a.id} overlaps ${b.id}`).toBe(true);
      }
    }
  });

  test('the tab strip fits the longest translated labels, not just the English ones', async ({ page }) => {
    // The check above runs in the session's language (English). Spanish is the binding case —
    // "Entrenamientos" (14 chars) alongside "Jugadores" is what would clip a four-across row.
    // Rather than drive the settings UI to switch language, this substitutes the longest label
    // from each catalogue directly and re-measures: it is the layout's capacity that is under
    // test, and this asserts it without depending on which language the account happens to use.
    await page.goto('/browse/trainings');

    const longest = {
      'browse-tab-teams': 'Equipos',
      'browse-tab-events': 'Veranstaltungen',
      'browse-tab-trainings': 'Entrenamientos',
      'browse-tab-players': 'Jugadores',
    } as const;

    for (const [id, label] of Object.entries(longest)) {
      const tab = page.getByTestId(id);
      await tab.evaluate((el, text) => { el.textContent = text; }, label);

      const overflows = await tab.evaluate((el) => el.scrollWidth > el.clientWidth + 1);
      expect(overflows, `${id} clips "${label}"`).toBe(false);

      const box = await tab.boundingBox();
      expect(box!.height).toBeGreaterThanOrEqual(44);
    }
  });

  test('filter chips show translated text, not raw keys, on every browse page', async ({ page }) => {
    // Only reproducible in a browser: the catalogue loads asynchronously, and a `computed()` whose
    // other dependencies never change afterwards keeps whatever `translate()` returned first — the
    // raw key. Jest preloads the catalogue synchronously (`preloadLangs: true`), so the unit suite
    // cannot see this. All four browse pages carry the fix (GH #147); this loops over each so a
    // regression on any one is caught. A raw chip surfaces as a `browse.*` key (teams/events/
    // trainings) or a `pompfen.*` key (players position chips).
    const rawKey = /(?:browse|pompfen)\.[A-Za-z.]+/;

    // Teams, events and trainings each render a default chip, so the bug shows on first paint.
    const withDefaultChip = [
      { path: '/browse/teams', text: 'Active' },
      { path: '/browse/events', text: 'Upcoming' },
      { path: '/browse/trainings', text: 'Upcoming' },
    ];
    for (const { path, text: expected } of withDefaultChip) {
      await page.goto(path);
      const chips = page.getByTestId('browse-chips');
      await chips.waitFor();

      const text = (await chips.textContent()) ?? '';
      expect(text, `${path}: a chip rendered a raw Transloco key`).not.toMatch(rawKey);
      expect(text, `${path}: chip text missing`).toContain(expected);
    }

    // Players has no default chip — apply a position filter to produce one, then assert.
    await page.goto('/browse/players');
    await page.getByTestId('browse-filters-button').click();
    const panel = page.getByTestId('filter-panel');
    await expect(panel).toBeVisible();
    await panel.getByRole('button', { name: 'Staff' }).click();
    await page.getByTestId('filter-apply').click();

    const playerChips = page.getByTestId('browse-chips');
    await playerChips.waitFor();
    const playerText = (await playerChips.textContent()) ?? '';
    expect(playerText, 'players: a chip rendered a raw Transloco key').not.toMatch(rawKey);
    expect(playerText, 'players: chip text missing').toContain('Staff');
  });

  test('the home empty state sends a teamless player to trainings, not events', async ({ page }) => {
    // The bug that motivated the feature: this button rendered "Browse open trainings" and
    // navigated to /browse/events, a list that only ever holds tournaments and workshops.
    await page.goto('/');

    const findATeam = page.getByTestId('find-a-team');
    await expect(findATeam).toBeVisible();

    await findATeam.getByRole('link', { name: /browse open trainings/i }).click();
    await expect(page).toHaveURL(/\/browse\/trainings$/);
    await expect(page.getByTestId('browse-tab-trainings')).toBeVisible();
  });
});
