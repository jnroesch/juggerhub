import { readFileSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Guards for the `legal` translation scope (feature 036).
 *
 * These are not conventional coverage — they are the two failure modes the feature's risk
 * register turns on (see specs/036-privacy-policy-imprint/plan.md → Risks & gotchas). Neither
 * should ever be weakened to make a build pass.
 */

const LANGS = ['en', 'de', 'es'] as const;
const CATALOG_DIR = join(__dirname, '../../../../public/i18n/legal');

/** The sentinel marking a value the owner still has to supply. See T024 / spec Q1. */
const PLACEHOLDER = '__TODO__';

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

/** Every leaf value in the tree, paired with its dotted path. */
function leaves(node: unknown, prefix = ''): [string, unknown][] {
  if (Array.isArray(node)) {
    return node.flatMap((item, i) => leaves(item, `${prefix}.${i}`));
  }
  if (node !== null && typeof node === 'object') {
    return Object.entries(node as Catalog).flatMap(([key, value]) =>
      leaves(value, prefix ? `${prefix}.${key}` : key),
    );
  }
  return [[prefix, node]];
}

const catalogs = Object.fromEntries(LANGS.map((lang) => [lang, load(lang)])) as Record<
  (typeof LANGS)[number],
  Catalog
>;

describe('legal translation scope', () => {
  describe('completeness (DM-1)', () => {
    /**
     * `app.config.ts` sets `missingHandler: { useFallbackTranslation: true }` with
     * `fallbackLang: 'en'`. For interface labels that is correct and required by feature 031.
     * For the legal scope it is a hazard: a paragraph missing from `de.json` does not render
     * blank and does not log in production — it renders the ENGLISH text, inside the legally
     * AUTHORITATIVE German document, with no visible signal. The result looks complete, is the
     * binding version, and is partly in the wrong language.
     *
     * Fix the catalog, never this test. Do not "solve" it by disabling the global fallback
     * either — that would break feature 031's guarantee for the other ~2000 keys in the app.
     */
    const reference = keyPaths(catalogs.en).sort();

    it.each(LANGS.filter((l) => l !== 'en'))('%s has exactly the same keys as en', (lang) => {
      const actual = keyPaths(catalogs[lang]).sort();

      const missing = reference.filter((k) => !actual.includes(k));
      const extra = actual.filter((k) => !reference.includes(k));

      expect({ missing, extra }).toEqual({ missing: [], extra: [] });
    });
  });

  describe('placeholders (DM-2)', () => {
    /**
     * ⚠️ THIS TEST IS EXPECTED TO FAIL until the owner supplies the imprint particulars
     * (tasks.md T024, spec.md Open Questions Q1). That red build is the feature working.
     *
     * It exists because the failure it prevents is specific and realistic: the structure is
     * complete, the page renders, review passes on everything visible, and a placeholder ships
     * to production inside the one document whose content is legally prescribed. "We'll fill it
     * in before deploy" is exactly how that happens. A red build is not.
     *
     * Do not skip, `xit`, or soften it. It turns green on its own when the particulars arrive.
     */
    it.each(LANGS)('%s contains no unfilled placeholder', (lang) => {
      const unfilled = leaves(catalogs[lang])
        .filter(([, value]) => typeof value === 'string' && value.includes(PLACEHOLDER))
        .map(([path]) => path);

      expect(unfilled).toEqual([]);
    });
  });

  describe('value rules', () => {
    /** CV-1/CV-2 + DM-4: paragraphs are array entries, never strings carrying markup, so no
     *  page ever needs an `[innerHTML]` binding (constitution I). */
    it.each(LANGS)('%s contains no markup in any value', (lang) => {
      const withMarkup = leaves(catalogs[lang])
        .filter(([, value]) => typeof value === 'string' && value.includes('<'))
        .map(([path]) => path);

      expect(withMarkup).toEqual([]);
    });

    it.each(LANGS)('%s has every leaf as a string', (lang) => {
      const nonString = leaves(catalogs[lang])
        .filter(([, value]) => typeof value !== 'string')
        .map(([path]) => path);

      expect(nonString).toEqual([]);
    });

    /** DM-5: rendered through transloco-locale, so it must be a real ISO date. */
    it.each(LANGS)('%s has an ISO meta.lastUpdated', (lang) => {
      const value = (catalogs[lang]['meta'] as Catalog)['lastUpdated'];

      expect(value).toMatch(/^\d{4}-\d{2}-\d{2}$/);
      expect(Number.isNaN(Date.parse(value as string))).toBe(false);
    });

    /** The authoritative-language notice is what tells an en/es reader which text governs
     *  (FR-019). It must exist in every language, including de, where it says the opposite. */
    it.each(LANGS)('%s has an authoritative-language notice', (lang) => {
      expect((catalogs[lang]['meta'] as Catalog)['authoritativeNotice']).toBeTruthy();
    });
  });
});
