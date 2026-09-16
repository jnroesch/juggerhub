import { Component, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { POMPFEN_CATALOG, Pompfe } from '../../../../shared/pompfen.catalog';
import { ChipDirective } from '../../../../shared/ui';

/**
 * The owner's pompfen picker: the full canonical set, with the player's selections
 * shown filled and the rest outlined. Multi-select; Läufer (a position) sits in the
 * same set. Emits the full desired selection.
 *
 * A picked pompfe wears the `secondary` chip tone — the same sage the profile shows it
 * in once saved (GH #301), so the picker previews the chip it produces rather than
 * inventing a selected-state colour of its own.
 */
@Component({
  selector: 'jh-pompfe-selector',
  imports: [TranslocoPipe, ChipDirective],
  templateUrl: './pompfe-selector.component.html',
  styleUrl: './pompfe-selector.component.css',
})
export class PompfeSelectorComponent {
  readonly selected = input<Pompfe[]>([]);
  readonly selectionChange = output<Pompfe[]>();

  protected readonly catalog = POMPFEN_CATALOG;

  protected isSelected(value: Pompfe): boolean {
    return this.selected().includes(value);
  }

  protected toggle(value: Pompfe): void {
    const set = new Set(this.selected());
    if (set.has(value)) {
      set.delete(value);
    } else {
      set.add(value);
    }
    this.selectionChange.emit([...set]);
  }
}
