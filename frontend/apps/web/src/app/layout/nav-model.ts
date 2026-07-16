import { MyTeam } from '../core/models/home.models';

/**
 * The single source of truth for the app's top-level destinations (feature 008), shared by
 * the desktop top bar and the mobile bottom tab bar so both provably expose the same set.
 * Profile is reached from the avatar, not a primary destination; Create is contextual.
 */
export type NavId = 'home' | 'browse' | 'my-team' | 'chat' | 'alerts';

export interface NavDestination {
  readonly id: NavId;
  readonly label: string;
  /** Static path for matching/anchor. "My team" resolves its real target from memberships. */
  readonly path: string;
}

export const NAV_DESTINATIONS: readonly NavDestination[] = [
  { id: 'home', label: 'Home', path: '/' },
  { id: 'browse', label: 'Browse', path: '/browse' },
  { id: 'my-team', label: 'My team', path: '/my-team' },
  { id: 'chat', label: 'Chat', path: '/chat' },
  { id: 'alerts', label: 'Alerts', path: '/alerts' },
];

/** Whether a destination is the active one for the current URL. */
export function isActiveDestination(id: NavId, url: string): boolean {
  const path = url.split('?')[0].split('#')[0];
  switch (id) {
    case 'home':
      return path === '/' || path === '';
    case 'browse':
      return path.startsWith('/browse');
    case 'my-team':
      // A team space (/t/:slug) and the multi-team chooser both light up "My team".
      return path.startsWith('/t/') || path.startsWith('/my-team');
    case 'chat':
      // The inbox and any open conversation (/chat/:id) both light up "Chat".
      return path.startsWith('/chat');
    case 'alerts':
      return path.startsWith('/alerts');
  }
}

/**
 * Compact unread-badge text for the Alerts bell (feature 010) and the Chat destination (feature 019):
 * empty when nothing is unread, the count up to 9, then a capped "9+" so the badge never grows
 * unbounded. Shared deliberately — two badges in the same nav must not cap differently.
 */
export function badgeText(count: number): string {
  if (count <= 0) {
    return '';
  }
  return count > 9 ? '9+' : String(count);
}

/**
 * Where "My team" navigates, by how many teams the player is on:
 * 0 → find a team (Browse teams); 1 → that team's space; many → the team chooser.
 */
export function myTeamTarget(teams: readonly MyTeam[]): string {
  if (teams.length === 0) {
    return '/browse/teams';
  }
  if (teams.length === 1) {
    return `/t/${teams[0].slug}`;
  }
  return '/my-team';
}
