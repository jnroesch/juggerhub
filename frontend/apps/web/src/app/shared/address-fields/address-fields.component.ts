import { Component, input, model, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { CityOption, Location } from '../../core/models/city.models';
import { CityPickerComponent } from '../city-picker/city-picker.component';

/**
 * The structured in-person address group (feature 042): venue name, street, postal code and a
 * canonical city picked through {@link CityPickerComponent}. Used by the three training forms —
 * create, series edit and single-session edit — and by event create/edit (GH #136), so every form
 * that locates something presents and validates the same thing. Its labels live in the shared
 * `address.*` catalogue namespace for the same reason.
 *
 * Deliberately template-driven-compatible (`model()` two-way bindings rather than a
 * `ControlValueAccessor`): the training forms use `ngModel`, and converting them to reactive forms
 * is a larger change than this feature needs (research R5). Reactive-form hosts (the two event
 * forms) hold these three values in signals beside their `FormGroup` rather than in it.
 *
 * Like the city picker, this component owns only input UX. The owning form persists the city by
 * sending `cityExternalId`; the backend re-resolves the canonical city and is the real validator
 * (constitution Principle I).
 */
@Component({
  selector: 'jh-address-fields',
  imports: [FormsModule, CityPickerComponent, TranslocoPipe],
  templateUrl: './address-fields.component.html',
  styleUrl: './address-fields.component.css',
})
export class AddressFieldsComponent {
  /** Optional venue name, e.g. "Sportpark Müngersdorf". */
  readonly venueName = model('');
  /** Street. Required for in-person, enforced by the owning form and the server. */
  readonly street = model('');
  /** Postal code. Required for in-person, enforced by the owning form and the server. */
  readonly postalCode = model('');

  /**
   * Already-stored city, so an edit form reads back what is currently set. Display only.
   *
   * ⚠ {@link CityPickerComponent} consumes its `initial` in `ngOnInit`, so this must be set at
   * FIRST render — a value pushed in after the picker exists never reaches the chip. Hosts that
   * load asynchronously must render this group only once their data has arrived (the training edit
   * forms do: the form lives in the `@else` branch of their loading gate).
   */
  readonly initialCity = input<Location | null>(null);

  /** A `data-testid` prefix so each host form can target its own fields. */
  readonly testIdPrefix = input('address');

  /** Emits the picked city, or null when cleared. The owning form holds the selection. */
  readonly cityChange = output<CityOption | null>();

  protected onCitySelected(option: CityOption | null): void {
    this.cityChange.emit(option);
  }
}
