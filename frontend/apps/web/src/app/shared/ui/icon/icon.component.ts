import { Component, computed, inject, input } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ICONS, IconName } from './icons';

/**
 * The three steps an icon is drawn at, and the whole of the size vocabulary.
 *
 * Named rather than numeric, and that is the point (GH #300): the audit found the product
 * drawing icons at **seven** sizes — 12, 14, 16, 18, 20, 22, 24 — chosen per template, two of
 * them outside the 16–22px range DESIGN.md states. A number input would let the eighth in;
 * a three-value union cannot.
 *
 * - `sm` (16) — inline with `caption`/`body-sm`, in a chip, in a dense table row.
 * - `md` (18) — the default: inline with body text, and in a button beside its label.
 * - `lg` (22) — a nav tab, an empty state, an icon standing on its own.
 */
export type IconSize = 'sm' | 'md' | 'lg';

const SIZE_PX: Record<IconSize, number> = { sm: 16, md: 18, lg: 22 };

/**
 * Shared icon primitive (feature 024, adopted product-wide by GH #300). Renders a curated
 * Lucide line icon as inline SVG (2px stroke, `currentColor`), at one of three sizes, and
 * decorative by default (`aria-hidden`).
 *
 * Centralising stroke, size and colour is the substance: before #300 the templates held **99
 * hand-inlined `<svg>` across 37 files at five stroke widths** (2.0, 1.8, 1.6, 3.0, 2.2), so
 * an icon drawn at 1.6 sat beside one drawn at 2.0 and the interface read as assembled rather
 * than drawn. A screen never inlines ad-hoc SVG, and never uses a text glyph (a literal `+`,
 * `✓`, `›`) as an icon — both are what this primitive is for (DESIGN.md; `icon-system.spec.ts`
 * fails the build if either comes back).
 *
 * Colour comes from the host: `<jh-icon name="check" class="text-brand-strong" />`. The markup
 * comes from a static, in-repo constant map — never user input — so the bypassed sanitisation
 * is safe.
 */
@Component({
  selector: 'jh-icon',
  templateUrl: './icon.component.html',
  styleUrl: './icon.component.css',
})
export class IconComponent {
  private readonly sanitizer = inject(DomSanitizer);

  readonly name = input.required<IconName>();
  /** Which of the three steps to draw at. Default `md` (18px). */
  readonly size = input<IconSize>('md');

  protected readonly svg = computed<SafeHtml>(() => {
    const inner = ICONS[this.name()];
    const px = SIZE_PX[this.size()];
    const markup =
      `<svg xmlns="http://www.w3.org/2000/svg" width="${px}" height="${px}" ` +
      `viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" ` +
      `stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">` +
      `${inner}</svg>`;
    return this.sanitizer.bypassSecurityTrustHtml(markup);
  });
}
