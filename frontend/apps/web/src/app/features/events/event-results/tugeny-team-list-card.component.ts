import { Component, OnInit, inject, input, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable, expand, reduce, EMPTY } from 'rxjs';
import { AlertComponent, ButtonDirective, CardComponent, EmptyStateComponent, LoadingComponent } from '../../../shared/ui';
import { PagedResult, Signup } from '../../../core/models/event.models';
import { EventService } from '../../../core/services/event.service';
import { TugenyTeamList, buildTugenyTeamList } from './tugeny-team-list';

const PAGE = 100;

/**
 * The confirmed team list, ready to paste into Tugeny's *Import Team Names* dialog (feature 050,
 * US3). Available before the tournament starts — that is when the bracket is set up.
 *
 * The text sits in a read-only box as well as behind a Copy button, so it can still be selected
 * and copied by hand wherever the browser blocks the Clipboard API.
 */
@Component({
  selector: 'jh-tugeny-team-list-card',
  imports: [TranslocoPipe, AlertComponent, ButtonDirective, CardComponent, EmptyStateComponent, LoadingComponent],
  templateUrl: './tugeny-team-list-card.component.html',
  styleUrl: './tugeny-team-list-card.component.css',
})
export class TugenyTeamListCardComponent implements OnInit {
  private readonly events = inject(EventService);

  readonly eventId = input.required<string>();

  protected readonly list = signal<TugenyTeamList | null>(null);
  protected readonly loadError = signal(false);
  protected readonly copied = signal(false);

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loadError.set(false);
    this.allJoined().subscribe({
      next: (signups) => this.list.set(buildTugenyTeamList(signups)),
      error: () => this.loadError.set(true),
    });
  }

  /** Every page of the confirmed group — the list must not stop at the first page. */
  private allJoined(): Observable<Signup[]> {
    const page = (skip: number) => this.events.getParticipants(this.eventId(), 'joined', skip, PAGE);
    return page(0).pipe(
      expand((p: PagedResult<Signup>) => (p.skip + p.items.length < p.totalCount && p.items.length > 0 ? page(p.skip + p.items.length) : EMPTY)),
      reduce((all, p) => [...all, ...p.items], [] as Signup[]),
    );
  }

  protected async copy(text: string, box: HTMLTextAreaElement): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.copied.set(true);
    } catch {
      // The Clipboard API is blocked here: select the text so the viewer can copy it themselves.
      box.focus();
      box.select();
    }
  }
}
