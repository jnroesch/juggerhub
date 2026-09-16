import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Token-level contrast guard (GH #298).
 *
 * DESIGN.md says: "**Do** maintain WCAG AA contrast (≥ 4.5:1 for body text). The sand text
 * ramp on light surfaces is tuned for this." It was not, and nothing in the repo could tell
 * anyone — there was no contrast test anywhere. Measured against the tokens the build
 * actually emits, the `subtle` step — the app's default secondary text, on 305 elements — was
 * **2.92:1** on white and 2.53:1 on `surface-sunken`; `faint` was 1.97:1 on 59 more; the
 * white label on every primary button was **3.14:1**; and the focus ring was coral-1, at
 * **1.32:1** the invisible half of the "2px coral border + soft coral ring" DESIGN.md
 * describes. Roughly a third of the text on screen sat below the floor, which is the
 * literal, measurable component of the "bland / washed out" report in #278.
 *
 * So this file is arithmetic, not a browser: WCAG 2.1 relative luminance over the hex
 * values in `styles.css`, reached the way the app reaches them — through the *resolved*
 * Tailwind theme, so a token renamed in the config without being repointed in the CSS is a
 * failure here rather than a silent `var(--nothing)`.
 *
 * ### What is asserted
 *
 * 1. **Foregrounds** clear 4.5:1 on every surface they are used on. Not "on white": the
 *    step that broke was one that passed on white (`text-muted`, 4.66:1) and failed on
 *    `surface-sunken` (4.03:1), and a token whose legibility depends on which panel it
 *    landed in is not a token. Each entry below therefore names its surfaces, and the
 *    neutral ramp — the only foreground the product puts on `surface-muted`, the darkest
 *    light fill — is measured against that too.
 * 2. **`text-on-accent`** clears 4.5:1 on every solid fill the product puts it on. This is
 *    the primary-CTA defect: white on coral-4 is 3.14:1, and the large-text allowance of
 *    3.0 needs ≥18.7px bold, where the button label is 16px semibold.
 * 3. **Non-text** — the focus indicator and the border that draws an input or a secondary
 *    button — clears WCAG 1.4.11's 3:1 on those same surfaces.
 * 4. **The markup agrees with the table.** Every `text-<colour>` utility written in the app
 *    names a token listed in (1), and no class list pairs a fill with a foreground the
 *    arithmetic rejects. Without this last part the table would only describe the tokens
 *    someone remembered to add to it, and `bg-brand text-on-accent` — the defect itself —
 *    would be free to come back one call site at a time.
 *
 * Fix the token or fix the markup. Never lower a threshold, and never add a token to the
 * foreground table without its surfaces: the table is the claim DESIGN.md makes.
 *
 * ### Not covered
 *
 * Gradient fills (`bg-brand-gradient`, coral-4 → teal-4) carry `text-on-accent` on the mark
 * tile and the avatar fallbacks. A gradient has no single background colour to measure, and
 * those two are single glyphs rather than copy; the palette work in #278 owns them.
 */

// Both are CommonJS: the Tailwind config is a `module.exports` file the build itself loads.
const resolveConfig = require('tailwindcss/resolveConfig');
const tailwindConfig = require('../../../../tailwind.config.js');

const SRC_DIR = join(__dirname, '../../..');
const STYLES = join(SRC_DIR, 'styles.css');
const SCANNED_EXTENSIONS = ['.html', '.ts'];
/** This file names every retired colour in prose; scanning it would report itself. */
const SELF = 'contrast.spec.ts';

/** WCAG AA for body text, and WCAG 1.4.11 for a UI component boundary or a focus indicator. */
const AA_TEXT = 4.5;
const AA_NON_TEXT = 3;

/** The neutral page surfaces the product sets text on, lightest to darkest. */
const PAGE_SURFACES = ['surface-card', 'surface-raised', 'surface-page', 'surface-sunken'];
/**
 * `surface-muted` (sand-2) is the darkest light fill — chat bubbles, toggle tracks, avatar
 * placeholders — and the neutral ramp is the only foreground the product puts on it.
 */
const ALL_LIGHT_SURFACES = [...PAGE_SURFACES, 'surface-muted'];

/** Every flat fill that carries `text-on-accent`. */
const SOLID_FILLS = [
  'brand-strong',
  'brand-strong-hover',
  'secondary-strong',
  'secondary-strong-hover',
  'danger',
  'danger-fg',
  'success',
  'info',
  'warning-fg',
  'surface-inverse',
];

/**
 * Foreground token → the surfaces it is used on. A Tailwind colour key may be written as
 * `text-<key>` only if it appears here, and then only if it clears 4.5:1 on all of them.
 */
const FOREGROUNDS: { token: string; on: string[] }[] = [
  { token: 'heading', on: ALL_LIGHT_SURFACES },
  { token: 'body', on: ALL_LIGHT_SURFACES },
  { token: 'muted', on: ALL_LIGHT_SURFACES },
  { token: 'link', on: PAGE_SURFACES },
  { token: 'link-hover', on: PAGE_SURFACES },
  // Coral and sage as text, each also on its own soft tint.
  { token: 'brand-strong', on: [...PAGE_SURFACES, 'surface-accent-soft'] },
  { token: 'secondary-strong', on: [...PAGE_SURFACES, 'surface-secondary-soft'] },
  // Status text, each also inside its own banner.
  { token: 'success-fg', on: [...PAGE_SURFACES, 'success-bg'] },
  { token: 'danger-fg', on: [...PAGE_SURFACES, 'danger-bg'] },
  { token: 'warning-fg', on: [...PAGE_SURFACES, 'warning-bg'] },
  { token: 'info-fg', on: [...PAGE_SURFACES, 'info-bg'] },
  // The two foregrounds for dark ground.
  { token: 'on-accent', on: SOLID_FILLS },
  { token: 'on-inverse', on: ['surface-inverse'] },
];

/** Non-text tokens that must read against the surface behind them (WCAG 1.4.11). */
const NON_TEXT: { token: string; on: string[] }[] = [
  { token: 'border-focus', on: ALL_LIGHT_SURFACES },
  { token: 'border-strong', on: ALL_LIGHT_SURFACES },
];

const FOREGROUND_TOKENS = new Set(FOREGROUNDS.map((f) => f.token));

/** `md:`, `hover:`, `focus-visible:` — any number of them, before the utility itself. */
const VARIANTS = /^(?:(?:\[[^\]]*\]|[^:\s[\]]+):)+/;
/**
 * A double- or single-quoted run. Backticks are deliberately left out: no class list in the
 * app is built from a template literal, while a class name quoted in Markdown backticks
 * inside a doc comment looks exactly like one — which is how the note in
 * `alert.component.ts` recording a token's retirement read as a use of it.
 */
