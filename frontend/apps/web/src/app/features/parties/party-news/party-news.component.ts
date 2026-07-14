import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Party, PartyNews } from '../../../core/models/party.models';
import { PartyService } from '../../../core/services/party.service';

/**
 * Party news (feature 016 · wireframe 6e). Private to the crew; only party admins compose. Behaves
 * like team news — a newest-first feed with a composer — scoped to this party and gone on disband.
 */
@Component({
  selector: 'jh-party-news',
  imports: [RouterLink, DatePipe, FormsModule],
  templateUrl: './party-news.component.html',
  styleUrl: './party-news.component.css',
})
export class PartyNewsComponent implements OnInit {
  private readonly parties = inject(PartyService);
  private readonly route = inject(ActivatedRoute);

  protected readonly party = signal<Party | null>(null);
  protected readonly posts = signal<PartyNews[]>([]);
  protected readonly loading = signal(true);
  protected readonly posting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected body = '';

  protected readonly isAdmin = computed(() => this.party()?.myRole === 'Admin');
  protected id = '';

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.parties.getParty(this.id).subscribe({ next: (p) => this.party.set(p), error: () => undefined });
    this.load();
  }

  private load(): void {
    this.parties.listNews(this.id).subscribe({
      next: (page) => {
        this.posts.set(page.items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected post(): void {
    const text = this.body.trim();
    if (this.posting() || text.length === 0) {
      return;
    }
    this.posting.set(true);
    this.error.set(null);
    this.parties.postNews(this.id, { body: text }).subscribe({
      next: () => {
        this.body = '';
        this.posting.set(false);
        this.load();
      },
      error: (err) => {
        this.posting.set(false);
        this.error.set(err instanceof HttpErrorResponse ? err.error?.detail ?? 'Could not post.' : 'Could not post.');
      },
    });
  }
}
