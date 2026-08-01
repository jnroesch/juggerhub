import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { provideTranslocoLocale } from '@jsverse/transloco-locale';
import { PRIVACY_SECTIONS, PrivacyComponent } from './privacy/privacy.component';
import { IMPRINT_SECTIONS, ImprintComponent } from './imprint/imprint.component';
import { translocoTestingModule } from '../../../testing/transloco-testing';

const legalEn = require('../../../../public/i18n/legal/en.json');
const legalDe = require('../../../../public/i18n/legal/de.json');
const legalEs = require('../../../../public/i18n/legal/es.json');

/** Root catalogs, so switching language in a test does not send Transloco looking for one. */
const rootLangs = {
  en: require('../../../../public/i18n/en.json'),
  de: require('../../../../public/i18n/de.json'),
  es: require('../../../../public/i18n/es.json'),
};

const CATALOGS: Record<string, unknown> = { en: legalEn, de: legalDe, es: legalEs };

describe('Legal pages (feature 036)', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule(rootLangs)],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslocoLocale({ langToLocaleMapping: { en: 'en-GB', de: 'de-DE', es: 'es-ES' } }),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  /** Creates the page and answers its content fetch for the active (English) language. */
  function render<T>(component: new (...args: never[]) => T): ComponentFixture<T> {
    const fixture = TestBed.createComponent(component);
    fixture.detectChanges();

    httpMock.expectOne('/i18n/legal/en.json').flush(legalEn);
    fixture.detectChanges();

    return fixture;
  }

  /**
   * Switches language on an already-rendered page and answers the follow-up fetch. This is the
   * real journey — the switcher sits on the page itself — and it also proves PC-6: the document
   * re-renders in place rather than navigating away.
   */
  function switchTo(fixture: ComponentFixture<unknown>, lang: 'de' | 'es'): void {
    TestBed.inject(TranslocoService).setActiveLang(lang);
    fixture.detectChanges();

    httpMock.expectOne(`/i18n/legal/${lang}.json`).flush(CATALOGS[lang]);
    fixture.detectChanges();
  }

  function el(fixture: ComponentFixture<unknown>, testId: string): HTMLElement | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
  }

  describe('privacy policy', () => {
    it('renders every section in the declared reading order', () => {
      const fixture = render(PrivacyComponent);
      const headings: string[] = Array.from(fixture.nativeElement.querySelectorAll('section h2')).map((h) =>
        (h as HTMLElement).textContent!.trim(),
      );

      expect(headings.length).toBe(PRIVACY_SECTIONS.length);
      expect(headings[0]).toBe(legalEn.privacy.sections.controller.heading);
      expect(headings[headings.length - 1]).toBe(legalEn.privacy.sections.objection.heading);
    });

    /**
     * The page renders only sections named in the declared order, which is what stops a reordered
     * catalog from reshuffling a legal document — but it also means a section added to the catalog
     * and forgotten here would silently never render. In a privacy policy that is a disclosure
     * that quietly does not happen. (This is not hypothetical: it was caught exactly this way when
     * the `eventContacts` section was added.)
     */
    it('declares every section that exists in the catalog', () => {
      const inCatalog = Object.keys(legalEn.privacy.sections).sort();
      const declared = [...PRIVACY_SECTIONS].sort();

      expect(declared).toEqual(inCatalog);
    });

    /**
     * FR-006 / SC-003, and the one thing in this document that must stay specific.
     *
     * The policy is otherwise written in general categories on purpose, so it survives new
     * features without an edit. This disclosure is the exception: recording page addresses
     * verbatim means the analytics store names the profiles and teams that were viewed, and that
     * is the entire reason issue #92 exists. A generic "we collect usage data" would hide exactly
     * what the feature was created to reveal. Asserted on substance, not on a heading.
     */
    it('discloses that page addresses are recorded verbatim, naming the profile or team viewed', () => {
      const fixture = render(PrivacyComponent);
      const text: string = fixture.nativeElement.textContent.toLowerCase();

      expect(text).toContain('exactly as it is');
      expect(text).toContain("contains that member's or team's name");
      expect(text).toContain('which profiles and team pages were looked at');
      // The counterpart matters as much: the viewer is never identified.
      expect(text).toContain('never shows who was looking');
    });

    /**
     * FR-009: the policy must describe a route that is actually honoured. "Write to us and we'll
     * take care of it" stays true whether or not a self-service control exists later — unlike the
     * earlier wording, which asserted no such control existed and would have dated the moment one
     * shipped (#105).
     */
    it('gives a working route for exercising rights', () => {
      const fixture = render(PrivacyComponent);
      const text: string = fixture.nativeElement.textContent;

      expect(text).toContain('hello@juggerhub.com');
      expect(text.toLowerCase()).toContain("we'll take care of it");
    });

    /**
     * The maintainability decision itself, guarded. An earlier draft enumerated every feature by
     * name, so shipping a feature silently dated a legally binding document in three languages.
     * These names are the ones most likely to creep back in.
     */
    it('describes categories rather than enumerating product features', () => {
      const fixture = render(PrivacyComponent);
      const text: string = fixture.nativeElement.textContent.toLowerCase();

      for (const featureName of ['marketplace', 'mercenary', 'badge', 'achievement', 'pompfen', 'party']) {
        expect(text).not.toContain(featureName);
      }
    });

    /** PC-3: the heading hierarchy is the screen-reader navigation. It must not skip a level. */
    it('has an unbroken heading hierarchy', () => {
      const fixture = render(PrivacyComponent);
      const levels: number[] = Array.from(fixture.nativeElement.querySelectorAll('h1,h2,h3,h4')).map((h) =>
        Number((h as HTMLElement).tagName.slice(1)),
      );

      expect(levels[0]).toBe(1);
      levels.slice(1).forEach((level, i) => expect(level - levels[i]).toBeLessThanOrEqual(1));
    });

    it('offers a table of contents linking to stable section anchors', () => {
      const fixture = render(PrivacyComponent);
      const toc = el(fixture, 'legal-toc');
      const first = toc!.querySelector('a') as HTMLAnchorElement;

      expect(first.getAttribute('href')).toBe('#privacy-controller');
      expect(fixture.nativeElement.querySelector('#privacy-controller')).not.toBeNull();
    });

    it('links to the imprint (FR-016)', () => {
      const fixture = render(PrivacyComponent);

      expect(el(fixture, 'legal-sibling-link')?.getAttribute('href')).toBe('/imprint');
    });

    /** Constitution I: paragraphs are catalog array entries, so there is no sink to sanitise. */
    it('uses no innerHTML binding', () => {
      const template = require('fs').readFileSync(`${__dirname}/legal-page.component.html`, 'utf-8');

      expect(template).not.toContain('innerHTML');
    });
  });

  describe('imprint', () => {
    it('renders its sections and links back to the privacy policy', () => {
      const fixture = render(ImprintComponent);

      expect(el(fixture, 'legal-imprint')).not.toBeNull();
      expect(fixture.nativeElement.querySelectorAll('section h2').length).toBe(IMPRINT_SECTIONS.length);
      expect(el(fixture, 'legal-sibling-link')?.getAttribute('href')).toBe('/privacy');
    });

    it('declares every section that exists in the catalog', () => {
      expect([...IMPRINT_SECTIONS].sort()).toEqual(Object.keys(legalEn.imprint.sections).sort());
    });

    it('has no table of contents — it is short enough not to need one', () => {
      const fixture = render(ImprintComponent);

      expect(el(fixture, 'legal-toc')).toBeNull();
    });
  });

  describe('authoritative language (FR-019, DM-3)', () => {
    it('tells an English reader that the German version governs', () => {
      const fixture = render(PrivacyComponent);

      expect(el(fixture, 'legal-authoritative-notice')?.textContent).toContain('German version is the binding one');
    });

    it('tells a Spanish reader the same, in Spanish', () => {
      const fixture = render(PrivacyComponent);
      switchTo(fixture, 'es');

      expect(el(fixture, 'legal-authoritative-notice')?.textContent).toContain('versión alemana es la vinculante');
    });

    /** A reader of the German text is reading the binding version; the notice would be noise. */
    it('does not show the notice on the German version', () => {
      const fixture = render(PrivacyComponent);
      switchTo(fixture, 'de');

      expect(el(fixture, 'legal-authoritative-notice')).toBeNull();
    });

    /** PC-6 / 031 FR-004: the switch swaps the text in place, on the same page. */
    it('re-renders the document in the new language without navigating away', () => {
      const fixture = render(PrivacyComponent);
      expect(el(fixture, 'legal-privacy')?.textContent).toContain(legalEn.privacy.sections.analytics.heading);

      switchTo(fixture, 'de');

      expect(el(fixture, 'legal-privacy')).not.toBeNull();
      expect(el(fixture, 'legal-privacy')?.textContent).toContain(legalDe.privacy.sections.analytics.heading);
      expect(el(fixture, 'legal-meta')?.textContent).toContain('Zuletzt aktualisiert');
    });
  });

  describe('failure handling (PC-7)', () => {
    /**
     * A blank privacy policy reads as a policy that says nothing — worse than an honest error.
     * This is also why the document is fetched directly rather than as a Transloco scope: a
     * failed scope load would have rendered the ENGLISH text inside the German document.
     */
    it('shows a visible error instead of an empty document', () => {
      const fixture = TestBed.createComponent(PrivacyComponent);
      fixture.detectChanges();

      httpMock.expectOne('/i18n/legal/en.json').error(new ProgressEvent('network error'));
      fixture.detectChanges();

      expect(el(fixture, 'legal-error')).not.toBeNull();
      expect(el(fixture, 'legal-privacy')).toBeNull();
    });

    it('can retry after a failure', () => {
      const fixture = TestBed.createComponent(PrivacyComponent);
      fixture.detectChanges();

      httpMock.expectOne('/i18n/legal/en.json').error(new ProgressEvent('network error'));
      fixture.detectChanges();

      (el(fixture, 'legal-retry') as HTMLButtonElement).click();
      fixture.detectChanges();

      httpMock.expectOne('/i18n/legal/en.json').flush(legalEn);
      fixture.detectChanges();

      expect(el(fixture, 'legal-error')).toBeNull();
      expect(el(fixture, 'legal-privacy')).not.toBeNull();
    });
  });
});