const QUOTED = /"([^"]*)"|'([^']*)'/g;
/** A single-quoted run *inside* one, i.e. a branch of `[class]="x ? 'a b' : 'c d'"`. */
const NESTED = /'([^']*)'/g;
/** Everything that opens a string in TypeScript, for the walk over a `.ts` file below. */
const STRING_DELIMITERS = ['"', "'", '`'];
/** `[class.mt-xs]="…"` — the utility is the binding name, not the value. */
const CLASS_BINDING = /\[class\.([^\]\s]+)\]/g;

// ---- Colour arithmetic ------------------------------------------------------------------

/** WCAG 2.1 relative luminance of an `#rrggbb` string. */
function luminance(hex: string): number {
  const channels = [1, 3, 5]
    .map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
    .map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4));
  return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
}

/** WCAG 2.1 contrast ratio, 1:1 … 21:1. */
function contrast(a: string, b: string): number {
  const [lighter, darker] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (lighter + 0.05) / (darker + 0.05);
}

const round = (ratio: number) => Math.round(ratio * 100) / 100;

// ---- The tokens the build actually emits ------------------------------------------------

/**
 * `--name: #hex` and `--name: var(--other)` from `styles.css`, resolved through the
 * indirection so `--text-link → --brand-primary-strong → --coral-6` lands on a hex.
 */
