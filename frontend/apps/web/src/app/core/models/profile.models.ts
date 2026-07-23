/**
 * Profile API contracts (mirror of backend Dtos/Profile). The public profile is
 * intentionally free of email/account data — the server strips it at the boundary.
 */
import { Pompfe } from '../../shared/pompfen.catalog';
import { EarnedRecognition } from './recognition.models';

export interface ActivityItem {
  eventName: string;
  /** ISO date (yyyy-MM-dd). */
  date: string;
  location: string;
  teamLabel: string;
}

export interface ProfileTeam {
  slug: string;
  name: string;
  type: 'CityTeam' | 'Mixteam';
  city: string | null;
  role: 'Member' | 'Admin';
}

export interface OwnerProfile {
  handle: string;
  displayName: string;
  hometown: string | null;
  description: string | null;
  hasAvatar: boolean;
  pompfen: Pompfe[];
  recentActivity: ActivityItem[];
  teams: ProfileTeam[];
  /** Feature 012 — earned badges & achievements. */
  badges: EarnedRecognition[];
  achievements: EarnedRecognition[];
  /** Feature 026 — whether the owner has made their profile anonymously viewable by link. */
  isPublic: boolean;
}

export interface PublicProfile {
  handle: string;
  displayName: string;
  hometown: string | null;
  description: string | null;
  hasAvatar: boolean;
  selectedPompfen: Pompfe[];
  recentActivity: ActivityItem[];
  teams: ProfileTeam[];
  /** Feature 012 — earned badges & achievements. */
  badges: EarnedRecognition[];
  achievements: EarnedRecognition[];
}

export interface UpdateProfileRequest {
  displayName: string;
  hometown: string | null;
  description: string | null;
  pompfen: Pompfe[];
  /** Feature 026 — owner-controlled anonymous visibility (default private). */
  isPublic: boolean;
}

/**
 * Normalized, read-only profile for the shared presentational view (feature 026). Both the owner's
 * own profile and another player's profile map to this, so they render through one component and
 * can never drift apart in structure/styling.
 */
export interface ProfileView {
  handle: string;
  displayName: string;
  hometown: string | null;
  description: string | null;
  /** Resolved avatar URL (with a cache-buster for the owner after an upload); null if none. */
  avatarUrl: string | null;
  pompfen: Pompfe[];
  teams: ProfileTeam[];
  recentActivity: ActivityItem[];
  badges: EarnedRecognition[];
  achievements: EarnedRecognition[];
}

export interface HandleAvailability {
  handle: string;
  normalized: string;
  available: boolean;
  reason: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  skip: number;
  take: number;
}
