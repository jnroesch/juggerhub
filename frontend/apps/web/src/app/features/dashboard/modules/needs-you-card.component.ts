import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { RouterLink } from '@angular/router';
import { CardComponent, ButtonDirective, ChipDirective } from '../../../shared/ui';
import { NeedsYouItem } from '../../../core/models/home.models';
import { TeamService } from '../../../core/services/team.service';
import { PartyService } from '../../../core/services/party.service';
import { MarketService } from '../../../core/services/market.service';
import { injectRelativeTime } from '../../../core/i18n/locale-format';

/** A catalogue key and the names it is filled with. */
interface Phrase {
  key: string;
  params: Record<string, string>;
}

/**
 * "Needs you" (feature 025, US1) — the pinned-top actionable block. Invites and requests only: team
 * invites, party participation requests, party co-admin invites, marketplace invites/applications
 * and — feature 058 — join requests waiting on the viewer as an admin, each resolved in place via its
 * existing per-domain endpoint. Training RSVP is deliberately NOT here (it lives inline in "Up
 * next"). Renders nothing when empty (FR-005). Emits `resolved` so the host can refresh the
 * composite once an item is handled.
 *
 * Every word is the client's (feature 058): the server sends names, never sentences, so the card
 * reads in the viewer's language.
 */
@Component({
  selector: 'jh-needs-you-card',
  imports: [RouterLink, CardComponent, ButtonDirective, ChipDirective, TranslocoPipe],
  templateUrl: './needs-you-card.component.html',
  styleUrl: './needs-you-card.component.css',
})
export class NeedsYouCardComponent {
  private readonly teams = inject(TeamService);
  private readonly parties = inject(PartyService);
  private readonly market = inject(MarketService);
  private readonly transloco = inject(TranslocoService);

  readonly items = input.required<NeedsYouItem[]>();
  readonly resolved = output<string>();
  /**
   * A join request turned out to be answered already, or withdrawn, when this admin pressed
   * (feature 058, FR-019). The host explains it and refreshes: the explanation must outlive this
   * card, which disappears with its last item, and the page's own count of waiting things has to
   * drop with it.
   */
  readonly gone = output<string>();

  protected readonly busyId = signal<string | null>(null);
  /** Items that left before the host's refresh lands — hidden at once, so nothing can be pressed twice. */
  protected readonly stale = signal<ReadonlySet<string>>(new Set());

  protected readonly visible = computed(() => this.items().filter((item) => !this.stale().has(item.id)));
  protected readonly hasAny = computed(() => this.visible().length > 0);

  protected readonly rel = injectRelativeTime();

  /** The item's headline: a key, filled with the names the server sent. */
  protected title(item: NeedsYouItem): Phrase {
    const p = item.params;
    switch (item.kind) {
      case 'TeamInvite':
        return { key: 'home.needsYouItem.teamInviteTitle', params: { team: p.teamName ?? '' } };
      case 'PartyCoAdminInvite':
        return { key: 'home.needsYouItem.partyCoAdminInviteTitle', params: { event: p.eventName ?? '' } };
      case 'PartyRequest':
        return { key: 'home.needsYouItem.partyRequestTitle', params: { team: p.teamName ?? '' } };
      case 'MarketInvite':
        return { key: 'home.needsYouItem.marketInviteTitle', params: { team: p.teamName ?? '' } };
      case 'MarketApplication':
        return { key: 'home.needsYouItem.marketApplicationTitle', params: { team: p.teamName ?? '' } };
      case 'TeamPoll':
        // Feature 062: the question itself is the headline — it is what the member is asked to answer.
        return { key: 'home.needsYouItem.teamPollTitle', params: { team: p.teamName ?? '', question: p.question ?? '' } };
      case 'JoinRequest':
        return {
          key: 'home.needsYouItem.joinRequestTitle',
          params: { player: p.playerName ?? this.transloco.translate('alerts.row.formerPlayer') },
        };
    }
  }

  /** The words before the time on the second line, for the one kind that has words there. */
  protected contextKey(item: NeedsYouItem): string | null {
    return item.kind === 'TeamInvite' ? 'home.needsYouItem.teamInviteContext' : null;
  }

  /** The name before the time on the second line: the event, or the team the item is about. */
  protected contextName(item: NeedsYouItem): string | null {
    switch (item.kind) {
      case 'PartyCoAdminInvite':
      case 'JoinRequest':
        return item.params.teamName;
      case 'PartyRequest':
      case 'MarketInvite':
      case 'MarketApplication':
        return item.params.eventName;
      default:
        return null;
    }
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
      case 'JoinRequest':
        // Who is asking: the admin looks at the player before answering (FR-019).
        return ['/u', item.linkTarget];
      case 'TeamPoll':
        // Feature 062: the team page, at the poll (fragment()).
        return ['/t', item.linkTarget];
      default:
        return null;
    }
  }

  /** Where on the page a link lands: a poll's own anchor (feature 062). Undefined for every other kind. */
  protected fragment(item: NeedsYouItem): string | undefined {
    return item.kind === 'TeamPoll' ? `poll-${item.id}` : undefined;
  }

  protected acceptLabel(item: NeedsYouItem): string {
    return item.kind === 'PartyRequest' ? 'home.imIn' : item.kind === 'JoinRequest' ? 'teams.detail.approve' : 'common.accept';
  }

  protected declineLabel(item: NeedsYouItem): string {
    return item.kind === 'PartyRequest' ? 'home.cant' : 'common.decline';
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
      case 'JoinRequest':
        this.run(item, this.teams.approveJoinRequest(item.params.teamSlug ?? '', item.id));
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
      case 'JoinRequest':
        this.run(item, this.teams.declineJoinRequest(item.params.teamSlug ?? '', item.id));
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
      error: (err: unknown) => {
        this.busyId.set(null);
        if (item.kind === 'JoinRequest' && err instanceof HttpErrorResponse && err.status === 404) {
          // Another admin answered first, or the player withdrew or joined another way (feature 058,
          // FR-019): the item goes now, and the host says why.
          this.stale.update((ids) => new Set([...ids, item.id]));
          this.gone.emit(item.id);
        }
      },
    });
  }
}
