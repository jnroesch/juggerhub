import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ButtonDirective, LoadingComponent, AlertComponent, EmptyStateComponent, CardComponent } from '../../../shared/ui';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { PartyInvitePreview } from '../../../core/models/party.models';
import { PartyService } from '../../../core/services/party.service';

/**
 * Party co-admin accept page (feature 016, full-screen, outside the shell). Shows a public preview
 * (team @ event + inviter + usability); accepting or declining requires auth + team membership.
 */
@Component({
  selector: 'jh-party-invite-accept',
  imports: [DatePipe, ButtonDirective, LoadingComponent, AlertComponent, EmptyStateComponent, CardComponent],
  templateUrl: './party-invite-accept.component.html',
  styleUrl: './party-invite-accept.component.css',
})
export class PartyInviteAcceptComponent implements OnInit {
  private readonly parties = inject(PartyService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly preview = signal<PartyInvitePreview | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly acting = signal(false);
  protected readonly error = signal<string | null>(null);

  private token = '';

  ngOnInit(): void {
    this.token = this.route.snapshot.paramMap.get('token') ?? '';
    this.parties.previewInvite(this.token).subscribe({
      next: (p) => {
        this.preview.set(p);
        this.loading.set(false);
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      },
    });
  }

  protected accept(): void {
    if (this.acting()) {
      return;
    }
    this.acting.set(true);
    this.error.set(null);
    this.parties.acceptInvite(this.token).subscribe({
      next: (r) => this.router.navigate(['/parties', r.partyId]),
      error: (err) => this.handleAuthOr(err),
    });
  }

  protected decline(): void {
    if (this.acting()) {
      return;
    }
    this.acting.set(true);
    this.parties.declineInvite(this.token).subscribe({
      next: () => this.router.navigate(['/']),
      error: (err) => this.handleAuthOr(err),
    });
  }

  private handleAuthOr(err: unknown): void {
    this.acting.set(false);
    if (err instanceof HttpErrorResponse && err.status === 401) {
      this.router.navigate(['/sign-in'], { queryParams: { returnUrl: `/party-invite/${this.token}` } });
      return;
    }
    if (err instanceof HttpErrorResponse) {
      this.error.set(err.error?.detail ?? 'That invitation could not be used.');
    }
  }
}
