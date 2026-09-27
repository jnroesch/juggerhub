import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Translation, TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { translocoTestingModule } from '../../../testing/transloco-testing';
import { PluralKeyPipe } from './plural-key.pipe';
import { isPluralNode, pluralCategory, resolvePluralKey, translatePlural } from './plural';

/**
 * A catalogue per rule shape: English and German have two forms, Spanish adds `many` for a round
 * million (left out here on purpose, as it is in the real catalogue), Polish has four.
 */
const en: Translation = {
  x: { items: { one: '{{count}} item', other: '{{count}} items' } },
  y: { one: '{{who}}: {{count}} thing', other: '{{who}}: {{count}} things' },
};
const es: Translation = { x: { items: { one: '{{count}} objeto', other: '{{count}} objetos' } } };
const pl: Translation = {
  x: {
    items: {
      one: '{{count}} przedmiot',
      few: '{{count}} przedmioty',
      many: '{{count}} przedmiotów',
      other: '{{count}} przedmiotu',
    },
  },
};

function configure(): TranslocoService {
  TestBed.configureTestingModule({
    imports: [
      translocoTestingModule(
        { en, es, pl },
        {
          translocoConfig: {
            availableLangs: ['en', 'es', 'pl'],
            defaultLang: 'en',
            fallbackLang: 'en',
            reRenderOnLangChange: true,
          },
        },
      ),
    ],
  });
  return TestBed.inject(TranslocoService);
}

describe('plural (GH #338)', () => {
  describe('pluralCategory', () => {
    it('follows each language’s CLDR rules rather than "is it 1"', () => {
      expect(pluralCategory('en', 1)).toBe('one');
      expect(pluralCategory('en', 0)).toBe('other');
      expect(pluralCategory('de', 1)).toBe('one');
      expect(pluralCategory('de', 2)).toBe('other');
      // The three shapes the two-form ternary could never produce.
      expect(pluralCategory('fr', 0)).toBe('one');
      expect(pluralCategory('pl', 3)).toBe('few');
      expect(pluralCategory('pl', 5)).toBe('many');
      expect(pluralCategory('pl', 22)).toBe('few');
      expect(pluralCategory('ar', 2)).toBe('two');
    });
  });

  describe('resolvePluralKey / translatePlural', () => {
    it('names the form the active language needs', () => {
      const t = configure();

      expect(resolvePluralKey(t, 'x.items', 1)).toBe('x.items.one');
      expect(translatePlural(t, 'x.items', 1)).toBe('1 item');
      expect(translatePlural(t, 'x.items', 4)).toBe('4 items');

      t.setActiveLang('pl');
      expect(translatePlural(t, 'x.items', 1)).toBe('1 przedmiot');
      expect(translatePlural(t, 'x.items', 3)).toBe('3 przedmioty');
      expect(translatePlural(t, 'x.items', 5)).toBe('5 przedmiotów');
    });

    it('falls back to `other` for a category the catalogue has no form for', () => {
      const t = configure();
      t.setActiveLang('es');

      // Spanish selects `many` for a round million; no JuggerHub count gets there, so the Spanish
      // catalogue carries no `many` forms and `other` stands in.
      expect(pluralCategory('es', 1_000_000)).toBe('many');
      expect(resolvePluralKey(t, 'x.items', 1_000_000)).toBe('x.items.other');
      expect(translatePlural(t, 'x.items', 1_000_000)).toBe('1000000 objetos');
    });

    it('passes the count through alongside any other params', () => {
      const t = configure();

      expect(translatePlural(t, 'y', 2, { who: 'Lena' })).toBe('Lena: 2 things');
    });
  });

  describe('PluralKeyPipe', () => {
    @Component({
      imports: [PluralKeyPipe, TranslocoPipe],
      template: `<p>{{ 'x.items' | pluralKey: n() | transloco: { count: n() } }}</p>`,
    })
    class HostComponent {
      readonly n = signal(1);
    }

    it('re-chooses the form when the count changes and when the language switches', () => {
      const t = configure();
      const fixture = TestBed.createComponent(HostComponent);
      const text = () => (fixture.nativeElement as HTMLElement).querySelector('p')?.textContent;

      fixture.detectChanges();
      expect(text()).toBe('1 item');

      fixture.componentInstance.n.set(2);
      fixture.detectChanges();
      expect(text()).toBe('2 items');

      // Same template, a four-form language: no call site change is needed to get it right.
      t.setActiveLang('pl');
      fixture.detectChanges();
      expect(text()).toBe('2 przedmioty');

      fixture.componentInstance.n.set(5);
      fixture.detectChanges();
      expect(text()).toBe('5 przedmiotów');
    });
  });

  describe('isPluralNode', () => {
    it('recognises an object keyed only by plural categories', () => {
      expect(isPluralNode({ one: 'a', other: 'b' })).toBe(true);
      expect(isPluralNode({ one: 'a', few: 'b', many: 'c', other: 'd' })).toBe(true);
    });

    it('leaves ordinary nesting alone, including one that happens to have an `other` key', () => {
      // events.type = { tournament, workshop, other } is a real catalogue node.
      expect(isPluralNode({ tournament: 'a', workshop: 'b', other: 'c' })).toBe(false);
      expect(isPluralNode({})).toBe(false);
      expect(isPluralNode('members')).toBe(false);
      expect(isPluralNode(['one'])).toBe(false);
    });
  });
});
