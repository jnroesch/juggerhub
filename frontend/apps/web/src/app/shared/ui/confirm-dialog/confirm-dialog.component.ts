import { Component, ElementRef, HostListener, afterNextRender, input, output, viewChild } from '@angular/core';
import { ButtonDirective, ButtonVariant } from '../button/button.directive';

let nextId = 0;

/**
 * A question asked before an action is taken (feature 064): removing a teammate, removing a party
 * player, disbanding a party, deleting a news post — and asking to join a team, where nothing is
 * destroyed but a stray press should still send nothing (GH #392). A bottom sheet on a phone, a
 * centred card from `sm`.
 *
 * It owns what every such question must get right and a hand-copied one tends not to: it is a
 * labelled modal dialog, the **safe answer has the focus** when it opens (so Enter on arrival keeps
 * things as they are), Tab stays inside while it is open, and Escape is the safe answer. While the
 * host is working it takes no second answer, Escape included.
 *
 * Purely presentational. The host renders it inside an `@if` while a question is pending, passes
 * translated text, runs the call on `confirmed`, and decides where focus goes once it closes — only
 * the host knows which button asked. It is fixed to the viewport, so a card's clipping does not reach
 * it; an ancestor that transforms would (it becomes what "fixed" is measured against), so a host
 * inside one renders the dialog at page level instead.
 */
@Component({
  selector: 'jh-confirm-dialog',
  imports: [ButtonDirective],
  templateUrl: './confirm-dialog.component.html',
  styleUrl: './confirm-dialog.component.css',
  // No box of its own: everything it draws is fixed to the viewport. With a box, a host that lays its
  // children out with a gap (a news post's row) would make room for it, and the page behind the open
  // dialog would shift.
  host: { class: 'contents' },
})
export class ConfirmDialogComponent {
  readonly heading = input.required<string>();
  readonly body = input.required<string>();
  /** The safe answer — focused on open. */
  readonly keepLabel = input.required<string>();
  /** The answer that acts. */
  readonly confirmLabel = input.required<string>();
  /**
   * How the acting answer looks. Destructive unless the host says otherwise: a question whose answer
   * destroys nothing (asking to join a team) passes `primary`.
   */
  readonly confirmVariant = input<ButtonVariant>('danger');
  /** The acting answer's label while {@link busy}. Falls back to {@link confirmLabel}. */
  readonly busyLabel = input('');
  readonly busy = input(false);
  /** One line under the explanation when the last attempt failed. Already translated. */
  readonly error = input<string | null>(null);

  readonly confirmed = output<void>();
  readonly dismissed = output<void>();

  protected readonly id = `jh-confirm-${++nextId}`;
  private readonly keepButton = viewChild.required<ElementRef<HTMLButtonElement>>('keep');
  private readonly panel = viewChild.required<ElementRef<HTMLElement>>('panel');

  constructor() {
    // The focus leaves the page now, before this dialog is in it; the safe answer takes it below.
    // Leaving it until then made the answers grow in from nothing. A dialog is usually asked for by a
    // menu item, and the menu closes in the same pass: a browser that loses its focused element
    // restyles the page at once — with this dialog already in it, its buttons not yet wearing their
    // classes. Those then arrive as a change and are animated: padding, corners and weight, none of
    // which DESIGN.md lets move.
    const focused = document.activeElement;
    if (focused instanceof HTMLElement) {
      focused.blur();
    }
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
      // Both answers are about to be disabled while the host works, and a disabled button drops focus
      // onto the page behind the modal. Hold it on the panel instead; Tab from there comes back in.
      this.panel().nativeElement.focus();
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
    const panel = key.currentTarget as HTMLElement;
    const buttons = Array.from(panel.querySelectorAll<HTMLElement>('button:not([disabled])'));
    if (buttons.length === 0) {
      // Busy: nothing to move to, and nowhere outside to go. Stay on the panel.
      key.preventDefault();
      panel.focus();
      return;
    }
    const first = buttons[0];
    const last = buttons[buttons.length - 1];
    if (document.activeElement === panel) {
      // Held here while busy (see confirm()); the answers are back, so Tab enters them.
      key.preventDefault();
      (key.shiftKey ? last : first).focus();
      return;
    }
    if (key.shiftKey && document.activeElement === first) {
      last.focus();
      key.preventDefault();
    } else if (!key.shiftKey && document.activeElement === last) {
      first.focus();
      key.preventDefault();
    }
  }
}
