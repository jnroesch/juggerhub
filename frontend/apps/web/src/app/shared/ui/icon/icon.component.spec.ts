import { ComponentFixture, TestBed } from '@angular/core/testing';
import { IconComponent } from './icon.component';

describe('IconComponent (jh-icon)', () => {
  let fixture: ComponentFixture<IconComponent>;

  function mount(name: string, size?: number): ComponentFixture<IconComponent> {
    const f = TestBed.createComponent(IconComponent);
    f.componentRef.setInput('name', name);
    if (size != null) {
      f.componentRef.setInput('size', size);
    }
    f.detectChanges();
    return f;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [IconComponent] });
  });

  it('renders an inline, decorative SVG for a curated name', () => {
    fixture = mount('plus');
    const svg = fixture.nativeElement.querySelector('svg') as SVGElement;
    expect(svg).toBeTruthy();
    expect(svg.getAttribute('aria-hidden')).toBe('true');
    expect(svg.getAttribute('stroke')).toBe('currentColor');
  });

  it('sizes the SVG from the size input', () => {
    fixture = mount('search', 22);
    const svg = fixture.nativeElement.querySelector('svg') as SVGElement;
    expect(svg.getAttribute('width')).toBe('22');
    expect(svg.getAttribute('height')).toBe('22');
  });
});
