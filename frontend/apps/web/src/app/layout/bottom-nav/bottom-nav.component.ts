import { Component, computed, inject } from '@angular/core';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';
import { MembershipService } from '../../core/services/membership.service';
import { NotificationService } from '../../core/services/notification.service';
import { ChatService } from '../../core/services/chat.service';
import { TranslocoPipe } from '@jsverse/transloco';
import { PluralKeyPipe } from '../../core/i18n/plural-key.pipe';
import { NavId, badgeText, isActiveDestination } from '../nav-model';
import { BumpDirective, IconComponent } from '../../shared/ui';

/**
 * The mobile bottom tab bar (feature 008): Home · Browse · My team · Chat · Alerts. Five
 * thumb-reachable fixed tabs (≤5), hidden at desktop width where the top bar carries the destinations.
 */
@Component({
  selector: 'jh-bottom-nav',
  imports: [RouterLink, TranslocoPipe, PluralKeyPipe, IconComponent, BumpDirective],
  templateUrl: './bottom-nav.component.html',
  styleUrl: './bottom-nav.component.css',
})
export class BottomNavComponent {
  private readonly router = inject(Router);
  private readonly membership = inject(MembershipService);
  private readonly notifications = inject(NotificationService);
  private readonly chat = inject(ChatService);

  /** Capped unread badge for the Alerts tab (feature 010). Empty string hides it. */
  protected readonly alertsBadge = computed(() => badgeText(this.notifications.unreadCount()));

  /** Capped unread badge for the Chat destination (feature 019). Same badgeText() as the bell — two
   * badges in one nav must not cap differently. */
  protected readonly chatBadge = computed(() => badgeText(this.chat.unreadCount()));

  /** The raw counts behind both badges, for `jhBump` — the capped text cannot say "9+ went up". */
  protected readonly alertsUnread = this.notifications.unreadCount;
  protected readonly chatUnread = this.chat.unreadCount;

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );

  protected readonly myTeamHref = this.membership.myTeamTarget;

  protected active(id: NavId): boolean {
    return isActiveDestination(id, this.url(), this.mySlugs());
  }

  /**
   * How a tab's label behaves below `narrow` (240px — see `tailwind.config.js`): the current
   * destination keeps its words, the other four are read out but not drawn (GH #473).
   *
   * Five labels do not fit in the 187px a 375px phone lays out at 200% browser zoom — German
   * needs 211px of them and Spanish 226px, so the bar pushed the whole page sideways. Of the
   * ways out this is the owner's call (2026-10-25). The alternatives each cost more: hyphenating
   * them fits but takes the bar from 75px to 96px, past the `pb-[76px]` every page reserves for
   * it and the same literal in `chat-shell.component.css`; letting the bar scroll its own tabs
   * puts a destination behind a swipe; and truncating a nav item is what DESIGN.md calls a broken
   * nav item.
   *
   * **`sr-only`, never `hidden`.** The label *is* the tab's accessible name — there is no
   * `aria-label` on these links — so `display: none` would leave a screen reader five anonymous
   * icons. `sr-only` withdraws it from the page and leaves the name intact.
   *
   * On a route that is none of the five (`/account`, an event, another team's page) no
   * destination is current, so the bar is icon-only there. That is this rule read literally
   * rather than an oversight: there is no current label to keep.
   */
  protected labelClass(id: NavId): string {
    return this.active(id) ? '' : 'narrow:sr-only';
  }

  private readonly mySlugs = computed(() => this.membership.teams().map((t) => t.slug));
}
