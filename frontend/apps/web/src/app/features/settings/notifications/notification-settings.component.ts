import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ButtonDirective } from '../../../shared/ui';
import { NotificationPreferencesService } from '../../../core/services/notification-preferences.service';
import { ChannelKey, NotificationCategoryId } from '../../../core/models/notification-preferences.models';

/**
 * Notification settings (feature 011). Renders the caller's per-category × per-channel matrix from
 * {@link NotificationPreferencesService} — a category × channel matrix on desktop, stacked cards on
 * mobile — and auto-saves each toggle (no save button). Security & sign-in shows as an always-on
 * group with no toggles. Load and save failures surface honestly rather than losing a change.
 */
@Component({
  selector: 'jh-notification-settings',
  imports: [ButtonDirective],
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

  toggle(category: NotificationCategoryId, channelKey: ChannelKey, current: boolean): void {
    this.saveError.set(false);
    this.prefs.setCell(category, channelKey, !current).subscribe({
      error: () => this.saveError.set(true),
    });
  }
}
