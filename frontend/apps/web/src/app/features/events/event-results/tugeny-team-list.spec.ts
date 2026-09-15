import { Signup } from '../../../core/models/event.models';
import { buildTugenyTeamList } from './tugeny-team-list';

const signup = (teamName: string | null, joinedAt: string, status: Signup['status'] = 'Joined'): Signup => ({
  id: `${teamName}-${joinedAt}`,
  status,
  joinedAt,
  userHandle: teamName ? null : 'someone',
  userDisplayName: teamName ? null : 'Someone',
  teamSlug: teamName ? teamName.toLowerCase().replace(/\s+/g, '-') : null,
  teamName,
});

describe('buildTugenyTeamList (contracts/tugeny.md §3)', () => {
  it('lists confirmed teams one per line, in sign-up order, without a trailing newline', () => {
    const list = buildTugenyTeamList([
      signup('Seven Sins', '2026-05-02T10:00:00Z'),
      signup('Rigor Mortis', '2026-05-01T10:00:00Z'),
    ]);

    expect(list.text).toBe('Rigor Mortis\nSeven Sins');
    expect(list.duplicates).toEqual([]);
  });

  it('leaves out pending and waitlisted teams', () => {
    const list = buildTugenyTeamList([
      signup('Rigor Mortis', '2026-05-01T10:00:00Z'),
      signup('Schatten', '2026-05-01T11:00:00Z', 'AwaitingApproval'),
      signup('Pink Pain', '2026-05-01T12:00:00Z', 'Waitlisted'),
    ]);

    expect(list.names).toEqual(['Rigor Mortis']);
  });

  it('ignores individual sign-ups, which have no team name', () => {
    expect(buildTugenyTeamList([signup(null, '2026-05-01T10:00:00Z')]).text).toBe('');
  });

  it('reports each clashing name once, ignoring case and surrounding spaces', () => {
    const list = buildTugenyTeamList([
      signup('Eclipse', '2026-05-01T10:00:00Z'),
      signup(' eclipse ', '2026-05-01T11:00:00Z'),
      signup('ECLIPSE', '2026-05-01T12:00:00Z'),
      signup('Schatten', '2026-05-01T13:00:00Z'),
    ]);

    expect(list.duplicates).toEqual(['Eclipse']);
  });

  it('is empty for no input', () => {
    expect(buildTugenyTeamList([])).toEqual({ text: '', names: [], duplicates: [] });
  });
});
