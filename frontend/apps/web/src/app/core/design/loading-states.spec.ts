import { readFileSync, readdirSync } from 'node:fs';
import { join, relative } from 'node:path';

/**
 * Loading-state guard (GH #337).
 *
 * DESIGN.md's loading rule is one muted text line via `jh-loading`, "never a spinner or
 * skeleton", and "Motion & states" forbids infinite decorative loops in content. Seven screens
 * broke both at once with pulsing `animate-pulse` skeletons: a shape the content may not have
 * (so the real rows arrive as a layout shift) and a `<div>` a screen reader announces as nothing,
 * where `jh-loading` carries `role="status"`.
 *
 * Tailwind's looping animation utilities are the way that comes back, so none of them may appear
 * in a template or a component. Use `jh-loading` with a contextual label instead.
 */

const SRC_DIR = join(__dirname, '../../..');
const SCANNED_EXTENSIONS = ['.html', '.ts'];
/** Every Tailwind animation utility that loops for ever — `pulse` and `spin` are the skeleton and the spinner. */
const LOOPING_UTILITY = /\banimate-(pulse|spin|ping|bounce)\b/;

function sourceFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) return sourceFiles(path);
    if (entry.name.endsWith('.spec.ts')) return [];
    return SCANNED_EXTENSIONS.some((ext) => entry.name.endsWith(ext)) ? [path] : [];
  });
}

describe('loading states', () => {
  const files = sourceFiles(SRC_DIR);

  it('scans the application source', () => {
    // A path that resolved to nothing would make the guard below pass vacuously.
    expect(files.length).toBeGreaterThan(100);
  });

  it('uses no looping animation utility — no skeleton, no spinner', () => {
    const offenders = files.flatMap((file) =>
      readFileSync(file, 'utf8')
        .split('\n')
        .flatMap((line, i) =>
          LOOPING_UTILITY.test(line) ? [`${relative(SRC_DIR, file).replace(/\\/g, '/')}:${i + 1}`] : [],
        ),
    );
    expect(offenders).toEqual([]);
  });
});
