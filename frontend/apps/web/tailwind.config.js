const { join } = require('path');

/**
 * Tailwind theme for the JuggerHub `web` app.
 *
 * The design tokens themselves live as CSS custom properties in
 * `src/styles.css` (sourced from the repo-root DESIGN.md, the single source of
 * truth for visual identity). This config maps those variables onto Tailwind's
 * scale so utilities like `bg-primary`, `text-ink`, `rounded-lg`, `p-md` resolve
 * to the design system. Add a token in DESIGN.md → styles.css first, then here.
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
        primary: 'var(--color-primary)',
        'primary-hover': 'var(--color-primary-hover)',
        accent: 'var(--color-accent)',
        'accent-hover': 'var(--color-accent-hover)',
        info: 'var(--color-info)',
        'info-strong': 'var(--color-info-strong)',
        success: 'var(--color-success)',
        warning: 'var(--color-warning)',
        danger: 'var(--color-danger)',
        ink: 'var(--color-ink)',
        text: 'var(--color-text)',
        'text-muted': 'var(--color-text-muted)',
        subtle: 'var(--color-subtle)',
        faint: 'var(--color-faint)',
        surface: 'var(--color-surface)',
        'surface-subtle': 'var(--color-surface-subtle)',
        background: 'var(--color-background)',
        border: 'var(--color-border)',
        'border-strong': 'var(--color-border-strong)',
      },
      spacing: {
        xs: 'var(--space-xs)',
        sm: 'var(--space-sm)',
        md: 'var(--space-md)',
        lg: 'var(--space-lg)',
        xl: 'var(--space-xl)',
        '2xl': 'var(--space-2xl)',
        '3xl': 'var(--space-3xl)',
      },
      borderRadius: {
        sm: 'var(--radius-sm)',
        md: 'var(--radius-md)',
        lg: 'var(--radius-lg)',
        full: 'var(--radius-full)',
      },
      fontFamily: {
        sans: 'var(--font-sans)',
        mono: 'var(--font-mono)',
      },
      fontSize: {
        display: ['var(--text-display)', { lineHeight: '1.2', letterSpacing: '-0.025em', fontWeight: '700' }],
        'heading-lg': ['var(--text-heading-lg)', { lineHeight: '1.3', fontWeight: '600' }],
        'heading-md': ['var(--text-heading-md)', { lineHeight: '1.3', fontWeight: '600' }],
        'body-md': ['var(--text-body-md)', { lineHeight: '1.6' }],
        'body-sm': ['var(--text-body-sm)', { lineHeight: '1.5' }],
        code: ['var(--text-code)', { lineHeight: '1.4' }],
      },
      boxShadow: {
        card: '0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)',
      },
    },
  },
  plugins: [],
};
