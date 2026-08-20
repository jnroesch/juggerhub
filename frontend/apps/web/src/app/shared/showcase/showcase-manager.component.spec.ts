import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ShowcaseImage } from '../../core/models/showcase.models';
import { ShowcaseService } from '../../core/services/showcase.service';
import { SHOWCASE_MAX_IMAGES, ShowcaseManagerComponent } from './showcase-manager.component';
import { translocoTestingModule } from '../../../testing/transloco-testing';

function image(id: string, caption: string | null = null, position = 0): ShowcaseImage {
  return { id, caption, position };
}

function fileList(file: File): FileList {
  return { 0: file, length: 1, item: () => file } as unknown as FileList;
}

describe('ShowcaseManagerComponent (jh-showcase-manager)', () => {
  let fixture: ComponentFixture<ShowcaseManagerComponent>;

  const showcase = {
    imageUrl: (_owner: unknown, id: string) => `/api/v1/profiles/ada/showcase/${id}/image`,
    upload: jest.fn(),
    remove: jest.fn(),
    reorder: jest.fn(),
    setCaption: jest.fn(),
    list: jest.fn(),
    // Mirrors the real classifier's status mapping; the classifier itself is covered directly in
    // showcase.service.spec.ts.
    classifyUploadFailure: (error: unknown) => {
      const status = (error as HttpErrorResponse).status;
      if (status === 409) return 'full';
      if (status === 503) return 'unavailable';
      if (status === 413) return 'size';
      return status === 400 ? 'unreadable' : 'unknown';
    },
  };

  function el(testId: string): HTMLElement | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
  }

  function setImages(images: ShowcaseImage[]): void {
    fixture.componentRef.setInput('images', images);
    fixture.detectChanges();
  }

  function pickFile(): void {
    const input = el('showcase-file-input') as HTMLInputElement;
    Object.defineProperty(input, 'files', { value: fileList(new File(['x'], 'p.png')), writable: true });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  beforeEach(() => {
    jest.clearAllMocks();
    showcase.list.mockReturnValue(of([]));

    TestBed.configureTestingModule({
      imports: [ShowcaseManagerComponent, translocoTestingModule()],
      providers: [{ provide: ShowcaseService, useValue: showcase }],
    });

    fixture = TestBed.createComponent(ShowcaseManagerComponent);
    fixture.componentRef.setInput('owner', { kind: 'profile', handle: 'ada' });
    fixture.componentRef.setInput('images', []);
    fixture.detectChanges();
  });

  it('invites the owner to add pictures when the gallery is empty', () => {
    expect(fixture.nativeElement.textContent).toContain('add up to');
    expect(el('showcase-manager-list')).toBeNull();
  });

  it('disables adding once the gallery is full, and says so', () => {
    setImages(Array.from({ length: SHOWCASE_MAX_IMAGES }, (_, i) => image(`i${i}`, null, i)));

    expect((el('showcase-file-input') as HTMLInputElement).disabled).toBe(true);
    expect((el('showcase-remaining') as HTMLElement).textContent).toContain(String(SHOWCASE_MAX_IMAGES));
  });

  it('uploads a picked file and hands the refreshed list back to the page', () => {
    const refreshed = [image('a', null, 0)];
    showcase.upload.mockReturnValue(of(refreshed[0]));
    showcase.list.mockReturnValue(of(refreshed));

    const changed = jest.fn();
    fixture.componentInstance.changed.subscribe(changed);

    pickFile();

    expect(showcase.upload).toHaveBeenCalled();
    expect(changed).toHaveBeenCalledWith(refreshed);
  });

  it('names the actual reason an upload was refused rather than a generic failure', () => {
    showcase.upload.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 409, error: { detail: 'Gallery full' } })),
    );

    setImages([image('a', null, 0)]);
    pickFile();

    const error = el('showcase-manager-error') as HTMLElement;
    expect(error).not.toBeNull();
    expect(error.textContent).toContain(String(SHOWCASE_MAX_IMAGES));
  });

  it('leaves the rendered gallery untouched when an upload is refused', () => {
    showcase.upload.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 400, error: { detail: 'unsupported type' } })),
    );

    const before = [image('a', 'kept', 0), image('b', null, 1)];
    setImages(before);
    pickFile();

    const rows = fixture.nativeElement.querySelectorAll('[data-testid="showcase-manager-list"] li');
    expect(rows).toHaveLength(2);
    expect(rows[0].textContent).toContain('kept');
  });

  it('submits the whole new order when a picture is moved', () => {
    showcase.reorder.mockReturnValue(of(undefined));
    showcase.list.mockReturnValue(of([]));

    setImages([image('a', null, 0), image('b', null, 1), image('c', null, 2)]);
    (el('showcase-move-down-0') as HTMLElement).click();

    expect(showcase.reorder).toHaveBeenCalledWith({ kind: 'profile', handle: 'ada' }, ['b', 'a', 'c']);
  });

  it('cannot move the first picture up or the last one down', () => {
    setImages([image('a', null, 0), image('b', null, 1)]);

    expect((el('showcase-move-up-0') as HTMLButtonElement).disabled).toBe(true);
    expect((el('showcase-move-down-1') as HTMLButtonElement).disabled).toBe(true);
  });

  it('re-reads the gallery after a failed reorder, so the screen shows what the server holds', () => {
    const serverOrder = [image('a', null, 0), image('b', null, 1)];
    showcase.reorder.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 409, error: { detail: 'Gallery changed' } })),
    );
    showcase.list.mockReturnValue(of(serverOrder));

    const changed = jest.fn();
    fixture.componentInstance.changed.subscribe(changed);

    setImages(serverOrder);
    (el('showcase-move-down-0') as HTMLElement).click();
    fixture.detectChanges();

    expect(el('showcase-manager-error')).not.toBeNull();
    expect(changed).toHaveBeenCalledWith(serverOrder);
  });

  it('saves a caption, and a blank one clears it rather than storing whitespace', () => {
    showcase.setCaption.mockReturnValue(of(undefined));
    showcase.list.mockReturnValue(of([]));

    setImages([image('a', 'old', 0)]);
    (el('showcase-edit-caption-0') as HTMLElement).click();
    fixture.detectChanges();

    const input = el('showcase-caption-input-0') as HTMLInputElement;
    input.value = 'Tempelhofer Feld';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    (el('showcase-caption-save-0') as HTMLElement).click();

    expect(showcase.setCaption).toHaveBeenCalledWith(
      { kind: 'profile', handle: 'ada' },
      'a',
      'Tempelhofer Feld',
    );

    setImages([image('a', 'Tempelhofer Feld', 0)]);
    (el('showcase-edit-caption-0') as HTMLElement).click();
    fixture.detectChanges();
    const blank = el('showcase-caption-input-0') as HTMLInputElement;
    blank.value = '   ';
    blank.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    (el('showcase-caption-save-0') as HTMLElement).click();

    expect(showcase.setCaption).toHaveBeenLastCalledWith({ kind: 'profile', handle: 'ada' }, 'a', null);
  });

  it('removes a picture', () => {
    showcase.remove.mockReturnValue(of(undefined));
    showcase.list.mockReturnValue(of([]));

    setImages([image('a', null, 0)]);
    (el('showcase-remove-0') as HTMLElement).click();

    expect(showcase.remove).toHaveBeenCalledWith({ kind: 'profile', handle: 'ada' }, 'a');
  });
});
