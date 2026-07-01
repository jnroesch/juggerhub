/**
 * Profile API contracts (mirror of backend Dtos/Profile). The public profile is
 * intentionally free of email/account data — the server strips it at the boundary.
 */
import { Pompfe } from '../../shared/pompfen.catalog';

export interface ActivityItem {
  eventName: string;
  /** ISO date (yyyy-MM-dd). */
  date: string;
  location: string;
  teamLabel: string;
}

export interface OwnerProfile {
  handle: string;
  displayName: string;
  hometown: string | null;
  description: string | null;
  hasAvatar: boolean;
  pompfen: Pompfe[];
  recentActivity: ActivityItem[];
}

export interface PublicProfile {
  handle: string;
  displayName: string;
  hometown: string | null;
  description: string | null;
  hasAvatar: boolean;
  selectedPompfen: Pompfe[];
  recentActivity: ActivityItem[];
}

export interface UpdateProfileRequest {
  displayName: string;
  hometown: string | null;
  description: string | null;
  pompfen: Pompfe[];
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
