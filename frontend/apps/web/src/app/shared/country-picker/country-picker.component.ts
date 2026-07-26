import {
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CityService } from '../../core/services/city.service';
import { Country } from '../../core/models/city.models';
import { IconComponent } from '../ui';

/** Keep the suggestion list short enough to sit under the input without a scroll area — the viewer
 *  narrows it by typing rather than scrolling a long list. */
const MAX_RESULTS = 6;

/**
 * Country filter type-ahead (feature 030). A controlled combobox whose displayed text IS the bound
 * `value` — the owning filter panel keeps that in its pending state, so Reset clears the field for
 * free. Suggestions come from {@link CityService.countries} (the countries that actually have a
 * located team/event/player), fetched once and filtered client-side as the viewer types, so there
 * is no per-keystroke request. Picking a suggestion emits the exact country name the backend filter
 * matches on.
 */
@Component({
  selector: 'jh-country-picker',
  imports: [IconComponent],
  templateUrl: './country-picker.component.html',
  styleUrl: './country-picker.component.css',
})
export class CountryPickerComponent implements OnInit {
  private readonly cityService = inject(CityService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /** The current filter text (owned by the parent). Its display and filtering both read from here. */
  readonly value = input('');
  readonly placeholder = input('Any country');

  /** Emits the new filter text on every edit, and the exact country name when a suggestion is picked. */
  readonly valueChange = output<string>();

  protected readonly open = signal(false);
  private readonly all = signal<readonly Country[]>([]);

  protected readonly matches = computed<readonly Country[]>(() => {
    const q = this.value().trim().toLowerCase();
    const list = this.all();
    const filtered = q
      ? list.filter((c) => c.name.toLowerCase().includes(q) || c.code?.toLowerCase() === q)
      : list;
    return filtered.slice(0, MAX_RESULTS);
  });

  ngOnInit(): void {
    // Slow-changing and shared session-wide by the service; a failure just yields a plain text input.
    this.cityService
      .countries()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => this.all.set(list),
        error: () => this.all.set([]),
      });
  }

  protected onInput(text: string): void {
    this.open.set(true);
    this.valueChange.emit(text);
  }

  protected pick(country: Country): void {
    this.open.set(false);
    this.valueChange.emit(country.name);
  }

  protected clear(): void {
    this.open.set(false);
    this.valueChange.emit('');
  }

  @HostListener('keydown.escape')
  protected onEscape(): void {
    this.open.set(false);
  }

  /** Close when focus leaves the control entirely (clicking a suggestion keeps focus inside first). */
  protected onFocusOut(event: FocusEvent): void {
    const next = event.relatedTarget as Node | null;
    if (!next || !this.host.nativeElement.contains(next)) {
      this.open.set(false);
    }
  }
}
