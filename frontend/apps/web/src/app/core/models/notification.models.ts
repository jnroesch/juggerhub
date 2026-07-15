/**
 * In-app notification contracts (feature 010) — mirror of backend Dtos/Notifications. The payload
 * shape is narrowed by `type`; `resolved` applies only to team invites (true once the underlying
 * invite is no longer actionable). Enums arrive as their names.
 */

import { PagedResult, TeamRole } from './home.models';

export type { PagedResult };

export type NotificationType =
  | 'TeamInvite'
  | 'TeamRoleChanged'
  | 'TeamNews'
  | 'PartyRequest'
  | 'PartyNews'
  | 'MarketInvite';

export interface TeamInvitePayload {
  invitationId: string;
  token: string;
  teamSlug: string;
  teamName: string;
  inviterName: string;
}

export interface TeamRoleChangedPayload {
  teamSlug: string;
  teamName: string;
  newRole: TeamRole;
}

export interface TeamNewsPayload {
  teamSlug: string;
  teamName: string;
  newsPostId: string;
  excerpt: string;
}

/** Party participation request / news (feature 016) — same shape for both. */
export interface PartyPayload {
  partyId: string;
  eventId: string;
  teamSlug: string;
  eventName: string;
  teamName: string;
}

/** Marketplace invite (feature 017) — a party invited the recipient to join it. */
export interface MarketInvitePayload {
  requestId: string;
  partyId: string;
  teamName: string;
  teamSlug: string;
  eventId: string;
  eventName: string;
  positions: string[];
}

export type NotificationPayload =
  | TeamInvitePayload
  | TeamRoleChangedPayload
  | TeamNewsPayload
  | PartyPayload
  | MarketInvitePayload;

export interface AppNotification {
  id: string;
  type: NotificationType;
  createdDate: string;
  isRead: boolean;
  actorDisplayName: string | null;
  resolved: boolean;
  payload: NotificationPayload;
}

export interface UnreadCount {
  count: number;
}

/** Narrowing helpers so templates/handlers can read the right payload safely. */
export function isTeamInvite(
  n: AppNotification,
): n is AppNotification & { type: 'TeamInvite'; payload: TeamInvitePayload } {
  return n.type === 'TeamInvite';
}

export function isTeamRoleChanged(
  n: AppNotification,
): n is AppNotification & { type: 'TeamRoleChanged'; payload: TeamRoleChangedPayload } {
  return n.type === 'TeamRoleChanged';
}

export function isTeamNews(
  n: AppNotification,
): n is AppNotification & { type: 'TeamNews'; payload: TeamNewsPayload } {
  return n.type === 'TeamNews';
}

export function isPartyRequest(
  n: AppNotification,
): n is AppNotification & { type: 'PartyRequest'; payload: PartyPayload } {
  return n.type === 'PartyRequest';
}

export function isPartyNews(
  n: AppNotification,
): n is AppNotification & { type: 'PartyNews'; payload: PartyPayload } {
  return n.type === 'PartyNews';
}

export function isMarketInvite(
  n: AppNotification,
): n is AppNotification & { type: 'MarketInvite'; payload: MarketInvitePayload } {
  return n.type === 'MarketInvite';
}
