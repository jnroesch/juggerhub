/**
 * Platform admin area models (feature 013). Mirrors backend/Dtos/Admin — see
 * specs/013-admin-area/contracts/admin-api.md.
 */

/** Account state (enum names on the wire). Banned = soft-deleted + invisible outside the admin area. */
export type AccountStatus = 'Active' | 'Suspended' | 'Banned';

export interface AdminNewPlayer {
  handle: string;
  displayName: string;
  hometown: string | null;
  joinedAt: string;
}

export interface AdminRecentGrant {
  kind: 'Badge' | 'Achievement';
  name: string;
  /** null for team awards — only player grants link into the admin detail. */
  subjectHandle: string | null;
  subjectDisplayName: string;
  grantedByDisplayName: string;
  grantedAt: string;
}

export interface AdminOverview {
  players: number;
  teams: number;
  eventsLast30Days: number;
  suspended: number;
  newPlayers: AdminNewPlayer[];
  recentGrants: AdminRecentGrant[];
}

export interface AdminUserListItem {
  handle: string;
  displayName: string;
  status: AccountStatus;
  isAdmin: boolean;
  teams: string[];
  badgeCount: number;
  joinedAt: string;
}

export interface AdminUserTeam {
  name: string;
  slug: string;
}

export interface AdminActivityItem {
  title: string;
  date: string;
}

export interface AdminUserDetail {
  userId: string;
  handle: string;
  displayName: string;
  hometown: string | null;
  joinedAt: string;
  status: AccountStatus;
  statusChangedAt: string | null;
  isAdmin: boolean;
  teams: AdminUserTeam[];
  pompfen: string[];
  lastActiveAt: string | null;
  recentActivity: AdminActivityItem[];
}

/** Team kind (enum names on the wire). */
export type TeamType = 'CityTeam' | 'Mixteam';

/** One row of the admin teams list (feature 014). */
export interface AdminTeamListItem {
  slug: string;
  name: string;
  city: string | null;
  type: TeamType;
  memberCount: number;
  awardCount: number;
}

/** Identity for the admin team detail header (feature 014). Awards come from the awards endpoint. */
export interface AdminTeamDetail {
  teamId: string;
  slug: string;
  name: string;
  city: string | null;
  type: TeamType;
  memberCount: number;
  createdAt: string;
}
