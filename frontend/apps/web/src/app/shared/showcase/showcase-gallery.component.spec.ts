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
    expect(el('showcase-grid')).toBeNull();
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

  it('shows a loading line rather than an empty gallery while the list is being read', () => {
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('jh-loading')).not.toBeNull();
    expect(el('showcase-grid')).toBeNull();
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
