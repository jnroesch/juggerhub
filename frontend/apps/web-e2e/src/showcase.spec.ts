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
  await expect(page.getByTestId('profile-showcase-manager')).toBeVisible();

  // 1. An empty gallery still shows the owner their card — that is where the invitation to add a
  //    picture lives — but there is nothing in it yet.
  await expect(page.getByTestId('profile-showcase')).toBeVisible();
  await expect(page.getByTestId('showcase-thumb-0')).toHaveCount(0);

  // 2. Add one; it appears in both the manager list and the read-only gallery.
  await addPicture(page, 0);
  await expect(page.getByTestId('showcase-manager-list').locator('li')).toHaveCount(1);
  await expect(page.getByTestId('showcase-thumb-0')).toBeVisible();

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
