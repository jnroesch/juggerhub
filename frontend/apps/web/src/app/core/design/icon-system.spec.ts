import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { ICONS } from '../../shared/ui/icon/icons';

/**
 * Iconography guard (GH #300).
 *
 * `jh-icon` existed, was well built, shipped seven glyphs — and was used five times in the whole
 * app. Everything else was drawn by hand: **99 inline `<svg>` across 37 files, at five stroke
 * widths** (2.0 ×54, 1.8 ×39, 1.6 ×8, 3.0 ×2, 2.2 ×1) and seven sizes, plus a set of places where
 * the icon was a **literal text character** — `✓`, `○`, `•`, `›`, `‹`, `✕`, `⋯` — which is what
 * #278's first screenshot was of. Two of those characters, `✓` and `○`, are outside the latin
 * subset `@fontsource/mona-sans` ships, so the browser drew them from a fallback face at a
 * different cap height beside a Mona Sans label: markers that cannot line up, whatever the box
 * around them does.
 *
 * Three rules, each closing one of the ways it came about:
 *
 * - **No inline `<svg>` in a screen.** One primitive owns stroke, size and colour, so an icon
 *   cannot be drawn at 1.6 next to one drawn at 2.0.
 * - **No text glyph as an icon**, in a template *or* in a translation catalogue. The catalogues
 *   are the half that hides: `common.back` was the string `"‹ Back"` in all three languages, so
 *   the chevron was invisible to any check that reads markup, and a translator had to carry it.
 * - **No icon sized by its call site.** `IconSize` is a three-value union in TypeScript; this
 *   adds the other half, that a `<jh-icon>` may not be given an `h-*` / `w-*` / `size-*` class,
 *   which would put the eighth size back one template at a time.
 *
 * Fix the markup, never this test.
 */

const SRC_DIR = join(__dirname, '../../..');
const I18N_DIR = join(__dirname, '../../../../public/i18n');
const SELF = 'icon-system.spec.ts';
/** The primitive is the one place an `<svg>` is supposed to be assembled. */
const PRIMITIVE_DIR = join('shared', 'ui', 'icon').replace(/\\/g, '/');

/**
 * Characters that are only ever an icon. Deliberately **not** in the set: `·` and `—`, which are
 * separators in running copy; `→`, which reads as "to" in a time range; `«»`, which are Spanish
 * quotation marks; and ASCII `+`/`-`, which are arithmetic and hyphens far more often than they
 * are buttons.
 */
const ICON_GLYPHS = [...'✓✔✅✗✘✕✖❌○◯●•‣▪▸▾▴◂►◄›‹⋯−⌄⌃'];

