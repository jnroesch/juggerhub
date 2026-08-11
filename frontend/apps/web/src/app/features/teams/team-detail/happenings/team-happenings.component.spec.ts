import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Translation, TranslocoService } from '@jsverse/transloco';
import { TeamHappening, TeamHappeningParams } from '../../../../core/models/team.models';
import { TeamHappeningsComponent } from './team-happenings.component';
import { translocoTestingModule } from '../../../../../testing/transloco-testing';

// See transloco-testing.ts — a JSON default import resolves to `undefined` under this ts-jest config.
const enCatalog: Translation = require('../../../../../../public/i18n/en.json');
const deCatalog: Translation = require('../../../../../../public/i18n/de.json');
const esCatalog: Translation = require('../../../../../../public/i18n/es.json');

const NO_PARAMS: TeamHappeningParams = {
  actorName: null,
  recognitionName: null,
  trainingName: null,
  sessionDate: null,
};

function happening(partial: Partial<TeamHappening> & Pick<TeamHappening, 'kind'>): TeamHappening {
  return {
    linkTarget: null,
    occurredAt: new Date().toISOString(),
    params: NO_PARAMS,
    ...partial,
  };
}

/**
 * "What's happening" (feature 044) composes its sentences client-side so they follow the viewer's
 * language (feature 031) — the same reason the dashboard's activity list does. Sentences are
 * asserted in more than one language on purpose: an English-only assertion would pass against a
 * server-rendered-prose implementation, which is exactly the defect this shape exists to prevent.
 */
describe('TeamHappeningsComponent', () => {
  let fixture: ComponentFixture<TeamHappeningsComponent>;

  function mount(items: TeamHappening[], lang = 'en'): HTMLElement {
    TestBed.inject(TranslocoService).setActiveLang(lang);
    fixture = TestBed.createComponent(TeamHappeningsComponent);
    fixture.componentRef.setInput('items', items);
    fixture.componentRef.setInput('slug', 'rheinfeuer');
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const lines = (el: HTMLElement) =>
    Array.from(el.querySelectorAll('li')).map((li) => li.textContent!.replace(/\s+/g, ' ').trim());

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule({ en: enCatalog, de: deCatalog, es: esCatalog })],
      providers: [provideRouter([])],
    });
  });

  it('renders a sentence for every kind', () => {
    const el = mount([
      happening({ kind: 'MemberJoined', params: { ...NO_PARAMS, actorName: 'Nik' }, linkTarget: 'nik' }),
      happening({ kind: 'RecognitionAwarded', params: { ...NO_PARAMS, recognitionName: 'Fair play' } }),
      happening({ kind: 'TrainingSeriesCreated', params: { ...NO_PARAMS, trainingName: 'Tuesday practice' } }),
      happening({
        kind: 'TrainingSessionCancelled',
        params: { ...NO_PARAMS, trainingName: 'Tuesday practice', sessionDate: '2026-08-18' },
        linkTarget: 'a2f0f1d0-0000-7000-8000-000000000000',
      }),
    ]);

    const text = lines(el).join(' | ');
    expect(text).toContain('Nik joined the team');
    expect(text).toContain('The team earned Fair play');
    expect(text).toContain('Tuesday practice was added to the training schedule');
    expect(text).toMatch(/Tuesday practice on .+ was cancelled/);
  });

  it('composes the sentence in the active language, not English', () => {
    const items = [happening({ kind: 'MemberJoined', params: { ...NO_PARAMS, actorName: 'Nik' } })];

    expect(lines(mount(items, 'de')).join(' ')).toContain('Nik ist dem Team beigetreten');
    expect(lines(mount(items, 'es')).join(' ')).toContain('Nik se unió al equipo');
  });

  /**
   * A banned or deleted player's name is suppressed server-side and arrives as null. The stand-in
   * must come from the catalogue — an English "Someone" inside a German page is the bug.
   */
  it('substitutes a translated stand-in when the player has no name', () => {
    const items = [happening({ kind: 'MemberJoined' })];

    expect(lines(mount(items, 'en')).join(' ')).toContain('Someone joined the team');
    expect(lines(mount(items, 'de')).join(' ')).toContain('Jemand ist dem Team beigetreten');
  });

  it('links each kind to its destination and leaves awards unlinked', () => {
    const el = mount([
      happening({ kind: 'MemberJoined', params: { ...NO_PARAMS, actorName: 'Nik' }, linkTarget: 'nik' }),
      happening({ kind: 'TrainingSeriesCreated', params: { ...NO_PARAMS, trainingName: 'Tuesday practice' } }),
      happening({ kind: 'RecognitionAwarded', params: { ...NO_PARAMS, recognitionName: 'Fair play' } }),
    ]);

    const hrefs = Array.from(el.querySelectorAll('a')).map((a) => a.getAttribute('href'));
    expect(hrefs).toContain('/u/nik');
    // A series has no page of its own — the team's trainings tab is the nearest honest destination.
    expect(hrefs).toContain('/t/rheinfeuer/trainings');
    // The award's home is the Badges & achievements card on the same page, so it is plain text.
    expect(hrefs).toHaveLength(2);
  });

  /** A newer server may send a kind this build does not know. Drop it, never render a blank line. */
  it('drops an unrecognised kind rather than rendering an empty row', () => {
    const el = mount([
      happening({ kind: 'MemberJoined', params: { ...NO_PARAMS, actorName: 'Nik' } }),
      happening({ kind: 'SomethingNewEntirely' as TeamHappening['kind'] }),
    ]);

    expect(lines(el)).toHaveLength(1);
  });

  /**
   * FR-014 — the deliberate divergence from the dashboard's activity list, which renders nothing at
   * all when empty. A member must see that the section exists and is simply quiet.
   */
  it('renders an empty state rather than disappearing when there is nothing lately', () => {
    const el = mount([]);

    expect(el.querySelector('[data-testid="happenings"]')).not.toBeNull();
    expect(el.textContent).toContain('Nothing lately');
    expect(el.querySelectorAll('li')).toHaveLength(0);
  });

  it('is read-only — no buttons or action affordances', () => {
    const el = mount([happening({ kind: 'MemberJoined', params: { ...NO_PARAMS, actorName: 'Nik' } })]);

    expect(el.querySelectorAll('button')).toHaveLength(0);
    expect(el.querySelectorAll('input')).toHaveLength(0);
  });
});
