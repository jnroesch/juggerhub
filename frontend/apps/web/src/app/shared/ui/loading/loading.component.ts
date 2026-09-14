import { Component, OnDestroy, OnInit, computed, input, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/** How long a load may run before the line switches to patient copy (DESIGN.md). */
export const PATIENT_THRESHOLD_MS = 2_000;

/**
 * Shared loading primitive (feature 024). The single, standardized loading treatment:
 * one muted text line (`body-sm` / `text-muted`) with component-owned spacing, so every
 * screen's "Loading…" reads identically (clarified: a text line, not a spinner or
 * skeleton). The label may be contextual ("Loading your profile…").
 *
 * Feature 028 added the **patient line**: if the load is still running after
 * {@link PATIENT_THRESHOLD_MS}, the same line switches to reassuring copy rather than
 * leaving the reader staring at a screen that looks stalled.
 *
 * The timing lives *here*, deliberately, and the component is told nothing by the HTTP
 * layer. This component is only ever rendered while something is loading, so it already
 * knows the one fact that matters — "this has been going a while". Wiring it to the retry
 * interceptor instead would be more code and less correct: that signal is global, so one
 * slow background request would make every loading line on the page announce itself. It is
 * also more honest this way — a genuinely slow first attempt and a silently retried one
 * look identical to the person waiting, and both deserve the same reassurance.
 *
 * The defaults are translation keys, not English strings (GH #290): most call sites render a
 * bare `<jh-loading />`, so a hardcoded default put "Loading…" on every German and Spanish page.
 */
@Component({
  selector: 'jh-loading',
  imports: [TranslocoPipe],
  templateUrl: './loading.component.html',
  styleUrl: './loading.component.css',
})
export class LoadingComponent implements OnInit, OnDestroy {
  /** The line of copy, already translated. Contextual variants are allowed; omit for `common.loading`. */
  readonly label = input<string>();
  /** Left-aligned by default; centered for standalone/full-width states. */
  readonly align = input<'left' | 'center'>('left');
  /** Replaces {@link label} once the load has run past the threshold; omit for `common.stillLoading`. */
  readonly patientLabel = input<string>();

  private readonly patient = signal(false);
  private timer?: ReturnType<typeof setTimeout>;

  /** Swaps the copy in place — same element, same classes, so nothing shifts. */
  protected readonly customLabel = computed(() =>
    this.patient() ? this.patientLabel() : this.label(),
  );
  /** What renders when the caller supplied no copy for the current phase. */
  protected readonly defaultKey = computed(() =>
    this.patient() ? 'common.stillLoading' : 'common.loading',
  );

  ngOnInit(): void {
    this.timer = setTimeout(() => this.patient.set(true), PATIENT_THRESHOLD_MS);
  }

  ngOnDestroy(): void {
    // The component is destroyed as soon as loading finishes, so a fast load never reaches
    // the threshold and the patient line is never flashed.
    clearTimeout(this.timer);
  }
}
