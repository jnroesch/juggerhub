import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Scale-key guard for the design-system utilities (GH #137, extended by GH #297).
 *
 * Tailwind emits **no rule at all** for an undefined scale key. The class is in the markup, the
 * build is clean, nothing fails — and the property is simply never set. `py-3xs` and `mt-2xs` were
 * used across a dozen templates while the `spacing` scale in `tailwind.config.js` defined neither,
 * so the intended padding silently collapsed to zero and every template that copied a neighbouring
 * line inherited it. #297 found the same bug live in three more places at once:
 *
 * - `text-heading-xl` on two page titles (`fontSize` defines `heading-lg`/`-md`, never `-xl`), so
 *   the event and training session names rendered at 16px — preflight resets headings to
 *   `font-size: inherit` and `styles.css` sets no size on `h1`–`h4`.
 * - `tracking-eyebrow` on seven elements, with `letterSpacing` not extended at all.
 * - `text-muted` on 277 elements: the colour was registered under the name `text-muted`, which
 *   produces the utility `text-text-muted`, so the obvious spelling matched nothing and all of it
 *   rendered as body text.
 *
 * The naming is what hid it: `text-heading` was a colour, `text-heading-lg` a size and
 * `text-heading-xl` nothing at all, and all three looked like one family in review. GH #299
 * has since retired the `heading-lg` / `heading-md` sizes — they were aliases of `h3` and
 * `h4` — so `text-heading` is now unambiguously the colour and this guard is what makes the
 * retirement stick: any `text-heading-*` size written from here on is an undefined key.
 *
 * GH #312 is the colour half of the same story, and the reason it needed a guard at all. The
 * theme registered `ink` beside `heading` and `text` beside `body` — two names each for one
 * custom property — so 121 text elements (and four fills) were painted in a colour whose
 * canonical name was something else and *nothing rendered wrong*, which is why review never
 * caught it. The aliases are gone from the `colors` map — along with `primary`, `accent`,
 * `surface`, `surface-subtle`, `background`, `border`, `info-strong` and `warning` — so a
 * `text-ink` or a `bg-surface` written tomorrow is an undefined key and fails here.
 *
 * This walks the app source and fails on any of those utilities whose key is not in the *resolved*
 * Tailwind scale — resolved from the config, not a hardcoded list, so the guard cannot drift.
 * Because colour and size share the `text-` prefix, a `text-` key is accepted when it is valid in
 * **either** scale (plus the handful of `text-` utilities, like `text-center`, that take no scale
 * value at all).
 *
 * Two passes, because classes are written in two very different places:
 *
 * 1. **Class contexts** (`class="…"`, `ngClass="…"`, `[class.x]`) — every token is checked against
 *    the full scale. Anything in there is unambiguously a class list.
 * 2. **Everywhere else** — `.ts` files build class lists as bare string literals (`'gap-xs'` in
 *    `button.directive.ts`, `'text-danger-fg'` in `news-list.component.ts`), which are
 *    indistinguishable from ordinary strings like the `'my-team'` route id. Each family narrows
 *    that pass its own way:
 *      - *spacing* checks only keys shaped like a size token (`xs`, `2xl`, `3xs`…). `my-team` is
 *        not one; `py-3xs` is. That is the failure mode this guards: copying a size token the
 *        scale never defined.
 *      - *`text-` / `tracking-`* take word-shaped keys, so a size filter cannot separate them from
 *        English — `a text-only message` in a doc comment reads exactly like a utility. Those are
 *        therefore read only from **quoted strings**, which is where every class list in a `.ts`
 *        file lives and where prose does not.
 *
 * Scope is margin / padding / gap / space-between, font size / text colour, and letter spacing:
 * the utilities the design system's named tokens are for, and where the bug has actually lived.
 * Sizing utilities (`w-`, `max-w-`, `top-`…) draw on additional scales of their own and would make
 * the extraction noisy for no added protection.
 *
 * Fix the markup, or add the token to `tailwind.config.js` — and to DESIGN.md's token tables first,
 * since those are the source of truth for the scales. Never widen the allow-list to make a class
 * pass.
 */

// Both are CommonJS: the Tailwind config is a `module.exports` file the build itself loads.
const resolveConfig = require('tailwindcss/resolveConfig');
const tailwindConfig = require('../../../../tailwind.config.js');

const SRC_DIR = join(__dirname, '../../..');
const SCANNED_EXTENSIONS = ['.html', '.ts'];
const SELF = 'scale-keys.spec.ts';

/** Longest-first, so `gap-x-md` is not read as `gap` + `x-md` and `mx-auto` not as `m` + `x-auto`. */
const MARGIN_PREFIXES = ['mx', 'my', 'mt', 'mb', 'ml', 'mr', 'ms', 'me', 'm'];
const SPACING_PREFIXES = [
  'gap-x',
  'gap-y',
  'gap',
  'space-x',
  'space-y',
  'px',
  'py',
  'pt',
  'pb',
  'pl',
  'pr',
  'ps',
  'pe',
  'p',
  ...MARGIN_PREFIXES,
];

/** `md:`, `hover:`, `dark:hover:`, `[&>li]:` — any number of them, before the utility itself. */
const VARIANTS = /^(?:(?:\[[^\]]*\]|[^:\s[\]]+):)+/;

