import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Chip-shape guard (GH #301).
 *
 * #278 reported text touching the edge of a box. The box was the "beginners welcome" chip —
 * `px-sm py-0` inside a 999px border — and behind it the audit found **the same small pill
 * built eleven ways across ~84 sites**, 21 of them with no vertical padding at all, two
 * spellings (`py-0.5`, `py-3xs`) for one 2px value, one (`py-1.5`) off the 4px base
 * entirely, and the interactive ones — the primary filter control on four Browse tabs and
 * two Marketplace boards — landing at roughly 19–25px tall against DESIGN.md's 44px.
 *
 * `jhChip` is the one chip now, so this guard holds the three things that would let the
 * eleven come back:
 *
 * - **a chip drawn by hand** — a pill sized by its own text and padding, outside the
 *   primitive;
 * - **a chip overriding the primitive** — a `jhChip` that writes its own padding, radius
 *   or text step, which is the drift restarting one call site at a time;
 * - **`rounded-full`** — the second spelling of `rounded-pill`, 44 uses against 109.
 *
 * ### What counts as a chip
 *
 * A pill **whose box comes from its text**: a radius, a padding and a type step, and no
 * size of its own. A pill that sets its own box — an avatar (`size-10`), the unread
 * counter over a nav icon (`h-4 min-w-4`), a ranking number (`h-7 min-w-7`), a floating
 * action pill (`min-h-11`) — is not a chip. That is not a loophole, it is the same
 * distinction that makes this bug a bug: `py-0` crowds a chip because the chip's height
 * *is* its padding, and means nothing on a box that was already 44px.
 */

const SRC_DIR = join(__dirname, '../../..');
const SCANNED_EXTENSIONS = ['.html', '.ts'];
const SELF = 'chip-shape.spec.ts';
/** The primitive itself is where the chip is *supposed* to be defined. */
const PRIMITIVE = join('shared', 'ui', 'chip', 'chip.directive').replace(/\\/g, '/');

/** `md:`, `hover:`, `[&>li]:` — any number of them, before the utility itself. */
const VARIANTS = /^(?:(?:\[[^\]]*\]|[^:\s[\]]+):)+/;
/** `class="…"` / `ngClass="…"` — a literal class list (see `scale-keys.spec.ts`). */
const CLASS_LIST_ATTR = /(?:\bclass|\bngClass)\s*=\s*(?:"([^"]*)"|'([^']*)')/g;
/** Any quoted run: where a `.ts` file keeps the class lists it builds. */
const QUOTED = /"([^"]*)"|'([^']*)'|`([^`]*)`/g;

