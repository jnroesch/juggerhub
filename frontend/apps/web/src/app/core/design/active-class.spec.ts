import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Cascade guard for conditionally applied colour classes (GH #318).
 *
 * The active Browse tab never got its dark label. `routerLinkActive="… text-heading"` sat on top
 * of a base `class="… text-muted"`, and `.text-heading` and `.text-muted` are plain single-class
 * selectors of identical specificity — so stylesheet order decides, Tailwind emits
 * `.text-heading` before `.text-muted`, and the inactive colour won on the active tab. It had
 * never worked, under either spelling the token ever had, and it read as "slightly flat" rather
 * than broken because `bg-surface-card` and `shadow-sm` had no competitor and did apply.
 * `hover:text-heading` was fine: the pseudo-class outranks a bare class.
 *
 * The rule this enforces: **a static `class` list must not name a colour of the same property
 * as a class the element applies conditionally** — through `routerLinkActive`, a `[class.x]`
 * binding, or a branch of `[class]` / `[ngClass]`. A conditional class cannot win a
 * same-specificity race by being added later; the base list has to stop naming the colour and
 * the binding has to spell out both states, the way `top-nav` and `admin-shell` do:
 *
 *     class="flex … font-medium"
 *     [class]="active() ? 'bg-surface-card text-heading' : 'text-muted hover:text-heading'"
 *
 * Deliberately order-independent: `class="text-heading" [class.text-muted]="…"` happens to work
 * because `.text-muted` sorts later, and "works because of the alphabet" is exactly the thing
 * this guard exists to remove. Only bare utilities count — a variant (`hover:`, `md:`) changes
 * the selector's specificity or scope, so `text-muted hover:text-heading` is not a race.
 *
 * Fix the markup. Never add an exception here.
 */

// CommonJS: the Tailwind config is a `module.exports` file the build itself loads.
const resolveConfig = require('tailwindcss/resolveConfig');
const tailwindConfig = require('../../../../tailwind.config.js');

const SRC_DIR = join(__dirname, '../../..');
const SELF = 'active-class.spec.ts';

/**
 * Colour utilities by the property they set. `border-t` and `border` both write a border colour;
 * `text-` is a colour only when its key is one (`text-body-sm` is a size, `text-body` a colour).
 */
const PROPERTY_OF: [RegExp, string][] = [
  [/^text-(.+)$/, 'color'],
  [/^bg-(.+)$/, 'background-color'],
  [/^border(?:-[xytblrse])?-(.+)$/, 'border-color'],
  [/^ring-(.+)$/, 'ring-color'],
  [/^outline-(.+)$/, 'outline-color'],
  [/^divide-(.+)$/, 'divide-color'],
  [/^decoration-(.+)$/, 'decoration-color'],
  [/^shadow-(.+)$/, 'shadow-color'],
];

/** `class="…"` on the tag — the static list. */
const STATIC_CLASS = /(?:^|\s)class\s*=\s*(?:"([^"]*)"|'([^']*)')/;
/** `routerLinkActive="…"` — classes the router adds while the link is active. */
const ROUTER_LINK_ACTIVE = /(?:^|\s)routerLinkActive\s*=\s*(?:"([^"]*)"|'([^']*)')/g;
/** `[class.text-heading]="…"` — the utility is the binding name. */
const CLASS_BINDING = /\[class\.([^\]\s]+)\]/g;
/** `[class]="…"` / `[ngClass]="…"` — an expression whose quoted runs are the class lists. */
const CLASS_EXPRESSION = /\[(?:class|ngClass)\]\s*=\s*(?:"([^"]*)"|'([^']*)')/g;
const QUOTED_RUN = /"([^"]*)"|'([^']*)'/g;

interface Tag {
  line: number;
  text: string;
}

interface Violation {
  file: string;
  line: number;
  detail: string;
}

/** Every key in the resolved colour theme, flattened the way Tailwind names the utilities. */
function colorKeys(colors: Record<string, unknown>): Set<string> {
  const keys = new Set<string>();
  for (const [name, value] of Object.entries(colors)) {
    if (typeof value === 'string' || typeof value === 'function') {
      keys.add(name);
      continue;
    }
    for (const step of Object.keys(value as Record<string, unknown>)) {
      keys.add(step === 'DEFAULT' ? name : `${name}-${step}`);
    }
  }
  return keys;
}

const colors = colorKeys(resolveConfig(tailwindConfig).theme.colors as Record<string, unknown>);

/**
 * Every opening tag in a template with the line it starts on. Quote-aware, so a `>` inside a
 * binding (`[class]="i > 0 ? …"`) does not end the tag early, and comment-aware, so an example
 * in a `<!-- … -->` note is not read as markup.
 */
