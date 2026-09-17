import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Card-surface guard (GH #302).
 *
 * `jh-card` has existed since feature 024, and by the time #302 was written the app had 40
 * uses of it and **59 hand-rolled copies of it** — `rounded-lg border bg-surface-card`,
 * written out by hand beside the primitive that exists to draw exactly that. They were not
 * carelessness: they were `<section>`s, `<li>`s, `<a>`s and `<ul>`s, and the primitive was
 * an *element*, so adopting it meant throwing the semantics away. The card is an attribute
 * now (`<section jhCard>`), which removes the reason to hand-roll one, and this guard
 * removes the drift that followed from it:
 *
 * - **the surface** — the hand-rolled copies disagreed about the border (`border-border-default`
 *   vs `border-border-muted`) and about whether there was a shadow at all, so "the JuggerHub
 *   card" was several slightly different boxes depending on which file you were in;
 * - **the padding** — DESIGN.md says `spacing.6` (24px). The app shipped three values, 16px
 *   on 32 of the 40 call sites, which is what #278 reported as text crowding the edge of
 *   the box. The primitive owns padding now, so a `p-…` class written back onto a card is
 *   the drift restarting.
 *
 * The two exempt surfaces below are the genuine article: a card at *one breakpoint*, which
 * the primitive cannot express — its `:host` rules are unconditional, and (component styles
 * being injected after the global sheet) a `md:border-0` utility on the host would lose to
 * them. Anything else belongs on `jhCard`. Never add an entry to make a failure go away;
 * adopt the primitive instead.
 */

const SRC_DIR = join(__dirname, '../../..');
const SCANNED_EXTENSIONS = ['.html', '.ts'];
const SELF = 'card-surface.spec.ts';
/** The primitive itself is where the surface is *supposed* to be defined. */
const PRIMITIVE = join('shared', 'ui', 'card', 'card.component');

/** `md:`, `hover:`, `[&>li]:` — any number of them, before the utility itself. */
const VARIANTS = /^(?:(?:\[[^\]]*\]|[^:\s[\]]+):)+/;
/** `class="…"` / `ngClass="…"` — a literal class list (see `scale-keys.spec.ts`). */
const CLASS_LIST_ATTR = /(?:\bclass|\bngClass)\s*=\s*(?:"([^"]*)"|'([^']*)')/g;
/** Any quoted run: where a `.ts` file keeps the class lists it builds. */
const QUOTED = /"([^"]*)"|'([^']*)'|`([^`]*)`/g;
/** Uniform padding — `p-md`, `p-xl`. `px-`/`py-` are how a `flush` card sets its own. */
const UNIFORM_PADDING = /^p-[a-z0-9]/;

interface Exemption {
  /** Source path, relative to `src/`. */
  file: string;
  /** A distinctive run of the offending class list — so the entry survives the line moving. */
  contains: string;
  why: string;
}

const EXEMPT: Exemption[] = [
  {
    file: 'app/features/settings/notifications/notification-settings.component.html',
    contains: 'md:rounded-lg md:border',
    why: 'The notification matrix is a card only from `md` up; below it the rows are their own cards.',
  },
  {
    file: 'app/features/settings/notifications/notification-settings.component.html',
    contains: 'md:rounded-none md:border-0',
    why: 'Each category is a card on mobile and dissolves into a row of that matrix at `md`.',
  },
];

interface Violation {
  file: string;
  line: number;
  detail: string;
}

function sourceFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) return sourceFiles(full);
    if (entry.name === SELF) return [];
    return SCANNED_EXTENSIONS.some((ext) => entry.name.endsWith(ext)) ? [full] : [];
  });
}

function relative(file: string): string {
  return file.slice(SRC_DIR.length + 1).replace(/\\/g, '/');
}

/**
 * Every class list written on this line. `tokens` has the variants stripped, so a surface
 * assembled out of `md:`-prefixed utilities is still recognised as one; `raw` keeps them,
 * because "a card only from `md` up" is precisely what an exemption is claiming.
 */
function classLists(text: string, isTypeScript: boolean): { raw: string; tokens: string[] }[] {
  const lists = [...text.matchAll(CLASS_LIST_ATTR)].map((m) => m[1] ?? m[2] ?? '');
  if (isTypeScript) {
    lists.push(...[...text.matchAll(QUOTED)].map((m) => m[1] ?? m[2] ?? m[3] ?? ''));
  }
  return lists.map((raw) => ({
    raw,
    tokens: raw
      .split(/\s+/)
      .filter(Boolean)
      .map((token) => token.replace(VARIANTS, '').replace(/^!/, '')),
  }));
}

