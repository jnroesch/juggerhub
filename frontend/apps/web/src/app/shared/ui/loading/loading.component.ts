import { Component, input } from '@angular/core';

/**
 * Shared loading primitive (feature 024). The single, standardized loading treatment:
 * one muted text line (`body-sm` / `text-muted`) with component-owned spacing, so every
 * screen's "Loading…" reads identically (clarified: a text line, not a spinner or
 * skeleton). The label may be contextual ("Loading your profile…").
 */
@Component({
  selector: 'jh-loading',
  templateUrl: './loading.component.html',
  styleUrl: './loading.component.css',
})
export class LoadingComponent {
  /** The line of copy. Contextual variants are allowed. */
  readonly label = input('Loading…');
  /** Left-aligned by default; centered for standalone/full-width states. */
  readonly align = input<'left' | 'center'>('left');
}
