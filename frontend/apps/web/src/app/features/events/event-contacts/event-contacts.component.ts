import { Component, OnInit, inject, signal } from '@angular/core';
import { ButtonDirective, LoadingComponent, AlertComponent } from '../../../shared/ui';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EventContact, EventDetail } from '../../../core/models/event.models';
import { EventService } from '../../../core/services/event.service';
import { problemDetail } from '../../../core/utils/problem';

/**
 * US6 — admin management of the event's free-form contacts (name + role + a phone
 * and/or email). Contacts show publicly on the event page. Admin-only; server-enforced.
 */
@Component({
  selector: 'jh-event-contacts',
  imports: [ReactiveFormsModule, RouterLink, ButtonDirective, LoadingComponent, AlertComponent],
  templateUrl: './event-contacts.component.html',
  styleUrl: './event-contacts.component.css',
})
export class EventContactsComponent implements OnInit {
  private readonly events = inject(EventService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);

  protected readonly detail = signal<EventDetail | null>(null);
  protected readonly contacts = signal<EventContact[]>([]);
  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  /** The contact being edited (null = the form adds a new one). */
  protected readonly editingId = signal<string | null>(null);

  private id = '';

  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    role: ['', [Validators.required, Validators.maxLength(80)]],
    phone: ['', [Validators.maxLength(40)]],
    email: ['', [Validators.maxLength(256)]],
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.events.getEvent(this.id).subscribe({
      next: (d) => {
        if (!d.viewer.isAdmin) {
          this.forbidden.set(true);
          this.loading.set(false);
          return;
        }
        this.detail.set(d);
        this.reload();
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.forbidden.set(true);
      },
    });
  }

  private reload(): void {
    this.events.getContacts(this.id).subscribe((r) => this.contacts.set(r.items));
  }

  /** Load a contact into the form to edit it. */
  protected startEdit(c: EventContact): void {
    this.editingId.set(c.id);
    this.error.set(null);
    this.form.setValue({ name: c.name, role: c.role, phone: c.phone ?? '', email: c.email ?? '' });
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
    this.error.set(null);
    this.form.reset();
  }

  /** Add a new contact, or update the one being edited. */
  protected save(): void {
    const v = this.form.getRawValue();
    if (this.form.invalid || this.saving()) {
      return;
    }
    if (!v.phone.trim() && !v.email.trim()) {
      this.error.set('Add a phone number, an email, or both.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);

    const request = {
      name: v.name.trim(),
      role: v.role.trim(),
      phone: v.phone.trim() || null,
      email: v.email.trim() || null,
    };
    const id = this.editingId();
    const request$ = id
      ? this.events.updateContact(this.id, id, request)
      : this.events.addContact(this.id, request);

    request$.subscribe({
      next: (c) => {
        this.contacts.update((list) => (id ? list.map((x) => (x.id === id ? c : x)) : [...list, c]));
        this.editingId.set(null);
        this.form.reset();
        this.saving.set(false);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  protected remove(contactId: string): void {
    if (this.editingId() === contactId) {
      this.cancelEdit();
    }
    this.events.removeContact(this.id, contactId).subscribe({
      next: () => this.contacts.update((list) => list.filter((c) => c.id !== contactId)),
      error: (err) => this.error.set(problemDetail(err)),
    });
  }
}
