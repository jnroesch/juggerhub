import { Component, input } from '@angular/core';

/** Status tone → DESIGN.md token triple. */
export type AlertTone = 'danger' | 'success' | 'warning' | 'info';

/**
 * Shared alert / status primitive (feature 024). One boxed treatment for page- and
 * form-level status, always announced to assistive tech via `role="alert"`. `tone`
 * maps to the paired `*-bg` / `*-border` / `*-fg` tokens — so danger errors are one
 * red (`danger-fg`) everywhere, retiring the bare `text-danger` (red-5) variant and
 * the two visual languages the audit found. Content is projected. Tone classes are
 * applied additively on the host, so callers may still add layout classes (e.g. mt-md).
 */
@Component({
  selector: 'jh-alert',
  templateUrl: './alert.component.html',
  styleUrl: './alert.component.css',
  host: {
    role: 'alert',
    class: 'block rounded-md border px-md py-sm text-body-sm',
    '[class.border-danger-border]': "tone() === 'danger'",
    '[class.bg-danger-bg]': "tone() === 'danger'",
    '[class.text-danger-fg]': "tone() === 'danger'",
    '[class.border-success-border]': "tone() === 'success'",
    '[class.bg-success-bg]': "tone() === 'success'",
    '[class.text-success-fg]': "tone() === 'success'",
    '[class.border-warning-border]': "tone() === 'warning'",
    '[class.bg-warning-bg]': "tone() === 'warning'",
    '[class.text-warning-fg]': "tone() === 'warning'",
    '[class.border-info-border]': "tone() === 'info'",
    '[class.bg-info-bg]': "tone() === 'info'",
    '[class.text-info-fg]': "tone() === 'info'",
  },
})
export class AlertComponent {
  readonly tone = input<AlertTone>('danger');
}
