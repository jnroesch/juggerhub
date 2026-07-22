import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ButtonDirective, LoadingComponent } from '../../../shared/ui';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { InvitePreview } from '../../../core/models/event.models';
import { EventService } from '../../../core/services/event.service';

/**
 * US7 — the anonymous co-admin accept page (full-screen, outside the shell). Shows a
 * public preview (event name + inviter + usability); accepting or declining requires
 * auth, so an unauthenticated visitor is sent to sign in and returns here to act.
 */
@Component({
  selector: 'jh-event-invite-accept',
  imports: [DatePipe, ButtonDirective, LoadingComponent],
  templateUrl: './event-invite-accept.component.html',
  styleUrl: './event-invite-accept.component.css',
})
export class EventInviteAcceptComponent implements OnInit {
  private readonly events = inject(EventService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly preview = signal<InvitePreview | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly acting = signal(false);

  private token = '';

  ngOnInit(): void {
    this.token = this.route.snapshot.paramMap.get('token') ?? '';
    this.events.getInvitePreview(this.token).subscribe({
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
    this.events.acceptInvite(this.token).subscribe({
      next: (r) => this.router.navigate(['/events', r.eventId]),
      error: (err) => this.handleAuthOr(err),
    });
  }

  protected decline(): void {
    if (this.acting()) {
      return;
    }
    this.acting.set(true);
    this.events.declineInvite(this.token).subscribe({
      next: () => this.router.navigate(['/']),
      error: (err) => this.handleAuthOr(err),
    });
  }

  /** On 401, send to sign-in and return to this invite; otherwise stop acting. */
  private handleAuthOr(err: unknown): void {
    this.acting.set(false);
    if (err instanceof HttpErrorResponse && err.status === 401) {
      this.router.navigate(['/sign-in'], { queryParams: { returnUrl: `/event-invite/${this.token}` } });
    }
  }
}
