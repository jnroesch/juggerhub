import { Component, computed, inject, input } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ICONS, IconName } from './icons';

/**
 * Shared icon primitive (feature 024). Renders a curated Lucide line icon as inline
 * SVG (2px stroke, `currentColor`), sized 16–22px inline with text and decorative by
 * default (`aria-hidden`). Centralises stroke/size/colour so screens never inline
 * ad-hoc SVG or use a text glyph (e.g. a literal "+") as an icon (DESIGN.md, FR-012).
 * The markup comes from a static, in-repo constant map — never user input — so the
 * bypassed sanitisation is safe.
 */
@Component({
  selector: 'jh-icon',
  templateUrl: './icon.component.html',
  styleUrl: './icon.component.css',
})
export class IconComponent {
  private readonly sanitizer = inject(DomSanitizer);

  readonly name = input.required<IconName>();
  /** Rendered box size in px (16–22 inline with text). */
  readonly size = input(18);

  protected readonly svg = computed<SafeHtml>(() => {
    const inner = ICONS[this.name()];
    const px = this.size();
    const markup =
      `<svg xmlns="http://www.w3.org/2000/svg" width="${px}" height="${px}" ` +
      `viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" ` +
      `stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">` +
      `${inner}</svg>`;
    return this.sanitizer.bypassSecurityTrustHtml(markup);
  });
}
