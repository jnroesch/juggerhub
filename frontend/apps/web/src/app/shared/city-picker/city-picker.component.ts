import {
  Component,
  DestroyRef,
  EventEmitter,
  Input,
  OnInit,
  Output,
  inject,
  signal,
  ChangeDetectionStrategy
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, switchMap, of, catchError } from 'rxjs';
import { CityService } from '../../core/services/city.service';
import { CityOption, Location } from '../../core/models/city.models';
import { IconComponent } from '../ui';

/**
 * Shared city picker (feature 030). Type-ahead search against the backend geocoder proxy, debounced
 * 250ms to match the browse/onboarding search feel. Emits the selected {@link CityOption} (or null on
 * clear). Never calls the geocoder directly; a 503 (geocoder unavailable) becomes a retryable
 * transient message rather than a broken control (FR-019).
 *
 * The component owns only selection UX. The chosen city is persisted by the OWNING form's save,
 * which sends `cityExternalId` and lets the backend re-resolve the canonical city (Principle I).
 */
@Component({
  selector: 'jh-city-picker',
  imports: [FormsModule, IconComponent],
  templateUrl: './city-picker.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './city-picker.component.css',
})
export class CityPickerComponent implements OnInit {
  private readonly cities = inject(CityService);
  private readonly destroyRef = inject(DestroyRef);

  /** Prefilled label from an already-set location (e.g. profile edit). Display only. */
  @Input() initial: Location | null = null;

  /** Placeholder shown in the search field. */
  @Input() placeholder = 'Search for a city…';

  /** Emits on every selection change: the picked option, or null when cleared. */
  @Output() readonly selectedChange = new EventEmitter<CityOption | null>();

  private readonly queryInput = new Subject<string>();

  protected readonly query = signal('');
  protected readonly results = signal<readonly CityOption[]>([]);
  protected readonly selected = signal<CityOption | null>(null);
  protected readonly selectedLabel = signal<string | null>(null);
  protected readonly searching = signal(false);
  protected readonly unavailable = signal(false);
  /** True once a non-empty search returned no matches (distinct from "haven't searched"). */
  protected readonly searched = signal(false);

  ngOnInit(): void {
    // Prefill the confirmed-selection chip from an existing location, so editing shows the current
    // city without re-searching. There is no CityOption for it (it came from the read model), so the
    // chip renders from the label; changing requires a fresh pick.
    this.selectedLabel.set(this.initial?.label ?? null);

    this.queryInput
      .pipe(
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((q) => {
          const trimmed = q.trim();
          this.query.set(trimmed);
          if (trimmed.length < 2) {
            this.searching.set(false);
            this.searched.set(false);
            return of<CityOption[]>([]);
          }
          this.searching.set(true);
          this.unavailable.set(false);
          return this.cities.search(trimmed).pipe(
            catchError((error: HttpErrorResponse) => {
              // 503 = geocoder unavailable → transient, retryable. Everything else also degrades
              // to the same neutral state; no provider detail is surfaced (Principle I).
              this.unavailable.set(true);
              return of<CityOption[]>([]);
            }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((options) => {
        this.searching.set(false);
        this.searched.set(this.query().length >= 2 && !this.unavailable());
        this.results.set(options);
      });
  }

  protected onQuery(value: string): void {
    this.queryInput.next(value);
  }

  protected pick(option: CityOption): void {
    this.selected.set(option);
    this.selectedLabel.set(option.label);
    this.results.set([]);
    this.query.set('');
    this.searched.set(false);
    this.selectedChange.emit(option);
  }

  protected clear(): void {
    this.selected.set(null);
    this.selectedLabel.set(null);
    this.results.set([]);
    this.query.set('');
    this.searched.set(false);
    this.selectedChange.emit(null);
  }
}
