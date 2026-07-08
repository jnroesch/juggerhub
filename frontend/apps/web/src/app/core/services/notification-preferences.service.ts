import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, catchError, tap, throwError } from 'rxjs';
import {
  ChannelKey,
  NotificationCategoryId,
  NotificationPreferenceMatrix,
  channelIdOf,
} from '../models/notification-preferences.models';

/**
 * Notification-preferences client (feature 011). Loads the effective matrix into a signal and
 * upserts one cell per toggle (auto-save). Toggles are optimistic — the signal updates immediately
 * and reverts if the PUT fails — so the UI feels instant while the server stays the boundary.
 */
@Injectable({ providedIn: 'root' })
export class NotificationPreferencesService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/notification-preferences';

  private readonly _matrix = signal<NotificationPreferenceMatrix | null>(null);
  readonly matrix = this._matrix.asReadonly();

  /** Load the caller's matrix. */
  load(): Observable<NotificationPreferenceMatrix> {
    return this.http
      .get<NotificationPreferenceMatrix>(this.base)
      .pipe(tap((m) => this._matrix.set(m)));
  }

  /** Set one (category, channel) cell, optimistically; reverts on failure. */
  setCell(category: NotificationCategoryId, channelKey: ChannelKey, enabled: boolean): Observable<void> {
    const previous = this.valueOf(category, channelKey);
    this.patch(category, channelKey, enabled);

    return this.http
      .put<void>(`${this.base}/${category}/${channelIdOf(channelKey)}`, { enabled })
      .pipe(
        catchError((err) => {
          this.patch(category, channelKey, previous);
          return throwError(() => err);
        }),
      );
  }

  private valueOf(category: NotificationCategoryId, channelKey: ChannelKey): boolean {
    const cat = this._matrix()?.categories.find((c) => c.category === category);
    return cat ? cat.channels[channelKey] : true;
  }

  private patch(category: NotificationCategoryId, channelKey: ChannelKey, enabled: boolean): void {
    this._matrix.update((m) =>
      m
        ? {
            ...m,
            categories: m.categories.map((c) =>
              c.category === category ? { ...c, channels: { ...c.channels, [channelKey]: enabled } } : c,
            ),
          }
        : m,
    );
  }
}
