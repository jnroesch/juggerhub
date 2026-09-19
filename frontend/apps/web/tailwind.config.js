const { join } = require('path');

/**
 * Makes one design-system colour composable with Tailwind's opacity modifier
 * (GH #317, #322).
 *
 * Every colour here is a CSS custom property, and Tailwind cannot derive an
 * alpha channel from a bare `var(--x)`: for `bg-brand/60` it parses the value
 * as a colour, fails, and drops the candidate — no rule, no warning, clean
 * build. Five sites shipped that way, each fully transparent: the filter
 * sheet's backdrop, the profile's sticky save bar, the event wizard's completed
 * knobs, and the own-message attachment's border and hover tint. Only
 * `bg-black/40` ever worked, because `black` is a Tailwind default hex rather
 * than one of ours.
 *
 * A colour may be a function of the requested alpha, so this returns one:
 *
 * - **No modifier** → the bare `var(--x)` it always was. The plain rule is
 *   byte-identical to before, and a browser without `color-mix()` loses
 *   nothing it had.
 * - **`/60`, `/[0.37]`** → `color-mix(in srgb, var(--x) 60%, transparent)`,
 *   composed at paint time. The mix is premultiplied, so that is exactly the
 *   token at 60% opacity with its hue untouched.
 *
 * The tokens stay hex in `styles.css` and in DESIGN.md. The channel-triplet
 * alternative (`--sand-9: 36 31 24` + `rgb(var(--sand-9) / <alpha-value>)`)
 * would have broken every direct `var(--token)` in a stylesheet and the
 * arithmetic in `contrast.spec.ts`, to fix the same five sites.
 *
 * Anything else — `bg-brand/[50%]`, or the `var(--tw-bg-opacity)` the
 * switched-off `backgroundOpacity` plugin would pass — is refused loudly
 * rather than rendered at full opacity under a modifier that promised less.
 *
 * `color-alpha.spec.ts` compiles every modifier the app writes and fails on
 * any that emits nothing, so the guard does not depend on this code path.
 */
function withAlpha(variable) {
  return ({ opacityValue }) => {
    if (opacityValue === undefined) return `var(${variable})`;
    if (typeof opacityValue === 'string' && /^\d*\.?\d+$/.test(opacityValue)) {
      const percent = Math.round(Number(opacityValue) * 10000) / 100;
      return `color-mix(in srgb, var(${variable}) ${percent}%, transparent)`;
    }
    throw new Error(
      `${variable}: opacity "${opacityValue}" is not a number — write the modifier as /60 or /[0.6]`,
    );
  };
}

/**
 * Wraps a (possibly nested) map of `'var(--x)'` strings with `withAlpha`, so the
 * palette below reads as the plain variable map it is.
 */
function alphaAware(map) {
  return Object.fromEntries(
    Object.entries(map).map(([key, value]) => {
      if (typeof value !== 'string') return [key, alphaAware(value)];
      const match = /^var\((--[a-z0-9-]+)\)$/.exec(value);
      if (!match) throw new Error(`colour "${key}" must be a var(--token), got "${value}"`);
      return [key, withAlpha(match[1])];
    }),
  );
}

/**
 * Tailwind theme for the JuggerHub `web` app.
 *
 * The design tokens themselves live as CSS custom properties in
 * `src/styles.css` (sourced from the repo-root DESIGN.md, the single source of
 * truth for visual identity). This config maps those variables onto Tailwind's
 * scale so utilities like `bg-brand`, `text-heading`, `rounded-lg`,
 * `font-display` resolve to the design system.
 *
 * Every colour has exactly one name here — the semantic one (`brand`,
 * `surface-card`, `text-body`, `border-default`…). The legacy aliases that used
 * to sit beside them (`ink`, `text`, `primary`, `accent`, `surface`,
 * `surface-subtle`, `background`, `border`) were second names for colours that
 * already had one, and are retired (GH #312). Add a token in DESIGN.md →
 * styles.css first, then here.
 *
 * @type {import('tailwindcss').Config}
 */
