import { Component, computed, inject, input, output, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { RouterLink } from '@angular/router';
import { CardComponent, ButtonDirective } from '../../../shared/ui';
import { NeedsYouItem } from '../../../core/models/home.models';
import { TeamService } from '../../../core/services/team.service';
import { PartyService } from '../../../core/services/party.service';
import { MarketService } from '../../../core/services/market.service';
import { relativeTime } from '../../../core/utils/format';

/**
 * "Needs you" (feature 025, US1) — the pinned-top actionable block. Invites and requests only: team
 * invites, party participation requests, party co-admin invites, and marketplace invites/applications,
 * each resolved in place via its existing per-domain endpoint. Training RSVP is deliberately NOT here
 * (it lives inline in "Up next"). Renders nothing when empty (FR-005). Emits `resolved` so the host can
 * refresh the composite once an item is handled.
 */
@Component({
  selector: 'jh-needs-you-card',
  imports: [RouterLink, CardComponent, ButtonDirective],
  templateUrl: './needs-you-card.component.html',
  styleUrl: './needs-you-card.component.css',
})
export class NeedsYouCardComponent {
  private readonly teams = inject(TeamService);
  private readonly parties = inject(PartyService);
  private readonly market = inject(MarketService);

  readonly items = input.required<NeedsYouItem[]>();
  readonly resolved = output<string>();

  protected readonly busyId = signal<string | null>(null);
  protected readonly hasAny = computed(() => this.items().length > 0);

  protected rel(iso: string): string {
    return relativeTime(iso);
  }

  /** The navigation route for an item's "view" link, by kind. */
  protected link(item: NeedsYouItem): unknown[] | null {
    if (!item.linkTarget) return null;
    switch (item.kind) {
      case 'TeamInvite':
        return ['/t', item.linkTarget];
      case 'PartyRequest':
      case 'PartyCoAdminInvite':
      case 'MarketInvite':
      case 'MarketApplication':
        return ['/events', item.linkTarget];
      default:
        return null;
    }
  }

  protected accept(item: NeedsYouItem): void {
    switch (item.kind) {
      case 'TeamInvite':
        this.run(item, this.teams.acceptInvite(item.id));
        break;
      case 'PartyCoAdminInvite':
        this.run(item, this.parties.acceptInvite(item.id));
        break;
      case 'PartyRequest':
        this.run(item, this.parties.join(item.id));
        break;
      case 'MarketInvite':
        this.run(item, this.market.accept(item.id));
        break;
    }
  }

  protected decline(item: NeedsYouItem): void {
    switch (item.kind) {
      case 'TeamInvite':
        this.run(item, this.teams.declineInvite(item.id));
        break;
      case 'PartyCoAdminInvite':
        this.run(item, this.parties.declineInvite(item.id));
        break;
      case 'PartyRequest':
        this.run(item, this.parties.decline(item.id));
        break;
      case 'MarketInvite':
        this.run(item, this.market.declineRequest(item.id));
        break;
    }
  }

  private run(item: NeedsYouItem, call: Observable<unknown>): void {
    if (this.busyId()) return;
    this.busyId.set(item.id);
    call.subscribe({
      next: () => {
        this.busyId.set(null);
        this.resolved.emit(item.id);
      },
      error: () => this.busyId.set(null),
    });
  }
}