/** The audit's own definition of a hand-rolled card: the three utilities `jhCard` replaces. */
function isCardSurface(tokens: string[]): boolean {
  return (
    tokens.includes('bg-surface-card') &&
    tokens.includes('rounded-lg') &&
    tokens.some((t) => t === 'border' || t.startsWith('border-'))
  );
}

function exempt(file: string, list: string): boolean {
  return EXEMPT.some((e) => file === e.file && list.includes(e.contains));
}

/**
 * The whole opening tag a match sits in, so a card written across several lines is read as
 * one element. Quotes are tracked because an Angular binding may hold a `>` of its own.
 */
function openingTagAt(text: string, index: number): string {
  const start = text.lastIndexOf('<', index);
  let quote: string | null = null;
  for (let i = start; i < text.length; i++) {
    const ch = text[i];
    if (quote) {
      if (ch === quote) quote = null;
    } else if (ch === '"' || ch === "'") {
      quote = ch;
    } else if (ch === '>') {
      return text.slice(start, i + 1);
    }
  }
  return text.slice(start);
}

const files = sourceFiles(SRC_DIR);

/** Class lists that redraw the card surface by hand. */
function handRolledSurfaces(): Violation[] {
  const found: Violation[] = [];

  for (const file of files) {
    const rel = relative(file);
    if (rel.includes(PRIMITIVE.replace(/\\/g, '/'))) continue;
    const isTypeScript = file.endsWith('.ts');

    readFileSync(file, 'utf-8')
      .split('\n')
      .forEach((text, index) => {
        for (const { raw, tokens } of classLists(text, isTypeScript)) {
          if (!isCardSurface(tokens) || exempt(rel, raw)) continue;
          found.push({ file: rel, line: index + 1, detail: raw });
        }
      });
  }

  return found;
}

/** Cards that write their own uniform padding, which is what the `padding` input is for. */
function cardsOverridingPadding(): Violation[] {
  const found: Violation[] = [];

  for (const file of files) {
    const rel = relative(file);
    if (rel.includes(PRIMITIVE.replace(/\\/g, '/'))) continue;
    const text = readFileSync(file, 'utf-8');

    for (const match of text.matchAll(/<jh-card\b|\bjhCard\b/g)) {
      const tag = openingTagAt(text, match.index);
      const padding = classLists(tag, false)
        .flatMap((list) => list.tokens)
        .filter((token) => UNIFORM_PADDING.test(token));
      if (padding.length === 0) continue;
      found.push({
        file: rel,
        line: text.slice(0, match.index).split('\n').length,
        detail: padding.join(' '),
      });
    }
  }

  return found;
}

const report = (violations: Violation[]) =>
  violations.map((v) => `${v.file}:${v.line} ${v.detail}`);

describe('card surface', () => {
  it('walks a non-empty source tree (guards against a silently passing scan)', () => {
    expect(files.length).toBeGreaterThan(100);
  });

  it('keeps the surface defined in one place — the primitive', () => {
    const css = readFileSync(join(SRC_DIR, 'app/shared/ui/card/card.component.css'), 'utf-8');

    expect(css).toContain('--radius-lg');
    expect(css).toContain('--surface-card');
    expect(css).toContain('--shadow-sm');
    expect(css).toContain('--border-muted');
  });

  it('has no hand-rolled card surface outside the primitive', () => {
    expect(report(handRolledSurfaces())).toEqual([]);
  });

  it('has no card that writes its own uniform padding', () => {
    expect(report(cardsOverridingPadding())).toEqual([]);
  });

  /*
   * #302's other half: both signature details were implemented in the primitive and enabled
   * nowhere — `interactive` zero times, so cards never answered the pointer at all. A
   * primitive nobody switches on is indistinguishable from one that was never written, and
   * the way that regresses is silently, one "simplifying" edit at a time.
   */
  it('ships the two card details DESIGN.md calls for', () => {
    const templates = files.filter((file) => file.endsWith('.html')).map((f) => readFileSync(f, 'utf-8'));
    const uses = (attribute: RegExp) => templates.filter((t) => attribute.test(t)).length;

    expect(uses(/\binteractive\b/)).toBeGreaterThan(0);
    expect(uses(/\baccent\b/)).toBeGreaterThan(0);
  });
});
