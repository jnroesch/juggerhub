import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ButtonDirective } from '../../../shared/ui';
import { RouterLink } from '@angular/router';
import { MarketService } from '../../../core/services/market.service';
import { MyListing, MyMarketRequest } from '../../../core/models/market.models';
import { pompfeLabel } from '../../../shared/pompfen.catalog';

/**
 * US5 — the dashboard market module. Surfaces the signed-in player's active marketplace items across
 * events: their live free-agent listings (their "placements", with a take-down), plus invites to
 * answer (accept/decline inline) and applications they've sent (status). Hidden when there's nothing
 * active. Owns its own load; a failure just hides the module.
 */
@Component({
  selector: 'jh-market-card',
  imports: [RouterLink, ButtonDirective],
  templateUrl: './market-card.component.html',
  styleUrl: './market-card.component.css',
})
export class MarketCardComponent implements OnInit {
  private readonly market = inject(MarketService);

  protected readonly label = pompfeLabel;
  protected readonly listings = signal<MyListing[]>([]);
  protected readonly items = signal<MyMarketRequest[]>([]);
  protected readonly acting = signal(false);

  protected readonly hasAny = computed(() => this.listings().length > 0 || this.items().length > 0);

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.market.myListings(0, 20).subscribe({ next: (r) => this.listings.set(r.items) });
    this.market.mine(0, 20).subscribe({ next: (r) => this.items.set(r.items) });
  }

  protected takeDown(eventId: string): void {
    if (this.acting()) return;
    this.acting.set(true);
    this.market.takeDownListing(eventId).subscribe({
      next: () => { this.acting.set(false); this.load(); },
      error: () => this.acting.set(false),
    });
  }

  protected accept(id: string): void {
    if (this.acting()) return;
    this.acting.set(true);
    this.market.accept(id).subscribe({
      next: () => { this.acting.set(false); this.load(); },
      error: () => this.acting.set(false),
    });
  }

  protected decline(id: string): void {
    this.market.declineRequest(id).subscribe({ next: () => this.load() });
  }
}
