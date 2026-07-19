import { PagedResult } from './profile.models';
import { EventType, SignupStatus } from './event.models';
import { Pompfe } from '../../shared/pompfen.catalog';

export type { PagedResult };

/** Party lifecycle (name-serialized on the wire). Disband is a delete, so there is no state for it. */
export type PartyStatus = 'Open' | 'Applied';

/** A member's role within a party. */
export type PartyMemberRole = 'Member' | 'Admin';

/** The signed-in viewer's relationship to a party. */
export type PartyViewerState = 'None' | 'NoResponse' | 'In' | 'Declined' | 'Admin';

/** The roster group a members list requests. */
export type PartyRosterGroup = 'In' | 'Declined' | 'NoResponse';

export interface PartyReadiness {
  enoughToFieldTeam: boolean;
  spotsOpen: number;
  unanswered: number;
}

export interface Party {
  id: string;
  eventId: string;
  eventName: string;
  eventType: EventType;
  startsAt: string;
  endsAt: string;
  teamId: string;
  teamSlug: string;
  teamName: string;
  rosterCap: number;
  inCount: number;
  declinedCount: number;
  noResponseCount: number;
  isFull: boolean;
  status: PartyStatus;
  myState: PartyViewerState;
  myRole: PartyMemberRole | null;
  message: string | null;
  appliedGroup: SignupStatus | null;
  readiness: PartyReadiness;
}

export interface PartyMember {
  userId: string;
  handle: string;
  displayName: string;
  role: PartyMemberRole | null;
  isYou: boolean;
  pompfen: Pompfe[];
  /** Feature 017: a mercenary seated through the marketplace (a guest, not on the team). */
  viaMarket: boolean;
}

export interface PartyNews {
  id: string;
  authorDisplayName: string;
  authorRole: PartyMemberRole;
  body: string;
  createdDate: string;
}

export interface PartyRequestCard {
  partyId: string;
  eventId: string;
  eventName: string;
  eventType: EventType;
  startsAt: string;
  endsAt: string;
  inCount: number;
  rosterCap: number;
  message: string | null;
  myState: PartyViewerState;
  isFull: boolean;
  status: PartyStatus;
}

export interface PartyContextTeam {
  teamId: string;
  teamName: string;
  teamSlug: string;
  isAdmin: boolean;
  partyId: string | null;
  canForm: boolean;
  myState: PartyViewerState;
  inCount: number | null;
  rosterCap: number | null;
  partyStatus: PartyStatus | null;
}

export interface PartyContext {
  mode: 'Teams' | 'Individuals';
  rosterCap: number | null;
  teams: PartyContextTeam[];
}

export type InvitationKind = 'Link' | 'Targeted';
export type InvitationStatus = 'Pending' | 'Accepted' | 'Declined' | 'Revoked';
export type InviteState = 'Usable' | 'Expired' | 'Invalid';
export type UserRelation = 'Invitable' | 'Invited' | 'Member';

export interface PartyInvitation {
  id: string;
  kind: InvitationKind;
  targetDisplayName: string | null;
  createdDate: string;
  expiresDate: string;
  status: InvitationStatus;
}

export interface PartyInviteLink {
  url: string;
  token: string;
  expiresDate: string;
}

export interface PartyInvitableUser {
  userId: string;
  handle: string;
  displayName: string;
  hometown: string | null;
  relation: UserRelation;
}

export interface PartyInvitePreview {
  partyId: string;
  teamName: string;
  eventName: string;
  startsAt: string;
  inviterDisplayName: string;
  state: InviteState;
}

// --- Requests ---------------------------------------------------------------

export interface FormPartyRequest {
  eventId: string;
  teamId: string;
  message?: string | null;
}

export interface CreatePartyNewsRequest {
  body: string;
}

export interface CreatePartyInviteRequest {
  userId: string;
}
