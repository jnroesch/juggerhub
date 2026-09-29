import { Component, ElementRef, HostListener, afterNextRender, input, output, viewChild } from '@angular/core';
import { ButtonDirective } from '../button/button.directive';

let nextId = 0;

/**
 * A question asked before something that cannot be undone (feature 064): removing a teammate,
 * removing a party player, disbanding a party. A bottom sheet on a phone, a centred card from `sm`.
 *
 * It owns what every such question must get right and a hand-copied one tends not to: it is a
 * labelled modal dialog, the **safe answer has the focus** when it opens (so Enter on arrival keeps
 * things as they are), Tab stays inside while it is open, and Escape is the safe answer. While the
 * host is working it takes no second answer, Escape included.
 *
 * Purely presentational. The host renders it inside an `@if` while a question is pending, passes
 * translated text, runs the call on `confirmed`, and decides where focus goes once it closes — only
 * the host knows which button asked. Rendered at page level, outside any `jh-card`: cards clip.
 */
@Component({
  selector: 'jh-confirm-dialog',
  imports: [ButtonDirective],
  templateUrl: './confirm-dialog.component.html',
  styleUrl: './confirm-dialog.component.css',
})
export class ConfirmDialogComponent {
  readonly heading = input.required<string>();
  readonly body = input.required<string>();
  /** The safe answer — focused on open. */
  readonly keepLabel = input.required<string>();
  /** The destructive answer. */
  readonly confirmLabel = input.required<string>();
  /** The destructive answer's label while {@link busy}. Falls back to {@link confirmLabel}. */
  readonly busyLabel = input('');
  readonly busy = input(false);
  /** One line under the explanation when the last attempt failed. Already translated. */
  readonly error = input<string | null>(null);

  readonly confirmed = output<void>();
  readonly dismissed = output<void>();

  protected readonly id = `jh-confirm-${++nextId}`;
  private readonly keepButton = viewChild.required<ElementRef<HTMLButtonElement>>('keep');

  constructor() {
    // Zoneless: the button exists only after the first render (GH #344 — never an effect).
    afterNextRender(() => this.keepButton().nativeElement.focus());
  }

  protected dismiss(): void {
    if (!this.busy()) {
      this.dismissed.emit();
    }
  }

  protected confirm(): void {
    if (!this.busy()) {
      this.confirmed.emit();
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.dismiss();
  }

  /** Keep Tab inside the open dialog: `aria-modal` promises that the page behind it is inert. */
  protected trapTab(event: Event): void {
    const key = event as KeyboardEvent;
    const buttons = Array.from(
      (key.currentTarget as HTMLElement).querySelectorAll<HTMLElement>('button:not([disabled])'),
    );
    if (buttons.length === 0) {
      return;
    }
    const first = buttons[0];
    const last = buttons[buttons.length - 1];
    if (key.shiftKey && document.activeElement === first) {
      last.focus();
      key.preventDefault();
    } else if (!key.shiftKey && document.activeElement === last) {
      first.focus();
      key.preventDefault();
    }
  }
}