function cssVariables(): Map<string, string> {
  const source = readFileSync(STYLES, 'utf-8');
  const direct = new Map<string, string>();

  for (const [, name, value] of source.matchAll(/--([a-z0-9-]+):\s*([^;]+);/g)) {
    direct.set(name, value.trim());
  }

  const resolved = new Map<string, string>();
  for (const name of direct.keys()) {
    let value = direct.get(name);
    // A short chain by construction; the bound stops a cycle from hanging the suite.
    for (let hop = 0; hop < 10 && value?.startsWith('var(--'); hop += 1) {
      value = direct.get(value.slice(6, value.indexOf(')')));
    }
    if (value && /^#[0-9a-f]{6}$/i.test(value)) resolved.set(name, value.toLowerCase());
  }

  return resolved;
}

/** Every colour key in the resolved theme, flattened the way Tailwind names the utilities. */
function themeColors(colors: Record<string, unknown>): Map<string, string> {
  const flat = new Map<string, string>();

  for (const [name, value] of Object.entries(colors)) {
    if (typeof value === 'string') {
      flat.set(name, value);
      continue;
    }
    for (const [step, stepValue] of Object.entries(value as Record<string, string>)) {
      flat.set(step === 'DEFAULT' ? name : `${name}-${step}`, stepValue);
    }
  }

  return flat;
}

const variables = cssVariables();
const themeKeys = themeColors(resolveConfig(tailwindConfig).theme.colors as Record<string, unknown>);

/** A Tailwind colour key → the hex it renders as, or `null` if it is not a flat colour. */
function hexOf(key: string): string | null {
  const value = themeKeys.get(key);
  if (!value) return null;
  if (/^#[0-9a-f]{6}$/i.test(value)) return value.toLowerCase();
  const match = /^var\(--([a-z0-9-]+)\)$/.exec(value);
  return match ? (variables.get(match[1]) ?? null) : null;
}

/** Fails loudly rather than silently skipping a token that has stopped resolving. */
function requireHex(key: string): string {
  const hex = hexOf(key);
  if (!hex) throw new Error(`token "${key}" does not resolve to a hex colour in styles.css`);
  return hex;
}

// ---- The markup -------------------------------------------------------------------------

function sourceFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) return sourceFiles(full);
    if (entry.name === SELF) return [];
    // A spec names a class to assert its *absence* — `not.toContain('text-danger')` is the
    // retirement holding, not a violation of it. The shipping surface is templates and
    // components.
    if (entry.name.endsWith('.spec.ts')) return [];
    return SCANNED_EXTENSIONS.some((ext) => entry.name.endsWith(ext)) ? [full] : [];
  });
}

/**
 * The class lists on this line, each kept whole — a fill and the label over it have to stay
 * together for the pairing check to mean anything.
 *
 * The one subtlety is `[class]="cond ? 'a b' : 'c d'"`. That is a single double-quoted run
 * holding **two mutually exclusive** lists, and reading it whole pairs the coral fill of one
 * branch with the dark label of the other — a contradiction no element ever renders. So a run
 * that contains quoted runs of its own yields those instead of itself.
 */
function listsOnLine(text: string): string[][] {
  const split = (value: string) => value.split(/\s+/).filter(Boolean);
  const lists: string[][] = [];

  for (const match of text.matchAll(QUOTED)) {
    const run = match[1] ?? match[2] ?? '';
    const branches = [...run.matchAll(NESTED)].map((branch) => branch[1]);
    lists.push(...(branches.length ? branches.map(split) : [split(run)]));
  }
  // A one-class binding, which can never be half of a pair.
  for (const match of text.matchAll(CLASS_BINDING)) {
    lists.push([match[1]]);
  }

  return lists;
}

/** The colour key a `text-`/`bg-` utility asks for, or `null` if it is not one. */
function colorKey(token: string, prefix: 'text' | 'bg'): string | null {
  const bare = token.replace(VARIANTS, '').replace(/^!/, '');
  if (!bare.startsWith(`${prefix}-`)) return null;
  const key = bare.slice(prefix.length + 1);
  return hexOf(key) ? key : null;
}

/**
 * Quoted runs grouped by the `[…]` array literal holding them, with the line the literal
 * opens on.
 *
 * This is how `button.directive.ts` and `chip.directive.ts` — the two files that decide what
 * every button and every chip in the product is coloured — write a class list: one string per
 * array entry, one entry per line. Line-at-a-time reading never sees `'bg-brand'` and
 * `'text-on-accent'` together, so the defect this whole file exists for would have been
 * invisible in the one place it actually shipped from.
 *
 * The walk skips strings and comments, so neither a `[` inside a string literal nor the
 * apostrophe in a comment's own prose can throw the bracket depth off.
 */
function arrayLiterals(source: string): { line: number; tokens: string[] }[] {
  const groups: { line: number; tokens: string[] }[] = [];
  const open: { at: number; line: number }[] = [];
  let line = 1;

  for (let i = 0; i < source.length; i += 1) {
    const char = source[i];

    if (char === '\n') {
      line += 1;
    } else if (char === '/' && source[i + 1] === '/') {
      for (i += 2; i < source.length && source[i] !== '\n'; i += 1);
      i -= 1;
    } else if (char === '/' && source[i + 1] === '*') {
      for (i += 2; i < source.length && !(source[i] === '*' && source[i + 1] === '/'); i += 1) {
        if (source[i] === '\n') line += 1;
      }
      i += 1;
    } else if (STRING_DELIMITERS.includes(char)) {
      // Skip the string whole; `\'` inside it is an escaped quote, not the end.
      for (i += 1; i < source.length && source[i] !== char; i += 1) {
        if (source[i] === '\\') i += 1;
        else if (source[i] === '\n') line += 1;
      }
    } else if (char === '[') {
      open.push({ at: i, line });
    } else if (char === ']') {
      const start = open.pop();
      if (!start) continue;
      const body = source.slice(start.at + 1, i);
      const tokens = [...body.matchAll(QUOTED)].flatMap((m) =>
        (m[1] ?? m[2] ?? '').split(/\s+/).filter(Boolean),
      );
      if (tokens.length) groups.push({ line: start.line, tokens });
    }
  }

  return groups;
}

