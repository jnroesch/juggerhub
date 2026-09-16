import { readFileSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Typographic guard for the interface and legal catalogs (GH #303).
 *
 * The dash rules are settled in DESIGN.md → Voice & content → "Dashes and separators".
 * Two of them are mechanical enough to check, and both were live defects when this was
 * written: German shipped 94 strings set with an em dash (the Gedankenstrich is a
 * Halbgeviertstrich, `–`), and Spanish used the raya the English way, as a single dangling
 * pause, where the language wants a colon, a comma or a full stop.
 *
 * What is NOT checked here is the third rule — that English uses ` — ` sparingly rather than
 * as the default shape for every hint. A count threshold would either be met by moving the tic
 * somewhere else or turn a judgement call into a build failure. That one is a review question.
 *
 * Fix the catalog, never this test.
 */

const CATALOG_DIR = join(__dirname, '../../../../public/i18n');

const EM_DASH = '—';
const EN_DASH = '–';

/**
 * `_meta` is English bookkeeping about the translation, not translated copy, so the rules for
 * the language of the file do not apply to it. The key-parity guard excludes it for the
 * matching reason.
 */
const EXCLUDED_ROOTS = ['_meta'];

type Catalog = Record<string, unknown>;

function load(path: string): Catalog {
  return JSON.parse(readFileSync(join(CATALOG_DIR, path), 'utf-8')) as Catalog;
}

/** Every leaf value in the tree, paired with its dotted path. */
function leaves(node: unknown, prefix = ''): [string, string][] {
  if (Array.isArray(node)) {
    return node.flatMap((item, i) => leaves(item, `${prefix}.${i}`));
  }
  if (node !== null && typeof node === 'object') {
    return Object.entries(node as Catalog).flatMap(([key, value]) =>
      leaves(value, prefix ? `${prefix}.${key}` : key),
    );
  }
  return typeof node === 'string' ? [[prefix, node]] : [];
}

function copyLeaves(path: string): [string, string][] {
  return leaves(load(path)).filter(
    ([p]) => !EXCLUDED_ROOTS.some((root) => p === root || p.startsWith(`${root}.`)),
  );
}

describe('catalog punctuation', () => {
  describe('German sets the Gedankenstrich as a Halbgeviertstrich', () => {
    /**
     * `–` is correct, `—` is not. The en dash is also the range dash ("A–Z", "{{start}}–{{end}}"),
     * which is correct in every language and deliberately not restricted here.
     */
    it.each(['de.json', 'legal/de.json'])('%s uses no em dash', (file) => {
      const offenders = copyLeaves(file)
        .filter(([, value]) => value.includes(EM_DASH))
        .map(([path]) => path);

      expect(offenders).toEqual([]);
    });
  });

  describe('Spanish uses the raya only as a paired incise', () => {
    /**
     * The raya is Spanish punctuation, but not the English way: it encloses an aside with both
     * dashes and no space on the inside (`texto —un inciso— sigue`). A single raya opening a
     * trailing clause is an English habit, and so is a space between the dash and the words it
     * encloses.
     */
    it.each(['es.json', 'legal/es.json'])('%s has no dangling or spaced raya', (file) => {
      const offenders = copyLeaves(file)
        .filter(([, value]) => {
          const at = [...value].flatMap((c, i) => (c === EM_DASH ? [i] : []));
          if (at.length === 0) return false;
          if (at.length % 2 !== 0) return true; // a dangling raya: one dash, English-style
          // Read them pairwise: the opener hugs the text after it, the closer the text before it.
          return at.some((i, n) => (n % 2 === 0 ? value[i + 1] === ' ' : value[i - 1] === ' '));
        })
        .map(([path]) => path);

      expect(offenders).toEqual([]);
    });

    /** Spanish has no Gedankenstrich; an en dash here would be a German habit leaking across. */
    it.each(['es.json', 'legal/es.json'])('%s uses no en dash as a clause dash', (file) => {
      const offenders = copyLeaves(file)
        .filter(([, value]) => value.includes(` ${EN_DASH} `))
        .map(([path]) => path);

      expect(offenders).toEqual([]);
    });
  });

  describe('one error voice (DESIGN.md → Loading, error & retry states)', () => {
    /**
     * DESIGN.md prescribes a single warm opening ("We couldn't load your teams."). "Could not",
     * "Couldn't" without the "we", and "Something went wrong" are the three it rules out, and
     * all three were in the English catalog at the same time.
     */
    const RULED_OUT = [/^Could not\b/, /^Couldn['’]t\b/, /^Something went wrong\b/];

    it.each(['en.json', 'legal/en.json'])('%s opens no message in a ruled-out voice', (file) => {
      const offenders = copyLeaves(file)
        .filter(([, value]) => RULED_OUT.some((pattern) => pattern.test(value)))
        .map(([path]) => path);

      expect(offenders).toEqual([]);
    });
  });
});
