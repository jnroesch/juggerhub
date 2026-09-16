import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TeamService } from './team.service';

/**
 * Feature 051 — the team logo client. The cache-busting behaviour is the part worth pinning: the
 * logo's address does not change when its image does, so without a revision an `<img>` already on
 * screen keeps showing the browser's cached copy after a replace.
 */
describe('TeamService logos', () => {
  let service: TeamService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    service = TestBed.inject(TeamService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('builds a plain logo URL for a team this session has not changed', () => {
    expect(service.logoUrl('rheinfeuer')).toBe('/api/v1/teams/rheinfeuer/logo');
  });

  it('PUTs the file as multipart form data', () => {
    const file = new File(['bytes'], 'crest.png', { type: 'image/png' });

    service.uploadLogo('rheinfeuer', file).subscribe();

    const req = httpMock.expectOne('/api/v1/teams/rheinfeuer/logo');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body instanceof FormData).toBe(true);
    expect((req.request.body as FormData).get('file')).toBe(file);
    req.flush(null);
  });

  it('busts the cached image after an upload, and only for that team', () => {
    const file = new File(['bytes'], 'crest.png', { type: 'image/png' });

    service.uploadLogo('rheinfeuer', file).subscribe();
    httpMock.expectOne('/api/v1/teams/rheinfeuer/logo').flush(null);

    expect(service.logoUrl('rheinfeuer')).toBe('/api/v1/teams/rheinfeuer/logo?v=1');
    // A second team's logo must not be forced to re-fetch by someone else's upload.
    expect(service.logoUrl('chaos-crew')).toBe('/api/v1/teams/chaos-crew/logo');
  });

  it('DELETEs the logo and busts the cached image so the placeholder appears', () => {
    service.removeLogo('rheinfeuer').subscribe();

    const req = httpMock.expectOne('/api/v1/teams/rheinfeuer/logo');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);

    expect(service.logoUrl('rheinfeuer')).toBe('/api/v1/teams/rheinfeuer/logo?v=1');
  });

  it('does not bust the cache when an upload fails', () => {
    const file = new File(['bytes'], 'crest.png', { type: 'image/png' });

    service.uploadLogo('rheinfeuer', file).subscribe({ error: () => undefined });
    httpMock
      .expectOne('/api/v1/teams/rheinfeuer/logo')
      .flush({ detail: 'Invalid image' }, { status: 400, statusText: 'Bad Request' });

    expect(service.logoUrl('rheinfeuer')).toBe('/api/v1/teams/rheinfeuer/logo');
  });

  it('escapes a slug before putting it in the URL', () => {
    expect(service.logoUrl('a b')).toBe('/api/v1/teams/a%20b/logo');
  });
});
