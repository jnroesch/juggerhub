import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/**
 * What this browser's push state is, and what the member can do about it (feature 055).
 *
 * Each value gets its own sentence in the settings page, because each has a different answer to
 * "what now?" — `blocked` in particular is the one the product cannot fix: a site that has been
 * denied notification permission cannot ask again, only the browser's own settings can undo it.
 */
export type PushDeviceState =
  /** No Push API here at all. Nothing to offer. */
  | 'unsupported'
  /** iOS delivers push only to an app added to the Home Screen. Explain how, do not show a button. */
  | 'needs-install'
  /** Permission was denied. Only the browser can change this; the product must not pretend otherwise. */
  | 'blocked'
  /** Supported and permitted or not yet asked — the member can turn it on. */
  | 'off'
  /** A press is in flight. */
  | 'enabling'
  /** This browser is subscribed and will receive notifications. */
  | 'on';

interface PublicKeyResponse {
  publicKey: string;
}

/**
 * Push notifications for the browser the member is currently using (feature 055).
 *
 * The subscription is per-device and lives in the browser; which categories are pushed is a
 * per-account preference and lives in the notification matrix. This service owns only the former.
 *
 * Permission is requested ONLY from {@link enable}, i.e. only from a member's press. Asking on page
 * load is penalised by browsers with a quiet permission UI, and it is forbidden by the spec.
 */
@Injectable({ providedIn: 'root' })
export class PushDeviceService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/push';

  private readonly _state = signal<PushDeviceState>('off');
  readonly state = this._state.asReadonly();

  /**
   * Works out where this browser stands, without asking for anything. Safe to call on page load.
   */
  async refresh(): Promise<void> {
    if (!this.supportsPush()) {
      this._state.set(this.needsHomeScreenInstall() ? 'needs-install' : 'unsupported');
      return;
    }

    if (Notification.permission === 'denied') {
      this._state.set('blocked');
      return;
    }

    const registration = await navigator.serviceWorker.ready;
    const existing = await registration.pushManager.getSubscription();
    this._state.set(existing ? 'on' : 'off');
  }

  /**
   * Subscribes this browser: asks the browser for permission, creates the subscription and
   * registers it. Returns true when the device ends up receiving notifications.
   */
  async enable(): Promise<boolean> {
    if (!this.supportsPush()) {
      this._state.set(this.needsHomeScreenInstall() ? 'needs-install' : 'unsupported');
      return false;
    }

    this._state.set('enabling');
    try {
      const permission = await Notification.requestPermission();
      if (permission !== 'granted') {
        // 'denied' is final until the member changes it in the browser; 'default' means they
        // dismissed the prompt, which leaves them able to press again.
        this._state.set(permission === 'denied' ? 'blocked' : 'off');
        return false;
      }

      const { publicKey } = await firstValueFrom(
        this.http.get<PublicKeyResponse>(`${this.base}/public-key`),
      );

      const registration = await navigator.serviceWorker.ready;
      const subscription = await registration.pushManager.subscribe({
        // Not optional: browsers require every push to show something, which is also why no
        // attempt is made to suppress a notification because a window is open elsewhere.
        userVisibleOnly: true,
        applicationServerKey: base64UrlToUint8Array(publicKey),
      });

      await firstValueFrom(this.http.post<void>(`${this.base}/subscriptions`, {
        ...subscriptionKeys(subscription),
        endpoint: subscription.endpoint,
        deviceLabel: deviceLabel(),
      }));

      this._state.set('on');
      return true;
    } catch {
      await this.refresh();
      return false;
    }
  }

  /**
   * Unsubscribes this browser and tells the server to forget it. Best effort in both directions:
   * if the server call fails the local unsubscribe still stands, and the row is pruned later when
   * the push service reports the endpoint gone.
   */
  async disable(): Promise<void> {
    if (!this.supportsPush()) {
      return;
    }

    const registration = await navigator.serviceWorker.ready;
    const subscription = await registration.pushManager.getSubscription();
    if (!subscription) {
      this._state.set('off');
      return;
    }

    const endpoint = subscription.endpoint;
    try {
      await subscription.unsubscribe();
    } catch {
      // Keep going: telling the server is the half that stops delivery.
    }

    try {
      await firstValueFrom(
        this.http.request<void>('delete', `${this.base}/subscriptions`, { body: { endpoint } }),
      );
    } catch {
      // Pruned later by the push service's 404/410 or by the retention sweep.
    }

    this._state.set('off');
  }

  /**
   * Used on sign-out. Never throws and never blocks the sign-out it is part of: a member leaving a
   * shared device must not be held up by a failing unsubscribe.
   */
  async disableQuietly(): Promise<void> {
    try {
      await this.disable();
    } catch {
      // Deliberately swallowed.
    }
  }

  private supportsPush(): boolean {
    return (
      typeof Notification !== 'undefined' &&
      'serviceWorker' in navigator &&
      'PushManager' in window
    );
  }

  /**
   * True for an iOS-like device that is not running as an installed app.
   *
   * Deliberately NOT user-agent version sniffing. On iOS `PushManager` is simply absent in a
   * browser tab and present in the app added to the Home Screen, so "no Push API, on a platform
   * that supports it once installed, and not running standalone" is the honest test.
   */
  private needsHomeScreenInstall(): boolean {
    const iOSLike =
      /iPad|iPhone|iPod/.test(navigator.userAgent) ||
      // iPadOS reports itself as a Mac; the touch points give it away.
      (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);

    const standalone = window.matchMedia?.('(display-mode: standalone)').matches ?? false;
    return iOSLike && !standalone;
  }
}

/** The browser's keys, named as the API expects them. */
function subscriptionKeys(subscription: PushSubscription): { p256dh: string; auth: string } {
  const json = subscription.toJSON();
  return { p256dh: json.keys?.['p256dh'] ?? '', auth: json.keys?.['auth'] ?? '' };
}

/** A label a member would recognise among their own devices. Never parsed. */
function deviceLabel(): string {
  const ua = navigator.userAgent;
  const browser = /Edg\//.test(ua)
    ? 'Edge'
    : /Firefox\//.test(ua)
      ? 'Firefox'
      : /Chrome\//.test(ua)
        ? 'Chrome'
        : /Safari\//.test(ua)
          ? 'Safari'
          : 'Browser';

  const platform = /Android/.test(ua)
    ? 'Android'
    : /iPhone|iPad|iPod/.test(ua)
      ? 'iOS'
      : /Windows/.test(ua)
        ? 'Windows'
        : /Mac OS X/.test(ua)
          ? 'macOS'
          : /Linux/.test(ua)
            ? 'Linux'
            : '';

  return platform ? `${browser} on ${platform}` : browser;
}

/**
 * The application server key must reach `pushManager.subscribe` as raw bytes; it arrives as
 * base64url. Padding is restored and the URL-safe alphabet mapped back before decoding.
 */
function base64UrlToUint8Array(base64Url: string): Uint8Array<ArrayBuffer> {
  const padding = '='.repeat((4 - (base64Url.length % 4)) % 4);
  const base64 = (base64Url + padding).replace(/-/g, '+').replace(/_/g, '/');
  const raw = atob(base64);
  // Backed by an explicit ArrayBuffer rather than the default ArrayBufferLike: `subscribe` wants a
  // BufferSource, which excludes SharedArrayBuffer, so the looser type is rejected.
  const bytes = new Uint8Array(new ArrayBuffer(raw.length));
  for (let i = 0; i < raw.length; i += 1) {
    bytes[i] = raw.charCodeAt(i);
  }
  return bytes;
}
