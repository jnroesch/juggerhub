import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { Health, HealthService } from './health.service';

describe('HealthService', () => {
  let service: HealthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(HealthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('requests GET /api/v1/health and returns the health payload', () => {
    const expected: Health = {
      status: 'healthy',
      database: 'reachable',
      version: '1.0.0',
      timestamp: '2026-06-29T12:00:00Z',
    };

    let actual: Health | undefined;
    service.getHealth().subscribe((health) => (actual = health));

    const req = httpMock.expectOne('/api/v1/health');
    expect(req.request.method).toBe('GET');
    req.flush(expected);

    expect(actual).toEqual(expected);
  });
});
