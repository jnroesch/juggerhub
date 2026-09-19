/**
 * Notification-preferences contracts (feature 011) — mirror of backend
 * Dtos/Notifications/NotificationPreferenceDtos. The matrix is opt-out: the server applies defaults
 * (on) for any cell the user hasn't changed. Labels/descriptions are server-owned so the desktop
 * matrix and mobile stack render the same copy.
 */

export type NotificationCategoryId = 'InvitesAndRoster' | 'TeamNews' | 'Trainings' | 'Events';
export type NotificationChannelId = 'InApp' | 'Email' | 'Push';

/** The three client-side keys of {@link PreferenceChannels}, matched to their API channel name. */
export type ChannelKey = 'inApp' | 'email' | 'push';

export interface PreferenceChannels {
  inApp: boolean;
  email: boolean;
  /**
   * Web push to this account's enabled devices (feature 055). Independent of the other two: on
   * here with in-app off still delivers. Whether any device is actually enabled is a separate,
   * per-device question answered by the device section, not by this flag.
   */
  push: boolean;
}

export interface PreferenceCategory {
  category: NotificationCategoryId;
  label: string;
  description: string;
  channels: PreferenceChannels;
}

export interface AlwaysOnGroup {
  label: string;
  description: string;
}

export interface NotificationPreferenceMatrix {
  categories: PreferenceCategory[];
  alwaysOn: AlwaysOnGroup[];
}

const CHANNEL_IDS: Record<ChannelKey, NotificationChannelId> = {
  inApp: 'InApp',
  email: 'Email',
  push: 'Push',
};

/** Map a client channel key to the API's channel route segment. */
export function channelIdOf(key: ChannelKey): NotificationChannelId {
  return CHANNEL_IDS[key];
}
