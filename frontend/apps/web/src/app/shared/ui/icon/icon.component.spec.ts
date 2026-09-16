import { ComponentFixture, TestBed } from '@angular/core/testing';
import { IconComponent, IconSize } from './icon.component';
import { ICONS } from './icons';

describe('IconComponent (jh-icon)', () => {
  let fixture: ComponentFixture<IconComponent>;

  function mount(name: string, size?: IconSize): ComponentFixture<IconComponent> {
    const f = TestBed.createComponent(IconComponent);
    f.componentRef.setInput('name', name);
    if (size != null) {
      f.componentRef.setInput('size', size);
    }
    f.detectChanges();
    return f;
  }

  function svgOf(f: ComponentFixture<IconComponent>): SVGElement {
    return f.nativeElement.querySelector('svg') as SVGElement;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [IconComponent] });
  });

  it('renders an inline, decorative SVG for a curated name', () => {
    fixture = mount('plus');
    const svg = svgOf(fixture);
    expect(svg).toBeTruthy();
    expect(svg.getAttribute('aria-hidden')).toBe('true');
    expect(svg.getAttribute('stroke')).toBe('currentColor');
  });

  it('draws the three size steps at 16 / 18 / 22, with md the default', () => {
    for (const [size, px] of [['sm', '16'], ['md', '18'], ['lg', '22']] as const) {
      const svg = svgOf(mount('search', size));
      expect([svg.getAttribute('width'), svg.getAttribute('height')]).toEqual([px, px]);
    }

    expect(svgOf(mount('search')).getAttribute('width')).toBe('18');
  });

  /*
   * The whole point of the primitive (GH #300): the templates held 99 hand-inlined SVGs at five
   * stroke widths — 1.6 beside 2.0 beside 3.0 — which is what read as "assembled" in #278. Every
   * glyph in the map is drawn at one weight, on one viewBox, or the consolidation bought nothing.
   */
  it('draws every curated glyph at one weight on one viewBox', () => {
    for (const name of Object.keys(ICONS)) {
      const svg = svgOf(mount(name));
      expect(`${name}: ${svg.getAttribute('stroke-width')} ${svg.getAttribute('viewBox')}`).toBe(
        `${name}: 2 0 0 24 24`,
      );
      expect(svg.childElementCount).toBeGreaterThan(0);
    }
  });
});
