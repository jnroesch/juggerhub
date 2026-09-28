import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Translation, TranslocoService } from '@jsverse/transloco';
import { HomeNews } from '../../../core/models/home.models';
import { NewsListComponent } from './news-list.component';
import { translocoTestingModule } from '../../../../testing/transloco-testing';

// See transloco-testing.ts — a JSON default import resolves to `undefined` under this ts-jest config.
const enCatalog: Translation = require('../../../../../public/i18n/en.json');
const deCatalog: Translation = require('../../../../../public/i18n/de.json');

function item(partial: Partial<HomeNews> & Pick<HomeNews, 'body'>): HomeNews {
  return {
    source: 'team',
    sourceName: 'Rheinfeuer',
    sourceSlugOrId: 'rheinfeuer',
    createdDate: new Date().toISOString(),
    editedDate: null,
    ...partial,
  };
}

/**
 * Home shows the same "edited" marker as the page a post lives on, so a member who reads the news
 * here can tell a post changed: team news since feature 057, event and party news since 059. The
 * list marks exactly the items the server says were edited, and invents nothing.
 */
describe('NewsListComponent', () => {
  function metaLines(items: HomeNews[], lang = 'en'): string[] {
    TestBed.inject(TranslocoService).setActiveLang(lang);
    const fixture = TestBed.createComponent(NewsListComponent);
    fixture.componentRef.setInput('news', items);
    fixture.detectChanges();
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="news-list-meta"]')).map((el) =>
      (el.textContent ?? '').replace(/\s+/g, ' ').trim(),
    );
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule({ en: enCatalog, de: deCatalog })],
      providers: [provideRouter([])],
    });
  });

  it('marks an edited team post, in the reader’s language', () => {
    const edited = item({ body: 'Kit order closes Friday.', editedDate: new Date().toISOString() });

    expect(metaLines([edited])[0]).toMatch(/· edited$/);
    expect(metaLines([edited], 'de')[0]).toMatch(/· bearbeitet$/);
  });

  it('marks an edited event post and an edited party post the same way (feature 059)', () => {
    const at = new Date().toISOString();
    const lines = metaLines([
      item({ body: 'Check-in opens at 09:00.', source: 'event', sourceName: 'Rhein Cup', sourceSlugOrId: 'e1', editedDate: at }),
      item({ body: 'Meet at 07:00.', source: 'party', sourceName: 'Rheinfeuer @ Rhein Cup', sourceSlugOrId: 'e1', editedDate: at }),
    ]);

    expect(lines).toHaveLength(2);
    for (const line of lines) {
      expect(line).toMatch(/· edited$/);
    }
  });

  it('marks nothing that was never edited — team, event or party', () => {
    const lines = metaLines([
      item({ body: 'Fresh.' }),
      item({ body: 'Schedule posted.', source: 'event', sourceName: 'Rhein Cup', sourceSlugOrId: 'e1' }),
      item({ body: 'Meet at 07:00.', source: 'party', sourceName: 'Rheinfeuer @ Rhein Cup', sourceSlugOrId: 'e1' }),
    ]);

    expect(lines).toHaveLength(3);
    for (const line of lines) {
      expect(line).not.toContain('edited');
    }
  });
});
