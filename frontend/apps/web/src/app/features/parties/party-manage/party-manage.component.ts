import { Component, ElementRef, Injector, OnInit, afterNextRender, computed, inject, signal } from '@angular/core';
import { AlertComponent, ButtonDirective, CardComponent, ChipDirective, EmptyStateComponent, IconComponent, LoadingComponent } from '../../../shared/ui';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { PluralKeyPipe } from '../../../core/i18n/plural-key.pipe';
import { Party, PartyMember, PartyNews, PartyRosterGroup } from '../../../core/models/party.models';
import { PartyService } from '../../../core/services/party.service';
import { Pompfe, pompfeLabelKey } from '../../../shared/pompfen.catalog';
import { NewsPostComponent } from '../../../shared/news-post/news-post.component';
import { NewsPostEditing } from '../../../shared/news-post/news-post-editing';

/**
 * The party manage hub (feature 016 · wireframes 6d–6h). One page for the whole party: roster in
 * three groups (In / Declined / No reply), readiness, the primary "Apply to event" action, party
 * tools (news, co-admins, disband + LATER placeholders), and — for a non-admin crew member — the
 * join/leave affordances. Admin controls are gated on the viewer's party role (server-enforced too).
 */
@Component({
  selector: 'jh-party-manage',
  imports: [RouterLink, TranslocoDatePipe, FormsModule, ButtonDirective, ChipDirective, LoadingComponent, AlertComponent, EmptyStateComponent, CardComponent, TranslocoPipe, PluralKeyPipe, IconComponent, NewsPostComponent],
  // Feature 059 — an open editor and its typed text belong to the page, not to one post's controls.
  providers: [NewsPostEditing],
  templateUrl: './party-manage.component.html',
  styleUrl: './party-manage.component.css',
})
export class PartyManageComponent implements OnInit {
  private readonly parties = inject(PartyService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

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
  protected readonly newsBody = signal('');
  /** Feature 059 — what happened to a post an admin tried to act on (a translation key). */
  protected readonly newsNotice = signal<string | null>(null);

  protected readonly saveNews = (postId: string, body: string): Observable<PartyNews> =>
    this.parties.editNews(this.id, postId, body);

  protected readonly deleteNews = (postId: string): Observable<void> => this.parties.deleteNews(this.id, postId);

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
    const text = this.newsBody().trim();
    if (this.posting() || text.length === 0) {
      return;
    }
    this.posting.set(true);
    this.parties.postNews(this.id, { body: text }).subscribe({
      next: () => {
        this.newsBody.set('');
        this.posting.set(false);
        this.loadNews();
      },
      error: () => this.posting.set(false),
    });
  }

  protected replaceNews(updated: PartyNews): void {
    this.newsNotice.set(null);
    this.news.update((list) => list.map((n) => (n.id === updated.id ? updated : n)));
  }

  /** The post is gone — deleted here, or by another admin meanwhile (FR-020), which the notice says. */
  protected dropNews(id: string, gone: boolean): void {
    this.news.update((list) => list.filter((n) => n.id !== id));
    this.newsNotice.set(gone ? 'news.gone' : null);
    // The button that opened the menu went with the post; land on the section's heading instead.
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>('#party-news-heading')?.focus(), {
      injector: this.injector,
    });
  }

  protected positions(pompfen: Pompfe[]): string {
    return pompfen.map((p) => this.transloco.translate(pompfeLabelKey(p))).join(' · ');
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
    if (!confirm(this.transloco.translate('parties.manage.confirmDisband'))) {
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
    this.error.set(err instanceof HttpErrorResponse ? err.error?.detail ?? this.transloco.translate('parties.manage.error') : this.transloco.translate('parties.manage.error'));
  }
}
