import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LegalLinksComponent } from './legal-links.component';
import { translocoTestingModule } from '../../../../testing/transloco-testing';

describe('LegalLinksComponent (jh-legal-links)', () => {
  let fixture: ComponentFixture<LegalLinksComponent>;

  function create(variant: 'footer' | 'inline' = 'footer') {
    fixture = TestBed.createComponent(LegalLinksComponent);
    fixture.componentRef.setInput('variant', variant);
    fixture.detectChanges();
    return fixture;
  }

  function link(which: 'privacy' | 'imprint'): HTMLAnchorElement | null {
    return fixture.nativeElement.querySelector(`[data-testid="legal-link-${which}"]`);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [provideRouter([])],
    });
  });

  it('links to both documents', () => {
    create();

    expect(link('privacy')?.getAttribute('href')).toBe('/privacy');
    expect(link('imprint')?.getAttribute('href')).toBe('/imprint');
  });

  it('renders the translated labels', () => {
    create();

    expect(link('privacy')?.textContent?.trim()).toBe('Privacy');
    expect(link('imprint')?.textContent?.trim()).toBe('Imprint');
  });

  /** Both placements must carry both links — the inline variant only drops the © line. */
  it('renders both links in the inline variant too', () => {
    create('inline');

    expect(link('privacy')).not.toBeNull();
    expect(link('imprint')).not.toBeNull();
  });

  it('is a labelled landmark so it is reachable by assistive tech', () => {
    create();
    const nav: HTMLElement = fixture.nativeElement.querySelector('[data-testid="legal-links"]');

    expect(nav.tagName).toBe('NAV');
    expect(nav.getAttribute('aria-label')).toBeTruthy();
  });

  /**
   * DESIGN.md → Long-form content: links in prose are underlined, unlike navigation links
   * elsewhere. Colour alone is a weak affordance and none at all for a colour-blind reader.
   */
  it('underlines the links', () => {
    create();

    expect(link('privacy')?.className).toContain('underline');
    expect(link('imprint')?.className).toContain('underline');
  });
});