/** DESIGN.md's declared common set. Parsed, not copied, so the two cannot drift apart. */
function designMdIconNames(): string[] {
  const design = readFileSync(join(SRC_DIR, '../../../../DESIGN.md'), 'utf-8');
  const listed = /Common icons:\s*`([^`]+)`/.exec(design.replace(/\n\s*/g, ' '));
  return (listed?.[1] ?? '').split(',').map((name) => name.trim()).filter(Boolean);
}

interface Violation {
  file: string;
  line: number;
  detail: string;
}

function sourceFiles(dir: string, extensions: string[]): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) return sourceFiles(full, extensions);
    if (entry.name === SELF) return [];
    return extensions.some((ext) => entry.name.endsWith(ext)) ? [full] : [];
  });
}

function relative(file: string, base = SRC_DIR): string {
  return file.slice(base.length + 1).replace(/\\/g, '/');
}

/** Comments explain the markup; they are not the markup. */
function withoutComments(html: string): string {
  return html.replace(/<!--[\s\S]*?-->/g, (block) => block.replace(/[^\n]/g, ' '));
}

const files = sourceFiles(SRC_DIR, ['.html', '.ts']);
const templates = files
  .filter((file) => file.endsWith('.html'))
  .filter((file) => !relative(file).includes(PRIMITIVE_DIR));

function inlineSvg(): Violation[] {
  const found: Violation[] = [];

  for (const file of files.filter((f) => !relative(f).includes(PRIMITIVE_DIR))) {
    const text = file.endsWith('.html') ? withoutComments(readFileSync(file, 'utf-8')) : readFileSync(file, 'utf-8');
    text.split('\n').forEach((line, index) => {
      if (!/<svg\b/.test(line)) return;
      found.push({ file: relative(file), line: index + 1, detail: line.trim().slice(0, 80) });
    });
  }

  return found;
}

function glyphsInTemplates(): Violation[] {
  const found: Violation[] = [];

  for (const file of templates) {
    withoutComments(readFileSync(file, 'utf-8'))
      .split('\n')
      .forEach((line, index) => {
        const hits = ICON_GLYPHS.filter((glyph) => line.includes(glyph));
        if (hits.length === 0) return;
        found.push({ file: relative(file), line: index + 1, detail: hits.join(' ') });
      });
  }

  return found;
}

/** Every leaf string in a catalogue, paired with its dotted key. */
function leaves(node: unknown, prefix = ''): [string, string][] {
  if (typeof node === 'string') return [[prefix, node]];
  if (node === null || typeof node !== 'object') return [];
  return Object.entries(node as Record<string, unknown>).flatMap(([key, value]) =>
    leaves(value, prefix ? `${prefix}.${key}` : key),
  );
}

function glyphsInCatalogues(): Violation[] {
  const found: Violation[] = [];

  for (const lang of ['en', 'de', 'es']) {
    const file = join(I18N_DIR, `${lang}.json`);
    const catalogue: unknown = JSON.parse(readFileSync(file, 'utf-8'));
    for (const [key, value] of leaves(catalogue)) {
      const hits = ICON_GLYPHS.filter((glyph) => value.includes(glyph));
      if (hits.length === 0) continue;
      found.push({ file: `public/i18n/${lang}.json`, line: 0, detail: `${key} — ${hits.join(' ')}` });
    }
  }

  return found;
}

/** The size ramp belongs to the primitive; a call site that resizes an icon has left it. */
function iconsSizedByTheCallSite(): Violation[] {
  const found: Violation[] = [];
  const OWN_BOX = /\b(?:h|w|size|min-h|min-w)-[a-z0-9[]/;

  for (const file of templates) {
    const text = withoutComments(readFileSync(file, 'utf-8'));
    for (const match of text.matchAll(/<jh-icon\b[^>]*\/>/g)) {
      const classes = /\bclass="([^"]*)"/.exec(match[0])?.[1] ?? '';
      if (!OWN_BOX.test(classes)) continue;
      found.push({
        file: relative(file),
        line: text.slice(0, match.index).split('\n').length,
        detail: classes,
      });
    }
  }

  return found;
}

const report = (violations: Violation[]) =>
  violations.map((v) => (v.line ? `${v.file}:${v.line} ${v.detail}` : `${v.file} ${v.detail}`));

describe('icon system', () => {
  it('walks a non-empty source tree (guards against a silently passing scan)', () => {
    expect(templates.length).toBeGreaterThan(90);
  });

  it('draws every icon through the primitive, never inline', () => {
    expect(report(inlineSvg())).toEqual([]);
  });

  it('uses no text glyph as an icon in a template', () => {
    expect(report(glyphsInTemplates())).toEqual([]);
  });

  /*
   * The catalogue half. A chevron baked into `"‹ Back"` is an icon a translator has to type, in
   * a face the label is not set in, and it is invisible to a scan of the markup — which is why
   * twenty keys carried one across all three languages until #300.
   */
  it('uses no text glyph as an icon in a translation catalogue', () => {
    expect(report(glyphsInCatalogues())).toEqual([]);
  });

  it('lets no call site give an icon a size of its own', () => {
    expect(report(iconsSizedByTheCallSite())).toEqual([]);
  });

  /*
   * DESIGN.md names the common set; the map has to be able to answer for all of it, or a screen
   * that wants `compass` draws its own — which is exactly how the 99 accumulated.
   */
  it('ships every icon DESIGN.md names', () => {
    const named = designMdIconNames();
    expect(named.length).toBeGreaterThan(10);
    expect(named.filter((name) => !(name in ICONS))).toEqual([]);
  });

  /*
   * And nothing beyond it that nobody draws. A curated map earns its curation: an unused glyph
   * is one nobody has looked at on a screen, and it makes the set harder to choose from.
   */
  it('has no glyph that is neither drawn nor part of the declared set', () => {
    const declared = new Set(designMdIconNames());
    const markup = templates.map((file) => readFileSync(file, 'utf-8')).join('\n');
    const unused = Object.keys(ICONS).filter(
      (name) => !declared.has(name) && !new RegExp(`'${name}'|name="${name}"`).test(markup),
    );
    expect(unused).toEqual([]);
  });
});
