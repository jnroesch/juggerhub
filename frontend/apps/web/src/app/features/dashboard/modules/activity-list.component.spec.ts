import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Translation, TranslocoService } from '@jsverse/transloco';
import { ActivityEntry, ActivityParams } from '../../../core/models/home.models';
import { ActivityListComponent } from './activity-list.component';
import { translocoTestingModule } from '../../../../testing/transloco-testing';

// See transloco-testing.ts — a JSON default import resolves to `undefined` under this ts-jest config.
const enCatalog: Translation = require('../../../../../public/i18n/en.json');
const deCatalog: Translation = require('../../../../../public/i18n/de.json');

const NO_PARAMS: ActivityParams = {
  actorName: null,
  eventName: null,
  teamName: null,
  trainingName: null,
  badgeName: null,
  isMine: false,
  newRole: null,
  changeKind: null,
};

function entry(partial: Partial<ActivityEntry> & Pick<ActivityEntry, 'kind'>): ActivityEntry {
  return {
    linkTarget: null,
    occurredAt: new Date().toISOString(),
    params: NO_PARAMS,
    ...partial,
  };
}

/**
 * The activity feed's sentences are composed client-side so they follow the viewer's language
 * (feature 031). They used to arrive pre-rendered in English from `HomeService`, which put lines
 * like "You earned the Fair play badge" under a German "Was ist los" heading — and no catalogue
 * parity guard could catch it, because prose that never became a key can't be missing from
 * `de.json`. These tests assert the sentence in *both* languages for that reason: an English-only
 * assertion would pass against the very bug this replaced.
 */
describe('ActivityListComponent', () => {
  let fixture: ComponentFixture<ActivityListComponent>;

  function mount(items: ActivityEntry[], lang = 'en'): HTMLElement {
    TestBed.inject(TranslocoService).setActiveLang(lang);
    fixture = TestBed.createComponent(ActivityListComponent);
    fixture.componentRef.setInput('items', items);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const lines = (el: HTMLElement) =>
    Array.from(el.querySelectorAll('li')).map((li) => li.textContent!.replace(/\s+/g, ' ').trim());

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule({ en: enCatalog, de: deCatalog })],
      providers: [provideRouter([])],
    });
  });

  it('renders nothing when there is no activity', () => {
    const el = mount([]);
    expect(el.querySelector('[data-testid="activity"]')).toBeNull();
  });

  it('composes the viewer\'s own badge award in English', () => {
    const el = mount([
      entry({
        kind: 'BadgeAwarded',
        params: { ...NO_PARAMS, badgeName: 'Fair play', isMine: true },
      }),
    ]);
    expect(lines(el)[0]).toContain('You earned the Fair play badge');
  });

  it('composes the same badge award in German, with the badge name untranslated', () => {
    const el = mount(
      [
        entry({
          kind: 'BadgeAwarded',
          params: { ...NO_PARAMS, badgeName: 'Fair play', isMine: true },
        }),
      ],
      'de',
    );
    const line = lines(el)[0];
    expect(line).toContain('Du hast das Abzeichen');
    expect(line).toContain('Fair play'); // user data — never translated
    expect(line).not.toContain('earned');
  });

  it('uses the third-person sentence for a teammate\'s badge', () => {
    const el = mount([
      entry({
        kind: 'BadgeAwarded',
        params: { ...NO_PARAMS, actorName: 'Mira', badgeName: 'Fair play' },
      }),
    ]);
    expect(lines(el)[0]).toContain('Mira earned the Fair play badge');
  });

  it('substitutes a translated stand-in when the player has no display name', () => {
    const el = mount(
      [
        entry({
          kind: 'NewTeamMember',
          params: { ...NO_PARAMS, actorName: null, teamName: 'Ravens' },
        }),
      ],
      'de',
    );
    // The old server-composed feed hard-coded "Someone" here, in every language.
    expect(lines(el)[0]).toContain('Jemand ist Ravens beigetreten');
  });

  it.each([
    ['TeammateJoinedEvent', { eventName: 'Open scrim' }, 'signed up for Open scrim'],
    ['PartyMemberJoined', { actorName: 'Ana', eventName: 'Summer Cup' }, 'Ana joined the party for Summer Cup'],
    ['RoleChanged', { teamName: 'Ravens', newRole: 'Admin' }, 'Your role in Ravens is now admin'],
    ['RoleChanged', { teamName: 'Ravens', newRole: 'Member' }, 'Your role in Ravens is now member'],
    ['TrainingChanged', { trainingName: 'Tuesday drills', changeKind: 'Cancelled' }, 'Tuesday drills was cancelled'],
    ['TrainingChanged', { trainingName: 'Tuesday drills', changeKind: 'Updated' }, 'Tuesday drills was updated'],
  ])('composes %s', (kind, params, expected) => {
    const el = mount([
      entry({ kind: kind as ActivityEntry['kind'], params: { ...NO_PARAMS, ...(params as Partial<ActivityParams>) } }),
    ]);
    expect(lines(el)[0]).toContain(expected);
  });

  it('falls back to a generic sentence when the role-change payload carried no team', () => {
    const el = mount([entry({ kind: 'RoleChanged', params: { ...NO_PARAMS, newRole: 'Admin' } })]);
    expect(lines(el)[0]).toContain('Your team role changed');
  });

  it('falls back to a generic sentence when a changed training has no name', () => {
    const el = mount([entry({ kind: 'TrainingChanged', params: { ...NO_PARAMS, changeKind: 'Cancelled' } })]);
    expect(lines(el)[0]).toContain('A training was cancelled');
  });

  it('links a badge award to the awarded player\'s profile', () => {
    const el = mount([
      entry({
        kind: 'BadgeAwarded',
        linkTarget: 'mira',
        params: { ...NO_PARAMS, actorName: 'Mira', badgeName: 'Fair play' },
      }),
    ]);
    expect(el.querySelector('a')!.getAttribute('href')).toBe('/u/mira');
  });

  it('re-composes the sentence AND the timestamp when the language changes at runtime', () => {
    const twoHoursAgo = new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString();
    const el = mount([
      entry({
        kind: 'BadgeAwarded',
        occurredAt: twoHoursAgo,
        params: { ...NO_PARAMS, badgeName: 'Fair play', isMine: true },
      }),
    ]);
    expect(lines(el)[0]).toContain('You earned');
    expect(lines(el)[0]).toContain('hr. ago');

    TestBed.inject(TranslocoService).setActiveLang('de');
    fixture.detectChanges();

    // The timestamp is `Intl`-formatted, not a catalogue key — it must follow the switch too.
    expect(lines(el)[0]).toContain('Du hast das Abzeichen');
    expect(lines(el)[0]).toContain('vor 2 Std.');
  });
});
