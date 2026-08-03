import { Component, input, output } from '@angular/core';

/**
 * A labelled on/off switch used inside browse filter panels (feature 007). Presentational —
 * the page owns the value and reloads on change.
 */
@Component({
  selector: 'jh-filter-toggle',
  imports: [],
  templateUrl: './filter-toggle.component.html',
  styleUrl: './filter-toggle.component.css',
})
export class FilterToggleComponent {
  readonly label = input.required<string>();
  readonly hint = input<string | null>(null);
  readonly checked = input(false);
  readonly toggled = output<boolean>();
}
