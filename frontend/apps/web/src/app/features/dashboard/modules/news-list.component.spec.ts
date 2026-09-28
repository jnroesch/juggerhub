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
 * Feature 057 — Home shows the same "edited" marker the team page does, so a member who reads team
 * news here can tell a post changed. Only team posts can be edited; the server never sets the date
 * on event or party items, and the list must not invent a marker either.
 */
describe('NewsListComponent', () => {
  function metaLines(items: HomeNews[], lang = 'en'): string[] {
    TestBed.inject(TranslocoService).setActiveLang(lang);
    const fixture = TestBed.createComponent(NewsListComponent);
    fixture.componentRef.setInput('news', items);
    fixture.detectChanges();
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="news-list-meta"]')).map((el) =>
      el.textContent!.replace(/\s+/g, ' ').trim(),
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
