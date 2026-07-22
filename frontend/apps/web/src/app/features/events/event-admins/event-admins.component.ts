import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { ButtonDirective, LoadingComponent, AlertComponent } from '../../../shared/ui';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EMPTY, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import {
  EventAdmin,
  EventDetail,
  EventInvitation,
  InvitableUser,
  InviteLink,
} from '../../../core/models/event.models';
import { EventService } from '../../../core/services/event.service';
import { problemDetail } from '../../../core/utils/problem';

/**
 * US7 — co-admin management: the admin roster (any admin can remove any, incl. stepping
 * down, under the last-admin guard), the shareable invite link (rotate/copy), targeted
 * invites by user search, and the pending-invitation list (revoke). Admin-only, server-enforced.
 */
@Component({
  selector: 'jh-event-admins',
  imports: [ReactiveFormsModule, RouterLink, ButtonDirective, LoadingComponent, AlertComponent],
  templateUrl: './event-admins.component.html',
  styleUrl: './event-admins.component.css',
})
export class EventAdminsComponent implements OnInit {
  private readonly events = inject(EventService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly detail = signal<EventDetail | null>(null);
  protected readonly admins = signal<EventAdmin[]>([]);
  protected readonly invitations = signal<EventInvitation[]>([]);
  protected readonly link = signal<InviteLink | null>(null);
  protected readonly results = signal<InvitableUser[]>([]);

  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly copied = signal(false);

  protected readonly search = new FormControl('', { nonNullable: true });

  private id = '';

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.events.getEvent(this.id).subscribe({
      next: (d) => {
        if (!d.viewer.isAdmin) {
          this.forbidden.set(true);
          this.loading.set(false);
          return;
        }
        this.detail.set(d);
        this.refresh();
        this.events.getInviteLink(this.id).subscribe((l) => this.link.set(l));
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.forbidden.set(true);
      },
    });

    this.search.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((q) => (q.trim() ? this.events.searchUsers(this.id, q.trim()) : EMPTY)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (r) => this.results.set(r.items),
        error: () => this.results.set([]),
      });
  }

  private refresh(): void {
    this.events.getAdmins(this.id).subscribe((r) => this.admins.set(r.items));
    this.events.getInvitations(this.id).subscribe((r) => this.invitations.set(r.items));
  }

  protected rotateLink(): void {
    this.events.rotateInviteLink(this.id).subscribe({
      next: (l) => this.link.set(l),
      error: (err) => this.error.set(problemDetail(err)),
    });
  }

  protected copyLink(): void {
    const url = this.link()?.url;
    if (url && navigator.clipboard) {
      navigator.clipboard.writeText(url).then(() => {
        this.copied.set(true);
        setTimeout(() => this.copied.set(false), 1500);
      });
    }
  }

  protected invite(user: InvitableUser): void {
    if (user.relation !== 'Invitable') {
      return;
    }
    this.events.createTargetedInvite(this.id, user.userId).subscribe({
      next: () => {
        this.refresh();
        // Reflect the new state locally so the button flips to "Invited".
        this.results.update((list) => list.map((u) => (u.userId === user.userId ? { ...u, relation: 'Invited' } : u)));
      },
      error: (err) => this.error.set(problemDetail(err)),
    });
  }

  protected revoke(invitationId: string): void {
    this.events.revokeInvite(this.id, invitationId).subscribe({
      next: () => this.invitations.update((list) => list.filter((i) => i.id !== invitationId)),
      error: (err) => this.error.set(problemDetail(err)),
    });
  }

  protected removeAdmin(userId: string): void {
    this.events.removeAdmin(this.id, userId).subscribe({
      next: () => this.refresh(),
      error: (err) => this.error.set(problemDetail(err)),
    });
  }
}
