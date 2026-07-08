/**
 * Notification-preferences contracts (feature 011) — mirror of backend
 * Dtos/Notifications/NotificationPreferenceDtos. The matrix is opt-out: the server applies defaults
 * (on) for any cell the user hasn't changed. Labels/descriptions are server-owned so the desktop
 * matrix and mobile stack render the same copy.
 */

export type NotificationCategoryId = 'InvitesAndRoster' | 'TeamNews';
export type NotificationChannelId = 'InApp' | 'Email';

/** The two client-side keys of {@link PreferenceChannels}, matched to their API channel name. */
export type ChannelKey = 'inApp' | 'email';

export interface PreferenceChannels {
  inApp: boolean;
  email: boolean;
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

/** Map a client channel key to the API's channel route segment. */
export function channelIdOf(key: ChannelKey): NotificationChannelId {
  return key === 'inApp' ? 'InApp' : 'Email';
}
