/**
 * Team API contracts (mirror of backend Dtos/Teams). Internal reads (roster/news)
 * are members-only; public info (name/type/city/count) and the invite preview are
 * anonymous. Enums are serialized as names by the backend.
 */
import { Pompfe } from '../../shared/pompfen.catalog';
import { ActivityItem, PagedResult } from './profile.models';
import { EarnedRecognition } from './recognition.models';

export type { ActivityItem, PagedResult };

export type TeamType = 'CityTeam' | 'Mixteam';
export type TeamRole = 'Member' | 'Admin';
export type InvitationKind = 'Link' | 'Targeted';
export type InvitationStatus = 'Pending' | 'Accepted' | 'Declined' | 'Revoked';
export type InviteState = 'Usable' | 'Expired' | 'Invalid';
export type UserRelation = 'Invitable' | 'Invited' | 'Member';

export interface CreateTeamRequest {
  name: string;
  slug: string;
  type: TeamType;
  city: string | null;
}

export interface TeamDetail {
  slug: string;
  name: string;
  type: TeamType;
  city: string | null;
  memberCount: number;
  myRole: TeamRole;
  /** Feature 007 — self-managed recruitment flag surfaced in browse. */
  beginnersWelcome: boolean;
}

export interface TeamPublic {
  slug: string;
  name: string;
  type: TeamType;
  city: string | null;
  memberCount: number;
}

/** How the viewer relates to a team (feature 009) — drives the sections + join action. */
export type TeamViewerRelation = 'Anonymous' | 'NonMember' | 'Requested' | 'Member' | 'Admin';

/** A public roster row — identity + position only, never contact details. */
export interface PublicMember {
  handle: string;
  displayName: string;
  role: TeamRole;
  hasAvatar: boolean;
  pompfen: Pompfe[];
}

/** An upcoming event the team is entered in, shown publicly. */
export interface Training {
  eventId: string;
  name: string;
  startsAt: string;
  locationLabel: string;
}

/** The public team page (feature 009). */
export interface TeamPublicDetail {
  slug: string;
  name: string;
  type: TeamType;
  city: string | null;
  memberCount: number;
  beginnersWelcome: boolean;
  isActive: boolean;
  viewerRelation: TeamViewerRelation;
  roster: PublicMember[];
  recentActivity: ActivityItem[];
  upcomingTrainings: Training[];
  /** Feature 012 — the team's earned badges & achievements. */
  badges: EarnedRecognition[];
  achievements: EarnedRecognition[];
}

/** One pending join request in the admin queue. */
export interface JoinRequest {
  id: string;
  handle: string;
  displayName: string;
  hasAvatar: boolean;
  createdDate: string;
}

export interface TeamMember {
  userId: string;
  handle: string;
  displayName: string;
  role: TeamRole;
  hasAvatar: boolean;
  pompfen: Pompfe[];
}

export interface TeamNews {
  authorDisplayName: string;
  authorHandle: string;
  authorRole: TeamRole;
  /** ISO date-time. */
  createdDate: string;
  body: string;
}

export interface TeamInvitation {
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
  city: string | null;
  relation: UserRelation;
}

export interface InvitePreview {
  teamName: string;
  teamSlug: string;
  type: TeamType;
  city: string | null;
  memberCount: number;
  inviterDisplayName: string;
  state: InviteState;
}

export interface AcceptInviteResult {
  teamSlug: string;
}

export interface SlugAvailability {
  slug: string;
  normalized: string;
  available: boolean;
  reason: string | null;
}