const files = sourceFiles(SRC_DIR);
/** Every class list in the app, as `file:line` plus its tokens. */
const classLists: { file: string; line: number; tokens: string[] }[] = files.flatMap((file) => {
  const relative = file.slice(SRC_DIR.length + 1).replace(/\\/g, '/');
  const source = readFileSync(file, 'utf-8');

  const perLine = source
    .split('\n')
    .flatMap((text, index) =>
      listsOnLine(text).map((tokens) => ({ file: relative, line: index + 1, tokens })),
    );
  const perLiteral = file.endsWith('.ts')
    ? arrayLiterals(source).map(({ line, tokens }) => ({ file: relative, line, tokens }))
    : [];

  return [...perLine, ...perLiteral];
});

// ---- The assertions ---------------------------------------------------------------------

describe('design-system contrast', () => {
  it('reads the tokens the build emits (guards against a silently empty scan)', () => {
    expect(variables.size).toBeGreaterThan(40);
    expect(files.length).toBeGreaterThan(100);
    expect(requireHex('brand')).toBe('#f5623a');
  });

  describe('text', () => {
    it.each(FOREGROUNDS)('$token clears AA on every surface it is used on', ({ token, on }) => {
      const fg = requireHex(token);
      const failing = on
        .map((surface) => ({ surface, ratio: round(contrast(fg, requireHex(surface))) }))
        .filter(({ ratio }) => ratio < AA_TEXT);

      expect(failing).toEqual([]);
    });

    it('has no text utility naming a colour that is not a listed foreground', () => {
      const offenders = classLists.flatMap(({ file, line, tokens }) =>
        tokens
          .map((token) => colorKey(token, 'text'))
          .filter((key): key is string => key !== null && !FOREGROUND_TOKENS.has(key))
          .map((key) => `${file}:${line} text-${key}`),
      );

      expect([...new Set(offenders)].sort()).toEqual([]);
    });

    it('never pairs a fill with a label the arithmetic rejects', () => {
      const offenders = classLists.flatMap(({ file, line, tokens }) => {
        const fills = tokens.map((t) => colorKey(t, 'bg')).filter((k): k is string => k !== null);
        const labels = tokens.map((t) => colorKey(t, 'text')).filter((k): k is string => k !== null);

        return fills.flatMap((fill) =>
          labels
            .map((label) => ({ label, ratio: round(contrast(requireHex(fill), requireHex(label))) }))
            .filter(({ ratio }) => ratio < AA_TEXT)
            .map(({ label, ratio }) => `${file}:${line} text-${label} on bg-${fill} = ${ratio}:1`),
        );
      });

      expect([...new Set(offenders)].sort()).toEqual([]);
    });
  });

  describe('non-text', () => {
    it.each(NON_TEXT)('$token clears 3:1 on every surface behind it', ({ token, on }) => {
      const fg = requireHex(token);
      const failing = on
        .map((surface) => ({ surface, ratio: round(contrast(fg, requireHex(surface))) }))
        .filter(({ ratio }) => ratio < AA_NON_TEXT);

      expect(failing).toEqual([]);
    });

    it('focuses everything with one token', () => {
      expect(resolveConfig(tailwindConfig).theme.ringColor.focus).toBe('var(--border-focus)');
    });
  });

  describe('the retired steps stay retired', () => {
    /** Why each one went, so a future palette tune reads the number rather than the name. */
    const retired: [string, number][] = [
      ['subtle', 2.92],
      ['faint', 1.97],
    ];

    it.each(retired)('%s (%s:1 on white) is not a colour key any more', (key) => {
      expect(themeKeys.has(key)).toBe(false);
    });

    it('measures the steps they were, so the numbers above stay checkable', () => {
      expect(round(contrast(requireHex('sand-5'), requireHex('surface-card')))).toBe(2.92);
      expect(round(contrast(requireHex('sand-4'), requireHex('surface-card')))).toBe(1.97);
      expect(round(contrast(requireHex('brand'), requireHex('on-accent')))).toBe(3.14);
    });
  });
});
