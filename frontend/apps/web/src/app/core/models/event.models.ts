/**
 * Event API contracts (mirror of backend Dtos/Events). The event page + its lists
 * are public; sign-up requires auth; every admin action is authorized server-side.
 * Enums are serialized as names by the backend.
 */
import { PagedResult } from './profile.models';

export type { PagedResult };

export type EventType = 'Tournament' | 'Workshop' | 'Other';
export type LocationKind = 'InPerson' | 'Virtual';
export type ParticipantMode = 'Teams' | 'Individuals';
export type EventStatus = 'Published' | 'Cancelled';
export type SignupStatus = 'Joined' | 'AwaitingApproval' | 'Waitlisted';
export type InvitationKind = 'Link' | 'Targeted';
export type InvitationStatus = 'Pending' | 'Accepted' | 'Declined' | 'Revoked';
export type InviteState = 'Usable' | 'Expired' | 'Invalid';
export type UserRelation = 'Invitable' | 'Invited' | 'Member';

export interface CreateEventRequest {
  name: string;
  type: EventType;
  customTypeLabel: string | null;
  description: string;
  /** ISO date-time (UTC). */
  startsAt: string;
  endsAt: string;
  locationKind: LocationKind;
  venueName: string | null;
  street: string | null;
  postalCode: string | null;
  city: string | null;
  country: string | null;
  virtualLink: string | null;
  participantMode: ParticipantMode;
  participationLimit: number;
  /** Players-per-team cap for teams-only events (feature 016): default 8, min 5; null otherwise. */
  rosterCap: number | null;
  isPaid: boolean;
  feeAmount: number | null;
  feeCurrency: string | null;
  feeRecipientName: string | null;
  feeIban: string | null;
  /** ISO date (yyyy-MM-dd). */
  feePaymentDeadline: string | null;
}

/** Edit carries the same fields except participantMode (immutable after creation). */
export type EditEventRequest = Omit<CreateEventRequest, 'participantMode' | 'rosterCap'>;

export interface ViewerTeamOption {
  teamId: string;
  name: string;
  slug: string;
}

export interface ViewerRelation {
  isAuthenticated: boolean;
  isAdmin: boolean;
  mySignupStatus: SignupStatus | null;
  mySignupId: string | null;
  teamsICanEnter: ViewerTeamOption[];
}

export interface EventDetail {
  id: string;
  name: string;
  type: EventType;
  customTypeLabel: string | null;
  description: string;
  startsAt: string;
  endsAt: string;
  locationKind: LocationKind;
  venueName: string | null;
  street: string | null;
  postalCode: string | null;
  city: string | null;
  country: string | null;
  virtualLink: string | null;
  participantMode: ParticipantMode;
  participationLimit: number;
  occupiedSpots: number;
  isFull: boolean;
  isPaid: boolean;
  feeAmount: number | null;
  feeCurrency: string | null;
  feeRecipientName: string | null;
  feeIban: string | null;
  feePaymentDeadline: string | null;
  status: EventStatus;
  viewer: ViewerRelation;
}

export interface SignupRequest {
  teamId: string | null;
}

export interface Signup {
  id: string;
  status: SignupStatus;
  joinedAt: string;
  userHandle: string | null;
  userDisplayName: string | null;
  teamSlug: string | null;
  teamName: string | null;
}

export interface CreateContactRequest {
  name: string;
  role: string;
  phone: string | null;
  email: string | null;
}

export interface EventContact {
  id: string;
  name: string;
  role: string;
  phone: string | null;
  email: string | null;
}

export interface EventNews {
  id: string;
  authorDisplayName: string;
  body: string;
  createdDate: string;
}

export interface EventAdmin {
  userId: string;
  handle: string;
  displayName: string;
}

export interface EventInvitation {
  id: string;
  kind: InvitationKind;
  targetDisplayName: string | null;
  createdDate: string;
  expiresDate: string;
  status: InvitationStatus;
}

export interface InviteLink {
  url: string;
  token: string;
  expiresDate: string;
}

export interface InvitableUser {
  userId: string;
  handle: string;
  displayName: string;
  hometown: string | null;
  relation: UserRelation;
}

export interface InvitePreview {
  eventId: string;
  eventName: string;
  startsAt: string;
  inviterDisplayName: string;
  state: InviteState;
}

export interface AcceptInviteResult {
  eventId: string;
}
