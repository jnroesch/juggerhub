import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Opacity-modifier guard for the design-system colours (GH #317, #322).
 *
 * Every colour in `tailwind.config.js` is a CSS custom property, and until #322 each was
 * registered as the bare string `var(--x)`. Tailwind cannot derive an alpha channel from that:
 * for `bg-brand/60` it parses the value as a colour, fails, and **drops the candidate** — no rule,
 * no warning, clean build. Five sites shipped that way, each fully transparent rather than faded:
 * the filter sheet's backdrop (a scrim that dimmed nothing), the profile's sticky save bar (the
 * form scrolled through the buttons), the event wizard's completed knobs (a row of holes beside
 * one coral knob), and the own-message attachment's border and hover tint. Only `bg-black/40`
 * ever worked, because `black` is a Tailwind default hex rather than one of ours. It is the
 * `py-3xs` / `text-muted` failure mode of `scale-keys.spec.ts` in a namespace that guard does
 * not watch: `surface-inverse` is a perfectly good key, and the `/40` is what killed it.
 *
 * The colours are functions of the requested alpha now (`withAlpha` in the config), so a
 * modifier composes through `color-mix()`. This file is what keeps that true, and it does not
 * trust the config to do it: it collects every colour utility carrying a modifier from the app
 * source, **compiles them through Tailwind itself**, and fails on any that emits no rule — the
 * one check that would have caught all five, and that no reading of a class list can replace.
 *
 * Three more pins:
 *
 * - **The plain form is byte-identical to what it always was** — `background-color:
 *   var(--brand-primary)`, no `--tw-bg-opacity` beside it. That is why the legacy
 *   `*-opacity-*` core plugins are switched off: they work by writing that variable into every
 *   colour rule, which would have forced the `color-mix` form onto every plain use and handed a
 *   browser without `color-mix()` a colourless page instead of a missing scrim.
 * - **A modifier composes to exactly the token at that alpha**, not to something near it.
 * - **Nobody writes the legacy `bg-opacity-50` spelling**, which the switched-off plugins would
 *   now ignore just as silently. The slash modifier is the one spelling.
 *
 * Fix the markup, or fix the config. Never drop a prefix from the list below to make a class
 * pass.
 */

// All CommonJS: the Tailwind config is a `module.exports` file the build itself loads, and the
// compile here is the build's own pipeline with the app's config and a raw content string.
const postcss = require('postcss');
const tailwindcss = require('tailwindcss');
const tailwindConfig = require('../../../../tailwind.config.js');

const SRC_DIR = join(__dirname, '../../..');
const SCANNED_EXTENSIONS = ['.html', '.ts'];
/** This file names the offending classes in prose; scanning it would report itself. */
const SELF = 'color-alpha.spec.ts';

/** Every utility family that takes a colour, and therefore a modifier. Longest-first. */
const COLOR_PREFIXES = [
  'ring-offset',
  'placeholder',
  'decoration',
  'outline',
  'divide',
  'border',
  'shadow',
  'stroke',
  'accent',
  'caret',
  'from',
  'fill',
  'ring',
  'text',
  'via',
  'bg',
  'to',
];

/**
 * `bg-brand/60`, `border-on-accent/30`, `bg-brand/[0.37]` — a colour utility with a modifier,
 * read without its variants (`hover:bg-brand/10` yields `bg-brand/10`; the variant changes the
 * selector, not the colour). The look-behind keeps a `/` inside a path (`/api/bg-1/3`) from
 * starting a match, and the look-ahead stops at the end of the class token.
 */
const MODIFIED_COLOR = new RegExp(
  `(?<![\\w/[-])(?:${COLOR_PREFIXES.join('|')})-[a-z0-9-]+/(?:\\d+|\\[[^\\]\\s]+\\])(?![\\w/-])`,
  'g',
);

/** The retired spelling — `bg-opacity-50` and its text, border, ring, divide and placeholder siblings. */
const LEGACY_OPACITY = /(?<![\w-])(?:bg|text|border|ring|divide|placeholder)-opacity-\d+(?![\w-])/g;

interface Use {
  file: string;
  line: number;
  utility: string;
}

function sourceFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) return sourceFiles(full);
    if (entry.name === SELF) return [];
    return SCANNED_EXTENSIONS.some((ext) => entry.name.endsWith(ext)) ? [full] : [];
  });
}

/** Every match of `pattern` in the app source, as `file:line utility`. */
function usesOf(pattern: RegExp): Use[] {
  return sourceFiles(SRC_DIR).flatMap((file) => {
    const relative = file.slice(SRC_DIR.length + 1).replace(/\\/g, '/');
    return readFileSync(file, 'utf-8')
      .split('\n')
      .flatMap((text, index) =>
        [...text.matchAll(pattern)].map((match) => ({ file: relative, line: index + 1, utility: match[0] })),
      );
  });
}

/**
 * Compiles a class list through Tailwind with the app's config, exactly as the build does, and
 * returns the selectors it emitted — unescaped, so `.bg-brand\/60` reads back as `.bg-brand/60`.
 */
async function compile(classes: string[]): Promise<{ css: string; selectors: string[] }> {
  const result = await postcss([
    tailwindcss({
      ...tailwindConfig,
      content: [{ raw: classes.join(' '), extension: 'html' }],
      corePlugins: { ...tailwindConfig.corePlugins, preflight: false },
    }),
  ]).process('@tailwind utilities;', { from: undefined });

  const selectors: string[] = [];
  result.root.walkRules((rule: { selectors: string[] }) => {
    selectors.push(...rule.selectors.map((s) => s.replace(/\\(.)/g, '$1')));
  });

  return { css: result.css, selectors };
}

/** Whether a utility produced a rule — `.divide-x/50 > :not([hidden]) ~ …` counts for `divide-x/50`. */
function emitted(selectors: string[], utility: string): boolean {
  const own = `.${utility}`;
  return selectors.some((s) => s === own || s.startsWith(`${own} `) || s.startsWith(`${own}:`));
}

describe('design-system colour opacity modifiers', () => {
  const modified = usesOf(MODIFIED_COLOR);
  const utilities = [...new Set(modified.map((use) => use.utility))].sort();

  it('walks a non-empty source tree and finds the modifiers the app is known to write', () => {
    expect(sourceFiles(SRC_DIR).length).toBeGreaterThan(100);
    // The scrim and the sticky bar — the two #317 was reported about — are in the set.
    expect(utilities).toEqual(expect.arrayContaining(['bg-surface-inverse/40', 'bg-surface-page/80']));
  });

  it('emits a rule for every colour modifier written in the app', async () => {
    const { selectors } = await compile(utilities);

    const silent = modified
      .filter((use) => !emitted(selectors, use.utility))
      .map((use) => `${use.file}:${use.line} ${use.utility}`);

    expect(silent).toEqual([]);
  });

  it('composes a modifier to exactly the token at that alpha', async () => {
    const { css } = await compile(['bg-brand/60', 'border-on-accent/30', 'bg-surface-page/[0.37]']);

    expect(css).toContain('background-color: color-mix(in srgb, var(--brand-primary) 60%, transparent)');
    expect(css).toContain('border-color: color-mix(in srgb, var(--text-on-accent) 30%, transparent)');
    expect(css).toContain('background-color: color-mix(in srgb, var(--surface-page) 37%, transparent)');
  });

  it('leaves the plain utility byte-identical — a bare var(), no opacity variable beside it', async () => {
    const { css } = await compile(['bg-brand', 'text-heading', 'border-border-strong', 'ring-focus']);

    expect(css).toContain('background-color: var(--brand-primary)');
    expect(css).toContain('color: var(--text-heading)');
    expect(css).toContain('border-color: var(--border-strong)');
    expect(css).toContain('--tw-ring-color: var(--border-focus)');
    expect(css).not.toMatch(/--tw-(?:bg|text|border|ring)-opacity/);
  });

  it('has no legacy *-opacity-N utility, which the switched-off plugins would ignore', () => {
    expect(usesOf(LEGACY_OPACITY).map((use) => `${use.file}:${use.line} ${use.utility}`)).toEqual([]);
  });
});
