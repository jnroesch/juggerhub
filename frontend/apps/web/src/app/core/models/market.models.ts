import { PagedResult } from './profile.models';
import { Pompfe } from '../../shared/pompfen.catalog';

export type { PagedResult };

/** Which way a market request points (name-serialized on the wire). */
export type MarketRequestDirection = 'Application' | 'Invite';

/** A market request's lifecycle. Only `Pending` is actionable. */
export type MarketRequestStatus = 'Pending' | 'Accepted' | 'Declined' | 'Revoked';

/** A direct-invite candidate's relation to a party. */
export type MarketInviteRelation = 'Invitable' | 'Invited' | 'Ineligible';

/** One free agent on the board's free-agents side. */
export interface MarketListingCard {
  userId: string;
  handle: string;
  displayName: string;
  hasAvatar: boolean;
  positions: Pompfe[];
  pitch: string;
}

/** One recruiting party on the board's parties side. */
export interface RecruitingPartyCard {
  partyId: string;
  teamId: string;
  teamName: string;
  teamSlug: string;
  eventId: string;
  openSpots: number;
  rosterCap: number;
  inCount: number;
  positionsNeeded: Pompfe[];
  blurb: string | null;
}

/** The caller's own free-agent listing for an event. */
export interface MarketListing {
  id: string;
  eventId: string;
  positions: Pompfe[];
  pitch: string;
}

/** One of the caller's active listings with event context (dashboard). */
export interface MyListing {
  id: string;
  eventId: string;
  eventName: string;
  startsAt: string;
  positions: Pompfe[];
  pitch: string;
}

/** One application/invite as seen in either inbox. */
export interface MarketRequest {
  id: string;
  partyId: string;
  teamName: string;
  teamSlug: string;
  eventId: string;
  eventName: string;
  userId: string;
  handle: string;
  displayName: string;
  hasAvatar: boolean;
  direction: MarketRequestDirection;
  positions: Pompfe[];
  status: MarketRequestStatus;
  createdDate: string;
}

/** A compact request row for the dashboard market module. */
export interface MyMarketRequest {
  id: string;
  partyId: string;
  teamName: string;
  eventId: string;
  eventName: string;
  direction: MarketRequestDirection;
  positions: Pompfe[];
  status: MarketRequestStatus;
  createdDate: string;
}

/** A recruiting party the caller administers on this event. */
export interface MyMarketAdminParty {
  partyId: string;
  teamName: string;
  teamSlug: string;
  isRecruiting: boolean;
  openSpots: number;
}

/** The signed-in caller's marketplace context for one event. */
export interface MyMarket {
  userId: string;
  mode: 'Teams' | 'Individuals';
  eligible: boolean;
  ineligibleReason: string | null;
  myListing: MarketListing | null;
  adminParties: MyMarketAdminParty[];
  invitesToAnswer: MarketRequest[];
  myApplications: MarketRequest[];
}

/** A party's recruiting settings + live fill. */
export interface RecruitingSettings {
  partyId: string;
  isRecruiting: boolean;
  spotsAdvertised: number;
  positionsNeeded: Pompfe[];
  blurb: string | null;
  rosterCap: number;
  inCount: number;
  openSpots: number;
}

/** One user-search candidate for a direct invite. */
export interface MarketInvitableUser {
  userId: string;
  handle: string;
  displayName: string;
  hometown: string | null;
  hasAvatar: boolean;
  relation: MarketInviteRelation;
}

// --- Requests ---------------------------------------------------------------

export interface PostListingRequest {
  positions: Pompfe[];
  pitch: string;
}

export interface ApplyRequest {
  positions: Pompfe[];
}

export interface InviteRequest {
  userId: string;
  positions: Pompfe[];
}

export interface SetRecruitingRequest {
  isRecruiting: boolean;
  spotsAdvertised: number;
  positionsNeeded: Pompfe[];
  blurb: string | null;
}
