import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ChatService } from '../../../core/services/chat.service';
import { PersonHit } from '../../../core/models/chat.models';

/**
 * Start a chat (feature 019, wireframe 9e): pick **one person** for a DM, or **several** for a named
 * group — the name field appears only once a second person is picked, because that is the moment it
 * becomes a group.
 *
 * Search reaches **any** player: DM reach is open by product decision (FR-049), and block is the
 * recourse. (FR-049's "teammates surfaced first" convenience is not built — the picker is
 * search-driven. It needs a my-teammates read that no endpoint offers yet; tracked as a follow-up.)
 */
@Component({
  selector: 'jh-chat-new',
  imports: [FormsModule, RouterLink],
  templateUrl: './chat-new.component.html',
  styleUrl: './chat-new.component.css',
})
export class ChatNewComponent {
  private readonly chat = inject(ChatService);
  private readonly router = inject(Router);

  protected readonly term = signal('');
  protected readonly people = signal<PersonHit[]>([]);
  protected readonly selected = signal<PersonHit[]>([]);
  protected readonly name = signal('');
  protected readonly starting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly searching = signal(false);

  /** Two or more picked ⇒ a group, which needs a name. One ⇒ a DM, which does not. */
  protected readonly isGroup = computed(() => this.selected().length >= 2);

  protected readonly canStart = computed(() => {
    const count = this.selected().length;
    if (count === 0 || this.starting()) {
      return false;
    }

    return count === 1 || this.name().trim().length > 0;
  });

  protected readonly hasTerm = computed(() => this.term().trim().length >= 2);

  private searchTimer?: ReturnType<typeof setTimeout>;

  protected onTermChange(value: string): void {
    this.term.set(value);
    clearTimeout(this.searchTimer);

    if (value.trim().length < 2) {
      this.people.set([]);
      return;
    }

    this.searching.set(true);
    this.searchTimer = setTimeout(() => {
      this.chat.search(value).subscribe({
        next: (r) => {
          this.people.set([...r.people.items]);
          this.searching.set(false);
        },
        error: () => this.searching.set(false),
      });
    }, 250);
  }

  protected isSelected(userId: string): boolean {
    return this.selected().some((p) => p.userId === userId);
  }

  protected toggle(person: PersonHit): void {
    this.selected.update((sel) =>
      sel.some((p) => p.userId === person.userId)
        ? sel.filter((p) => p.userId !== person.userId)
        : [...sel, person],
    );
  }

  protected remove(userId: string): void {
    this.selected.update((sel) => sel.filter((p) => p.userId !== userId));
  }

  protected start(): void {
    if (!this.canStart()) {
      return;
    }

    const ids = this.selected().map((p) => p.userId);
    const groupName = this.isGroup() ? this.name().trim() : null;

    // A single person is a direct message. An existing thread opens straight away; otherwise open a
    // compose draft (feature 022 lazy creation) — the DM is created only when the first message is
    // sent, so picking someone and leaving pollutes nothing. Groups (2+) are still created on start.
    if (ids.length === 1) {
      const person = this.selected()[0];
      if (person.existingConversationId) {
        void this.router.navigate(['/chat', person.existingConversationId]);
      } else {
        void this.router.navigate(['/chat/compose', person.handle ?? person.userId], {
          state: { userId: person.userId, displayName: person.displayName },
        });
      }
      return;
    }

    this.starting.set(true);
    this.error.set(null);

    this.chat.start(ids, groupName).subscribe({
      next: (c) => void this.router.navigate(['/chat', c.id]),
      error: (e: { error?: { detail?: string } }) => {
        this.starting.set(false);
        this.error.set(e.error?.detail ?? "That chat couldn't be started.");
      },
    });
  }
}
