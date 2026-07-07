import { isActiveDestination, myTeamTarget } from './nav-model';
import { MyTeam } from '../core/models/home.models';

const team = (slug: string): MyTeam => ({ slug, name: slug, role: 'Member' });

describe('nav-model', () => {
  describe('isActiveDestination', () => {
    it('marks Home active only at the root', () => {
      expect(isActiveDestination('home', '/')).toBe(true);
      expect(isActiveDestination('home', '/browse')).toBe(false);
      expect(isActiveDestination('home', '/alerts')).toBe(false);
    });

    it('marks Browse active on any /browse path', () => {
      expect(isActiveDestination('browse', '/browse')).toBe(true);
      expect(isActiveDestination('browse', '/browse/teams')).toBe(true);
      expect(isActiveDestination('browse', '/browse/events?type=Tournament')).toBe(true);
      expect(isActiveDestination('browse', '/')).toBe(false);
    });

    it('marks My team active on a team space or the chooser', () => {
      expect(isActiveDestination('my-team', '/t/bloodhounds')).toBe(true);
      expect(isActiveDestination('my-team', '/my-team')).toBe(true);
      expect(isActiveDestination('my-team', '/browse')).toBe(false);
    });

    it('marks Alerts active on /alerts', () => {
      expect(isActiveDestination('alerts', '/alerts')).toBe(true);
      expect(isActiveDestination('alerts', '/')).toBe(false);
    });

    it('ignores query and fragment', () => {
      expect(isActiveDestination('home', '/?x=1')).toBe(true);
      expect(isActiveDestination('alerts', '/alerts#top')).toBe(true);
    });
  });

  describe('myTeamTarget', () => {
    it('routes a team-less player to find a team', () => {
      expect(myTeamTarget([])).toBe('/browse/teams');
    });

    it('routes a single-team player straight to their team', () => {
      expect(myTeamTarget([team('bloodhounds')])).toBe('/t/bloodhounds');
    });

    it('routes a multi-team player to the chooser', () => {
      expect(myTeamTarget([team('a'), team('b')])).toBe('/my-team');
    });
  });
});
