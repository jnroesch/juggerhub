import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Page-title guard (GH #299).
 *
 * The app had 81 `<h1>` elements written 18 different ways: four sizes (18 / 19 / 22 / 28px),
 * three weights, two colour spellings and — because `heading-lg` was an alias of `h3` and
 * `heading-md` of `h4` — two names for each of the two sizes actually in use. Nothing in that
 * list was wrong on its own, which is exactly why it drifted: every new page copied whichever
 * neighbour it was written next to, and the product ended up with no page title at all, only
 * 81 one-off ones.
 *
 * The scale had the other half of the problem. `text-h1` and `text-lead` were used **zero**
 * times and `text-h2` once, so the whole upper half of DESIGN.md's type scale never reached a
 * screen and every page was the same three sizes between 12 and 16px — a large part of what
 * GH #278 reported as "bland/samey".
 *
 * There are four title roles now, and this guard holds them:
 *
 * - `text-h1` — the hero. The onboarding welcome screen, and nothing else.
 * - `text-h2` — a focal page: one card, nothing around it (sign in, register, an invite
 *   landing, "no such player"), plus the dashboard greeting, which is the app's front door.
 * - `text-h3` — every other page title.
 * - `text-body-lg` — a bar title: the name in the chat header, which is a nav bar, not a page.
 *
 * Weight, family, colour and tracking are **not** written on a title: `styles.css`'s base layer
 * gives every `h1`–`h4` the display face and heading colour, and each `text-h*` token carries
 * its own weight. A `font-bold` on a title is therefore either a no-op or a silent override of
 * the scale — both were live before #299 — so this guard rejects the whole class of them.
 */

const SRC_DIR = join(__dirname, '../../..');
const SELF = 'heading-roles.spec.ts';

/** The four roles. A title carries exactly one. */
const TITLE_STEPS = ['text-h1', 'text-h2', 'text-h3', 'text-body-lg'];
/** Supplied by the base layer (family, colour) or by the size token (weight). */
const SUPPLIED_ELSEWHERE = /^(font-(?:thin|extralight|light|normal|medium|semibold|bold|extrabold|black|display|body))$|^text-(?:ink|heading)$/;
/** `text-body-lg` is the one step with no weight of its own, so a bar title must set one. */
const BAR_STEP = 'text-body-lg';

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
    return entry.name.endsWith('.html') ? [full] : [];
  });
}

const files = sourceFiles(SRC_DIR);

/** Every `<h1>` in the app, with its class list. */
function titles(): { file: string; line: number; tokens: string[] }[] {
  return files.flatMap((file) => {
    // Comments are blanked, not dropped, so line numbers still point at the real markup. A
    // template that *mentions* `<h1>` in a note (team-detail does) is prose, not a title.
    const text = readFileSync(file, 'utf-8').replace(/<!--[\s\S]*?-->/g, (c) =>
      c.replace(/[^\n]/g, ' '),
    );
    const relative = file.slice(SRC_DIR.length + 1).replace(/\\/g, '/');

    return [...text.matchAll(/<h1\b[^>]*>/g)].map((match) => ({
      file: relative,
      line: text.slice(0, match.index).split('\n').length,
      tokens: (/\sclass="([^"]*)"/.exec(match[0])?.[1] ?? '').split(/\s+/).filter(Boolean),
    }));
  });
}

const report = (violations: Violation[]) =>
  violations.map((v) => `${v.file}:${v.line} ${v.detail}`);

describe('page titles', () => {
  const all = titles();

  it('finds the app\'s titles (guards against a silently passing scan)', () => {
    expect(all.length).toBeGreaterThan(50);
  });

  it('gives every <h1> exactly one of the four title steps', () => {
    const wrong = all
      .map((t) => ({ ...t, steps: t.tokens.filter((token) => TITLE_STEPS.includes(token)) }))
      .filter((t) => t.steps.length !== 1)
      .map((t) => ({ file: t.file, line: t.line, detail: t.tokens.join(' ') || '(no class)' }));

    expect(report(wrong)).toEqual([]);
  });

  it('leaves weight, family and colour to the base layer and the size token', () => {
    const wrong = all
      .filter((t) => !t.tokens.includes(BAR_STEP))
      .map((t) => ({
        file: t.file,
        line: t.line,
        detail: t.tokens.filter((token) => SUPPLIED_ELSEWHERE.test(token)).join(' '),
      }))
      .filter((t) => t.detail.length > 0);

    expect(report(wrong)).toEqual([]);
  });

  /*
   * A scale whose upper half is never reached is a scale the product does not have. These two
   * steps were used zero times between them before #299; if they go back to zero, the page has
   * quietly flattened again and the "samey" report is back with it.
   */
  it('reaches the top of the scale and the lead step', () => {
    const uses = (utility: string) =>
      files.filter((file) => new RegExp(`\\b${utility}\\b`).test(readFileSync(file, 'utf-8'))).length;

    expect(uses('text-h1')).toBeGreaterThan(0);
    expect(uses('text-h2')).toBeGreaterThan(0);
    expect(uses('text-lead')).toBeGreaterThan(0);
  });
});