/** Size tokens: `xs`…`xl` plus the numbered steps (`2xl`, `3xs`). The shape the spacing bug took. */
const TSHIRT_KEY = /^\d*(?:xs|sm|md|lg|xl)$/;

/** A plausible class key — lowercase words joined by hyphens. Filters `text/plain` and friends out. */
const CLASS_KEY = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

/**
 * `class="…"` / `ngClass="…"` — a literal class list. `[class]="…"` and `[ngClass]="…"` are
 * deliberately excluded: their value is an *expression*, so `active('my-team') ? …` would read as
 * a class token. The class strings those expressions resolve to are quoted, so the second pass
 * covers them.
 */
const CLASS_LIST_ATTR = /(?:\bclass|\bngClass)\s*=\s*(?:"([^"]*)"|'([^']*)')/g;
/** `[class.mt-xs]="…"` — the utility is the binding name, not the value. */
const CLASS_BINDING = /\[class\.([^\]\s]+)\]/g;
/** Any quoted run on the line: where `.ts` keeps its class lists, and where prose does not live. */
const QUOTED = /"([^"]*)"|'([^']*)'|`([^`]*)`/g;

/** `space-{x,y}-reverse` toggles a CSS variable rather than taking a scale value. */
const NON_SCALE_VALUES = ['reverse'];

/** `text-` utilities that set alignment, wrapping or overflow — no scale, nothing to look up. */
const TEXT_KEYWORDS = [
  'left',
  'center',
  'right',
  'justify',
  'start',
  'end',
  'wrap',
  'nowrap',
  'balance',
  'pretty',
  'ellipsis',
  'clip',
];

interface Violation {
  file: string;
  line: number;
  utility: string;
}

interface Family {
  /** Utility prefixes, longest-first. */
  prefixes: string[];
  /** Is this key defined (or deliberately not a scale value)? */
  accepts(key: string, prefix: string): boolean;
  /** Which keys the second pass may report — see the file comment. */
  loose: 'tshirt-keys' | 'quoted-strings';
}

function sourceFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) return sourceFiles(full);
    // This file names the offending classes in prose; scanning it would report itself.
    if (entry.name === SELF) return [];
    return SCANNED_EXTENSIONS.some((ext) => entry.name.endsWith(ext)) ? [full] : [];
  });
}

/**
 * Every key in a resolved colour theme, flattened the way Tailwind names the utilities. A
 * design-system colour is a function of the requested alpha since GH #322 (so `bg-brand/60`
 * composes); a Tailwind default such as `black` is still a string. Both are leaves.
 */
function colorKeys(colors: Record<string, unknown>): Set<string> {
  const keys = new Set<string>();

  for (const [name, value] of Object.entries(colors)) {
    if (typeof value === 'string' || typeof value === 'function') {
      keys.add(name);
      continue;
    }
    for (const step of Object.keys(value as Record<string, string>)) {
      keys.add(step === 'DEFAULT' ? name : `${name}-${step}`);
    }
  }

  return keys;
}

/** Class tokens written inside an explicit class context on this line. */
function classContextTokens(text: string): string[] {
  const tokens: string[] = [];

  for (const match of text.matchAll(CLASS_LIST_ATTR)) {
    tokens.push(...(match[1] ?? match[2] ?? '').split(/\s+/));
  }
  for (const match of text.matchAll(CLASS_BINDING)) {
    tokens.push(match[1]);
  }

  return tokens.filter(Boolean);
}

/** Tokens inside any quoted run on the line, split as a class list. */
function quotedTokens(text: string): string[] {
  return [...text.matchAll(QUOTED)].flatMap((match) =>
    (match[1] ?? match[2] ?? match[3] ?? '').split(/\s+/).filter(Boolean),
  );
}

/** The scale key a token asks for, or `null` if it is not a utility of this family at all. */
function scaleKey(token: string, family: Family): { prefix: string; key: string } | null {
  const bare = token.replace(VARIANTS, '').replace(/^!/, '');
  const match = new RegExp(`^-?(${family.prefixes.join('|')})-(.+)$`).exec(bare);
  return match ? { prefix: match[1], key: match[2] } : null;
}

