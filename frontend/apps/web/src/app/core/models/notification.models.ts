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
  | 'MarketInvite'
  | 'TrainingScheduled'
  | 'TrainingUpdated'
  | 'EventCancelled'
  | 'TeamJoinRequest'
  | 'TeamJoinRequestAnswered'
  | 'TeamPoll'
  | 'TeamMemberRemoved'
  | 'TeamMemberDeparted';

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

/** Training heads-up (feature 018) — a team scheduled a new series/one-off. */
export interface TrainingScheduledPayload {
  teamSlug: string;
  trainingId: string;
  trainingName: string;
  sessionId: string | null;
  isRecurring: boolean;
}

/** Training change notice (feature 018) — a series edit or a session cancellation. */
export interface TrainingUpdatedPayload {
  teamSlug: string;
  trainingId: string;
  sessionId: string | null;
  trainingName: string;
  kind: 'seriesEdit' | 'cancelled';
}

/**
 * Event cancellation (feature 039) — an event the recipient signed up for was called off.
 * Deliberately not reusing {@link PartyPayload}: a cancellation concerns the event itself and
 * carries no team or party context, even when the recipient joined via a team sign-up.
 */
export interface EventCancelledPayload {
  eventId: string;
  eventName: string;
}

/**
 * Someone asked to join a team the recipient administers (feature 058). There is deliberately no
 * name here: the player is the notification's actor, so `actorDisplayName` carries their current
 * name — and null once they are banned or have deleted their account. `resolved` is true once the
 * request no longer waits for an answer.
 */
export interface TeamJoinRequestPayload {
  requestId: string;
  teamSlug: string;
  teamName: string;
}

/**
 * The answer to the recipient's request to join (feature 058). Names the team, never the admin who
 * answered.
 */
export interface TeamJoinRequestAnsweredPayload {
  teamSlug: string;
  teamName: string;
  accepted: boolean;
}

/**
 * A team the recipient belongs to started a poll (feature 062). Carries the question, and no person
 * — the author is the row's actor.
 */
export interface TeamPollPayload {
  teamSlug: string;
  teamName: string;
  pollId: string;
  question: string;
}

/**
 * An admin removed the recipient from a team (feature 064). The team and nothing else: the row has no
 * actor, so nothing here names the admin who removed them.
 */
export interface TeamMemberRemovedPayload {
  teamSlug: string;
  teamName: string;
}

/**
 * A player left a team the recipient administers, or was removed from it (feature 064). The player is
 * the row's actor (`actorDisplayName`, null once banned or erased), never a field here; which admin
 * removed them is stated nowhere.
 */
export interface TeamMemberDepartedPayload {
  teamSlug: string;
  teamName: string;
  /** False: they left on their own. True: another admin removed them. */
  removed: boolean;
}

export type NotificationPayload =
  | TeamInvitePayload
  | TeamRoleChangedPayload
  | TeamNewsPayload
  | PartyPayload
  | MarketInvitePayload
  | TrainingScheduledPayload
  | TrainingUpdatedPayload
  | EventCancelledPayload
  | TeamJoinRequestPayload
  | TeamJoinRequestAnsweredPayload
  | TeamPollPayload
  | TeamMemberRemovedPayload
  | TeamMemberDepartedPayload;

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

export function isTrainingScheduled(
  n: AppNotification,
): n is AppNotification & { type: 'TrainingScheduled'; payload: TrainingScheduledPayload } {
  return n.type === 'TrainingScheduled';
}

export function isTrainingUpdated(
  n: AppNotification,
): n is AppNotification & { type: 'TrainingUpdated'; payload: TrainingUpdatedPayload } {
  return n.type === 'TrainingUpdated';
}

export function isEventCancelled(
  n: AppNotification,
): n is AppNotification & { type: 'EventCancelled'; payload: EventCancelledPayload } {
  return n.type === 'EventCancelled';
}

export function isTeamJoinRequest(
  n: AppNotification,
): n is AppNotification & { type: 'TeamJoinRequest'; payload: TeamJoinRequestPayload } {
  return n.type === 'TeamJoinRequest';
}

export function isTeamJoinRequestAnswered(
  n: AppNotification,
): n is AppNotification & { type: 'TeamJoinRequestAnswered'; payload: TeamJoinRequestAnsweredPayload } {
  return n.type === 'TeamJoinRequestAnswered';
}

export function isTeamPoll(n: AppNotification): n is AppNotification & { type: 'TeamPoll'; payload: TeamPollPayload } {
  return n.type === 'TeamPoll';
}

export function isTeamMemberRemoved(
  n: AppNotification,
): n is AppNotification & { type: 'TeamMemberRemoved'; payload: TeamMemberRemovedPayload } {
  return n.type === 'TeamMemberRemoved';
}

export function isTeamMemberDeparted(
  n: AppNotification,
): n is AppNotification & { type: 'TeamMemberDeparted'; payload: TeamMemberDepartedPayload } {
  return n.type === 'TeamMemberDeparted';
}
