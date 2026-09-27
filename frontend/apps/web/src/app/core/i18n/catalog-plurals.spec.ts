import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { isPluralNode } from './plural';
import { SUPPORTED_LANGUAGES } from './supported-languages';

/**
 * Plural guard for the MAIN interface catalogs (GH #338).
 *
 * A message whose wording depends on a number is an object keyed by CLDR plural category, and the
 * category is chosen at runtime by `Intl.PluralRules` (see `plural.ts`). Two things can still go
 * wrong, both silently, and both are checked here against the same CLDR data the runtime uses:
 *
 * 1. A catalogue lacks a form its language needs. The runtime then shows the `other` form — for
 *    Polish, "5 przedmiotu" instead of "5 przedmiotów" — and nothing fails. So every plural
 *    message carries a form for every category a whole-number count below 10 000 falls into in
 *    that language, plus `other`, the form every plural message must have. Categories no count
 *    reaches (Spanish `many` is a round million) may be left out; `other` stands in.
 *
 * 2. A message interpolates `{{count}}` as a plain string. Then its wording is the same whatever
 *    the number, which is the English author deciding for every language that it may be: German
 *    "Bei dir stehen {{count}} an." was wrong for exactly one appointment, and nobody writing the
 *    English "You've got {{count}} coming up." could have seen it. Whether the words around a
 *    number change with it is the translator's call, so every `{{count}}` message is a plural
 *    message, even where the English forms are identical.
 *
 * Adding a language runs every rule here against its CLDR data — the test that would have failed
 * the moment a fourth language arrived with the old ternaries, which could not see them.
 *
 * Fix the catalog, never this test.
 */

const CATALOG_DIR = join(__dirname, '../../../../public/i18n');

/** See `catalog-parity.spec.ts`: translation bookkeeping, not copy. */
const EXCLUDED_ROOTS = ['_meta'];

/** Counts in JuggerHub are members, spots, messages, days — this bounds them with room to spare. */
const LARGEST_REALISTIC_COUNT = 9_999;

const COUNT_PARAM = /\{\{\s*count\s*\}\}/;

type Catalog = Record<string, unknown>;
type PluralForms = Record<string, string>;

function load(lang: string): Catalog {
  return JSON.parse(readFileSync(join(CATALOG_DIR, `${lang}.json`), 'utf-8')) as Catalog;
}

function excluded(path: string): boolean {
  return EXCLUDED_ROOTS.some((root) => path === root || path.startsWith(`${root}.`));
}

/** Plural messages by path, and every other string leaf by path. Plural nodes are not descended. */
function walk(node: unknown, prefix = '', plurals = new Map<string, PluralForms>(), strings = new Map<string, string>()) {
  if (excluded(prefix)) {
    return { plurals, strings };
  }
  if (isPluralNode(node)) {
    plurals.set(prefix, node as PluralForms);
  } else if (Array.isArray(node)) {
    node.forEach((item, i) => walk(item, `${prefix}.${i}`, plurals, strings));
  } else if (node !== null && typeof node === 'object') {
    Object.entries(node as Catalog).forEach(([key, value]) =>
      walk(value, prefix ? `${prefix}.${key}` : key, plurals, strings),
    );
  } else if (typeof node === 'string') {
    strings.set(prefix, node);
  }
  return { plurals, strings };
}

/** The categories a language has at all — a form for any other one can never be shown. */
function allowedCategories(lang: string): string[] {
  return new Intl.PluralRules(lang).resolvedOptions().pluralCategories;
}

/** The categories whole-number counts actually reach, plus `other`. */
function requiredCategories(lang: string): string[] {
  const rules = new Intl.PluralRules(lang);
  const reached = new Set<string>(['other']);
  for (let n = 0; n <= LARGEST_REALISTIC_COUNT; n++) {
    reached.add(rules.select(n));
  }
  return [...reached].sort();
}

const catalogs = Object.fromEntries(SUPPORTED_LANGUAGES.map((lang) => [lang, walk(load(lang))]));

describe('plural messages in the main catalogs', () => {
  it('the reference catalog has plural messages (guards against a walk that found nothing)', () => {
    expect(catalogs['en'].plurals.size).toBeGreaterThan(10);
  });

  it.each(SUPPORTED_LANGUAGES.filter((l) => l !== 'en'))(
    '%s has the same plural messages as en',
    (lang) => {
      // Parity collapses a plural message to its own path, so it cannot tell a plural message from
      // a plain string at the same path. This can.
      const reference = [...catalogs['en'].plurals.keys()].sort();
      const actual = [...catalogs[lang].plurals.keys()].sort();

      expect({
        missing: reference.filter((p) => !actual.includes(p)),
        extra: actual.filter((p) => !reference.includes(p)),
      }).toEqual({ missing: [], extra: [] });
    },
  );

  it.each(SUPPORTED_LANGUAGES)('%s has every form its plural rules need, and no others', (lang) => {
    const required = requiredCategories(lang);
    const allowed = allowedCategories(lang);
    const problems: string[] = [];

    for (const [path, forms] of catalogs[lang].plurals) {
      const categories = Object.keys(forms);
      const missing = required.filter((c) => !categories.includes(c));
      const unused = categories.filter((c) => !allowed.includes(c));
      const empty = categories.filter((c) => typeof forms[c] !== 'string' || forms[c].trim() === '');

      if (missing.length) problems.push(`${path}: missing ${missing.join(', ')}`);
      if (unused.length) problems.push(`${path}: ${lang} never selects ${unused.join(', ')}`);
      if (empty.length) problems.push(`${path}: empty ${empty.join(', ')}`);
    }

    expect(problems).toEqual([]);
  });

  it.each(SUPPORTED_LANGUAGES)('%s interpolates {{count}} only inside plural messages', (lang) => {
    const offenders = [...catalogs[lang].strings]
      .filter(([, value]) => COUNT_PARAM.test(value))
      .map(([path]) => path);

    expect(offenders).toEqual([]);
  });
});