function violationsIn(file: string, family: Family): Violation[] {
  const found: Violation[] = [];
  const relative = file.slice(SRC_DIR.length + 1).replace(/\\/g, '/');

  readFileSync(file, 'utf-8')
    .split('\n')
    .forEach((text, index) => {
      const report = (token: string) => found.push({ file: relative, line: index + 1, utility: token });

      const check = (token: string, gate: (key: string) => boolean) => {
        const parsed = scaleKey(token, family);
        if (!parsed || !gate(parsed.key)) return;
        // Arbitrary values (`mt-[3px]`, `tracking-[0.06em]`) opt out of the scale on purpose and
        // are visible in review.
        if (parsed.key.startsWith('[')) return;
        if (!family.accepts(parsed.key, parsed.prefix)) report(token);
      };

      for (const token of classContextTokens(text)) check(token, () => true);

      const looseTokens =
        family.loose === 'quoted-strings' ? quotedTokens(text) : (text.match(looseUtility(family)) ?? []);
      const looseGate = family.loose === 'quoted-strings' ? CLASS_KEY : TSHIRT_KEY;
      for (const token of looseTokens) check(token, (key) => looseGate.test(key));
    });

  // A token can be caught by both passes on the same line.
  return found.filter(
    (v, i) =>
      found.findIndex((o) => o.file === v.file && o.line === v.line && o.utility === v.utility) === i,
  );
}

/** Loose scan for the t-shirt pass; the key is validated separately, so over-matching is harmless. */
function looseUtility(family: Family): RegExp {
  return new RegExp(`(?<![\\w-])-?(?:${family.prefixes.join('|')})-[^\\s"'\`<>{}]+`, 'g');
}

const theme = resolveConfig(tailwindConfig).theme;
const spacing = new Set(Object.keys(theme.spacing as Record<string, string>));
const fontSize = new Set(Object.keys(theme.fontSize as Record<string, unknown>));
const letterSpacing = new Set(Object.keys(theme.letterSpacing as Record<string, string>));
const colors = colorKeys(theme.colors as Record<string, unknown>);
const files = sourceFiles(SRC_DIR);

/** Reported as `file:line utility`, so a failure points straight at the markup to fix. */
const report = (family: Family) =>
  files.flatMap((file) => violationsIn(file, family)).map((v) => `${v.file}:${v.line} ${v.utility}`);

describe('design-system scale keys', () => {
  it('walks a non-empty source tree (guards against a silently passing scan)', () => {
    expect(files.length).toBeGreaterThan(100);
  });

  describe('spacing', () => {
    const family: Family = {
      prefixes: SPACING_PREFIXES,
      accepts: (key, prefix) => {
        if (NON_SCALE_VALUES.includes(key)) return true;
        // `auto` is a margin keyword, not a scale key — and valid only on margins.
        if (key === 'auto') return MARGIN_PREFIXES.includes(prefix);
        return spacing.has(key);
      },
      loose: 'tshirt-keys',
    };

    it('defines every named step DESIGN.md lists', () => {
      const named = ['3xs', '2xs', 'xs', 'sm', 'md', 'lg', 'xl', '2xl', '3xl'];

      expect(named.filter((key) => !spacing.has(key))).toEqual([]);
    });

    it('has no margin/padding/gap utility using an undefined scale key', () => {
      expect(report(family)).toEqual([]);
    });
  });

  describe('typography', () => {
    const textFamily: Family = {
      prefixes: ['text'],
      // Colour and size share the prefix: a key valid in either scale is a real utility.
      accepts: (key) => fontSize.has(key) || colors.has(key) || TEXT_KEYWORDS.includes(key),
      loose: 'quoted-strings',
    };
    const trackingFamily: Family = {
      prefixes: ['tracking'],
      accepts: (key) => letterSpacing.has(key),
      loose: 'quoted-strings',
    };

    it('defines every type step and tracking token DESIGN.md lists', () => {
      const steps = [
        'display',
        'h1',
        'h2',
        'h3',
        'h4',
        'lead',
        'body-lg',
        'body-md',
        'body-sm',
        'caption',
        'eyebrow',
      ];
      const tracking = ['tight', 'normal', 'wide', 'eyebrow'];

      expect(steps.filter((key) => !fontSize.has(key))).toEqual([]);
      expect(tracking.filter((key) => !letterSpacing.has(key))).toEqual([]);
    });

    it('has no text utility using a key that is neither a size nor a colour', () => {
      expect(report(textFamily)).toEqual([]);
    });

    it('has no tracking utility using an undefined letter-spacing key', () => {
      expect(report(trackingFamily)).toEqual([]);
    });
  });
});
