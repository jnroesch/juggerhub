// Renders the PWA icon set (feature 054) from the two SVG sources under apps/web/public:
//
//   favicon.svg            -> icons/icon-192.png, icons/icon-512.png          (purpose "any", transparent corners)
//   icons/icon-maskable.svg -> icons/icon-maskable-192.png, icons/icon-maskable-512.png (purpose "maskable", opaque)
//                          -> icons/apple-touch-icon.png                     (180x180, opaque — iOS paints transparency black)
//
// One-off: run `node tools/render-pwa-icons.mjs` from `frontend/` and COMMIT the PNGs. The guard spec
// (apps/web/src/app/core/pwa/pwa-shell.spec.ts) checks each PNG's IHDR size against the manifest, so a
// wrong-size regeneration cannot land. Re-run whenever the mark changes (GH #304).
//
// Why a Node script and not a .ps1 (constitution VI): nothing else available rasterises SVG with
// transparent corners — no ImageMagick/Inkscape/rsvg on the machine, no sharp/resvg in node_modules —
// but playwright-core is already a dev dependency with its Chromium installed, and Chromium's SVG
// renderer with `omitBackground` is exactly the tool. The Playwright CLI's `screenshot` has no
// omit-background flag, hence the API. Precedent: backend/Data/Seed/regenerate-cities500.mjs.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';

const PUBLIC = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../apps/web/public');

/** [source (relative to public/), edge px, output (relative to public/), transparent background] */
const JOBS = [
  ['favicon.svg', 192, 'icons/icon-192.png', true],
  ['favicon.svg', 512, 'icons/icon-512.png', true],
  ['icons/icon-maskable.svg', 192, 'icons/icon-maskable-192.png', false],
  ['icons/icon-maskable.svg', 512, 'icons/icon-maskable-512.png', false],
  ['icons/icon-maskable.svg', 180, 'icons/apple-touch-icon.png', false],
];

const browser = await chromium.launch();
try {
  const context = await browser.newContext({ deviceScaleFactor: 1 });
  const page = await context.newPage();

  for (const [src, size, out, transparent] of JOBS) {
    // Inline the SVG as a data URL so no file:// permissions are involved.
    const svg = readFileSync(path.join(PUBLIC, src));
    const dataUrl = `data:image/svg+xml;base64,${svg.toString('base64')}`;

    await page.setViewportSize({ width: size, height: size });
    await page.setContent(
      `<!doctype html><html><head><style>` +
        `html,body{margin:0;background:transparent}` +
        `img{display:block;width:${size}px;height:${size}px}` +
        `</style></head><body><img src="${dataUrl}" alt=""></body></html>`,
    );
    await page.waitForFunction(() => {
      const img = document.querySelector('img');
      return img !== null && img.complete && img.naturalWidth > 0;
    });
    await page.screenshot({ path: path.join(PUBLIC, out), omitBackground: transparent });
    console.log(`${out}  ${size}x${size}  ${transparent ? 'transparent' : 'opaque'}`);
  }
} finally {
  await browser.close();
}
