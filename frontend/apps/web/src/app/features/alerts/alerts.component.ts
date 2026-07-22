import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ButtonDirective } from '../../shared/ui';
import { NotificationService } from '../../core/services/notification.service';
import { TeamService } from '../../core/services/team.service';
import { MembershipService } from '../../core/services/membership.service';
import { AppNotification, isTeamInvite } from '../../core/models/notification.models';
import { NotificationRowComponent } from './notification-row/notification-row.component';

/**
 * Alerts / notifications inbox (feature 010). Renders the signed-in user's notifications from the
 * shared {@link NotificationService} (which keeps them live over SignalR), with loading / empty /
 * error states, "mark all read", and pagination. Inline invite actions reuse the existing
 * invitation endpoints via {@link TeamService}, so the notification never reimplements invite
 * semantics — it just triggers them and reflects the resolved state.
 */
@Component({
  selector: 'jh-alerts',
  imports: [NotificationRowComponent, ButtonDirective],
  templateUrl: './alerts.component.html',
  styleUrl: './alerts.component.css',
})
export class AlertsComponent implements OnInit {
  private readonly notifications = inject(NotificationService);
  private readonly teams = inject(TeamService);
  private readonly membership = inject(MembershipService);

  protected readonly items = this.notifications.items;
  protected readonly hasMore = this.notifications.hasMore;
  protected readonly unread = this.notifications.unreadCount;

  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly loadingMore = signal(false);
  /** Ids with an in-flight inline action, so the row's buttons can't be double-fired. */
  protected readonly busy = signal<ReadonlySet<string>>(new Set());

  protected readonly isEmpty = computed(() => !this.loading() && !this.failed() && this.items().length === 0);
  protected readonly hasUnread = computed(() => this.unread() > 0);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.notifications.loadFirstPage().subscribe({
      next: () => this.loading.set(false),
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      },
    });
  }

  loadMore(): void {
    if (this.loadingMore()) {
      return;
    }
    this.loadingMore.set(true);
    this.notifications.loadMore().subscribe({
      next: () => this.loadingMore.set(false),
      error: () => this.loadingMore.set(false),
    });
  }

  markAllRead(): void {
    this.notifications.markAllRead().subscribe({ error: () => undefined });
  }

  open(n: AppNotification): void {
    if (!n.isRead) {
      this.notifications.markRead(n.id).subscribe({ error: () => undefined });
    }
  }

  accept(n: AppNotification): void {
    if (!isTeamInvite(n) || this.busy().has(n.id)) {
      return;
    }
    this.setBusy(n.id, true);
    this.teams.acceptInvite(n.payload.token).subscribe({
      next: () => {
        this.notifications.markInviteResolved(n.id);
        // Joining changed this player's teams — refresh the cache the nav's "My team"
        // target reads from, so it reflects the new membership without a page reload.
        this.membership.load();
        this.setBusy(n.id, false);
      },
      // Expired/revoked/out-of-band: reconcile the row to resolved rather than erroring at the user.
      error: () => {
        this.notifications.markInviteResolved(n.id);
        this.setBusy(n.id, false);
      },
    });
  }

  decline(n: AppNotification): void {
    if (!isTeamInvite(n) || this.busy().has(n.id)) {
      return;
    }
    this.setBusy(n.id, true);
    this.teams.declineInvite(n.payload.token).subscribe({
      next: () => {
        this.notifications.markInviteResolved(n.id);
        this.setBusy(n.id, false);
      },
      error: () => {
        this.notifications.markInviteResolved(n.id);
        this.setBusy(n.id, false);
      },
    });
  }

  private setBusy(id: string, on: boolean): void {
    this.busy.update((set) => {
      const next = new Set(set);
      if (on) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }
}
