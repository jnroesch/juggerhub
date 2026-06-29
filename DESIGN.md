---
version: alpha
name: JuggerHub Design System
description: >-
  Visual identity for the JuggerHub application. Tokens are derived from
  the existing transactional email templates (backend/EmailTemplates) and map to
  the standard Tailwind palette. This is the starting baseline — extend it as the
  frontend evolves, and keep it as the single source of truth for UI/visual work.
colors:
  primary: "#4f46e5"
  primary-hover: "#4338ca"
  accent: "#7c3aed"
  accent-hover: "#6d28d9"
  info: "#3b82f6"
  info-strong: "#1e40af"
  success: "#16a34a"
  warning: "#d97706"
  danger: "#dc2626"
  ink: "#111827"
  text: "#374151"
  text-muted: "#4b5563"
  subtle: "#6b7280"
  faint: "#9ca3af"
  surface: "#ffffff"
  surface-subtle: "#f3f4f6"
  background: "#f9fafb"
  border: "#e5e7eb"
  border-strong: "#d1d5db"
typography:
  display:
    fontFamily: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Oxygen, Ubuntu, Cantarell, sans-serif
    fontSize: 28px
    fontWeight: 700
    lineHeight: 1.2
    letterSpacing: -0.025em
  heading-lg:
    fontFamily: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Oxygen, Ubuntu, Cantarell, sans-serif
    fontSize: 24px
    fontWeight: 600
    lineHeight: 1.3
  heading-md:
    fontFamily: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Oxygen, Ubuntu, Cantarell, sans-serif
    fontSize: 20px
    fontWeight: 600
    lineHeight: 1.3
  body-md:
    fontFamily: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Oxygen, Ubuntu, Cantarell, sans-serif
    fontSize: 16px
    fontWeight: 400
    lineHeight: 1.6
  body-sm:
    fontFamily: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Oxygen, Ubuntu, Cantarell, sans-serif
    fontSize: 14px
    fontWeight: 400
    lineHeight: 1.5
  code:
    fontFamily: '"SF Mono", Monaco, "Cascadia Code", monospace'
    fontSize: 13px
    fontWeight: 400
    lineHeight: 1.4
spacing:
  xs: 8px
  sm: 12px
  md: 16px
  lg: 20px
  xl: 24px
  2xl: 32px
  3xl: 40px
rounded:
  sm: 6px
  md: 8px
  lg: 12px
  full: 9999px
components:
  button:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.surface}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: 14px 28px
  button-secondary:
    backgroundColor: "{colors.surface-subtle}"
    textColor: "{colors.text}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: 14px 28px
  card:
    backgroundColor: "{colors.surface}"
    rounded: "{rounded.lg}"
    padding: "{spacing.xl}"
  input:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
---

## Overview

The JuggerHub visual identity is clean, modern, and calm: a neutral gray
foundation, generous whitespace on an 8px rhythm, and an indigo→violet brand
accent reserved for primary actions and key moments. The goal is clarity and
trust — interfaces should feel uncluttered and legible, with color used
deliberately rather than decoratively.

These tokens are extracted from the project's existing transactional email
templates so the product and its emails share one identity. Treat this file as
the source of truth for all UI/visual decisions; when product UI lands, refine
the tokens here first and let the implementation follow.

## Colors

- **Primary** (`#4f46e5`) — the indigo brand color for primary buttons, links,
  and focused/active states. Pairs with **Primary Hover** (`#4338ca`).
- **Accent** (`#7c3aed`) — violet used alongside primary in gradients and
  highlight moments (e.g. headers). Use sparingly.
- **Info / Info Strong** (`#3b82f6` / `#1e40af`) — informational callouts.
- **Status** — Success (`#16a34a`), Warning (`#d97706`), Danger (`#dc2626`) for
  feedback and validation.
- **Text ramp** — Ink (`#111827`) for strong headings, Text (`#374151`) for body
  headings, Text Muted (`#4b5563`) for body copy, Subtle (`#6b7280`) and Faint
  (`#9ca3af`) for secondary and tertiary text.
- **Surfaces** — Surface (`#ffffff`) for cards/content, Surface Subtle
  (`#f3f4f6`) for inset panels, Background (`#f9fafb`) for the page canvas.
- **Borders** — Border (`#e5e7eb`) for default separators, Border Strong
  (`#d1d5db`) for inputs and emphasized edges.

## Typography

A system sans-serif stack keeps text fast and native across platforms; a
monospace stack is reserved for code, tokens, and URLs.

- **Display** — 28px / 700, tight tracking. App name, hero moments.
- **Heading LG** — 24px / 600. Page titles.
- **Heading MD** — 20px / 600. Section titles.
- **Body MD** — 16px / 400, line-height 1.6. Default reading size.
- **Body SM** — 14px / 400. Secondary text, captions, footers.
- **Code** — 13px monospace. Inline code, tokens, raw URLs.

## Layout

Spacing follows an 8px base unit (`xs` 8 → `3xl` 40). Compose layouts from these
steps rather than arbitrary values. Content sits on light surfaces over the
`background` canvas; primary content columns cap around 600px for comfortable
line length, widening for app shells and data-dense views.

## Elevation & Depth

Depth is subtle. The standard raised surface (cards, dialogs) uses a soft
two-layer shadow:

```
0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)
```

Most surfaces are flat with a 1px border; reserve shadow for elements that float
above the page (cards, popovers, modals). Avoid heavy or high-contrast shadows.

## Shapes

Rounded corners soften the UI: `sm` 6px for small controls and inset boxes,
`md` 8px for buttons, inputs, and standard elements, `lg` 12px for cards and
containers, and `full` for pills and avatars.

## Components

- **Button (primary)** — indigo `primary` background, white label, 8px radius,
  14×28px padding, 600 weight. Darkens to `primary-hover` on hover.
- **Button (secondary)** — `surface-subtle` background, `text` label, 1px
  `border-strong` outline, no shadow.
- **Card** — white `surface`, 12px radius, `xl` (24px) padding, optional soft
  elevation.
- **Input** — white `surface`, `text` color, 1px `border-strong`, 8px radius;
  focus uses the `primary` color for the ring/border.

## Do's and Don'ts

- **Do** maintain WCAG AA contrast (≥ 4.5:1 for body text). The text ramp on
  light surfaces is tuned for this.
- **Do** reserve `primary`/`accent` for primary actions and key emphasis.
- **Do** build spacing and sizing from the 8px scale tokens.
- **Don't** introduce new colors, fonts, or radii ad hoc — add a token here
  first so the system stays consistent.
- **Don't** rely on color alone to convey status; pair it with text or an icon.
- **Don't** invent a new visual style for one screen; extend this system.
