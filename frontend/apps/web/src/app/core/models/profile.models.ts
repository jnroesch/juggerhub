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
  /** Feature 007 — whether the owner opted into appearing in player search. */
  appearInSearch: boolean;
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
  /** Feature 007 — opt-in to appear in player search. Optional; the backend defaults it to
   *  false (privacy-safe) when omitted, so flows that don't manage it (e.g. onboarding) skip it. */
  appearInSearch?: boolean;
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
