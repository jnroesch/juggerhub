import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { ButtonDirective, LoadingComponent, AlertComponent } from '../../../shared/ui';
import { RouterLink } from '@angular/router';
import { MarketService } from '../../../core/services/market.service';
import {
  MarketListingCard,
  MarketRequest,
  MyMarket,
  RecruitingPartyCard,
} from '../../../core/models/market.models';
import { POMPFEN_CATALOG, Pompfe, pompfeLabel } from '../../../shared/pompfen.catalog';
import { problemDetail } from '../../../core/utils/problem';

/** An in-progress apply / invite / post action driving the modal. */
type BoardAction =
  | { kind: 'apply'; party: RecruitingPartyCard }
  | { kind: 'invite'; user: MarketListingCard }
  | { kind: 'post'; editing: boolean }
  | null;

/**
 * US1/US3/US5 — the mercenary market on the event page. A two-sided board (free agents ⇄ recruiting
 * parties) with a position filter, the caller's own "post yourself" affordance, and their market inbox
 * (invites to answer + applications sent). The board is public; every affordance is re-checked
 * server-side. Rendered only for teams events by the parent event page.
 */
@Component({
  selector: 'jh-market-board',
  imports: [RouterLink, ButtonDirective, LoadingComponent, AlertComponent],
  templateUrl: './market-board.component.html',
  styleUrl: './market-board.component.css',
})
export class MarketBoardComponent {
  private readonly market = inject(MarketService);

  readonly eventId = input.required<string>();
  /** Whether the signed-in viewer can act (drives the post/apply/invite affordances). */
  readonly authenticated = input(false);

  protected readonly catalog = POMPFEN_CATALOG;
  protected readonly label = pompfeLabel;

  protected readonly position = signal<Pompfe | null>(null);

