import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Scale-key guard for margin / padding / gap utilities (GH #137).
 *
 * Tailwind emits **no rule at all** for an undefined scale key. `py-3xs` and `mt-2xs` were used
 * across a dozen templates while the `spacing` scale in `tailwind.config.js` defined neither, so
 * the intended padding silently collapsed to zero: nothing failed, the build was clean, and the
 * UI review's "spacing composes from the scale tokens" check passed on inspection while the
 * rendered result did not match. Every template that copied a neighbouring line inherited it.
 *
 * This walks the app source and fails on any spacing utility whose key is not in the *resolved*
 * Tailwind scale — resolved from the config, not a hardcoded list, so the guard cannot drift.
 *
 * Two passes, because classes are written in two very different places:
 *
 * 1. **Class contexts** (`class="…"`, `[class]="…"`, `[ngClass]="…"`, `[class.x]`) — every token
 *    is checked against the full scale. Anything in there is unambiguously a class list.
 * 2. **Everywhere else, t-shirt keys only** — `.ts` files build class lists as bare string
 *    literals (`'gap-xs'` in `button.directive.ts`), which are indistinguishable from ordinary
 *    strings like the `'my-team'` route id. So outside a class context only keys shaped like a
 *    size token (`xs`, `2xl`, `3xs`…) are checked. `my-team` is not one; `py-3xs` is. That is the
 *    failure mode this guards: copying a size token the scale never defined.
 *
 * Scope is margin / padding / gap / space-between. Those are the utilities the design system's
 * named tokens are for, and where the bug lived. Sizing utilities (`w-`, `max-w-`, `top-`…) draw
 * on additional scales of their own and would make the extraction noisy for no added protection.
 *
 * Fix the markup, or add the token to `tailwind.config.js` — and to DESIGN.md's spacing table
 * first, since that is the source of truth for the scale. Never widen the allow-list to make a
 * class pass.
 */

// Both are CommonJS: the Tailwind config is a `module.exports` file the build itself loads.
const resolveConfig = require('tailwindcss/resolveConfig');
const tailwindConfig = require('../../../../tailwind.config.js');

const SRC_DIR = join(__dirname, '../../..');
const SCANNED_EXTENSIONS = ['.html', '.ts'];
const SELF = 'spacing-scale.spec.ts';

/** Longest-first, so `gap-x-md` is not read as `gap` + `x-md` and `mx-auto` not as `m` + `x-auto`. */
const MARGIN_PREFIXES = ['mx', 'my', 'mt', 'mb', 'ml', 'mr', 'ms', 'me', 'm'];
const PREFIXES = [
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

/** A whole token: optional negation, a spacing prefix, a key. Variants/`!` are stripped first. */
const UTILITY = new RegExp(`^(-?)(${PREFIXES.join('|')})-(.+)$`);

/** `md:`, `hover:`, `dark:hover:`, `[&>li]:` — any number of them, before the utility itself. */
const VARIANTS = /^(?:(?:\[[^\]]*\]|[^:\s[\]]+):)+/;

/** Size tokens: `xs`…`xl` plus the numbered steps (`2xl`, `3xs`). The shape the bug took. */
const TSHIRT_KEY = /^\d*(?:xs|sm|md|lg|xl)$/;

/**
 * `class="…"` / `ngClass="…"` — a literal class list. `[class]="…"` and `[ngClass]="…"` are
 * deliberately excluded: their value is an *expression*, so `active('my-team') ? …` would read as
 * a class token. The class strings those expressions resolve to live in `.ts` and are covered by
 * the second pass.
 */
const CLASS_LIST_ATTR = /(?:\bclass|\bngClass)\s*=\s*(?:"([^"]*)"|'([^']*)')/g;
/** `[class.mt-xs]="…"` — the utility is the binding name, not the value. */
const CLASS_BINDING = /\[class\.([^\]\s]+)\]/g;

/** Loose scan for the second pass; the key is validated separately, so over-matching is harmless. */
const LOOSE_UTILITY = new RegExp(`(?<![\\w-])-?(?:${PREFIXES.join('|')})-[^\\s"'\`<>{}]+`, 'g');

/** `space-{x,y}-reverse` toggles a CSS variable rather than taking a scale value. */
const NON_SCALE_VALUES = ['reverse'];

interface Violation {
  file: string;
  line: number;
  utility: string;
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

/** The scale key a token asks for, or `null` if it is not a spacing utility at all. */
function spacingKey(token: string): { prefix: string; key: string } | null {
  const bare = token.replace(VARIANTS, '').replace(/^!/, '');
  const match = UTILITY.exec(bare);
  return match ? { prefix: match[2], key: match[3] } : null;
}

function isDefined(prefix: string, key: string, scale: Set<string>): boolean {
  // Arbitrary values (`mt-[3px]`) opt out of the scale on purpose and are visible in review.
  if (key.startsWith('[')) return true;
  if (NON_SCALE_VALUES.includes(key)) return true;
  // `auto` is a margin keyword, not a scale key — and valid only on margins.
  if (key === 'auto') return MARGIN_PREFIXES.includes(prefix);
  return scale.has(key);
}

function violationsIn(file: string, scale: Set<string>): Violation[] {
  const found: Violation[] = [];
  const relative = file.slice(SRC_DIR.length + 1).replace(/\\/g, '/');

  readFileSync(file, 'utf-8')
    .split('\n')
    .forEach((text, index) => {
      const report = (token: string) => found.push({ file: relative, line: index + 1, utility: token });

      for (const token of classContextTokens(text)) {
        const spacing = spacingKey(token);
        if (spacing && !isDefined(spacing.prefix, spacing.key, scale)) report(token);
      }

      for (const token of text.match(LOOSE_UTILITY) ?? []) {
        const spacing = spacingKey(token);
        if (!spacing || !TSHIRT_KEY.test(spacing.key)) continue;
        if (!scale.has(spacing.key)) report(token);
      }
    });

  // A token can be caught by both passes on the same line.
  return found.filter(
    (v, i) =>
      found.findIndex((o) => o.file === v.file && o.line === v.line && o.utility === v.utility) === i,
  );
}

describe('spacing scale', () => {
  const scale = new Set(
    Object.keys(resolveConfig(tailwindConfig).theme.spacing as Record<string, string>),
  );
  const files = sourceFiles(SRC_DIR);

  it('defines every named step DESIGN.md lists', () => {
    const named = ['3xs', '2xs', 'xs', 'sm', 'md', 'lg', 'xl', '2xl', '3xl'];

    expect(named.filter((key) => !scale.has(key))).toEqual([]);
  });

  it('walks a non-empty source tree (guards against a silently passing scan)', () => {
    expect(files.length).toBeGreaterThan(100);
  });

  it('has no margin/padding/gap utility using an undefined scale key', () => {
    const violations = files.flatMap((file) => violationsIn(file, scale));

    // Reported as `file:line utility` so a failure points straight at the markup to fix.
    expect(violations.map((v) => `${v.file}:${v.line} ${v.utility}`)).toEqual([]);
  });
});
