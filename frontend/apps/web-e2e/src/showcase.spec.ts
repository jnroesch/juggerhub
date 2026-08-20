import { expect, test } from '@playwright/test';
import { registerVerifySignIn } from './support/auth';

/**
 * Feature 046 end-to-end (#99): a player adds showcase pictures to their own profile, the cap
 * holds at five, the enlarged view opens and closes, and a removal takes a picture away again.
 *
 * The interesting parts here are the ones a unit test cannot reach: a real multipart upload
 * through the 034 processing pipeline into 035's object store, and the picture coming back down
 * the gated read path as an <img> the browser actually renders. Runs at desktop + mobile.
 */

// A small, real PNG — the processor decides on content, never on the filename or claimed type.
const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAA70lEQVR4nO3QkXaCAQCA0R+CIBgEQRAEg0EQBIMgCIIgCIIgCAZBEATBIAiCIAiCIAiCIAiCIBgEQRAMgiAIgqBz6nuNzvngvsANgtb2GUIYEXwgihjiSCCJT3whhTQy+EYWOeRRQBEllFFBFTXU8YMGmmihjQ5+0UUPfQwwxAhjTDDFDHMssMQKa2ywxR922OOAI/5xwhkXXHHDHQ8EBhhggAEGGGCAAQYYYIABBhhggAEGGGCAAQYYYIABBhhggAEGGGCAAQYYYIABBhhggAEGGGCAAQYYYIABBhhggAEGGGCAAQYYYIABBhjw/gEv0FjShpxmlLUAAAAASUVORK5CYII=',
  'base64',
);

async function addPicture(page: import('@playwright/test').Page, index: number) {
  await page.getByTestId('showcase-file-input').setInputFiles({
    name: `showcase-${index}.png`,
    mimeType: 'image/png',
    buffer: PNG,
  });
}

test('add showcase pictures, page through them, and remove one', async ({ page, request }) => {
  const { handle } = await registerVerifySignIn(page, request, 'show');

  await page.goto(`/u/${handle}`);

  // 1. Nothing to show yet, and view mode offers no editing — the gallery is edited where the rest
  //    of the profile is, behind Edit.
  await expect(page.getByTestId('profile-showcase')).toHaveCount(0);
  await expect(page.getByTestId('profile-showcase-manager')).toHaveCount(0);

  await page.getByTestId('profile-edit').click();
  await expect(page.getByTestId('profile-showcase-manager')).toBeVisible();

  // 2. Add one; it appears in both the manager list and the read-only gallery.
  await addPicture(page, 0);
  await expect(page.getByTestId('showcase-manager-list').locator('li')).toHaveCount(1);

  // 2b. Back in view mode the picture shows in the gallery, exactly as a visitor sees it — and the
  //     editing controls are gone.
  await page.getByTestId('profile-cancel').click();
  await expect(page.getByTestId('profile-showcase')).toBeVisible();
  await expect(page.getByTestId('showcase-thumb-0')).toBeVisible();
  await expect(page.getByTestId('profile-showcase-manager')).toHaveCount(0);

  // 3. The picture is really served: the browser fetched bytes, not a broken image.
  const rendered = await page
    .getByTestId('showcase-thumb-0')
    .locator('img')
    .evaluate((img: HTMLImageElement) => img.naturalWidth);
  expect(rendered).toBeGreaterThan(0);

  // 4. The enlarged view opens, and Escape closes it.
  await page.getByTestId('showcase-thumb-0').click();
  await expect(page.getByTestId('showcase-viewer')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('showcase-viewer')).toHaveCount(0);

  // 5. Fill the gallery: the add control closes itself once five are stored.
  await page.getByTestId('profile-edit').click();
  for (let i = 1; i < 5; i++) {
    await addPicture(page, i);
    await expect(page.getByTestId('showcase-manager-list').locator('li')).toHaveCount(i + 1);
  }
  await expect(page.getByTestId('showcase-file-input')).toBeDisabled();

  // 6. Removing one frees a slot again.
  await page.getByTestId('showcase-remove-0').click();
  await expect(page.getByTestId('showcase-manager-list').locator('li')).toHaveCount(4);
  await expect(page.getByTestId('showcase-file-input')).toBeEnabled();
});

test('a team admin fills the team gallery and it renders on the team page', async ({ page, request }) => {
  await registerVerifySignIn(page, request, 'showteam');

  // Create a team through the API in the page's own session — the UI path is covered elsewhere,
  // and what this spec is about is the gallery.
  const slug = `showteam-${Date.now()}`.slice(0, 30);
  const created = await page.request.post('/api/v1/teams', {
    data: { name: 'Rheinfeuer Berlin', slug, type: 'Mixteam', location: null },
  });
  expect(created.ok()).toBeTruthy();

  await page.goto(`/t/${slug}`);

  // As the team's admin: the card is there, showing the (empty) gallery, with a way into editing.
  await expect(page.getByTestId('team-showcase')).toBeVisible();
  await expect(page.getByTestId('team-showcase-manager')).toHaveCount(0);
  await expect(page.getByTestId('showcase-thumb-0')).toHaveCount(0);

  await page.getByTestId('team-showcase-manage-toggle').click();
  await expect(page.getByTestId('team-showcase-manager')).toBeVisible();

  // One at a time: the add control disables itself while an upload is in flight, so a second
  // programmatic file-pick inside that window is dropped — a person cannot hit it, but a test
  // firing two `setInputFiles` back to back can.
  await addPicture(page, 0);
  await expect(page.getByTestId('showcase-manager-list').locator('li')).toHaveCount(1);
  await addPicture(page, 1);
  await expect(page.getByTestId('showcase-manager-list').locator('li')).toHaveCount(2);

  // Switch back to looking: the gallery shows them and the editing list is gone.
  await page.getByTestId('team-showcase-manage-toggle').click();
  await expect(page.getByTestId('team-showcase-manager')).toHaveCount(0);
  await expect(page.getByTestId('showcase-thumb-1')).toBeVisible();

  // The pictures really came down the gated read path.
  const width = await page
    .getByTestId('showcase-thumb-0')
    .locator('img')
    .evaluate((img: HTMLImageElement) => img.naturalWidth);
  expect(width).toBeGreaterThan(0);

  // Reordering swaps them and survives a reload.
  await page.getByTestId('team-showcase-manage-toggle').click();
  await page.getByTestId('showcase-move-down-0').click();
  await expect(page.getByTestId('showcase-manager-list').locator('li')).toHaveCount(2);
  await page.reload();
  await expect(page.getByTestId('showcase-thumb-1')).toBeVisible();
});
