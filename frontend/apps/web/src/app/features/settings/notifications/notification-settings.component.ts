import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ButtonDirective, CardComponent, ChipDirective } from '../../../shared/ui';
import { NotificationPreferencesService } from '../../../core/services/notification-preferences.service';
import {
  ChannelKey,
  NotificationCategoryId,
  NotificationChannelId,
  PreferenceCategory,
  channelIdOf,
} from '../../../core/models/notification-preferences.models';
import { PushDeviceSectionComponent } from './push-device-section.component';

/**
 * Notification settings (feature 011). Renders the caller's per-category × per-channel matrix from
 * {@link NotificationPreferencesService} — a category × channel matrix on desktop, stacked cards on
 * mobile — and auto-saves each toggle (no save button). Security & sign-in shows as an always-on
 * group with no toggles. Load and save failures surface honestly rather than losing a change.
 */
@Component({
  selector: 'jh-notification-settings',
  imports: [CardComponent, ButtonDirective, ChipDirective, TranslocoPipe, PushDeviceSectionComponent],
  templateUrl: './notification-settings.component.html',
  styleUrl: './notification-settings.component.css',
})
export class NotificationSettingsComponent implements OnInit {
  private readonly prefs = inject(NotificationPreferencesService);

  protected readonly matrix = this.prefs.matrix;
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly saveError = signal(false);

  protected readonly categories = computed(() => this.matrix()?.categories ?? []);
  protected readonly alwaysOn = computed(() => this.matrix()?.alwaysOn ?? []);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.prefs.load().subscribe({
      next: () => this.loading.set(false),
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      },
    });
  }

  /**
   * Whether this category has this cell at all (feature 056).
   *
   * Not every category is deliverable on every channel: chat has its own inbox and badge rather
   * than Alerts rows, and there is no email for a missed message. Such a cell is rendered as
   * unavailable, never as a switched-off toggle — the toggle would say "you could turn this on",
   * which is untrue and is the support question the category's description exists to pre-empt.
   *
   * Reads `availableChannels`, never `channels`: the server leaves an unavailable cell's value at
   * its default precisely so it cannot be confused with a member's own choice.
   */
  isAvailable(category: PreferenceCategory, channelKey: ChannelKey): boolean {
    return category.availableChannels.includes(channelIdOf(channelKey) as NotificationChannelId);
  }

  toggle(category: NotificationCategoryId, channelKey: ChannelKey, current: boolean): void {
    this.saveError.set(false);
    this.prefs.setCell(category, channelKey, !current).subscribe({
      error: () => this.saveError.set(true),
    });
  }
}