  protected readonly freeAgents = signal<MarketListingCard[]>([]);
  protected readonly freeAgentTotal = signal(0);
  protected readonly parties = signal<RecruitingPartyCard[]>([]);
  protected readonly partyTotal = signal(0);
  protected readonly me = signal<MyMarket | null>(null);

  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);

  protected readonly action = signal<BoardAction>(null);
  protected readonly selected = signal<Pompfe[]>([]);
  protected readonly pitch = signal('');
  protected readonly acting = signal(false);
  protected readonly actionError = signal<string | null>(null);

  /** A party admin here with an open seat can invite free agents. */
  protected readonly inviteParty = computed(() =>
    this.me()?.adminParties.find((p) => p.isRecruiting && p.openSpots > 0)
    ?? this.me()?.adminParties.find((p) => p.openSpots > 0)
    ?? null,
  );
  protected readonly canInvite = computed(() => this.authenticated() && this.inviteParty() !== null);
  protected readonly eligible = computed(() => this.me()?.eligible ?? false);

  private readonly pendingApplicationIds = computed(
    () => new Set((this.me()?.myApplications ?? []).filter((a) => a.status === 'Pending').map((a) => a.partyId)),
  );
  private readonly invitesByParty = computed(() => {
    const map = new Map<string, MarketRequest>();
    for (const inv of this.me()?.invitesToAnswer ?? []) map.set(inv.partyId, inv);
    return map;
  });

  constructor() {
    // Load whenever the event id resolves; default the visible side to the admin's shopping view.
    effect(() => {
      const id = this.eventId();
      if (id) {
        this.load(id);
      }
    });
  }

  private load(eventId: string): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.market.freeAgents(eventId, this.position()).subscribe({
      next: (fa) => {
        this.freeAgents.set(fa.items);
        this.freeAgentTotal.set(fa.totalCount);
      },
      error: () => this.loadError.set(true),
    });
    this.market.recruitingParties(eventId, this.position()).subscribe({
      next: (p) => {
        this.parties.set(p.items);
        this.partyTotal.set(p.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
    if (this.authenticated()) {
      this.market.myMarket(eventId).subscribe({ next: (m) => this.me.set(m) });
    }
  }

  protected reloadBoardOnly(): void {
    const id = this.eventId();
    this.market.freeAgents(id, this.position()).subscribe({ next: (fa) => { this.freeAgents.set(fa.items); this.freeAgentTotal.set(fa.totalCount); } });
    this.market.recruitingParties(id, this.position()).subscribe({ next: (p) => { this.parties.set(p.items); this.partyTotal.set(p.totalCount); } });
    if (this.authenticated()) this.market.myMarket(id).subscribe({ next: (m) => this.me.set(m) });
  }

  protected setPosition(p: Pompfe | null): void {
    this.position.set(this.position() === p ? null : p);
    this.reloadBoardOnly();
  }

  // --- Affordance state per card --------------------------------------------

  protected pendingApplication(party: RecruitingPartyCard): boolean {
    return this.pendingApplicationIds().has(party.partyId);
  }

  /** Whether a free-agent card is the signed-in viewer's own listing (shows Edit/Take-down). */
  protected isMe(fa: MarketListingCard): boolean {
    return this.authenticated() && fa.userId === this.me()?.userId;
  }

  protected inviteFor(party: RecruitingPartyCard): MarketRequest | null {
    return this.invitesByParty().get(party.partyId) ?? null;
  }

  // --- Modal actions --------------------------------------------------------

  protected openApply(party: RecruitingPartyCard): void {
    this.selected.set([]);
    this.actionError.set(null);
    this.action.set({ kind: 'apply', party });
  }

  protected openInvite(user: MarketListingCard): void {
    this.selected.set(user.positions.slice(0, 1));
    this.actionError.set(null);
    this.action.set({ kind: 'invite', user });
  }

  protected openPost(editing: boolean): void {
    const listing = this.me()?.myListing;
    this.selected.set(editing && listing ? [...listing.positions] : []);
    this.pitch.set(editing && listing ? listing.pitch : '');
    this.actionError.set(null);
    this.action.set({ kind: 'post', editing });
  }

  protected closeAction(): void {
    this.action.set(null);
  }

  protected toggleSelected(p: Pompfe): void {
    this.selected.update((cur) => (cur.includes(p) ? cur.filter((x) => x !== p) : [...cur, p]));
  }

  protected submit(): void {
    const a = this.action();
    if (!a || this.acting()) return;
    this.acting.set(true);
    this.actionError.set(null);
    const done = () => { this.acting.set(false); this.action.set(null); this.reloadBoardOnly(); };
    const fail = (err: unknown) => { this.acting.set(false); this.actionError.set(problemDetail(err)); };

    if (a.kind === 'apply') {
      this.market.apply(a.party.partyId, { positions: this.selected() }).subscribe({ next: done, error: fail });
    } else if (a.kind === 'invite') {
      const party = this.inviteParty();
      if (!party) { this.acting.set(false); return; }
      this.market.invite(party.partyId, { userId: a.user.userId, positions: this.selected() }).subscribe({ next: done, error: fail });
    } else if (a.kind === 'post') {
      const req = { positions: this.selected(), pitch: this.pitch().trim() };
      const call = a.editing ? this.market.editListing(this.eventId(), req) : this.market.postListing(this.eventId(), req);
      call.subscribe({ next: done, error: fail });
    }
  }

  protected takeDown(): void {
    if (this.acting()) return;
    this.acting.set(true);
    this.market.takeDownListing(this.eventId()).subscribe({
      next: () => { this.acting.set(false); this.reloadBoardOnly(); },
      error: (err) => { this.acting.set(false); this.actionError.set(problemDetail(err)); },
    });
  }

  protected accept(requestId: string): void {
    this.market.accept(requestId).subscribe({ next: () => this.reloadBoardOnly() });
  }

  protected decline(requestId: string): void {
    this.market.declineRequest(requestId).subscribe({ next: () => this.reloadBoardOnly() });
  }

  protected postValid = computed(() => this.selected().length > 0 && this.pitch().trim().length > 0);
}
