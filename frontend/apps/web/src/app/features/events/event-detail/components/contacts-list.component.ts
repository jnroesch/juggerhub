import { Component, input } from '@angular/core';
import { EventContact } from '../../../../core/models/event.models';

/** Read-only public contacts list shown on the event page. */
@Component({
  selector: 'jh-event-contacts-list',
  templateUrl: './contacts-list.component.html',
  styleUrl: './contacts-list.component.css',
})
export class EventContactsListComponent {
  readonly contacts = input.required<EventContact[]>();
}
