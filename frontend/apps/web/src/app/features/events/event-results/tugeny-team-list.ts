import { Signup } from '../../../core/models/event.models';

/**
 * The confirmed team list, in the plain-text form Tugeny's *Import Team Names* dialog accepts —
 * one team name per row (feature 050, contracts/tugeny.md §3). Pure.
 *
 * Only confirmed (`Joined`) team sign-ups count; pending and waitlisted teams have no place yet.
 * Tugeny refuses two teams with the same name, so clashes are reported before anyone copies.
 */
export interface TugenyTeamList {
  /** One name per line, sign-up order, no trailing newline. */
  text: string;
  names: string[];
  /** Each clashing name once, as first spelled; compared ignoring case and surrounding spaces. */
  duplicates: string[];
}

export function buildTugenyTeamList(signups: readonly Signup[]): TugenyTeamList {
  const names = signups
    .filter((s) => s.status === 'Joined' && !!s.teamName?.trim())
    .map((s, index) => ({ s, index }))
    .sort((a, b) => a.s.joinedAt.localeCompare(b.s.joinedAt) || a.index - b.index)
    .map(({ s }) => (s.teamName as string).trim());

  const seen = new Map<string, { first: string; count: number }>();
  for (const name of names) {
    const key = name.toLocaleLowerCase();
    const entry = seen.get(key);
    seen.set(key, entry ? { ...entry, count: entry.count + 1 } : { first: name, count: 1 });
  }

  return {
    text: names.join('\n'),
    names,
    duplicates: [...seen.values()].filter((e) => e.count > 1).map((e) => e.first),
  };
}
