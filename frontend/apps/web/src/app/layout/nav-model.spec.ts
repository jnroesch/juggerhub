import { NAV_DESTINATIONS, badgeText, isActiveDestination, myTeamTarget } from './nav-model';
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

    it('marks Chat active on the inbox and on an open conversation', () => {
      expect(isActiveDestination('chat', '/chat')).toBe(true);
      expect(isActiveDestination('chat', '/chat/0198e1f2-0000-7000-8000-000000000001')).toBe(true);
      expect(isActiveDestination('chat', '/')).toBe(false);
      expect(isActiveDestination('chat', '/alerts')).toBe(false);
    });

    it('ignores query and fragment', () => {
      expect(isActiveDestination('home', '/?x=1')).toBe(true);
      expect(isActiveDestination('alerts', '/alerts#top')).toBe(true);
      expect(isActiveDestination('chat', '/chat#latest')).toBe(true);
    });
  });

  describe('NAV_DESTINATIONS', () => {
    it('exposes Chat as a top-level destination (feature 019)', () => {
      const chat = NAV_DESTINATIONS.find((d) => d.id === 'chat');
      expect(chat).toEqual({ id: 'chat', label: 'Chat', path: '/chat' });
    });

    it('labels every destination in sentence case (DESIGN.md)', () => {
      for (const d of NAV_DESTINATIONS) {
        expect(d.label).not.toEqual(d.label.toUpperCase());
      }
    });
  });

  describe('badgeText', () => {
    // Shared by the Alerts bell (010) and the Chat destination (019) so two badges in the same nav
    // cannot cap differently.
    it('is empty when nothing is unread', () => {
      expect(badgeText(0)).toBe('');
      expect(badgeText(-1)).toBe('');
    });

    it('shows the count up to 9 and caps beyond', () => {
      expect(badgeText(1)).toBe('1');
      expect(badgeText(9)).toBe('9');
      expect(badgeText(10)).toBe('9+');
      expect(badgeText(1200)).toBe('9+');
    });
  });

  describe('myTeamTarget', () => {
    it('routes a team-less player to the "My team" home (feature 023)', () => {
      expect(myTeamTarget([])).toBe('/my-team');
    });

    it('routes a single-team player straight to their team', () => {
      expect(myTeamTarget([team('bloodhounds')])).toBe('/t/bloodhounds');
    });

    it('routes a multi-team player to the chooser', () => {
      expect(myTeamTarget([team('a'), team('b')])).toBe('/my-team');
    });
  });
});