const PADDING = /^p[xytblrse]?-[a-z0-9]/;
/** The type steps a chip could be written in — anything that sets the label's size. */
const TYPE_STEP = /^text-(display|h[1-4]|lead|body-(lg|md|sm)|caption|eyebrow|code|\[)/;
/** A box the element gives itself, rather than one its text gives it. */
const OWN_BOX = /^(h|w|size|min-h|min-w|max-h|max-w)-/;
/** What the primitive owns, and a `jhChip` call site therefore must not write. */
const PRIMITIVE_OWNED = /^(p[xy]?-[a-z0-9]|rounded-|text-(caption|body-sm|eyebrow|body-md))/;

interface Exemption {
  /** Source path, relative to `src/`. */
  file: string;
  /** A distinctive run of the offending class list — so the entry survives the line moving. */
  contains: string;
  why: string;
}

/**
 * Pills that are shaped like a chip and are not one. Each is a *composite* — something
 * lives inside it that the primitive's single padding cannot serve. Never add an entry to
 * make a failure go away; adopt `jhChip` instead.
 */
const EXEMPT: Exemption[] = [
  {
    file: 'app/shared/city-picker/city-picker.component.html',
    contains: 'pl-md pr-xs',
    why: 'A chip with a clear button inside it: the trailing 44px control owns the right inset, which is why the padding is asymmetric.',
  },
  {
    file: 'app/features/chat/chat-conversation/chat-conversation.component.html',
    contains: 'border-border-default bg-surface-muted pl-sm',
    why: 'Same composite: an attachment pill whose trailing remove button owns the right inset.',
  },
  {
    file: 'app/features/chat/chat-conversation/chat-conversation.component.html',
    contains: 'bg-surface-sunken px-sm py-xs text-body-sm italic',
    why: 'The system line in a transcript ("X joined") is a message, not a label on something: it is centred in the message column and sets its own italic body step.',
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

/** A pill sized by its own text: radius + padding + a type step, and no box of its own. */
function isChipSurface(tokens: string[]): boolean {
  return (
    tokens.includes('rounded-pill') &&
    tokens.some((t) => PADDING.test(t)) &&
    tokens.some((t) => TYPE_STEP.test(t)) &&
    !tokens.some((t) => OWN_BOX.test(t))
  );
}

function exempt(file: string, list: string): boolean {
  return EXEMPT.some((e) => file === e.file && list.includes(e.contains));
}

/**
 * The whole opening tag a match sits in, so a chip written across several lines is read as
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

const files = sourceFiles(SRC_DIR).filter((f) => !relative(f).includes(PRIMITIVE));
const templates = files.filter((file) => file.endsWith('.html'));

function handRolledChips(): Violation[] {
  const found: Violation[] = [];

  for (const file of files) {
    const rel = relative(file);
    const isTypeScript = file.endsWith('.ts');
    const text = readFileSync(file, 'utf-8');

    text.split('\n').forEach((line, index) => {
      for (const { raw, tokens } of classLists(line, isTypeScript)) {
        if (!isChipSurface(tokens) || exempt(rel, raw)) continue;
        found.push({ file: rel, line: index + 1, detail: raw });
      }
    });
  }

  return found;
}

/** Chips that write what the primitive owns — its padding, its radius, its text step. */
function chipsOverridingTheShape(): Violation[] {
  const found: Violation[] = [];

  for (const file of templates) {
    const text = readFileSync(file, 'utf-8');

    for (const match of text.matchAll(/\bjhChip\b/g)) {
      const tag = openingTagAt(text, match.index);
      const written = classLists(tag, false)
        .flatMap((list) => list.tokens)
        .filter((token) => PRIMITIVE_OWNED.test(token));
      if (written.length === 0) continue;
      found.push({
        file: relative(file),
        line: text.slice(0, match.index).split('\n').length,
        detail: written.join(' '),
      });
    }
  }

  return found;
}

/** `rounded-full` — the retired second spelling of the same 999px corner. */
function secondRadiusSpelling(): Violation[] {
  const found: Violation[] = [];

  for (const file of files) {
    readFileSync(file, 'utf-8')
      .split('\n')
      .forEach((line, index) => {
        if (!/\brounded-full\b/.test(line)) return;
        found.push({ file: relative(file), line: index + 1, detail: 'rounded-full' });
      });
  }

  return found;
}

const report = (violations: Violation[]) =>
  violations.map((v) => `${v.file}:${v.line} ${v.detail}`);

describe('chip shape', () => {
  it('walks a non-empty source tree (guards against a silently passing scan)', () => {
    expect(files.length).toBeGreaterThan(100);
  });

  it('has no hand-rolled chip outside the primitive', () => {
    expect(report(handRolledChips())).toEqual([]);
  });

  it('has no chip that writes its own padding, radius or text step', () => {
    expect(report(chipsOverridingTheShape())).toEqual([]);
  });

  it('spells the 999px corner one way', () => {
    expect(report(secondRadiusSpelling())).toEqual([]);
  });

  /*
   * A tone nobody selects is a tone nobody maintains — and the way a palette drifts back
   * apart is a call site reaching for a colour class because the tone it wanted "isn't
   * there". Every tone the primitive offers is answering a real chip somewhere.
   */
  it('uses every tone it declares', () => {
    const directive = readFileSync(
      join(SRC_DIR, 'app/shared/ui/chip/chip.directive.ts'),
      'utf-8',
    );
    const declared = /export type ChipTone =([^;]+);/.exec(directive)?.[1] ?? '';
    const tones = [...declared.matchAll(/'([a-z-]+)'/g)].map((m) => m[1]);
    expect(tones.length).toBeGreaterThan(1);

    /*
     * Only where a tone can actually be a *chip's*: inside a `jhChip` tag, or in a file
     * that has imported `ChipTone` to compute one. A bare `tone="danger"` anywhere in the
     * tree would otherwise answer for the chip — `jh-alert` has tones of its own.
     */
    const written: string[] = [];
    for (const file of templates) {
      const text = readFileSync(file, 'utf-8');
      for (const match of text.matchAll(/\bjhChip\b/g)) {
        written.push(openingTagAt(text, match.index));
      }
    }
    for (const file of files.filter((f) => f.endsWith('.ts'))) {
      const text = readFileSync(file, 'utf-8');
      if (/\bChipTone\b/.test(text)) written.push(text);
    }
    const haystack = written.join('\n');

    const unused = tones.filter(
      (tone) => tone !== 'muted' && !new RegExp(`'${tone}'|tone="${tone}"`).test(haystack),
    );
    expect(unused).toEqual([]);
  });
});
