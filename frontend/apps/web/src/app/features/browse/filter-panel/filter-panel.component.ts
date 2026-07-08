import { Component, ElementRef, HostListener, effect, input, output, viewChild } from '@angular/core';

/**
 * On-demand filter panel (feature 007) — a bottom sheet on mobile, a slide-over drawer on
 * desktop (CSS/media-query driven). Presentational chrome shared by all three browse pages:
 * a Reset control, the projected page-specific filter controls, the locked "Near me — coming
 * soon" placeholder (the hook for a future location feature), and a primary "Show N …" action
 * bound to the live pending count. The page owns the pending-vs-applied filter state.
 *
 * Accessibility: rendered as a modal dialog with a focus trap — focus moves into the panel on
 * open, Tab/Shift+Tab wrap within it, Escape closes it, and focus is restored to the opener.
 */
@Component({
  selector: 'jh-filter-panel',
  imports: [],
  templateUrl: './filter-panel.component.html',
  styleUrl: './filter-panel.component.css',
})
export class FilterPanelComponent {
  readonly open = input(false);
  /** Live count of results the pending selection would show (null = unknown/loading). */
  readonly pendingCount = input<number | null>(null);
  /** Plural noun for the primary button, e.g. "teams". */
  readonly resultNoun = input('results');

  readonly apply = output<void>();
  readonly resetFilters = output<void>();
  readonly closePanel = output<void>();

  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');
  private lastFocused: HTMLElement | null = null;

  constructor() {
    // Move focus into the panel on open; restore it to the opener on close.
    effect(() => {
      if (this.open()) {
        this.lastFocused = (document.activeElement as HTMLElement) ?? null;
        setTimeout(() => this.focusable()[0]?.focus(), 0);
      } else if (this.lastFocused) {
        this.lastFocused.focus();
        this.lastFocused = null;
      }
    });
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.open()) {
      this.closePanel.emit();
    }
  }

  /** Wrap Tab focus within the panel (called for Tab and Shift+Tab). */
  protected onTab(event: KeyboardEvent): void {
    const items = this.focusable();
    if (items.length === 0) {
      return;
    }
    const first = items[0];
    const last = items[items.length - 1];
    const active = document.activeElement;
    if (event.shiftKey && active === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }

  private focusable(): HTMLElement[] {
    const root = this.panel()?.nativeElement;
    if (!root) {
      return [];
    }
    const selector =
      'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    return Array.from(root.querySelectorAll<HTMLElement>(selector)).filter((el) => el.offsetParent !== null);
  }
}
