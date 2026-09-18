import { TranslocoService } from '@jsverse/transloco';

/**
 * How long an invitation (or the shared invite link) still has, as a phrase — "expires today",
 * "expires tomorrow", "expires in 5 days".
 *
 * Shared because two surfaces word it: the pending-invitation list on the team's invitations
 * screen, and the link block (`jh-invite-link`) that the invitations screen and the create
 * wizard both render. Day-granular on purpose — the server hands out multi-day windows, and
 * "expires in 4 days" is the only part of that anybody acts on.
 */
export function expiresIn(iso: string, t: TranslocoService): string {
  const days = Math.ceil((new Date(iso).getTime() - Date.now()) / 86_400_000);
  if (days <= 0) {
    return t.translate('teams.invitations.expiresToday');
  }
  if (days === 1) {
    return t.translate('teams.invitations.expiresTomorrow');
  }
  return t.translate('teams.invitations.expiresInDays', { count: days });
}
