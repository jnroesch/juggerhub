import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ShowcaseImage } from '../../core/models/showcase.models';
import { ShowcaseService } from '../../core/services/showcase.service';
import { ShowcaseGalleryComponent } from './showcase-gallery.component';
import { translocoTestingModule } from '../../../testing/transloco-testing';

function image(id: string, caption: string | null = null, position = 0): ShowcaseImage {
  return { id, caption, position };
}

describe('ShowcaseGalleryComponent (jh-showcase-gallery)', () => {
  let fixture: ComponentFixture<ShowcaseGalleryComponent>;

  const showcase = {
    imageUrl: jest.fn((_owner: unknown, id: string) => `/api/v1/profiles/ada/showcase/${id}/image`),
  };

  function el(testId: string): HTMLElement | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
  }

  function setImages(images: ShowcaseImage[]): void {
    fixture.componentRef.setInput('images', images);
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ShowcaseGalleryComponent, translocoTestingModule()],
      providers: [{ provide: ShowcaseService, useValue: showcase }],
    });

    fixture = TestBed.createComponent(ShowcaseGalleryComponent);
    fixture.componentRef.setInput('owner', { kind: 'profile', handle: 'ada' });
    fixture.componentRef.setInput('images', []);
    fixture.componentRef.setInput('ownerName', 'Ada');
    fixture.detectChanges();
  });

  it('renders nothing at all for an empty gallery — an empty frame would promise pictures that are not there', () => {
    expect(el('showcase-strip')).toBeNull();
    expect(el('showcase-viewer')).toBeNull();
  });

  it('renders one thumbnail per picture, in the order given', () => {
    setImages([image('a', null, 0), image('b', 'Tempelhof', 1), image('c', null, 2)]);

    const thumbs = fixture.nativeElement.querySelectorAll('[data-testid^="showcase-thumb-"]');
    expect(thumbs).toHaveLength(3);
    expect(thumbs[1].querySelector('img').getAttribute('src')).toContain('/showcase/b/image');
  });

  it('uses the caption as the text alternative, and a generic one when there is none', () => {
    setImages([image('a', 'Tempelhofer Feld', 0), image('b', null, 1)]);

    const alts = Array.from(
      fixture.nativeElement.querySelectorAll('[data-testid^="showcase-thumb-"] img'),
    ).map((img) => (img as HTMLImageElement).getAttribute('alt'));

    expect(alts[0]).toBe('Tempelhofer Feld');
    expect(alts[1]).not.toBe('');
    expect(alts[1]).not.toBeNull();
  });

  it('opens the enlarged view, pages through it, and stops at both ends', () => {
    setImages([image('a', null, 0), image('b', null, 1)]);

    (el('showcase-thumb-0') as HTMLElement).click();
    fixture.detectChanges();
    expect(el('showcase-viewer')).not.toBeNull();
    expect((el('showcase-previous') as HTMLButtonElement).disabled).toBe(true);

    (el('showcase-next') as HTMLElement).click();
    fixture.detectChanges();
    expect((el('showcase-next') as HTMLButtonElement).disabled).toBe(true);
    expect((el('showcase-previous') as HTMLButtonElement).disabled).toBe(false);
  });

  it('pages with the arrow keys, closes on Escape, and returns focus to the thumbnail', () => {
    setImages([image('a', null, 0), image('b', null, 1)]);

    const opener = el('showcase-thumb-0') as HTMLElement;
    opener.focus();
    opener.click();
    fixture.detectChanges();

    const viewer = el('showcase-viewer') as HTMLElement;
    viewer.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    fixture.detectChanges();
    expect((el('showcase-next') as HTMLButtonElement).disabled).toBe(true);

    viewer.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(el('showcase-viewer')).toBeNull();
    expect(document.activeElement).toBe(opener);
  });

  it('shows each picture whole inside its frame rather than cropping it to a tile', () => {
    // The Fit processing profile exists so a panorama survives upload intact; a cover-cropped
    // thumbnail would undo that at the last step.
    setImages([image('a', null, 0)]);

    const img = fixture.nativeElement.querySelector('[data-testid="showcase-thumb-0"] img') as HTMLElement;
    expect(img.classList).toContain('object-contain');
    expect(img.classList).not.toContain('object-cover');
  });

  it('is a labelled, keyboard-focusable scroll region', () => {
    setImages([image('a', null, 0), image('b', null, 1)]);

    const strip = el('showcase-strip') as HTMLElement;
    expect(strip.getAttribute('tabindex')).toBe('0');
    expect(strip.getAttribute('aria-label')).toBeTruthy();
    // The native list role is deliberately left in place: it is what makes a screen reader
    // announce how many pictures there are. `role="group"` would suppress that, and putting the
    // count in the label instead would have to read "1 pictures".
    expect(strip.tagName).toBe('UL');
    expect(strip.hasAttribute('role')).toBe(false);
    expect(strip.querySelectorAll('li')).toHaveLength(2);
  });

  it('offers no scroll arrows when everything already fits', () => {
    // jsdom reports zero geometry, which is exactly the "nothing to scroll to" case: an arrow that
    // does nothing is worse than no arrow.
    setImages([image('a', null, 0), image('b', null, 1)]);

    expect(el('showcase-strip-controls')).toBeNull();
  });

  it('offers arrows once the strip overflows, disabled at the end it has reached', () => {
    setImages([image('a', null, 0), image('b', null, 1), image('c', null, 2)]);

    const strip = el('showcase-strip') as HTMLElement;
    Object.defineProperty(strip, 'scrollWidth', { value: 900, configurable: true });
    Object.defineProperty(strip, 'clientWidth', { value: 300, configurable: true });
    strip.dispatchEvent(new Event('scroll'));
    fixture.detectChanges();

    expect(el('showcase-strip-controls')).not.toBeNull();
    expect((el('showcase-strip-previous') as HTMLButtonElement).disabled).toBe(true);
    expect((el('showcase-strip-next') as HTMLButtonElement).disabled).toBe(false);

    strip.scrollLeft = 600;
    strip.dispatchEvent(new Event('scroll'));
    fixture.detectChanges();

    expect((el('showcase-strip-previous') as HTMLButtonElement).disabled).toBe(false);
    expect((el('showcase-strip-next') as HTMLButtonElement).disabled).toBe(true);
  });

  it('scrolls the strip by roughly a screenful when an arrow is used', () => {
    setImages([image('a', null, 0), image('b', null, 1), image('c', null, 2)]);

    const strip = el('showcase-strip') as HTMLElement;
    Object.defineProperty(strip, 'scrollWidth', { value: 900, configurable: true });
    Object.defineProperty(strip, 'clientWidth', { value: 300, configurable: true });
    strip.scrollBy = jest.fn();
    strip.dispatchEvent(new Event('scroll'));
    fixture.detectChanges();

    (el('showcase-strip-next') as HTMLElement).click();

    expect(strip.scrollBy).toHaveBeenCalledWith(
      expect.objectContaining({ left: 300 * 0.85 }),
    );
  });

  it('shows a loading line rather than an empty gallery while the list is being read', () => {
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('jh-loading')).not.toBeNull();
    expect(el('showcase-strip')).toBeNull();
  });

  it('shows an error with a retry — never an empty state — when the list could not be read', () => {
    const retry = jest.fn();
    fixture.componentRef.setInput('error', "We couldn't load these pictures.");
    fixture.componentRef.setInput('retry', retry);
    fixture.detectChanges();

    const error = el('showcase-error') as HTMLElement;
    expect(error).not.toBeNull();
    expect(error.textContent).toContain("We couldn't load these pictures.");

    (error.querySelector('button') as HTMLElement).click();
    expect(retry).toHaveBeenCalled();
  });
});
