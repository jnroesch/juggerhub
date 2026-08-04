import { provideHttpClient, withXhr } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { OwnerProfile, PublicProfile, UpdateProfileRequest } from '../models/profile.models';
import { ProfileService } from './profile.service';

const UPDATE: UpdateProfileRequest = {
  displayName: 'Nik',
  location: null,
  description: null,
  pompfen: [],
  isPublic: false,
};

const OWNER: OwnerProfile = {
  handle: 'nik-berlin',
  displayName: 'Nik',
  location: { externalId: 'TEST:berlin', name: 'Berlin', region: null, countryName: 'Germany', countryCode: 'DE', label: 'Berlin, Germany' },
  description: null,
  hasAvatar: false,
  pompfen: ['Stab', 'Schild'],
  recentActivity: [],
  isPublic: false,
};

const PUBLIC: PublicProfile = {
  handle: 'nik-berlin',
  displayName: 'Nik',
  location: { externalId: 'TEST:berlin', name: 'Berlin', region: null, countryName: 'Germany', countryCode: 'DE', label: 'Berlin, Germany' },
  description: null,
  hasAvatar: false,
  selectedPompfen: ['Stab'],
  recentActivity: [],
};

describe('ProfileService', () => {
  let service: ProfileService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    service = TestBed.inject(ProfileService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getMine GETs /profiles/me', () => {
    service.getMine().subscribe();
    const req = httpMock.expectOne('/api/v1/profiles/me');
    expect(req.request.method).toBe('GET');
    req.flush(OWNER);
  });

  it('updateMine PUTs the update body', () => {
    const body = { displayName: 'Nik', location: null, description: null, pompfen: ['Stab' as const] };
    service.updateMine(body).subscribe();
    const req = httpMock.expectOne('/api/v1/profiles/me');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(body);
    req.flush(OWNER);
  });

  /**
   * The cache is what lets several profile views share one `/me` read, but it used to live for the
   * whole session: setting a home city and navigating to Browse still saw the city-less profile, so
   * the proximity Sort option stayed hidden until a page reload. Every owner mutation now drops it.
   */
  describe('cached owner profile', () => {
    /** Prime the cache and assert the reader is served without a second request. */
    function primeCache(): void {
      service.getMineCached().subscribe();
      httpMock.expectOne('/api/v1/profiles/me').flush(OWNER);

      service.getMineCached().subscribe();
      httpMock.expectNone('/api/v1/profiles/me');
    }

    it('serves repeat reads from one request', () => {
      primeCache();
    });

    it.each([
      [
        'updateMine',
        () => service.updateMine(UPDATE).subscribe(),
        '/api/v1/profiles/me',
        OWNER,
      ],
      [
        'setHomeCity',
        () => service.setHomeCity({ cityExternalId: 'TEST:berlin', name: 'Berlin' }).subscribe(),
        '/api/v1/profiles/me/home-city',
        null,
      ],
      [
        'uploadAvatar',
        () => service.uploadAvatar(new File(['x'], 'a.png', { type: 'image/png' })).subscribe(),
        '/api/v1/profiles/me/avatar',
        null,
      ],
    ])('%s drops the cache so the next read refetches', (_name, mutate, url, response) => {
      primeCache();

      mutate();
      httpMock.expectOne(url).flush(response);

      service.getMineCached().subscribe();
      const refetch = httpMock.expectOne('/api/v1/profiles/me');
      expect(refetch.request.method).toBe('GET');
      refetch.flush(OWNER);
    });

    it('does not drop the cache when a mutation fails', () => {
      primeCache();

      service.updateMine(UPDATE).subscribe({ error: () => undefined });
      httpMock.expectOne('/api/v1/profiles/me').flush('nope', { status: 500, statusText: 'Server Error' });

      // Nothing changed server-side, so the cached read stands.
      service.getMineCached().subscribe();
      httpMock.expectNone('/api/v1/profiles/me');
    });
  });

  it('uploadAvatar PUTs multipart form data with a "file" field', () => {
    const file = new File(['x'], 'a.png', { type: 'image/png' });
    service.uploadAvatar(file).subscribe();
    const req = httpMock.expectOne('/api/v1/profiles/me/avatar');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body instanceof FormData).toBe(true);
    expect((req.request.body as FormData).get('file')).toBe(file);
    req.flush(null);
  });

  it('getPublic GETs the anonymous profile endpoint', () => {
    service.getPublic('nik-berlin').subscribe();
    const req = httpMock.expectOne('/api/v1/profiles/nik-berlin');
    expect(req.request.method).toBe('GET');
    req.flush(PUBLIC);
  });

  it('getActivity passes skip/take params', () => {
    service.getActivity('nik-berlin', 4, 10).subscribe();
    const req = httpMock.expectOne(
      (r) => r.url === '/api/v1/profiles/nik-berlin/activity' && r.params.get('skip') === '4' && r.params.get('take') === '10',
    );
    req.flush({ items: [], totalCount: 0, skip: 4, take: 10 });
  });

  it('checkHandle calls the availability endpoint with the handle param', () => {
    service.checkHandle('nik-berlin').subscribe();
    const req = httpMock.expectOne(
      (r) => r.url === '/api/v1/auth/handle-available' && r.params.get('handle') === 'nik-berlin',
    );
    req.flush({ handle: 'nik-berlin', normalized: 'nik-berlin', available: true, reason: null });
  });

  it('avatarUrl builds the canonical avatar URL', () => {
    expect(service.avatarUrl('nik-berlin')).toBe('/api/v1/profiles/nik-berlin/avatar');
  });
});
