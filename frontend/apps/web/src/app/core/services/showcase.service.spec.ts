import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ShowcaseOwner } from '../models/showcase.models';
import { ShowcaseService } from './showcase.service';

const profile: ShowcaseOwner = { kind: 'profile', handle: 'ada' };
const team: ShowcaseOwner = { kind: 'team', slug: 'rheinfeuer' };

describe('ShowcaseService', () => {
  let service: ShowcaseService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ShowcaseService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads a profile gallery by handle', () => {
    service.list(profile).subscribe();
    http.expectOne('/api/v1/profiles/ada/showcase').flush([]);
  });

  it('writes a profile gallery through "me", never through a handle', () => {
    // The server acts on the authenticated subject alone, so whose gallery is being changed is
    // not something a client gets to say.
    service.upload(profile, new File(['x'], 'p.png')).subscribe();
    const upload = http.expectOne('/api/v1/profiles/me/showcase');
    expect(upload.request.method).toBe('POST');
    upload.flush({ id: 'a', caption: null, position: 0 });

    service.remove(profile, 'a').subscribe();
    http.expectOne('/api/v1/profiles/me/showcase/a').flush(null);

    service.setCaption(profile, 'a', 'hi').subscribe();
    http.expectOne('/api/v1/profiles/me/showcase/a').flush(null);

    service.reorder(profile, ['a', 'b']).subscribe();
    const reorder = http.expectOne('/api/v1/profiles/me/showcase/order');
    expect(reorder.request.body).toEqual({ imageIds: ['a', 'b'] });
    reorder.flush(null);
  });

  it('reads and writes a team gallery on the slug, where actor and owner genuinely differ', () => {
    service.list(team).subscribe();
    http.expectOne('/api/v1/teams/rheinfeuer/showcase').flush([]);

    service.upload(team, new File(['x'], 'p.png')).subscribe();
    http.expectOne('/api/v1/teams/rheinfeuer/showcase').flush({ id: 'a', caption: null, position: 0 });
  });

  it('builds image URLs from the owner and the image id — never from a stored location', () => {
    expect(service.imageUrl(profile, 'a')).toBe('/api/v1/profiles/ada/showcase/a/image');
    expect(service.imageUrl(team, 'a')).toBe('/api/v1/teams/rheinfeuer/showcase/a/image');
  });

  it('escapes an owner identifier rather than pasting it into a path', () => {
    expect(service.imageUrl({ kind: 'profile', handle: 'a/b' }, 'x')).toContain('a%2Fb');
  });

  describe('classifyUploadFailure', () => {
    function failure(status: number, detail = ''): HttpErrorResponse {
      return new HttpErrorResponse({ status, error: { detail } });
    }

    it('separates a full gallery from every processing failure', () => {
      // The distinction matters to the person: "you already have five" is fixed by removing one,
      // "we could not read that" by choosing a different picture (spec FR-016).
      expect(service.classifyUploadFailure(failure(409, 'Gallery full'))).toBe('full');
      expect(service.classifyUploadFailure(failure(400, 'That image could not be read.'))).toBe(
        'unreadable',
      );
    });

    it('recognises size, type and store-unavailable refusals', () => {
      expect(service.classifyUploadFailure(failure(413))).toBe('size');
      expect(service.classifyUploadFailure(failure(400, 'Image is too large.'))).toBe('size');
      expect(service.classifyUploadFailure(failure(400, 'Unsupported image type.'))).toBe('type');
      expect(service.classifyUploadFailure(failure(503))).toBe('unavailable');
    });

    it('falls back to unknown rather than guessing', () => {
      expect(service.classifyUploadFailure(failure(500))).toBe('unknown');
      expect(service.classifyUploadFailure(new Error('offline'))).toBe('unknown');
    });
  });
});
