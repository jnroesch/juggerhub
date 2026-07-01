import { Component, input, output } from '@angular/core';
import { POMPFEN_CATALOG, Pompfe } from '../../../../shared/pompfen.catalog';

/**
 * The owner's pompfen picker: the full canonical set, with the player's selections
 * shown filled and the rest available (dotted), per the wireframe. Multi-select;
 * Läufer (a position) sits in the same set. Emits the full desired selection.
 */
@Component({
  selector: 'jh-pompfe-selector',
  imports: [],
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
