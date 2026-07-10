import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RecognitionDefinition, RecognitionUpsert } from '../models/recognition.models';
import { RecognitionAdminService } from './recognition-admin.service';

const DEF: RecognitionDefinition = {
  id: 'def-1',
  name: 'Fair play',
  description: 'Good spirit on the pitch.',
  appliesToPlayers: true,
  appliesToTeams: false,
  isRetired: false,
  hasIcon: false,
  grantedCount: 0,
  createdAt: '2026-03-04T00:00:00Z',
};

const UPSERT: RecognitionUpsert = {
  name: 'Fair play',
  description: 'Good spirit on the pitch.',
  appliesToPlayers: true,
  appliesToTeams: false,
};

describe('RecognitionAdminService (catalogue management, feature 014)', () => {
  let service: RecognitionAdminService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(RecognitionAdminService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('createDefinition POSTs the upsert body to the kind catalogue', () => {
    service.createDefinition('badge', UPSERT).subscribe();
    const req = httpMock.expectOne('/api/v1/admin/badges');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(UPSERT);
    req.flush(DEF);
  });

  it('createDefinition routes achievements to the achievements catalogue', () => {
    service.createDefinition('achievement', UPSERT).subscribe();
    const req = httpMock.expectOne('/api/v1/admin/achievements');
    expect(req.request.method).toBe('POST');
    req.flush(DEF);
  });

  it('updateDefinition PUTs to the definition id', () => {
    service.updateDefinition('badge', 'def-1', UPSERT).subscribe();
    const req = httpMock.expectOne('/api/v1/admin/badges/def-1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(UPSERT);
    req.flush(DEF);
  });

  it('retireDefinition DELETEs the definition', () => {
    service.retireDefinition('badge', 'def-1').subscribe();
    const req = httpMock.expectOne('/api/v1/admin/badges/def-1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('reinstateDefinition POSTs to the reinstate sub-route', () => {
    service.reinstateDefinition('achievement', 'def-1').subscribe();
    const req = httpMock.expectOne('/api/v1/admin/achievements/def-1/reinstate');
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('setIcon PUTs the raw File as the request body', () => {
    const file = new File(['x'], 'icon.png', { type: 'image/png' });
    service.setIcon('badge', 'def-1', file).subscribe();
    const req = httpMock.expectOne('/api/v1/admin/badges/def-1/icon');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toBe(file);
    req.flush(null);
  });

  it('removeIcon DELETEs the icon sub-route', () => {
    service.removeIcon('badge', 'def-1').subscribe();
    const req = httpMock.expectOne('/api/v1/admin/badges/def-1/icon');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('listDefinitions passes take + includeRetired for the kind', () => {
    service.listDefinitions('badge', true).subscribe();
    const req = httpMock.expectOne(
      (r) => r.url === '/api/v1/admin/badges' && r.params.get('take') === '100' && r.params.get('includeRetired') === 'true',
    );
    req.flush({ items: [DEF], totalCount: 1, skip: 0, take: 100 });
  });
});
