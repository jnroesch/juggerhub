import { readFileSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Browser-surface + reduced-motion guard for the base layer.
 *
 * Two classes of rule live in `styles.css` that nothing else in the repo watches, and both
 * fail silently — the page keeps rendering, it just stops being ours.
 *
 * **Browser surfaces.** The selection highlight, the caret, the scrollbar and the underline
 * offset are drawn by the browser from values that belong to no design system. On a warm sand
 * page the default selection blue is the one cool colour in the product, and nobody reviewing a
 * screenshot notices, because you have to drag across text to see it. DESIGN.md's "Browser
 * surfaces" section names each of them; this file is what keeps the CSS agreeing with it.
 *
 * **Reduced motion.** The hazard here is not absence but the *wrong shape*. The reflex rule —
 *
 *     @media (prefers-reduced-motion: reduce) {
 *       *, ::before, ::after {
 *         transition-duration: 0.01ms !important;
 *         animation: none !important;
 *       }
 *     }
 *
 * — is what both external design skills this was harvested from explicitly warn against, and it
 * would do two bad things here. It flattens every state change the product has, and it would
 * overwrite `card.component.css`, the one component that already handles this properly by
 * dropping the 3px lift and *keeping* the deeper shadow. `animation: none` is worse than it
 * looks: an entrance keyframe that never runs can leave its element parked on the opening
 * frame, so a reader asking for less motion is handed invisible content instead.
 *
 * So the assertions below pin the shape, not just the presence. Movement leaves the transition
 * set, infinite loops stop after one pass, and the blanket kill is rejected by name. Fix the
 * CSS; never relax a matcher here to make it pass.
 */

const STYLES = join(__dirname, '../../../styles.css');

/** The base layer as written, comments stripped, so a matcher cannot pass on prose. */
function stylesheet(): string {
  return readFileSync(STYLES, 'utf-8').replace(/\/\*[\s\S]*?\*\//g, '');
}

/** The body of the `prefers-reduced-motion` block, or null when there is none. */
function reducedMotionBlock(css: string): string | null {
  const start = css.indexOf('@media (prefers-reduced-motion: reduce)');
  if (start === -1) return null;

  let depth = 0;
  for (let i = css.indexOf('{', start); i < css.length; i++) {
    if (css[i] === '{') depth++;
    else if (css[i] === '}' && --depth === 0) return css.slice(start, i + 1);
  }
  return null;
}

/** Every transition/animation duration inside a block, in milliseconds. */
function durations(block: string): Array<[string, number]> {
  const found: Array<[string, number]> = [];
  const pattern = /(transition-duration|animation-duration):\s*([^;}]+)/g;

  for (let m = pattern.exec(block); m; m = pattern.exec(block)) {
    for (const value of m[2].split(',')) {
      const parsed = /^\s*(-?[\d.]+)(ms|s)\b/.exec(value);
      if (parsed) found.push([m[1], Number(parsed[1]) * (parsed[2] === 's' ? 1000 : 1)]);
    }
  }
  return found;
}

describe('browser surfaces', () => {
  const css = stylesheet();

  it('themes the selection highlight from tokens, text colour included', () => {
    const selection = /::selection\s*\{([^}]*)\}/.exec(css);
    expect(selection).not.toBeNull();

    // Both halves matter: a background alone leaves the browser picking the text colour.
    expect(selection![1]).toMatch(/background-color:\s*var\(--coral-1\)/);
    expect(selection![1]).toMatch(/color:\s*var\(--text-body\)/);
  });

  it('paints the caret with the focus step, so a focused input agrees with itself', () => {
    expect(css).toMatch(/caret-color:\s*var\(--border-focus\)/);
  });

  it('themes the scrollbar for both engines', () => {
    // Firefox reads these two properties and ignores the pseudo-elements.
    expect(css).toMatch(/scrollbar-color:\s*var\(--sand-4\)\s+var\(--surface-sunken\)/);
    expect(css).toMatch(/scrollbar-width:\s*thin/);

    // Chrome and Safari read only the pseudo-elements.
    expect(css).toMatch(/::-webkit-scrollbar-thumb\s*\{/);
    expect(css).toMatch(/::-webkit-scrollbar-track\s*\{/);
  });

  it('offsets the underline clear of the descenders', () => {
    expect(css).toMatch(/text-underline-offset:\s*0\.18em/);
  });

  it('gives the mono face tabular figures, as DESIGN.md promises for scores and times', () => {
    const mono = /\.font-mono\s*\{([^}]*)\}/.exec(css);
    expect(mono).not.toBeNull();
    expect(mono![1]).toMatch(/font-variant-numeric:\s*tabular-nums/);
  });

  it('introduces no colour of its own — every surface is a token', () => {
    // The claim DESIGN.md makes about this section. Sizes are literal (10px, thin, 0.18em);
    // colours never are, or a browser surface becomes the one place a hex can hide.
    const surfaces = css.slice(css.indexOf('::selection'));
    const literalColours = surfaces.match(/(?:^|[\s:(,])(#[0-9a-f]{3,8}|rgba?\(|hsla?\()/gi) ?? [];
    expect(literalColours).toEqual([]);
  });
});

describe('reduced motion', () => {
  const css = stylesheet();
  const block = reducedMotionBlock(css);

  it('is honoured at the base layer, not only inside one component', () => {
    expect(block).not.toBeNull();
  });

  it('stops infinite loops after a single pass', () => {
    // DESIGN.md forbids decorative loops in content; seven `animate-pulse` skeletons ship one
    // anyway (GH #337). This is the floor that keeps them from pulsing at a reader who asked
    // for less motion, and it must outlive that fix.
    expect(block).toMatch(/animation-iteration-count:\s*1\s*!important/);
  });

  it('holds movement out of the transition set while keeping the rest', () => {
    const property = /transition-property:\s*([^;]+);/.exec(block ?? '');
    expect(property).not.toBeNull();

    const kept = property![1];
    for (const survivor of ['color', 'background-color', 'border-color', 'box-shadow', 'opacity']) {
      expect(kept).toContain(survivor);
    }

    // The two that carry movement. `all` is the same mistake spelled shorter.
    expect(kept).not.toMatch(/\btransform\b/);
    expect(kept).not.toMatch(/\ball\b/);
  });

  it('is not the blanket kill', () => {
    // Each of these flattens every state change at once; `animation: none` can additionally
    // strand an element on its opening keyframe. See the file comment.
    expect(block).not.toMatch(/animation:\s*none/);
    expect(block).not.toMatch(/transition:\s*none/);

    // Durations are compared as numbers, never matched as spellings. The first version of this
    // assertion pattern-matched `0(\.0+)?m?s` and let `0.01ms` — the exact value the reflex rule
    // uses — walk straight past it, which a mutation of the stylesheet caught. Anything under a
    // frame is the blanket kill wearing a different number.
    for (const [property, ms] of durations(block ?? '')) {
      expect(`${property}: ${ms}ms`).toBe(`${property}: ${Math.max(ms, 50)}ms`);
    }
  });
});
