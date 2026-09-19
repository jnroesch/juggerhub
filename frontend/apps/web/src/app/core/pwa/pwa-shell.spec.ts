import { existsSync, readFileSync } from 'node:fs';
import * as path from 'node:path';

/**
 * Guard for the PWA shell (feature 054). Reads the shipped files from disk and fails the build
 * if any of the spec's structural promises stops being true:
 *
 * - the worker has no fetch handler and pulls nothing in (FR-007);
 * - the worker never caches or stores anything — "no offline mode, ever" (FR-011, owner decision);
 * - the manifest carries no string a browser would show (FR-005) and its colours are the
 *   DESIGN.md tokens (FR-001);
 * - every icon the manifest names exists and is the size it claims (FR-002);
 * - the entry page links the manifest and the iOS icon (FR-003).
 *
 * These are rules about future diffs, which is why they are tests and not comments.
 */
const PUBLIC_DIR = path.resolve(__dirname, '../../../../public');
const INDEX_HTML = path.resolve(__dirname, '../../../index.html');

function readPublic(rel: string): string {
  return readFileSync(path.join(PUBLIC_DIR, rel), 'utf8');
}

const PNG_SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

/** Width/height from the PNG IHDR chunk (bytes 16–23), after checking the 8-byte signature. */
function pngSize(buf: Buffer): { width: number; height: number } {
  expect(buf.subarray(0, 8).equals(PNG_SIGNATURE)).toBe(true);
  return { width: buf.readUInt32BE(16), height: buf.readUInt32BE(20) };
}

interface ManifestIcon {
  src: string;
  sizes: string;
  type: string;
  purpose: string;
}

interface Manifest {
  name: string;
  short_name: string;
  id: string;
  start_url: string;
  scope: string;
  display: string;
  background_color: string;
  theme_color: string;
  icons: ManifestIcon[];
  [key: string]: unknown;
}

describe('PWA shell (054)', () => {
  it('ships from apps/web/public', () => {
    expect(existsSync(PUBLIC_DIR)).toBe(true);
    expect(existsSync(INDEX_HTML)).toBe(true);
  });

  describe('sw.js', () => {
    const sw = readPublic('sw.js');

    it('has lifecycle listeners only', () => {
      expect(sw).toContain("addEventListener('install'");
      expect(sw).toContain("addEventListener('activate'");
    });

    it('has no fetch handler (FR-007)', () => {
      // A worker with a fetch handler sits between every page and the server. This one must
      // never do that: there is nothing to serve offline (026), and a no-op handler adds a
      // worker hop to every navigation for nothing.
      expect(sw).not.toContain("addEventListener('fetch'");
      expect(sw).not.toContain('addEventListener("fetch"');
      expect(sw).not.toContain('onfetch');
    });

    it('pulls nothing in from elsewhere (FR-007)', () => {
      expect(sw).not.toContain('importScripts(');
    });

    it('never caches or stores anything — no offline mode, ever (FR-011)', () => {
      // Owner decision (spec Clarification 3). This test IS the rule: delete it only with a new
      // owner decision recorded in a spec. 033 injects per-environment config into index.html
      // at serve time; a cached entry page would freeze it on the device.
      expect(sw).not.toContain('caches.');
      expect(sw).not.toContain('caches.open');
      expect(sw).not.toContain('indexedDB');
      expect(sw).not.toContain('localStorage');
      expect(sw).not.toContain('sessionStorage');
    });
  });

  describe('manifest.webmanifest', () => {
    const manifest = JSON.parse(readPublic('manifest.webmanifest')) as Manifest;

    it('describes a standalone app rooted at /', () => {
      expect(manifest.name).toBe('JuggerHub');
      expect(manifest.short_name).toBe('JuggerHub');
      expect(manifest.display).toBe('standalone');
      expect(manifest.start_url).toBe('/');
      expect(manifest.scope).toBe('/');
      expect(manifest.id).toBe('/');
    });

    it('takes its colours from DESIGN.md tokens (FR-001)', () => {
      // background_color = surface-page (sand-0): the page ground, so the splash matches.
      // theme_color = surface-card: the top bar and bottom bar, so the OS chrome matches them.
      // A token change in DESIGN.md means a change here — and in index.html's theme-color meta.
      expect(manifest.background_color).toBe('#FBF8F3');
      expect(manifest.theme_color).toBe('#FFFFFF');
    });

    it('carries no string a browser would show (FR-005)', () => {
      // Chrome renders `description` (and screenshots/shortcuts) in its richer install dialog.
      // This feature ships zero interface copy; the install affordance and its words are #308's.
      expect('description' in manifest).toBe(false);
      expect('lang' in manifest).toBe(false);
      expect('screenshots' in manifest).toBe(false);
      expect('shortcuts' in manifest).toBe(false);
    });

    it('names icons that exist at the sizes it claims, with separate purposes (FR-002)', () => {
      expect(manifest.icons.length).toBeGreaterThanOrEqual(4);
      const purposes = new Set<string>();
      for (const icon of manifest.icons) {
        const file = path.join(PUBLIC_DIR, icon.src);
        expect(existsSync(file)).toBe(true);
        expect(icon.type).toBe('image/png');
        // "any maskable" on one icon shows maskable padding wherever no mask is applied.
        expect(['any', 'maskable']).toContain(icon.purpose);
        purposes.add(icon.purpose);
        const [w, h] = icon.sizes.split('x').map(Number);
        expect(pngSize(readFileSync(file))).toEqual({ width: w, height: h });
      }
      expect(purposes).toEqual(new Set(['any', 'maskable']));
      const sizes = manifest.icons.map((i) => `${i.purpose}:${i.sizes}`);
      expect(sizes).toEqual(expect.arrayContaining(['any:192x192', 'any:512x512', 'maskable:512x512']));
    });
  });

  describe('index.html', () => {
    const html = readFileSync(INDEX_HTML, 'utf8');

    it('links the manifest so every route is installable (FR-003)', () => {
      expect(html).toMatch(/<link\s+rel="manifest"\s+href="manifest\.webmanifest"\s*\/?>/);
    });

    it('sets the theme colour to the same token as the manifest', () => {
      expect(html).toMatch(/<meta\s+name="theme-color"\s+content="#FFFFFF"\s*\/?>/);
    });

    it('links an opaque 180×180 icon for iOS Home Screen', () => {
      // iOS takes the Home Screen icon from apple-touch-icon, not from the manifest, and paints
      // transparent pixels black.
      expect(html).toMatch(/<link\s+rel="apple-touch-icon"\s+href="icons\/apple-touch-icon\.png"\s*\/?>/);
      const file = path.join(PUBLIC_DIR, 'icons/apple-touch-icon.png');
      expect(existsSync(file)).toBe(true);
      expect(pngSize(readFileSync(file))).toEqual({ width: 180, height: 180 });
    });

    it('keeps the viewport as it is — no viewport-fit=cover', () => {
      // The existing env(safe-area-inset-bottom) paddings are 0 under the default viewport, and
      // with it iOS boxes the standalone app inside the safe areas (FR-004). `cover` would put the
      // sticky header under the status bar with no top-inset handling anywhere (research R6).
      const viewport = html.match(/<meta\s+name="viewport"\s+content="([^"]*)"/);
      expect(viewport).not.toBeNull();
      expect(viewport?.[1]).toBe('width=device-width, initial-scale=1');
    });
  });
});
