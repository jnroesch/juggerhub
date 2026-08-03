import { Component, input, ChangeDetectionStrategy } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { EventContact } from '../../../../core/models/event.models';

/** Read-only public contacts list shown on the event page. */
@Component({
  selector: 'jh-event-contacts-list',
  imports: [TranslocoPipe],
  templateUrl: './contacts-list.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './contacts-list.component.css',
})
export class EventContactsListComponent {
  readonly contacts = input.required<EventContact[]>();
}
