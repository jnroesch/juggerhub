/**
 * Curated Lucide (https://lucide.dev) glyphs used across JuggerHub, inlined as SVG
 * inner markup (feature 024). Line icons only, drawn on a 24×24 viewBox with a 2px
 * stroke and `currentColor` — the wrapper `<svg>` (in icon.component) supplies size,
 * stroke width, and colour. Add a glyph here (copy Lucide's inner nodes) before using
 * a new `name`; do not use text glyphs as icons (DESIGN.md).
 */
export const ICONS = {
  plus: '<line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>',
  check: '<polyline points="20 6 9 17 4 12"/>',
  x: '<line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>',
  search:
    '<circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>',
  bell: '<path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/>',
  'arrow-right': '<line x1="5" y1="12" x2="19" y2="12"/><polyline points="12 5 19 12 12 19"/>',
  'user-plus':
    '<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><line x1="19" y1="8" x2="19" y2="14"/><line x1="22" y1="11" x2="16" y2="11"/>',
} as const;

/** Valid `jh-icon` names — keys of the curated map. */
export type IconName = keyof typeof ICONS;
