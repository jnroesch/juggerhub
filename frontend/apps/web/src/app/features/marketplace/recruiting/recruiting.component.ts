import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MarketService } from '../../../core/services/market.service';
import {
  MarketInvitableUser,
  MarketRequest,
  RecruitingSettings,
} from '../../../core/models/market.models';
import { POMPFEN_CATALOG, Pompfe, pompfeLabel } from '../../../shared/pompfen.catalog';
import { problemDetail } from '../../../core/utils/problem';
import { relativeTime } from '../../../core/utils/format';

/**
 * US2/US3/US5/US6 — the party admin's recruiting screen (`/parties/:id/recruiting`). Flip the party
 * onto the board, set spots/positions/blurb, review applications (accept/decline) and sent invites
 * (revoke), and invite any eligible player directly by name/@handle. Party-admin gated server-side; a
 * non-admin gets a forbidden state.
 */
@Component({
  selector: 'jh-recruiting',
  imports: [RouterLink],
  templateUrl: './recruiting.component.html',
  styleUrl: './recruiting.component.css',
})
export class RecruitingComponent implements OnInit {
  private readonly market = inject(MarketService);
  private readonly route = inject(ActivatedRoute);

  protected readonly catalog = POMPFEN_CATALOG;
  protected readonly label = pompfeLabel;
  protected readonly rel = relativeTime;

  protected id = '';

  protected readonly settings = signal<RecruitingSettings | null>(null);
  protected readonly applications = signal<MarketRequest[]>([]);
  protected readonly sentInvites = signal<MarketRequest[]>([]);
  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly loadError = signal(false);

  // Editable recruiting form.
  protected readonly on = signal(false);
  protected readonly spots = signal(0);
  protected readonly positions = signal<Pompfe[]>([]);
  protected readonly blurb = signal('');
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  // Direct-invite search.
  protected readonly query = signal('');
  protected readonly results = signal<MarketInvitableUser[]>([]);
  protected readonly searching = signal(false);
  protected readonly acting = signal(false);

  protected readonly fill = computed(() => {
    const s = this.settings();
    return s ? `${s.inCount} / ${s.rosterCap}` : '';
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.forbidden.set(false);
    this.loadError.set(false);
    this.market.getRecruiting(this.id).subscribe({
      next: (s) => {
        this.applyServer(s);
        this.loading.set(false);
        this.refreshInboxes();
      },
      error: (err) => {
        this.loading.set(false);
        if (err?.status === 403) this.forbidden.set(true);
        else this.loadError.set(true);
      },
    });
  }

  private applyServer(s: RecruitingSettings): void {
    this.settings.set(s);
    this.on.set(s.isRecruiting);
    this.spots.set(s.spotsAdvertised);
    this.positions.set([...s.positionsNeeded]);
    this.blurb.set(s.blurb ?? '');
  }

  private refreshInboxes(): void {
    this.market.listApplications(this.id).subscribe({ next: (r) => this.applications.set(r.items) });
    this.market.listSentInvites(this.id).subscribe({ next: (r) => this.sentInvites.set(r.items) });
  }

  // --- Recruiting form ------------------------------------------------------

  protected togglePosition(p: Pompfe): void {
    this.positions.update((cur) => (cur.includes(p) ? cur.filter((x) => x !== p) : [...cur, p]));
  }

  protected changeSpots(delta: number): void {
    const cap = this.settings()?.rosterCap ?? 0;
    this.spots.update((s) => Math.max(0, Math.min(cap, s + delta)));
  }

  protected save(): void {
    if (this.saving()) return;
    this.saving.set(true);
    this.saveError.set(null);
    this.market
      .setRecruiting(this.id, {
        isRecruiting: this.on(),
        spotsAdvertised: this.spots(),
        positionsNeeded: this.positions(),
        blurb: this.blurb().trim() || null,
      })
      .subscribe({
        next: (s) => {
          this.applyServer(s);
          this.saving.set(false);
        },
        error: (err) => {
          this.saving.set(false);
          this.saveError.set(problemDetail(err));
        },
      });
  }

  // --- Inbox actions --------------------------------------------------------

  protected accept(id: string): void {
    if (this.acting()) return;
    this.acting.set(true);
    this.market.accept(id).subscribe({
      next: () => { this.acting.set(false); this.load(); },
      error: () => this.acting.set(false),
    });
  }

  protected decline(id: string): void {
    this.market.declineRequest(id).subscribe({ next: () => this.refreshInboxes() });
  }

  protected revoke(id: string): void {
    this.market.revoke(id).subscribe({ next: () => this.refreshInboxes() });
  }

  // --- Direct invite --------------------------------------------------------

  protected onQuery(value: string): void {
    this.query.set(value);
    const term = value.trim();
    if (term.length === 0) {
      this.results.set([]);
      return;
    }
    this.searching.set(true);
    this.market.searchUsers(this.id, term).subscribe({
      next: (r) => { this.results.set(r.items); this.searching.set(false); },
      error: () => this.searching.set(false),
    });
  }

  protected invite(user: MarketInvitableUser): void {
    if (this.acting() || user.relation !== 'Invitable') return;
    this.acting.set(true);
    this.market.invite(this.id, { userId: user.userId, positions: [] }).subscribe({
      next: () => { this.acting.set(false); this.onQuery(this.query()); this.refreshInboxes(); },
      error: () => this.acting.set(false),
    });
  }
}
