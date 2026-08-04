import { readFileSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Key-parity guard for the MAIN interface catalogs (feature 042, research R8).
 *
 * `app.config.ts` sets `missingHandler: { useFallbackTranslation: true }` with
 * `fallbackLang: 'en'` — required by feature 031 so a partially translated app still renders.
 * The cost is that a key missing from `de.json` renders the ENGLISH string, silently: no blank,
 * no console warning in production, nothing a reviewer would notice unless they read German.
 *
 * Feature 036 guarded the `legal` scope this way (`legal-catalog.spec.ts`). The main catalogs
 * had no equivalent, so every feature that adds a label could ship English into `de`/`es` and
 * fail nothing. This closes that gap.
 *
 * Fix the catalog, never this test. In particular do not "solve" it by disabling the global
 * fallback — that would break feature 031's guarantee for ~1200 other keys.
 */

const LANGS = ['en', 'de', 'es'] as const;
const CATALOG_DIR = join(__dirname, '../../../../public/i18n');

/**
 * `_meta` carries per-catalog translation-status bookkeeping and exists only in the non-English
 * catalogs by design. It is not user-facing copy, so it is excluded rather than mirrored into
 * `en.json` just to satisfy a comparison.
 */
const EXCLUDED_ROOTS = ['_meta'];

type Catalog = Record<string, unknown>;

function load(lang: string): Catalog {
  return JSON.parse(readFileSync(join(CATALOG_DIR, `${lang}.json`), 'utf-8')) as Catalog;
}

/** Every leaf path in the tree, dotted; array entries are indexed (`a.b.0`). */
function keyPaths(node: unknown, prefix = ''): string[] {
  if (Array.isArray(node)) {
    return node.flatMap((item, i) => keyPaths(item, `${prefix}.${i}`));
  }
  if (node !== null && typeof node === 'object') {
    return Object.entries(node as Catalog).flatMap(([key, value]) =>
      keyPaths(value, prefix ? `${prefix}.${key}` : key),
    );
  }
  return [prefix];
}

function comparableKeys(catalog: Catalog): string[] {
  return keyPaths(catalog)
    .filter((path) => !EXCLUDED_ROOTS.some((root) => path === root || path.startsWith(`${root}.`)))
    .sort();
}

const catalogs = Object.fromEntries(LANGS.map((lang) => [lang, load(lang)])) as Record<
  (typeof LANGS)[number],
  Catalog
>;

describe('main translation catalogs', () => {
  const reference = comparableKeys(catalogs.en);

  it('en is non-empty (guards against a silently broken reference)', () => {
    expect(reference.length).toBeGreaterThan(100);
  });

  it.each(LANGS.filter((l) => l !== 'en'))('%s has exactly the same keys as en', (lang) => {
    const actual = comparableKeys(catalogs[lang]);

    const missing = reference.filter((k) => !actual.includes(k));
    const extra = actual.filter((k) => !reference.includes(k));

    expect({ missing, extra }).toEqual({ missing: [], extra: [] });
  });
});