module.exports = {
  content: [
    join(__dirname, 'src/**/*.{html,ts}'),
    join(__dirname, 'index.html'),
  ],
  theme: {
    extend: {
      /* Every entry is `var(--token)`; `alphaAware` makes each take a `/NN` modifier. */
      colors: alphaAware({
        /* Raw scales */
        sand: {
          0: 'var(--sand-0)',
          1: 'var(--sand-1)',
          2: 'var(--sand-2)',
          3: 'var(--sand-3)',
          4: 'var(--sand-4)',
          5: 'var(--sand-5)',
          6: 'var(--sand-6)',
          7: 'var(--sand-7)',
          8: 'var(--sand-8)',
          9: 'var(--sand-9)',
        },
        coral: {
          0: 'var(--coral-0)',
          1: 'var(--coral-1)',
          2: 'var(--coral-2)',
          3: 'var(--coral-3)',
          4: 'var(--coral-4)',
          5: 'var(--coral-5)',
          6: 'var(--coral-6)',
          7: 'var(--coral-7)',
          8: 'var(--coral-8)',
          9: 'var(--coral-9)',
          DEFAULT: 'var(--coral-4)',
        },
        teal: {
          0: 'var(--teal-0)',
          1: 'var(--teal-1)',
          2: 'var(--teal-2)',
          3: 'var(--teal-3)',
          4: 'var(--teal-4)',
          5: 'var(--teal-5)',
          6: 'var(--teal-6)',
          7: 'var(--teal-7)',
          8: 'var(--teal-8)',
          9: 'var(--teal-9)',
          DEFAULT: 'var(--teal-4)',
        },
        lemon: {
          0: 'var(--lemon-0)',
          1: 'var(--lemon-1)',
          2: 'var(--lemon-2)',
          3: 'var(--lemon-3)',
          4: 'var(--lemon-4)',
          5: 'var(--lemon-5)',
          6: 'var(--lemon-6)',
          7: 'var(--lemon-7)',
          DEFAULT: 'var(--lemon-2)',
        },

        /*
         * Semantic — brand. Two roles per accent, and the pair is load-bearing
         * (GH #298): `brand` is coral-4, the identity colour, and it is
         * decorative — a gradient, a border, a progress fill, a dot. White on it
         * is 3.14:1 and it is 3.14:1 as text, so it carries no label and is no
         * label. `brand-strong` is coral-6, legible in both directions at
         * 5.71:1: filled buttons, filled chips, count badges, coral text, coral
         * icons. Sage is split the same way.
         *
         * `brand-hover` / `brand-active` (coral-5 / coral-6) are gone with the
         * split — the hover of a coral-4 fill that no longer carries text has no
         * callers, and coral-6 under a second name was how `bg-brand-active`
         * came to be a rest state. `scale-keys.spec.ts` fails on either spelling
         * written from here on.
         */
        brand: 'var(--brand-primary)',
        'brand-strong': 'var(--brand-primary-strong)',
        'brand-strong-hover': 'var(--brand-primary-strong-hover)',
        secondary: 'var(--brand-secondary)',
        'secondary-strong': 'var(--brand-secondary-strong)',
        'secondary-strong-hover': 'var(--brand-secondary-strong-hover)',
        highlight: 'var(--brand-highlight)',

        /* Semantic — surfaces */
        'surface-card': 'var(--surface-card)',
        'surface-raised': 'var(--surface-raised)',
        'surface-page': 'var(--surface-page)',
        'surface-sunken': 'var(--surface-sunken)',
        'surface-muted': 'var(--surface-muted)',
        'surface-inverse': 'var(--surface-inverse)',
        'surface-accent-soft': 'var(--surface-accent-soft)',
        'surface-secondary-soft': 'var(--surface-secondary-soft)',

        /*
         * Semantic — text. One name per colour: `ink` and `text` were second
         * names for `heading` and `body`, and the app wrote both — `text-ink` 52
         * times and `text-heading` 143, `text-text` 69 and `text-body` 252 — for
         * two colours (GH #312). Nothing rendered wrong, which is why it lasted:
         * both aliases resolved to the right custom property, so the duplication
         * was invisible in review, the same shape as `text-muted` (#297) and
         * `heading-lg` (#299). Retiring the keys is what enforces the single
         * spelling: `scale-keys.spec.ts` fails on a `text-` key that is neither a
         * size nor a colour, so a `text-ink` written tomorrow turns the suite red
         * instead of quietly painting the right colour under the wrong name.
         */
        heading: 'var(--text-heading)',
        body: 'var(--text-body)',
        /*
         * `muted`, not `text-muted`: a colour named `text-muted` produces the
         * utility `text-text-muted`, so the 277 templates that wrote the
         * obvious `text-muted` emitted nothing and rendered as body text
         * (GH #297 — the same bug class as `py-3xs`, found by
         * `scale-keys.spec.ts`). The nine sites that had found the working
         * spelling were rewritten to the short one, so the token has a single
         * name again.
         */
        muted: 'var(--text-muted)',
        /*
         * `subtle` (sand-5) and `faint` (sand-4) are retired (GH #298). They were
         * the app's default secondary text — 305 and 59 elements — at 2.92:1 and
         * 1.97:1 on white, below the 4.5:1 floor DESIGN.md claims the ramp is
         * tuned to, and they cannot be darkened into compliance without landing
         * on top of `muted`. Everything that wore them wears `muted` now, and
         * removing the keys is what makes the retirement stick: `scale-keys.spec.ts`
         * fails on a `text-subtle` written tomorrow, the way it does for
         * `text-heading-lg` (GH #299) and `rounded-full` (GH #301).
         */
        link: 'var(--text-link)',
        'link-hover': 'var(--text-link-hover)',
        'on-accent': 'var(--text-on-accent)',
        'on-inverse': 'var(--text-on-inverse)',

        /* Semantic — borders */
        'border-default': 'var(--border-default)',
        'border-muted': 'var(--border-muted)',
        'border-accent': 'var(--border-accent)',
        'border-focus': 'var(--border-focus)',
        'border-strong': 'var(--border-strong)',

        /* Semantic — status (fg / bg / border) */
        'success-fg': 'var(--success-fg)',
        'success-bg': 'var(--success-bg)',
        'success-border': 'var(--success-border)',
        'danger-fg': 'var(--danger-fg)',
        'danger-bg': 'var(--danger-bg)',
        'danger-border': 'var(--danger-border)',
        'warning-fg': 'var(--warning-fg)',
        'warning-bg': 'var(--warning-bg)',
        'warning-border': 'var(--warning-border)',
        'info-fg': 'var(--info-fg)',
        'info-bg': 'var(--info-bg)',
        'info-border': 'var(--info-border)',

        /*
         * Status accents at the raw scale step. These are NOT second names: they
         * sit one step lighter than `danger-fg` / `success-fg` / `info-fg`
         * (red/green/blue-5 against -6), and `--red-5`, `--green-5` and
         * `--blue-5` are reachable through these keys and nowhere else —
         * `contrast.spec.ts` lists all three as solid fills. `info-strong`
         * (blue-6) and `warning` (`var(--warning-fg)`) *were* duplicates, of
         * `info-fg` and `warning-fg`, and went with the rest in GH #312; both had
         * zero call sites. `text-danger` was retired by #298 and a single
         * `bg-danger` survives it — a colour question, not a naming one, so it is
         * left for the contrast work rather than collapsed here.
         */
        info: 'var(--blue-5)',
        success: 'var(--green-5)',
        danger: 'var(--red-5)',
      }),
      spacing: {
        /*
         * `3xs` (2px) is the half-step below the 4px base — pill padding and
         * other hairline insets only. `2xs` is `space-1`. Both were used in
         * templates long before they were defined here (GH #137): Tailwind
         * emits nothing for an unknown scale key, so `py-3xs` silently
         * rendered as no padding at all. `scale-keys.spec.ts` now fails on
         * any spacing utility whose key is not in this scale.
         */
        '3xs': '2px',
        '2xs': '4px',
        xs: '8px',
        sm: '12px',
        md: '16px',
        lg: '20px',
        xl: '24px',
        '2xl': '32px',
        '3xl': '40px',
        'section-gap': 'clamp(48px, 8vw, 112px)',
      },
      maxWidth: {
        'container-sm': '640px',
        'container-md': '860px',
        'container-lg': '1100px',
        'container-xl': '1320px',
      },
      borderRadius: {
        xs: 'var(--radius-xs)',
        sm: 'var(--radius-sm)',
        md: 'var(--radius-md)',
        lg: 'var(--radius-lg)',
        xl: 'var(--radius-xl)',
        '2xl': 'var(--radius-2xl)',
        /*
         * `pill` only. `full` was a second name for this exact value, and the app
         * wrote both — `rounded-pill` 109 times, `rounded-full` 44 — for one 999px
         * corner (GH #301). Retiring the key is what enforces the single spelling,
         * the way #299 retired `heading-lg`; `chip-shape.spec.ts` fails on a
         * `rounded-full` written tomorrow, before Tailwind's own 9999px default
         * can render it as if nothing happened.
         */
        pill: 'var(--radius-pill)',
      },
      fontFamily: {
        display: 'var(--font-display)',
        sans: 'var(--font-body)',
        body: 'var(--font-body)',
        mono: 'var(--font-mono)',
      },
      fontSize: {
        display: ['var(--text-display)', { lineHeight: '1.1', letterSpacing: '-0.02em', fontWeight: '800' }],
        h1: ['var(--text-h1)', { lineHeight: '1.1', letterSpacing: '-0.02em', fontWeight: '800' }],
        h2: ['var(--text-h2)', { lineHeight: '1.25', letterSpacing: '-0.02em', fontWeight: '700' }],
        h3: ['var(--text-h3)', { lineHeight: '1.25', letterSpacing: '-0.02em', fontWeight: '700' }],
        h4: ['var(--text-h4)', { lineHeight: '1.25', fontWeight: '700' }],
        lead: ['var(--text-lead)', { lineHeight: '1.5' }],
        'body-lg': ['var(--text-body-lg)', { lineHeight: '1.65' }],
        'body-md': ['var(--text-body-md)', { lineHeight: '1.5' }],
        'body-sm': ['var(--text-body-sm)', { lineHeight: '1.5' }],
        caption: ['var(--text-caption)', { lineHeight: '1.4' }],
        eyebrow: ['var(--text-eyebrow)', { lineHeight: '1.2', letterSpacing: '0.06em', fontWeight: '600' }],
        /*
         * `heading-lg` and `heading-md` were aliases of `h3` and `h4` — the same two sizes
         * under a second set of names. They did not coexist peacefully: page titles were
         * written as `heading-lg` in 37 places and `h3` in 27, the app looked like it had a
         * larger scale than it did, and nothing made the duplication visible in review
         * (GH #299). Both are retired, and removing them from this scale is what enforces
         * it: `scale-keys.spec.ts` fails on a `text-` key that is neither a size nor a
         * colour, so a `text-heading-lg` written tomorrow turns the suite red.
         */
        code: ['var(--text-body-sm)', { lineHeight: '1.5' }],
      },
      letterSpacing: {
        /*
         * DESIGN.md's `tracking` tokens. They were listed there from the start
         * but never mapped here, so the whole `tracking-*` namespace fell
         * through to Tailwind's own defaults and `tracking-eyebrow` — the one
         * the eyebrow step is named after — emitted nothing at all (GH #297).
         * `tight` and `wide` deliberately override Tailwind's 0.025em defaults
         * with DESIGN.md's values; `scale-keys.spec.ts` fails on any
         * `tracking-` key this scale (plus Tailwind's remaining defaults) does
         * not define.
         */
        tight: '-0.02em',
        normal: '0',
        wide: '0.02em',
        eyebrow: '0.06em',
      },
      boxShadow: {
        xs: 'var(--shadow-xs)',
        sm: 'var(--shadow-sm)',
        md: 'var(--shadow-md)',
        lg: 'var(--shadow-lg)',
        xl: 'var(--shadow-xl)',
        coral: 'var(--shadow-coral)',
        teal: 'var(--shadow-teal)',
        card: 'var(--shadow-sm)',
      },
      backgroundImage: {
        'brand-gradient': 'var(--brand-gradient)',
        'brand-gradient-soft': 'var(--brand-gradient-soft)',
        'hero-gradient': 'var(--hero-gradient)',
      },
      transitionTimingFunction: {
        standard: 'var(--ease-standard)',
        out: 'var(--ease-out)',
        bounce: 'var(--ease-bounce)',
      },
      transitionDuration: {
        fast: '120ms',
        base: '200ms',
        slow: '320ms',
      },
      ringColor: {
        /*
         * The focus indicator. It was coral-1 — 1.32:1 against white, i.e. very
         * nearly invisible, against WCAG 2.2's 3:1 for focus indicators (GH #298).
         * It is `border-focus` now, the same token the inputs put on their focus
         * border, so every focusable thing in the product focuses one colour.
         */
        focus: withAlpha('--border-focus'),
      },
    },
  },
  /*
   * The legacy `bg-opacity-*` / `text-opacity-*` … utilities are off (GH #322).
   * They work by writing a `--tw-bg-opacity` variable into every colour rule,
   * which would have forced the `color-mix` form onto plain `bg-brand` too —
   * see `withAlpha`. The slash modifier (`bg-brand/60`) is the one spelling;
   * `color-alpha.spec.ts` fails on the legacy one.
   */
  corePlugins: {
    backgroundOpacity: false,
    textOpacity: false,
    borderOpacity: false,
    ringOpacity: false,
    divideOpacity: false,
    placeholderOpacity: false,
  },
  plugins: [],
};
