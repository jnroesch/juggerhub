const { join } = require('path');

/**
 * Tailwind theme for the JuggerHub `web` app.
 *
 * The design tokens themselves live as CSS custom properties in
 * `src/styles.css` (sourced from the repo-root DESIGN.md, the single source of
 * truth for visual identity). This config maps those variables onto Tailwind's
 * scale so utilities like `bg-brand`, `text-heading`, `rounded-lg`,
 * `font-display` resolve to the design system.
 *
 * Prefer the semantic names (`brand`, `surface-card`, `text-body`,
 * `border-default`…). The legacy aliases (`primary`, `ink`, `surface`,
 * `background`…) are kept pointing at the warm palette so existing markup keeps
 * working. Add a token in DESIGN.md → styles.css first, then here.
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
      colors: {
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
          DEFAULT: 'var(--lemon-2)',
        },

        /* Semantic — brand */
        brand: 'var(--brand-primary)',
        'brand-hover': 'var(--brand-primary-hover)',
        'brand-active': 'var(--brand-primary-active)',
        secondary: 'var(--brand-secondary)',
        'secondary-hover': 'var(--brand-secondary-hover)',
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

        /* Semantic — text */
        heading: 'var(--text-heading)',
        body: 'var(--text-body)',
        link: 'var(--text-link)',
        'link-hover': 'var(--text-link-hover)',
        'on-accent': 'var(--text-on-accent)',
        'on-inverse': 'var(--text-on-inverse)',

        /* Semantic — borders */
        'border-default': 'var(--border-default)',
        'border-muted': 'var(--border-muted)',
        'border-accent': 'var(--border-accent)',
        'border-focus': 'var(--border-focus)',

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

        /* Legacy aliases — repointed to the warm palette */
        primary: 'var(--brand-primary)',
        'primary-hover': 'var(--brand-primary-hover)',
        accent: 'var(--brand-secondary)',
        'accent-hover': 'var(--brand-secondary-hover)',
        info: 'var(--blue-5)',
        'info-strong': 'var(--blue-6)',
        success: 'var(--green-5)',
        warning: 'var(--warning-fg)',
        danger: 'var(--red-5)',
        ink: 'var(--text-heading)',
        text: 'var(--text-body)',
        'text-muted': 'var(--text-muted)',
        subtle: 'var(--text-subtle)',
        faint: 'var(--sand-4)',
        surface: 'var(--surface-card)',
        'surface-subtle': 'var(--surface-sunken)',
        background: 'var(--surface-page)',
        border: 'var(--border-default)',
        'border-strong': 'var(--border-strong)',
      },
      spacing: {
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
        pill: 'var(--radius-pill)',
        full: 'var(--radius-pill)',
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
        /* Legacy heading aliases → new scale */
        'heading-lg': ['var(--text-h3)', { lineHeight: '1.25', letterSpacing: '-0.02em', fontWeight: '700' }],
        'heading-md': ['var(--text-h4)', { lineHeight: '1.25', fontWeight: '700' }],
        code: ['var(--text-body-sm)', { lineHeight: '1.5' }],
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
        focus: 'var(--coral-1)',
      },
    },
  },
  plugins: [],
};
