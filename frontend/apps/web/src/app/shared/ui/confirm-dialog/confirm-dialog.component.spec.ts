import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConfirmDialogComponent } from './confirm-dialog.component';

/** A page asking a question the way the team and party pages do: it owns the state and the call. */
@Component({
  imports: [ConfirmDialogComponent],
  template: `
    <button type="button" data-testid="opener">Remove</button>
    @if (open()) {
      <jh-confirm-dialog
        heading="Remove Jonas from Rheinfeuer?"
        body="They lose access to the team's members-only pages and chat."
        keepLabel="Keep Jonas"
        confirmLabel="Remove from team"
        busyLabel="Removing…"
        [busy]="busy()"
        [error]="error()"
        (confirmed)="confirmed = confirmed + 1"
        (dismissed)="dismissed = dismissed + 1"
      />
    }
  `,
})
class HostComponent {
  readonly open = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  confirmed = 0;
  dismissed = 0;
}

/**
 * Feature 064 — the confirmation every destructive action on the team and party pages asks through.
 * The focus and keyboard rules are the point: they are what a hand-copied dialog gets wrong.
 */
describe('ConfirmDialogComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  const el = <T extends HTMLElement = HTMLElement>(selector: string): T | null =>
    fixture.nativeElement.querySelector(selector);
  /** The element, or a failing test naming what was missing. */
  function must<T extends HTMLElement = HTMLElement>(selector: string): T {
    const found = el<T>(selector);
    if (!found) {
      throw new Error(`Nothing matches ${selector}`);
    }
    return found;
  }
  const keep = () => must<HTMLButtonElement>('[data-testid="confirm-dialog-keep"]');
  const confirm = () => must<HTMLButtonElement>('[data-testid="confirm-dialog-confirm"]');

  function press(key: string, shiftKey = false, target: EventTarget = document): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key, shiftKey, bubbles: true, cancelable: true });
    target.dispatchEvent(event);
    fixture.detectChanges();
    return event;
  }

  it('is a labelled modal dialog with the question, the explanation and both answers', () => {
    const dialog = must('[data-testid="confirm-dialog"]');

    expect(dialog.getAttribute('role')).toBe('dialog');
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    const title = document.getElementById(dialog.getAttribute('aria-labelledby') ?? '');
    const body = document.getElementById(dialog.getAttribute('aria-describedby') ?? '');
    expect(title?.textContent?.trim()).toBe('Remove Jonas from Rheinfeuer?');
    expect(body?.textContent?.trim()).toBe("They lose access to the team's members-only pages and chat.");
    expect(keep().textContent?.trim()).toBe('Keep Jonas');
    expect(confirm().textContent?.trim()).toBe('Remove from team');
  });

  it('puts the focus on the safe answer when it opens', () => {
    expect(document.activeElement).toBe(keep());
  });

  it('dismisses on the safe answer and on Escape, and confirms only on the destructive answer', () => {
    keep().click();
    press('Escape');
    expect(host.dismissed).toBe(2);
    expect(host.confirmed).toBe(0);

    confirm().click();
    expect(host.confirmed).toBe(1);
  });

  it('while busy, shows it is working and takes no second answer, not even Escape', () => {
    host.busy.set(true);
    fixture.detectChanges();

    expect(confirm().disabled).toBe(true);
    expect(keep().disabled).toBe(true);
    expect(confirm().textContent?.trim()).toBe('Removing…');
    press('Escape');
    expect(host.dismissed).toBe(0);
  });

  it('keeps Tab inside the dialog in both directions', () => {
    confirm().focus();
    const forward = press('Tab', false, confirm());
    expect(forward.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(keep());

    const back = press('Tab', true, keep());
    expect(back.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(confirm());
  });

  it('holds focus on the dialog while both answers are disabled, and Tab cannot leave it', () => {
    // CodeRabbit on PR #394: a disabled button drops focus on the page behind the modal.
    const panel = must('[data-testid="confirm-dialog"]');

    confirm().click();
    host.busy.set(true);
    fixture.detectChanges();

    expect(document.activeElement).toBe(panel);
    const tab = press('Tab', false, panel);
    expect(tab.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(panel);

    // The host's call failed: the answers come back, and Tab from the panel enters them.
    host.busy.set(false);
    fixture.detectChanges();
    press('Tab', false, panel);
    expect(document.activeElement).toBe(keep());
  });

  it('shows an error as one alert line under the explanation', () => {
    expect(el('[data-testid="confirm-dialog-error"]')).toBeNull();

    host.error.set("That didn't work. Try again.");
    fixture.detectChanges();

    const error = must('[data-testid="confirm-dialog-error"]');
    expect(error.getAttribute('role')).toBe('alert');
    expect(error.textContent?.trim()).toBe("That didn't work. Try again.");
  });

  it('uses the default button size, never the 36px small one', () => {
    // DESIGN.md's 44px touch target (the 063 lesson): `sm` would put both answers below it.
    expect(keep().classList).toContain('min-h-11');
    expect(confirm().classList).toContain('min-h-11');
  });
});
