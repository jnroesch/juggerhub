import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Party, PartyMember, PartyNews, PartyRosterGroup } from '../../../core/models/party.models';
import { PartyService } from '../../../core/services/party.service';
import { Pompfe, pompfeLabel } from '../../../shared/pompfen.catalog';

/**
 * The party manage hub (feature 016 · wireframes 6d–6h). One page for the whole party: roster in
 * three groups (In / Declined / No reply), readiness, the primary "Apply to event" action, party
 * tools (news, co-admins, disband + LATER placeholders), and — for a non-admin crew member — the
 * join/leave affordances. Admin controls are gated on the viewer's party role (server-enforced too).
 */
@Component({
  selector: 'jh-party-manage',
  imports: [RouterLink, DatePipe, FormsModule],
  templateUrl: './party-manage.component.html',
  styleUrl: './party-manage.component.css',
})
export class PartyManageComponent implements OnInit {
  private readonly parties = inject(PartyService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly party = signal<Party | null>(null);
  protected readonly members = signal<PartyMember[]>([]);
  protected readonly activeTab = signal<PartyRosterGroup>('In');
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly acting = signal(false);
  protected readonly error = signal<string | null>(null);

  // Party news, shown inline on the manage view (crew-only).
  protected readonly news = signal<PartyNews[]>([]);
  protected readonly posting = signal(false);
  protected newsBody = '';

  protected readonly isAdmin = computed(() => this.party()?.myRole === 'Admin');
  protected readonly isApplied = computed(() => this.party()?.status === 'Applied');
  /** Crew members (In or Admin) can see the private news feed. */
  protected readonly isCrew = computed(() => {
    const s = this.party()?.myState;
    return s === 'In' || s === 'Admin';
  });

  protected id = '';

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.reload();
  }

  private reload(): void {
    this.parties.getParty(this.id).subscribe({
      next: (p) => {
        this.party.set(p);
        this.loading.set(false);
        this.loadTab(this.activeTab());
        if (this.isCrew()) {
          this.loadNews();
        }
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      },
    });
  }

  private loadNews(): void {
    this.parties.listNews(this.id).subscribe({ next: (page) => this.news.set(page.items), error: () => this.news.set([]) });
  }

  protected postNews(): void {
    const text = this.newsBody.trim();
    if (this.posting() || text.length === 0) {
      return;
    }
    this.posting.set(true);
    this.parties.postNews(this.id, { body: text }).subscribe({
      next: () => {
        this.newsBody = '';
        this.posting.set(false);
        this.loadNews();
      },
      error: () => this.posting.set(false),
    });
  }

  protected positions(pompfen: Pompfe[]): string {
    return pompfen.map((p) => pompfeLabel(p)?.de ?? p).join(' · ');
  }

  protected loadTab(group: PartyRosterGroup): void {
    this.activeTab.set(group);
    this.parties.listMembers(this.id, group).subscribe({
      next: (page) => this.members.set(page.items),
      error: () => this.members.set([]),
    });
  }

  // --- Self actions (crew member) ------------------------------------------

  protected join(): void {
    this.run(() => this.parties.join(this.id));
  }

  protected declineRequest(): void {
    this.run(() => this.parties.decline(this.id));
  }

  protected leave(): void {
    this.run(() => this.parties.leave(this.id));
  }

  // --- Admin actions --------------------------------------------------------

  protected nudge(userId: string): void {
    this.parties.nudge(this.id, userId).subscribe({ error: () => undefined });
  }

  protected remove(userId: string): void {
    this.run(() => this.parties.removeMember(this.id, userId));
  }

  protected apply(): void {
    this.run(() => this.parties.apply(this.id));
  }

  protected withdraw(): void {
    this.run(() => this.parties.withdraw(this.id));
  }

  protected disband(): void {
    if (!confirm('Disband this party? This cannot be undone.')) {
      return;
    }
    this.acting.set(true);
    this.parties.disband(this.id).subscribe({
      next: () => {
        const slug = this.party()?.teamSlug;
        this.router.navigate(slug ? ['/t', slug] : ['/']);
      },
      error: (err) => this.fail(err),
    });
  }

  private run(op: () => Observable<unknown>): void {
    if (this.acting()) {
      return;
    }
    this.acting.set(true);
    this.error.set(null);
    op().subscribe({
      next: () => {
        this.acting.set(false);
        this.reload();
      },
      error: (err) => this.fail(err),
    });
  }

  private fail(err: unknown): void {
    this.acting.set(false);
    this.error.set(err instanceof HttpErrorResponse ? err.error?.detail ?? 'Something went wrong.' : 'Something went wrong.');
  }
}
