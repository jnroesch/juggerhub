import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { OwnerProfile, PublicProfile } from '../models/profile.models';
import { ProfileService } from './profile.service';

const OWNER: OwnerProfile = {
  handle: 'nik-berlin',
  displayName: 'Nik',
  hometown: 'Berlin',
  description: null,
  hasAvatar: false,
  pompfen: ['Stab', 'Schild'],
  recentActivity: [],
};

const PUBLIC: PublicProfile = {
  handle: 'nik-berlin',
  displayName: 'Nik',
  hometown: 'Berlin',
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
      providers: [provideHttpClient(), provideHttpClientTesting()],
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
    const body = { displayName: 'Nik', hometown: null, description: null, pompfen: ['Stab' as const] };
    service.updateMine(body).subscribe();
    const req = httpMock.expectOne('/api/v1/profiles/me');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(body);
    req.flush(OWNER);
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