function openingTags(source: string): Tag[] {
  const tags: Tag[] = [];
  let line = 1;

  for (let i = 0; i < source.length; i += 1) {
    const char = source[i];
    if (char === '\n') {
      line += 1;
      continue;
    }
    if (source.startsWith('<!--', i)) {
      const end = source.indexOf('-->', i + 4);
      const stop = end === -1 ? source.length : end + 3;
      line += (source.slice(i, stop).match(/\n/g) ?? []).length;
      i = stop - 1;
      continue;
    }
    if (char !== '<' || !/[a-zA-Z]/.test(source[i + 1] ?? '')) continue;

    const start = i;
    const startLine = line;
    let quote: string | null = null;
    for (i += 1; i < source.length; i += 1) {
      const c = source[i];
      if (c === '\n') line += 1;
      if (quote) {
        if (c === quote) quote = null;
      } else if (c === '"' || c === "'") {
        quote = c;
      } else if (c === '>') {
        break;
      }
    }
    tags.push({ line: startLine, text: source.slice(start, i + 1) });
  }

  return tags;
}

const split = (list: string | undefined) => (list ?? '').split(/\s+/).filter(Boolean);

/** The property a bare colour utility sets, or `null` for anything else (a variant, a size…). */
function colorProperty(token: string): string | null {
  if (token.includes(':')) return null;
  const bare = token.replace(/^!/, '').replace(/\/(?:\d+|\[[^\]]+\])$/, '');
  for (const [pattern, property] of PROPERTY_OF) {
    const match = pattern.exec(bare);
    if (match && colors.has(match[1])) return property;
  }
  return null;
}

/** The colour utilities an element applies only sometimes, with where each comes from. */
function conditionalClasses(tag: string): { source: string; token: string }[] {
  const found: { source: string; token: string }[] = [];

  for (const match of tag.matchAll(ROUTER_LINK_ACTIVE)) {
    for (const token of split(match[1] ?? match[2])) found.push({ source: 'routerLinkActive', token });
  }
  for (const match of tag.matchAll(CLASS_BINDING)) {
    found.push({ source: `[class.${match[1]}]`, token: match[1] });
  }
  for (const match of tag.matchAll(CLASS_EXPRESSION)) {
    const expression = match[1] ?? match[2] ?? '';
    for (const run of expression.matchAll(QUOTED_RUN)) {
      for (const token of split(run[1] ?? run[2])) found.push({ source: '[class]', token });
    }
  }

  return found;
}

/** The races on one template. */
function violationsIn(source: string, file: string): Violation[] {
  const violations: Violation[] = [];

  for (const { line, text } of openingTags(source)) {
    const staticMatch = STATIC_CLASS.exec(text);
    if (!staticMatch) continue;

    const fixed = new Map<string, string>();
    for (const token of split(staticMatch[1] ?? staticMatch[2])) {
      const property = colorProperty(token);
      if (property && !fixed.has(property)) fixed.set(property, token);
    }
    if (fixed.size === 0) continue;

    for (const { source: from, token } of conditionalClasses(text)) {
      const property = colorProperty(token);
      const rival = property && fixed.get(property);
      if (rival) violations.push({ file, line, detail: `class="… ${rival} …" races ${from} ${token}` });
    }
  }

  return violations;
}

function templates(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) return templates(full);
    if (entry.name === SELF) return [];
    return entry.name.endsWith('.html') ? [full] : [];
  });
}

const files = templates(SRC_DIR);

describe('conditional colour classes do not race the static class list', () => {
  it('walks a non-empty template tree (guards against a silently passing scan)', () => {
    expect(files.length).toBeGreaterThan(50);
  });

  it('would have caught the Browse tab', () => {
    const browseTab = `
      <a
        routerLink="/browse/events"
        routerLinkActive="bg-surface-card text-heading shadow-sm"
        class="flex min-h-[44px] text-body-sm font-medium text-muted hover:text-heading"
      >Events</a>`;

    expect(violationsIn(browseTab, 'browse.html')).toEqual([
      { file: 'browse.html', line: 2, detail: 'class="… text-muted …" races routerLinkActive text-heading' },
    ]);
  });

  it('accepts the top-nav shape, a variant, and an example inside a comment', () => {
    const fine = `
      <!-- routerLinkActive="text-heading" class="text-muted" — the shape this guard rejects -->
      <a
        class="flex items-center font-medium"
        [class]="active() ? 'bg-surface-accent-soft text-brand-strong' : 'text-body hover:bg-surface-sunken'"
      >Home</a>
      <span class="text-muted hover:text-heading" [class.font-semibold]="on()">x</span>
      <span class="text-body-sm" [class.text-heading]="on()">size, not colour</span>`;

    expect(violationsIn(fine, 'nav.html')).toEqual([]);
  });

  it('has no element whose static colour races a conditional one of the same property', () => {
    const report = files.flatMap((file) => {
      const relative = file.slice(SRC_DIR.length + 1).replace(/\\/g, '/');
      return violationsIn(readFileSync(file, 'utf-8'), relative);
    });

    expect(report.map((v) => `${v.file}:${v.line} ${v.detail}`)).toEqual([]);
  });
});
